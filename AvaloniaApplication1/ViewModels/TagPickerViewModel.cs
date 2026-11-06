using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AvaloniaApplication1.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    public partial class TagPickerViewModel : ViewModelBase
    {
        private readonly List<TagDefinition> _allTags;

        public ObservableCollection<TagDefinition> FilteredTags { get; } = new();

        [ObservableProperty]
        private TagDefinition? _selectedTag;

        [ObservableProperty]
        private string _searchText = string.Empty;

        public bool IsConfirmed { get; private set; }
        public Action? CloseAction { get; set; }

        public TagPickerViewModel(IEnumerable<TagDefinition> tags, string? initialTagId = null)
        {
            _allTags = tags?.ToList() ?? new List<TagDefinition>();

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

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            FilteredTags.Clear();

            var query = SearchText?.Trim() ?? "";
            var matches = string.IsNullOrEmpty(query)
                ? _allTags
                : _allTags.Where(t =>
                    t.TagId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    t.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    t.Address.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    t.ConnId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (t.Unit != null && t.Unit.Contains(query, StringComparison.OrdinalIgnoreCase)));

            foreach (var tag in matches)
            {
                FilteredTags.Add(tag);
            }

            if (SelectedTag != null && !FilteredTags.Contains(SelectedTag))
            {
                SelectedTag = FilteredTags.FirstOrDefault();
            }
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
