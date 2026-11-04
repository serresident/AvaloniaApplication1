using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;

namespace AvaloniaApplication1.Services
{
    /// <summary>
    /// Centralized Tag Engine implementation adapted from Rapid SCADA v6 architecture.
    /// Manages tag definitions, fast indexed snapshot lookups, stale watchdog detection, and manual overrides.
    /// </summary>
    public class TagManagerService : ITagManagerService
    {
        private readonly ConcurrentDictionary<string, TagDefinition> _tags = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> _addressToTagId = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, TagSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, object> _overrides = new(StringComparer.OrdinalIgnoreCase);

        private readonly Subject<TagSnapshot> _snapshotSubject = new();
        public IObservable<TagSnapshot> SnapshotUpdates => _snapshotSubject;

        private CancellationTokenSource? _watchdogCts;
        private readonly bool _startWatchdogLoop;

        public IReadOnlyCollection<TagDefinition> AllTags => _tags.Values.ToList().AsReadOnly();

        public TagManagerService(bool startWatchdogLoop = true)
        {
            _startWatchdogLoop = startWatchdogLoop;
            if (_startWatchdogLoop)
            {
                StartWatchdog();
            }
        }

        public void Initialize(IEnumerable<TagDefinition>? tags)
        {
            if (tags == null) return;
            foreach (var tag in tags)
            {
                RegisterTag(tag);
            }
        }

        public void RegisterTag(TagDefinition tag)
        {
            if (tag == null || string.IsNullOrWhiteSpace(tag.TagId)) return;

            _tags[tag.TagId] = tag;
            if (!string.IsNullOrWhiteSpace(tag.ConnId) && !string.IsNullOrWhiteSpace(tag.Address))
            {
                var key = ConfigMigrationService.BuildAddressKey(tag.ConnId, tag.Address);
                _addressToTagId[key] = tag.TagId;
            }

            if (!_snapshots.ContainsKey(tag.TagId))
            {
                _snapshots[tag.TagId] = new TagSnapshot(tag.TagId, null, TagQuality.Uninitialized, DateTime.UtcNow);
            }
        }

        public TagDefinition? GetTagDefinition(string tagId)
        {
            if (string.IsNullOrWhiteSpace(tagId)) return null;
            return _tags.TryGetValue(tagId, out var def) ? def : null;
        }

        public TagDefinition? FindTagByAddress(string connId, string address)
        {
            var key = ConfigMigrationService.BuildAddressKey(connId, address);
            if (_addressToTagId.TryGetValue(key, out var tagId) && _tags.TryGetValue(tagId, out var def))
            {
                return def;
            }

            // Transparent fallback: Auto-synthesize tag definition if not pre-configured
            var synthTagId = ConfigMigrationService.GenerateTagId(connId, address);
            var synthDef = new TagDefinition
            {
                TagId = synthTagId,
                Name = $"{connId}:{address}",
                ConnId = connId,
                Address = address,
                DataType = "Float32",
                StaleTimeout = TimeSpan.FromSeconds(5)
            };
            RegisterTag(synthDef);
            return synthDef;
        }

        public TagSnapshot GetSnapshot(string tagId)
        {
            if (string.IsNullOrWhiteSpace(tagId)) return default;
            if (_snapshots.TryGetValue(tagId, out var snapshot))
            {
                return snapshot;
            }

            // Return default uninitialized snapshot
            return new TagSnapshot(tagId, null, TagQuality.Uninitialized, DateTime.UtcNow);
        }

        public TagSnapshot GetSnapshotByAddress(string connId, string address)
        {
            var tag = FindTagByAddress(connId, address);
            if (tag != null)
            {
                return GetSnapshot(tag.TagId);
            }
            return default;
        }

        public void UpdateTagValue(string connId, string address, object value, TagQuality quality = TagQuality.Good, string? errorMessage = null)
        {
            var tag = FindTagByAddress(connId, address);
            if (tag != null)
            {
                UpdateTagValue(tag.TagId, value, quality, errorMessage);
            }
        }

