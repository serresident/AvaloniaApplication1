using AvaloniaApplication1.ViewModels.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels.Windows.Tags
{
    /// <summary>
    /// Master ViewModel for the industrial SCADA Tag Manager interface.
    /// Manages centralized tag dictionary, real-time telemetry streaming, CRUD operations,
    /// manual operator overrides [M], and signal table import/export.
    /// </summary>
    public partial class TagManagerViewModel : ViewModelBase, IDisposable
    {
        private readonly HmiConfiguration _config;
        private readonly ITagManagerService _tagManager;
        private readonly IDialogService _dialogService;
        private readonly IConfigurationService? _configurationService;
        private readonly IHmiClipboardService? _clipboard;
        private readonly CompositeDisposable _disposables = new();

        private readonly ConcurrentDictionary<string, TagItemViewModel> _tagMap = new(StringComparer.OrdinalIgnoreCase);

        public ObservableCollection<TagItemViewModel> Tags { get; } = new();
        public ObservableCollection<TagItemViewModel> FilteredTags { get; } = new();

        public ObservableCollection<string> ConnectionFilterOptions { get; } = new();
        public ObservableCollection<string> QualityFilterOptions { get; } = new()
        {
            "Все качества",
            "🟢 Good (Достоверно)",
            "⏳ Stale (Устарело)",
            "⚠️ Timeout / Error (Сбой)",
            "⚡ [M] Overridden (Фиксация)"
        };

        [ObservableProperty]
        private TagItemViewModel? _selectedTag;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _selectedConnectionFilter = "Все подключения";

        [ObservableProperty]
        private string _selectedQualityFilter = "Все качества";

        // Statistics Badges
        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private int _goodCount;

        [ObservableProperty]
        private int _staleCount;

        [ObservableProperty]
        private int _badCount;

        [ObservableProperty]
        private int _overriddenCount;

        [ObservableProperty]
        private string _statusSummaryText = string.Empty;

        [ObservableProperty]
        private string _sortColumn = "TagId";

        [ObservableProperty]
        private bool _sortAscending = true;

        private bool _isSortingBatch;

        public string TagIdHeader => "Идентификатор (TagId)" + GetArrow("TagId");
        public string NameHeader => "Наименование" + GetArrow("Name");
        public string ConnIdHeader => "Драйвер" + GetArrow("ConnId");
        public string AddressHeader => "Адрес / Топик" + GetArrow("Address");
        public string DataTypeHeader => "Тип данных" + GetArrow("DataType");
        public string UnitHeader => "Ед. изм." + GetArrow("Unit");
        public string ScalingHeader => "Масштаб (Y)" + GetArrow("Scaling");
        public string DeadbandHeader => "Апертура" + GetArrow("Deadband");
        public string LiveValueHeader => "Текущее (Live)" + GetArrow("LiveValue");
        public string QualityHeader => "Качество" + GetArrow("Quality");
        public string TimestampHeader => "Время" + GetArrow("Timestamp");

        private string GetArrow(string column)
        {
            if (string.Equals(SortColumn, column, StringComparison.OrdinalIgnoreCase))
            {
                return SortAscending ? " ▲" : " ▼";
            }
            return string.Empty;
        }

        private void NotifyHeaderProperties()
        {
            OnPropertyChanged(nameof(TagIdHeader));
            OnPropertyChanged(nameof(NameHeader));
            OnPropertyChanged(nameof(ConnIdHeader));
            OnPropertyChanged(nameof(AddressHeader));
            OnPropertyChanged(nameof(DataTypeHeader));
            OnPropertyChanged(nameof(UnitHeader));
            OnPropertyChanged(nameof(ScalingHeader));
            OnPropertyChanged(nameof(DeadbandHeader));
            OnPropertyChanged(nameof(LiveValueHeader));
            OnPropertyChanged(nameof(QualityHeader));
            OnPropertyChanged(nameof(TimestampHeader));
        }

        partial void OnSortColumnChanged(string value)
        {
            if (!_isSortingBatch)
            {
                ApplyFilter();
                NotifyHeaderProperties();
            }
        }

        partial void OnSortAscendingChanged(bool value)
        {
            if (!_isSortingBatch)
            {
                ApplyFilter();
                NotifyHeaderProperties();
            }
        }

        [RelayCommand]
        public void Sort(string column)
        {
            if (string.IsNullOrWhiteSpace(column)) return;

            var normalized = column.ToLowerInvariant() switch
            {
                "tagid" => "TagId",
                "name" => "Name",
                "connid" or "driver" => "ConnId",
                "address" or "topic" => "Address",
                "datatype" or "type" => "DataType",
                "unit" => "Unit",
                "scaling" => "Scaling",
                "deadband" => "Deadband",
                "livevalue" or "value" => "LiveValue",
                "quality" => "Quality",
                "timestamp" or "time" => "Timestamp",
                _ => column
            };

            if (string.Equals(SortColumn, normalized, StringComparison.OrdinalIgnoreCase))
            {
                SortAscending = !SortAscending;
            }
            else
            {
                _isSortingBatch = true;
                try
                {
                    SortAscending = true;
                    SortColumn = normalized;
                }
                finally
                {
                    _isSortingBatch = false;
                }
                ApplyFilter();
                NotifyHeaderProperties();
            }
        }

        [RelayCommand]
        public void Close()
        {
            CloseAction?.Invoke();
        }

        public Action? CloseAction { get; set; }

        public TagManagerViewModel(
            HmiConfiguration config,
            ITagManagerService tagManager,
            IDialogService dialogService,
            IConfigurationService? configurationService = null,
            IHmiClipboardService? clipboard = null)
        {
            _config = config;
            _tagManager = tagManager;
            _dialogService = dialogService;
            _configurationService = configurationService;
            _clipboard = clipboard;

            InitializeConnectionFilters();
            LoadTags();
            SubscribeTelemetry();
        }

        private void InitializeConnectionFilters()
        {
            ConnectionFilterOptions.Clear();
            ConnectionFilterOptions.Add("Все подключения");
            if (_config.Connections != null)
            {
                foreach (var conn in _config.Connections)
                {
                    if (!string.IsNullOrWhiteSpace(conn.Id) && !ConnectionFilterOptions.Contains(conn.Id))
                    {
                        ConnectionFilterOptions.Add(conn.Id);
                    }
                }
            }
        }

        public void LoadTags()
        {
            Tags.Clear();
            _tagMap.Clear();

            var allDefs = _tagManager.AllTags;
            foreach (var def in allDefs)
            {
                var itemVm = CreateTagItemViewModel(def);
                Tags.Add(itemVm);
                _tagMap[def.TagId] = itemVm;

                // Load initial snapshot if exists
                var initialSnap = _tagManager.GetSnapshot(def.TagId);
                if (initialSnap.Quality != TagQuality.Uninitialized)
                {
                    itemVm.ApplySnapshot(initialSnap);
                }
            }

            UpdateStatistics();
            ApplyFilter();
        }

        private TagItemViewModel CreateTagItemViewModel(TagDefinition def)
        {
            var itemVm = new TagItemViewModel(def)
            {
                RequestForceOverride = item => ForceOverrideCommand.Execute(item),
                RequestClearOverride = item => ClearOverrideCommand.Execute(item)
            };
            return itemVm;
        }

        private void SubscribeTelemetry()
        {
            _tagManager.SnapshotUpdates
                .Buffer(TimeSpan.FromMilliseconds(50), 20)
                .Where(list => list.Count > 0)
                .Subscribe(snapshots =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        foreach (var snap in snapshots)
                        {
                            if (_tagMap.TryGetValue(snap.TagId, out var itemVm))
                            {
                                itemVm.ApplySnapshot(snap);
                            }
                        }
                        UpdateStatistics();
                    });
                })
                .DisposeWith(_disposables);
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnSelectedConnectionFilterChanged(string value) => ApplyFilter();
        partial void OnSelectedQualityFilterChanged(string value) => ApplyFilter();

        public void ApplyFilter()
        {
            var currentSelected = SelectedTag;
            FilteredTags.Clear();

            var query = SearchText?.Trim();
            bool hasQuery = !string.IsNullOrWhiteSpace(query);

            bool filterConn = !string.IsNullOrEmpty(SelectedConnectionFilter) && SelectedConnectionFilter != "Все подключения";
            bool filterQuality = !string.IsNullOrEmpty(SelectedQualityFilter) && SelectedQualityFilter != "Все качества";

            IEnumerable<TagItemViewModel> stream = Tags;

            if (hasQuery)
            {
                stream = stream.Where(tag =>
                    (tag.TagId != null && tag.TagId.Contains(query!, StringComparison.OrdinalIgnoreCase)) ||
                    (tag.Name != null && tag.Name.Contains(query!, StringComparison.OrdinalIgnoreCase)) ||
                    (tag.Address != null && tag.Address.Contains(query!, StringComparison.OrdinalIgnoreCase)) ||
                    (tag.ConnId != null && tag.ConnId.Contains(query!, StringComparison.OrdinalIgnoreCase)) ||
                    (tag.Unit != null && tag.Unit.Contains(query!, StringComparison.OrdinalIgnoreCase)) ||
                    (tag.Description != null && tag.Description.Contains(query!, StringComparison.OrdinalIgnoreCase)));
            }

            if (filterConn)
            {
                stream = stream.Where(tag => string.Equals(tag.ConnId, SelectedConnectionFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (filterQuality)
            {
                stream = stream.Where(tag =>
                {
                    if (SelectedQualityFilter.Contains("Good") && tag.Quality != TagQuality.Good) return false;
                    if (SelectedQualityFilter.Contains("Stale") && tag.Quality != TagQuality.Stale) return false;
                    if (SelectedQualityFilter.Contains("Timeout") && tag.Quality != TagQuality.Timeout && tag.Quality != TagQuality.CommFailure && tag.Quality != TagQuality.Bad) return false;
                    if (SelectedQualityFilter.Contains("Overridden") && !tag.IsOverridden) return false;
                    return true;
                });
            }

            // Sorting
            stream = (SortColumn?.ToLowerInvariant(), SortAscending) switch
            {
                ("tagid", true) => stream.OrderBy(t => t.TagId ?? string.Empty, StringComparer.OrdinalIgnoreCase),
                ("tagid", false) => stream.OrderByDescending(t => t.TagId ?? string.Empty, StringComparer.OrdinalIgnoreCase),

                ("name", true) => stream.OrderBy(t => t.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                ("name", false) => stream.OrderByDescending(t => t.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),

                ("connid" or "driver", true) => stream.OrderBy(t => t.ConnId ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                ("connid" or "driver", false) => stream.OrderByDescending(t => t.ConnId ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),

                ("address" or "topic", true) => stream.OrderBy(t => t.Address ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                ("address" or "topic", false) => stream.OrderByDescending(t => t.Address ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),

                ("datatype" or "type", true) => stream.OrderBy(t => t.DataType ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                ("datatype" or "type", false) => stream.OrderByDescending(t => t.DataType ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),

                ("unit", true) => stream.OrderBy(t => t.Unit ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                ("unit", false) => stream.OrderByDescending(t => t.Unit ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),

                ("scaling", true) => stream.OrderBy(t => t.ScalingDisplay ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                ("scaling", false) => stream.OrderByDescending(t => t.ScalingDisplay ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),

                ("deadband", true) => stream.OrderBy(t => t.Deadband).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                ("deadband", false) => stream.OrderByDescending(t => t.Deadband).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),

                ("livevalue" or "value", true) => stream.OrderBy(t => t.LiveValue ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                ("livevalue" or "value", false) => stream.OrderByDescending(t => t.LiveValue ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),

                ("quality", true) => stream.OrderBy(t => (int)t.Quality).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                ("quality", false) => stream.OrderByDescending(t => (int)t.Quality).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),

                ("timestamp" or "time", true) => stream.OrderBy(t => t.TimestampText ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                ("timestamp" or "time", false) => stream.OrderByDescending(t => t.TimestampText ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),

                (_, false) => stream.OrderByDescending(t => t.TagId ?? string.Empty, StringComparer.OrdinalIgnoreCase),
                _ => stream.OrderBy(t => t.TagId ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            };

            foreach (var tag in stream)
            {
                FilteredTags.Add(tag);
            }

            if (currentSelected != null && FilteredTags.Contains(currentSelected))
            {
                SelectedTag = currentSelected;
            }
            else
            {
                SelectedTag = FilteredTags.FirstOrDefault();
            }

            StatusSummaryText = $"Показано {FilteredTags.Count} из {Tags.Count} тегов";
        }

        private void UpdateStatistics()
        {
            TotalCount = Tags.Count;
            GoodCount = Tags.Count(t => t.Quality == TagQuality.Good && !t.IsOverridden);
            StaleCount = Tags.Count(t => t.Quality == TagQuality.Stale && !t.IsOverridden);
            BadCount = Tags.Count(t => (t.Quality == TagQuality.Timeout || t.Quality == TagQuality.CommFailure || t.Quality == TagQuality.Bad) && !t.IsOverridden);
            OverriddenCount = Tags.Count(t => t.IsOverridden);
        }

        [RelayCommand]
        public async Task AddTagAsync()
        {
            var newTag = await _dialogService.ShowTagEditorAsync(null, _config.Connections, _tagManager.AllTags);
            if (newTag != null)
            {
                _tagManager.RegisterTag(newTag);
                var itemVm = CreateTagItemViewModel(newTag);
                Tags.Add(itemVm);
                _tagMap[newTag.TagId] = itemVm;

                SelectedTag = itemVm;
                UpdateStatistics();
                ApplyFilter();
                await SyncAndSaveConfigAsync();
            }
        }

        [RelayCommand]
        public async Task EditTagAsync()
        {
            if (SelectedTag == null) return;

            var existingDef = SelectedTag.Definition;
            var updated = await _dialogService.ShowTagEditorAsync(existingDef, _config.Connections, _tagManager.AllTags);
            if (updated != null)
            {
                _tagManager.UpdateTag(updated);
                SelectedTag.RefreshFromDefinition();
                UpdateStatistics();
                ApplyFilter();
                await SyncAndSaveConfigAsync();
            }
        }

        [RelayCommand]
        public async Task DuplicateTagAsync()
        {
            if (SelectedTag == null) return;

            var clone = SelectedTag.Definition.Clone();
            int suffix = 1;
            var baseId = clone.TagId;
            var newId = $"{baseId}_copy";
            while (_tagManager.GetTagDefinition(newId) != null)
            {
                newId = $"{baseId}_copy{suffix++}";
            }
            clone.TagId = newId;
            clone.Name = $"{clone.Name} (Копия)";

            var updated = await _dialogService.ShowTagEditorAsync(clone, _config.Connections, _tagManager.AllTags);
            if (updated != null)
            {
                _tagManager.RegisterTag(updated);
                var itemVm = CreateTagItemViewModel(updated);
                Tags.Add(itemVm);
                _tagMap[updated.TagId] = itemVm;

                SelectedTag = itemVm;
                UpdateStatistics();
                ApplyFilter();
                await SyncAndSaveConfigAsync();
            }
        }

        [RelayCommand]
        public async Task DeleteTagAsync()
        {
            if (SelectedTag == null) return;

            var target = SelectedTag;
            var confirm = await _dialogService.ShowConfirmAsync(
                "Удаление тега", 
                $"Вы уверены, что хотите удалить тег '{target.TagId}' ({target.Name}) из базы проекта?");

            if (confirm)
            {
                _tagManager.RemoveTag(target.TagId);
                Tags.Remove(target);
                _tagMap.TryRemove(target.TagId, out _);

                UpdateStatistics();
                ApplyFilter();
                await SyncAndSaveConfigAsync();
            }
        }

        [RelayCommand]
        public async Task ForceOverrideAsync(TagItemViewModel? target)
        {
            var item = target ?? SelectedTag;
            if (item == null) return;

            var (confirmed, value) = await _dialogService.ShowTagForceDialogAsync(item.Definition, item.RawValue);
            if (confirmed && value != null)
            {
                _tagManager.ForceOverride(item.TagId, value);
                item.ApplySnapshot(_tagManager.GetSnapshot(item.TagId));
                UpdateStatistics();
            }
        }

        [RelayCommand]
        public void ClearOverride(TagItemViewModel? target)
        {
            var item = target ?? SelectedTag;
            if (item == null) return;

            _tagManager.ClearOverride(item.TagId);
            item.ApplySnapshot(_tagManager.GetSnapshot(item.TagId));
            UpdateStatistics();
        }

        [RelayCommand]
        public async Task ExportTagsAsync()
        {
            await _dialogService.ShowTagImportExportAsync(isImportMode: false, currentTags: _tagManager.AllTags, clipboard: _clipboard);
        }

        [RelayCommand]
        public async Task ImportTagsAsync()
        {
            var imported = await _dialogService.ShowTagImportExportAsync(isImportMode: true, currentTags: _tagManager.AllTags, clipboard: _clipboard);
            if (imported != null && imported.Count > 0)
            {
                foreach (var tag in imported)
                {
                    _tagManager.RegisterTag(tag);
                }
                LoadTags();
                await SyncAndSaveConfigAsync();
            }
        }

        [RelayCommand]
        public async Task SaveConfigAsync()
        {
            await SyncAndSaveConfigAsync();
        }

        private async Task SyncAndSaveConfigAsync()
        {
            _config.Tags = _tagManager.AllTags.ToList();
            if (_configurationService != null)
            {
                try
                {
                    await _configurationService.SaveConfigurationAsync(_config);
                    Console.WriteLine($"[TagManager] Synchronized and saved {_config.Tags.Count} tags to project configuration.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TagManager] Config save failed: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            _disposables.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
