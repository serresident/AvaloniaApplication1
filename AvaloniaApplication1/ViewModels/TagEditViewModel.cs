using System;
using System.Collections.Generic;
using System.Linq;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Models.Config;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    public partial class TagEditViewModel : ViewModelBase
    {
        private readonly HashSet<string> _existingTagIds = new(StringComparer.OrdinalIgnoreCase);

        public bool IsNewTag { get; }

        public string WindowTitle => IsNewTag ? "➕ Добавление нового тега" : $"✏️ Редактирование тега [{TagId}]";

        [ObservableProperty]
        private string _tagId = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _connId = string.Empty;

        [ObservableProperty]
        private string _address = string.Empty;

        public string[] AvailableDataTypes { get; } = new[]
        {
            "Float32", "Int16", "Int32", "Double", "Bool", "UInt16", "UInt32", "String"
        };

        [ObservableProperty]
        private string _dataType = "Float32";

        [ObservableProperty]
        private string? _unit = string.Empty;

        [ObservableProperty]
        private double _scaleA = 1.0;

        [ObservableProperty]
        private double _scaleB = 0.0;

        [ObservableProperty]
        private double _deadband = 0.0;

        [ObservableProperty]
        private double _staleTimeoutSec = 5.0;

        [ObservableProperty]
        private string? _description = string.Empty;

        [ObservableProperty]
        private string? _errorMessage;

        public List<string> AvailableConnections { get; }

        public bool IsConfirmed { get; private set; }
        public TagDefinition? ResultTag { get; private set; }
        public Action? CloseAction { get; set; }

        public TagEditViewModel(
            TagDefinition? existingTag, 
            IEnumerable<ConnectionConfig> connections, 
            IEnumerable<TagDefinition> existingTags)
        {
            AvailableConnections = connections?.Select(c => c.Id).Distinct().ToList() ?? new List<string>();
            if (AvailableConnections.Count > 0 && string.IsNullOrEmpty(ConnId))
            {
                ConnId = AvailableConnections[0];
            }

            foreach (var t in existingTags ?? Enumerable.Empty<TagDefinition>())
            {
                if (!string.IsNullOrWhiteSpace(t.TagId))
                    _existingTagIds.Add(t.TagId);
            }

            if (existingTag != null)
            {
                IsNewTag = false;
                _tagId = existingTag.TagId;
                _name = existingTag.Name;
                _connId = existingTag.ConnId;
                _address = existingTag.Address;
                _dataType = string.IsNullOrWhiteSpace(existingTag.DataType) ? "Float32" : existingTag.DataType;
                _unit = existingTag.Unit;
                _scaleA = existingTag.ScaleA;
                _scaleB = existingTag.ScaleB;
                _deadband = existingTag.Deadband;
                _staleTimeoutSec = existingTag.StaleTimeout.TotalSeconds > 0 ? existingTag.StaleTimeout.TotalSeconds : 5.0;
                _description = existingTag.Description;
            }
            else
            {
                IsNewTag = true;
                _scaleA = 1.0;
                _scaleB = 0.0;
                _deadband = 0.0;
                _staleTimeoutSec = 5.0;
            }
        }

        [RelayCommand]
        private void ApplyUnitPreset(string preset)
        {
            Unit = preset;
        }

        [RelayCommand]
        private void Save()
        {
            ErrorMessage = null;

            if (string.IsNullOrWhiteSpace(TagId))
            {
                ErrorMessage = "Идентификатор тега (TagId) обязателен.";
                return;
            }

            var cleanTagId = TagId.Trim();

            if (IsNewTag && _existingTagIds.Contains(cleanTagId))
            {
                ErrorMessage = $"Тег с идентификатором '{cleanTagId}' уже существует в проекте.";
                return;
            }

            if (string.IsNullOrWhiteSpace(ConnId))
            {
                ErrorMessage = "Необходимо указать подключение / драйвер.";
                return;
            }

            if (string.IsNullOrWhiteSpace(Address))
            {
                ErrorMessage = "Необходимо указать адрес регистра или топик.";
                return;
            }

            ResultTag = new TagDefinition
            {
                TagId = cleanTagId,
                Name = string.IsNullOrWhiteSpace(Name) ? cleanTagId : Name.Trim(),
                ConnId = ConnId.Trim(),
                Address = Address.Trim(),
                DataType = string.IsNullOrWhiteSpace(DataType) ? "Float32" : DataType.Trim(),
                Unit = string.IsNullOrWhiteSpace(Unit) ? null : Unit.Trim(),
                ScaleA = ScaleA,
                ScaleB = ScaleB,
                Deadband = Math.Max(0, Deadband),
                StaleTimeout = TimeSpan.FromSeconds(Math.Max(0.5, StaleTimeoutSec)),
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim()
            };

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
