using System;
using System.Collections.Generic;
using System.Linq;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    public partial class TagImportExportViewModel : ViewModelBase
    {
        private readonly List<TagDefinition> _currentTags;
        private readonly IHmiClipboardService? _clipboard;

        public bool IsImportMode { get; }

        public string WindowTitle => IsImportMode 
            ? "📥 Импорт базы сигналов и тегов (CSV / JSON)" 
            : "📤 Экспорт базы тегов (CSV / JSON)";

        [ObservableProperty]
        private string _selectedFormat = "CSV";

        [ObservableProperty]
        private string _contentText = string.Empty;

        [ObservableProperty]
        private string? _statusMessage;

        [ObservableProperty]
        private int _tagsCount;

        public bool IsConfirmed { get; private set; }
        public List<TagDefinition> ResultTags { get; private set; } = new();
        public Action? CloseAction { get; set; }

        public TagImportExportViewModel(
            bool isImportMode, 
            IEnumerable<TagDefinition> currentTags, 
            IHmiClipboardService? clipboard = null)
        {
            IsImportMode = isImportMode;
            _currentTags = currentTags?.ToList() ?? new List<TagDefinition>();
            _clipboard = clipboard;

            TagsCount = _currentTags.Count;

            if (!IsImportMode)
            {
                GenerateExportText();
            }
        }

        partial void OnSelectedFormatChanged(string value)
        {
            if (!IsImportMode)
            {
                GenerateExportText();
            }
        }

        private void GenerateExportText()
        {
            if (string.Equals(SelectedFormat, "JSON", StringComparison.OrdinalIgnoreCase))
            {
                ContentText = TagExportImportService.ExportJson(_currentTags);
            }
            else
            {
                ContentText = TagExportImportService.ExportCsv(_currentTags);
            }
            StatusMessage = $"Сгенерировано {_currentTags.Count} тегов в формате {SelectedFormat}.";
        }

        [RelayCommand]
        private void SelectCsvFormat()
        {
            SelectedFormat = "CSV";
        }

        [RelayCommand]
        private void SelectJsonFormat()
        {
            SelectedFormat = "JSON";
        }

        [RelayCommand]
        private void CopyToClipboard()
        {
            if (_clipboard != null && !string.IsNullOrEmpty(ContentText))
            {
                _clipboard.CurrentValue = ContentText;
                StatusMessage = "Текст скопирован во внутренний буфер обмена.";
            }
        }

        [RelayCommand]
        private void PasteFromClipboard()
        {
            if (_clipboard != null && !string.IsNullOrEmpty(_clipboard.CurrentValue))
            {
                ContentText = _clipboard.CurrentValue;
                StatusMessage = "Текст вставлен из буфера обмена.";
            }
        }

        [RelayCommand]
        private void ApplyImport()
        {
            StatusMessage = null;

            if (string.IsNullOrWhiteSpace(ContentText))
            {
                StatusMessage = "Текст для импорта пуст.";
                return;
            }

            List<TagDefinition> imported;
            var trimmed = ContentText.Trim();
            if (trimmed.StartsWith("[") || trimmed.StartsWith("{"))
            {
                imported = TagExportImportService.ImportJson(trimmed);
            }
            else
            {
                imported = TagExportImportService.ImportCsv(trimmed);
            }

            if (imported.Count == 0)
            {
                StatusMessage = "Не удалось распознать ни одного тега из введённого текста.";
                return;
            }

            ResultTags = imported;
            IsConfirmed = true;
            CloseAction?.Invoke();
        }

        [RelayCommand]
        private void Cancel()
        {
            IsConfirmed = false;
            CloseAction?.Invoke();
        }
    }
}