        public void UpdateTagValue(string tagId, object value, TagQuality quality = TagQuality.Good, string? errorMessage = null)
        {
            if (string.IsNullOrWhiteSpace(tagId)) return;

            // If manual override is active, maintain operator override value but note telemetry received
            if (_overrides.TryGetValue(tagId, out var forcedVal))
            {
                var overrideSnapshot = new TagSnapshot(tagId, forcedVal, TagQuality.Overridden, DateTime.UtcNow, errorMessage, true);
                _snapshots[tagId] = overrideSnapshot;
                _snapshotSubject.OnNext(overrideSnapshot);
                return;
            }

            var snapshot = new TagSnapshot(tagId, value, quality, DateTime.UtcNow, errorMessage, false);
            _snapshots[tagId] = snapshot;
            _snapshotSubject.OnNext(snapshot);
        }

        public void ForceOverride(string tagId, object value)
        {
            if (string.IsNullOrWhiteSpace(tagId)) return;

            _overrides[tagId] = value;
            var snapshot = new TagSnapshot(tagId, value, TagQuality.Overridden, DateTime.UtcNow, null, true);
            _snapshots[tagId] = snapshot;
            _snapshotSubject.OnNext(snapshot);
            Console.WriteLine($"[TagManager] Manual override applied to tag '{tagId}': {value}");
        }

        public void ClearOverride(string tagId)
        {
            if (string.IsNullOrWhiteSpace(tagId)) return;

            if (_overrides.TryRemove(tagId, out _))
            {
                // Revert snapshot to last known value or uninitialized
                var existing = _snapshots.TryGetValue(tagId, out var s) ? s : default;
                var restored = new TagSnapshot(tagId, existing.Value, TagQuality.Good, DateTime.UtcNow, null, false);
                _snapshots[tagId] = restored;
                _snapshotSubject.OnNext(restored);
                Console.WriteLine($"[TagManager] Manual override cleared for tag '{tagId}'");
            }
        }

        public void CheckStaleTags()
        {
            var now = DateTime.UtcNow;

            foreach (var tag in _tags.Values)
            {
                if (_overrides.ContainsKey(tag.TagId)) continue; // Overrides are not affected by watchdog

                if (_snapshots.TryGetValue(tag.TagId, out var snapshot))
                {
                    if (snapshot.Quality == TagQuality.Good || snapshot.Quality == TagQuality.Stale)
                    {
                        var age = now - snapshot.Timestamp;
                        if (age > (tag.StaleTimeout * 2))
                        {
                            if (snapshot.Quality != TagQuality.Timeout)
                            {
                                var timeoutSnapshot = new TagSnapshot(tag.TagId, snapshot.Value, TagQuality.Timeout, snapshot.Timestamp, "Телеметрия не отвечает (Timeout)", false);
                                _snapshots[tag.TagId] = timeoutSnapshot;
                                _snapshotSubject.OnNext(timeoutSnapshot);
                            }
                        }
                        else if (age > tag.StaleTimeout)
                        {
                            if (snapshot.Quality == TagQuality.Good)
                            {
                                var staleSnapshot = new TagSnapshot(tag.TagId, snapshot.Value, TagQuality.Stale, snapshot.Timestamp, "Данные устарели (Stale)", false);
                                _snapshots[tag.TagId] = staleSnapshot;
                                _snapshotSubject.OnNext(staleSnapshot);
                            }
                        }
                    }
                }
            }
        }

        private void StartWatchdog()
        {
            _watchdogCts = new CancellationTokenSource();
            var ct = _watchdogCts.Token;

            Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(500, ct);
                        CheckStaleTags();
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[TagManager Watchdog] Check error: {ex.Message}");
                    }
                }
            }, ct).FireAndForget(context: "TagManager.Watchdog");
        }

        public void Dispose()
        {
            _watchdogCts?.Cancel();
            _watchdogCts?.Dispose();
            _watchdogCts = null;
            _snapshotSubject.Dispose();
        }
    }
}
