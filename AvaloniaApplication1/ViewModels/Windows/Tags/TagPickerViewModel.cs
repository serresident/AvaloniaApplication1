using AvaloniaApplication1.ViewModels.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Models.Config;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels.Windows.Tags
{
    public partial class TagPickerViewModel : ViewModelBase
    {
        private readonly List<TagDefinition> _allTags;
        private bool _isSortingBatch;

        public ObservableCollection<TagDefinition> FilteredTags { get; } = new();

        public ObservableCollection<string> AvailableConnectionsFilter { get; } = new();

        public ObservableCollection<string> AvailableDataTypesFilter { get; } = new();

        [ObservableProperty]
        private TagDefinition? _selectedTag;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _selectedConnectionFilter = "Все подключения";

        [ObservableProperty]
        private string _selectedDataTypeFilter = "Все типы";

        [ObservableProperty]
        private string _sortColumn = "TagId";

        [ObservableProperty]
        private bool _sortAscending = true;

        public bool IsConfirmed { get; private set; }
        public Action? CloseAction { get; set; }

        public bool HasNoTags => FilteredTags.Count == 0;

        public string TagIdHeader => "TagId" + GetArrow("TagId");
        public string NameHeader => "Наименование" + GetArrow("Name");
        public string DriverHeader => "Драйвер" + GetArrow("ConnId");
        public string AddressHeader => "Адрес" + GetArrow("Address");
        public string DataTypeHeader => "Тип" + GetArrow("DataType");
        public string UnitHeader => "Ед." + GetArrow("Unit");
        public string ScalingHeader => "Масштаб Y" + GetArrow("Scaling");

        public TagPickerViewModel(IEnumerable<TagDefinition> tags, string? initialTagId = null, string? targetDataType = null)
        {
            _allTags = tags?.ToList() ?? new List<TagDefinition>();

            // Populate Connections filter
            AvailableConnectionsFilter.Add("Все подключения");
            var conns = _allTags
                .Select(t => t.ConnId)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase);
            foreach (var c in conns)
            {
                AvailableConnectionsFilter.Add(c);
            }

            // Populate Data Types filter
            AvailableDataTypesFilter.Add("Все типы");
            var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dt in DataTypes.All)
            {
                types.Add(dt);
            }
            foreach (var t in _allTags)
            {
                if (!string.IsNullOrWhiteSpace(t.DataType))
                {
                    types.Add(t.DataType);
                }
            }
            foreach (var dt in types.OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                AvailableDataTypesFilter.Add(dt);
            }

            // Target data type pre-filtering
            if (!string.IsNullOrWhiteSpace(targetDataType))
            {
                var matched = AvailableDataTypesFilter.FirstOrDefault(d => string.Equals(d, targetDataType, StringComparison.OrdinalIgnoreCase));
                if (matched != null)
                {
                    _selectedDataTypeFilter = matched;
                }
                else
                {
                    AvailableDataTypesFilter.Add(targetDataType);
                    _selectedDataTypeFilter = targetDataType;
                }
            }

            ApplyFilter();

            if (!string.IsNullOrEmpty(initialTagId))
            {
                SelectedTag = FilteredTags.FirstOrDefault(t => string.Equals(t.TagId, initialTagId, StringComparison.OrdinalIgnoreCase))
                           ?? FilteredTags.FirstOrDefault();
            }
            else
            {
                SelectedTag = FilteredTags.FirstOrDefault();
            }
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        partial void OnSelectedConnectionFilterChanged(string value) => ApplyFilter();

        partial void OnSelectedDataTypeFilterChanged(string value) => ApplyFilter();

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
                "address" => "Address",
                "datatype" or "type" => "DataType",
                "unit" => "Unit",
                "scaling" => "Scaling",
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
        public void ResetFilters()
        {
            SearchText = string.Empty;
            SelectedConnectionFilter = "Все подключения";
            SelectedDataTypeFilter = "Все типы";
        }

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
            OnPropertyChanged(nameof(DriverHeader));
            OnPropertyChanged(nameof(AddressHeader));
            OnPropertyChanged(nameof(DataTypeHeader));
            OnPropertyChanged(nameof(UnitHeader));
            OnPropertyChanged(nameof(ScalingHeader));
        }

        private void ApplyFilter()
        {
            var currentSelected = SelectedTag;
            FilteredTags.Clear();

            IEnumerable<TagDefinition> query = _allTags;

            // 1. Text filter
            var text = SearchText?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                query = query.Where(t =>
                    (t.TagId != null && t.TagId.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                    (t.Name != null && t.Name.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                    (t.Address != null && t.Address.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                    (t.ConnId != null && t.ConnId.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                    (t.DataType != null && t.DataType.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                    (t.Unit != null && t.Unit.Contains(text, StringComparison.OrdinalIgnoreCase)));
            }

            // 2. Connection filter
            if (!string.IsNullOrEmpty(SelectedConnectionFilter) &&
                !SelectedConnectionFilter.Equals("Все подключения", StringComparison.OrdinalIgnoreCase) &&
                !SelectedConnectionFilter.Equals("Все", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(t => string.Equals(t.ConnId, SelectedConnectionFilter, StringComparison.OrdinalIgnoreCase));
            }

            // 3. DataType filter
            if (!string.IsNullOrEmpty(SelectedDataTypeFilter) &&
                !SelectedDataTypeFilter.Equals("Все типы", StringComparison.OrdinalIgnoreCase) &&
                !SelectedDataTypeFilter.Equals("Все", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(t => string.Equals(t.DataType, SelectedDataTypeFilter, StringComparison.OrdinalIgnoreCase));
            }

            // 4. Sorting
            query = (SortColumn?.ToLowerInvariant(), SortAscending) switch
            {
                ("name", true) => query.OrderBy(t => t.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                ("name", false) => query.OrderByDescending(t => t.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                ("connid" or "driver", true) => query.OrderBy(t => t.ConnId ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                ("connid" or "driver", false) => query.OrderByDescending(t => t.ConnId ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                ("address", true) => query.OrderBy(t => t.Address ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                ("address", false) => query.OrderByDescending(t => t.Address ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                ("datatype" or "type", true) => query.OrderBy(t => t.DataType ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                ("datatype" or "type", false) => query.OrderByDescending(t => t.DataType ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                ("unit", true) => query.OrderBy(t => t.Unit ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                ("unit", false) => query.OrderByDescending(t => t.Unit ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                ("scaling", true) => query.OrderBy(t => t.ScalingDisplay ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                ("scaling", false) => query.OrderByDescending(t => t.ScalingDisplay ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TagId, StringComparer.OrdinalIgnoreCase),
                (_, false) => query.OrderByDescending(t => t.TagId ?? string.Empty, StringComparer.OrdinalIgnoreCase),
                _ => query.OrderBy(t => t.TagId ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            };

            foreach (var tag in query)
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

            OnPropertyChanged(nameof(HasNoTags));
        }

        [RelayCommand]
        private void Confirm()
        {
            if (SelectedTag != null)
            {
                IsConfirmed = true;
                CloseAction?.Invoke();
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            IsConfirmed = false;
            CloseAction?.Invoke();
        }
    }
}
