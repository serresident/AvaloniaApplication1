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
        private readonly ConcurrentDictionary<string, (object? Value, TagQuality Quality, DateTime Timestamp, string? ErrorMessage)> _lastTelemetry = new(StringComparer.OrdinalIgnoreCase);

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

            var now = DateTime.UtcNow;

            // 1. Maintain ground-truth physical telemetry state independent of operator force/overrides
            _lastTelemetry[tagId] = (value, quality, now, errorMessage);

            // 2. If manual override is active, maintain operator override value but note telemetry timestamp
            if (_overrides.TryGetValue(tagId, out var forcedVal))
            {
                var overrideSnapshot = new TagSnapshot(tagId, forcedVal, TagQuality.Overridden, now, errorMessage, true);
                _snapshots[tagId] = overrideSnapshot;
                _snapshotSubject.OnNext(overrideSnapshot);
                return;
            }

            // 3. Deadband check: suppress high-frequency noise when value change is below configured threshold
            if (_tags.TryGetValue(tagId, out var tagDef) && tagDef.Deadband > 0 && quality == TagQuality.Good)
            {
                if (_snapshots.TryGetValue(tagId, out var existing) && existing.Quality == TagQuality.Good && existing.Value != null)
                {
                    if (TryGetDouble(value, out double newVal) && TryGetDouble(existing.Value, out double oldVal))
                    {
                        if (Math.Abs(newVal - oldVal) < tagDef.Deadband)
                        {
                            // Keep timestamp fresh for watchdog, but suppress redundant snapshot re-renders
                            _snapshots[tagId] = existing with { Timestamp = now };
                            return;
                        }
                    }
                }
            }

            var snapshot = new TagSnapshot(tagId, value, quality, now, errorMessage, false);
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
                TagSnapshot restored;
                if (_lastTelemetry.TryGetValue(tagId, out var tele))
                {
                    restored = new TagSnapshot(tagId, tele.Value, tele.Quality, tele.Timestamp, tele.ErrorMessage, false);
                }
                else
                {
                    var existing = _snapshots.TryGetValue(tagId, out var s) ? s : default;
                    restored = new TagSnapshot(tagId, existing.Value, TagQuality.Uninitialized, DateTime.UtcNow, null, false);
                }

                _snapshots[tagId] = restored;
                _snapshotSubject.OnNext(restored);
                Console.WriteLine($"[TagManager] Manual override cleared for tag '{tagId}' (restored quality: {restored.Quality})");
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
                    // Guard against system clock jumping backwards (NTP steps / leap seconds)
                    if (snapshot.Timestamp > now)
                    {
                        snapshot = snapshot with { Timestamp = now };
                        _snapshots[tag.TagId] = snapshot;
                    }

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

        private static bool TryGetDouble(object? val, out double result)
        {
            result = 0;
            if (val == null) return false;
            if (val is double d) { result = d; return true; }
            if (val is float f) { result = f; return true; }
            if (val is int i) { result = i; return true; }
            if (val is short s) { result = s; return true; }
            if (val is ushort us) { result = us; return true; }
            if (val is uint ui) { result = ui; return true; }
            if (val is long l) { result = l; return true; }
            if (val is ulong ul) { result = ul; return true; }
            if (val is decimal dec) { result = (double)dec; return true; }
            return double.TryParse(val.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result);
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
