using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI;

namespace AvaloniaApplication1.ViewModels
{
    public partial class WidgetViewModelBase : ViewModelBase, IDisposable
    {
        public readonly IDataCoreService DataService;
        protected readonly CompositeDisposable Disposables = new();

        public IAlarmNotificationService? AlarmService { get; set; }
        public IChildWindowService? ChildWindowService { get; set; }
        public IDialogService? DialogService { get; set; }

        [ObservableProperty]
        private string _type = string.Empty;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private double _row;

        [ObservableProperty]
        private double _col;

        [ObservableProperty]
        private int _sizeX;

        [ObservableProperty]
        private int _sizeY;
        
        [ObservableProperty]
        private DataSourceConfig? _source;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private double _fontSize;

        [ObservableProperty]
        private bool _autoScaleText = true;

        [ObservableProperty]
        private bool _showTitle = true;

        [ObservableProperty]
        private bool _showBorder = true;

        [ObservableProperty]
        private string _labelPosition = "Default";

        [ObservableProperty]
        private bool _compactMode;

        [ObservableProperty]
        private string? _groupId;

        // --- Rapid SCADA v6 Tag Quality & ISA-101 HMI State ---
        [ObservableProperty]
        private TagQuality _quality = TagQuality.Good;

        [ObservableProperty]
        private bool _isOverridden;

        [ObservableProperty]
        private bool _isStale;

        [ObservableProperty]
        private bool _hasCommError;

        [ObservableProperty]
        private double _qualityOpacity = 1.0;

        [ObservableProperty]
        private string _qualityGlyph = string.Empty;

        [ObservableProperty]
        private string _qualityGlyphColor = "Transparent";

        [ObservableProperty]
        private string? _qualityToolTip;

        partial void OnGroupIdChanged(string? value)
        {
            if (OriginalConfig != null)
                OriginalConfig.GroupId = value;
        }

        public IProjectContextService ProjectContext { get; }

        /// <summary>
        /// Reference to the original config for syncing back changes in Design Mode.
        /// </summary>
        public WidgetConfig OriginalConfig { get; }

        public WidgetViewModelBase(WidgetConfig config, IDataCoreService dataService, IProjectContextService projectContext)
        {
            DataService = dataService;
            ProjectContext = projectContext;
            OriginalConfig = config;
            Type = config.Type;
            Title = config.Title;
            Row = config.Position.Row;
            Col = config.Position.Col;
            SizeX = config.Position.SizeX;
            SizeY = config.Position.SizeY;
            Source = config.Source;

            FontSize = config.FontSize;
            AutoScaleText = config.AutoScaleText;
            ShowTitle = config.ShowTitle;
            ShowBorder = config.ShowBorder;
            LabelPosition = config.LabelPosition;
            CompactMode = config.CompactMode;
            GroupId = config.GroupId;

            InitializeTagSubscriptions();
        }

        private void InitializeTagSubscriptions()
        {
            if (Source == null) return;

            string? tagId = Source.TagId;
            string connId = Source.ConnId;
            string address = Source.Address;

            if (string.IsNullOrEmpty(tagId) && string.IsNullOrEmpty(address)) return;

            // Fetch initial state
            TagSnapshot initialSnapshot = default;
            if (!string.IsNullOrEmpty(tagId))
            {
                initialSnapshot = DataService.GetTagSnapshot(tagId);
            }
            if (initialSnapshot.Quality == TagQuality.Uninitialized && !string.IsNullOrEmpty(connId) && !string.IsNullOrEmpty(address))
            {
                initialSnapshot = DataService.GetTagSnapshot(connId, address);
            }

            if (initialSnapshot.Quality != TagQuality.Uninitialized && initialSnapshot.Value != null)
            {
                ApplySnapshot(initialSnapshot);
            }
            else if (!string.IsNullOrEmpty(connId) && !string.IsNullOrEmpty(address))
            {
                var initialVal = DataService.GetCurrentValue(connId, address);
                if (initialVal != null)
                {
                    OnTagValueUpdated(initialVal);
                }
            }

            // High-Performance TagManager Snapshot Stream
            DataService.SnapshotUpdates
                .Where(s => MatchesSource(s))
                .Subscribe(snapshot => Dispatcher.UIThread.Post(() => ApplySnapshot(snapshot)))
                .DisposeWith(Disposables);

            // Raw TagUpdates Stream for legacy/driver updates
            if (!string.IsNullOrEmpty(connId) && !string.IsNullOrEmpty(address))
            {
                DataService.TagUpdates
                    .Where(tag => tag.ConnId == connId && tag.Address == address)
                    .Subscribe(tag => Dispatcher.UIThread.Post(() =>
                    {
                        if (Quality == TagQuality.Good && tag.Value != null)
                        {
                            OnTagValueUpdated(tag.Value);
                        }
                    }))
                    .DisposeWith(Disposables);
            }
        }

        private bool MatchesSource(TagSnapshot s)
        {
            if (Source == null) return false;
            if (!string.IsNullOrEmpty(Source.TagId) && string.Equals(s.TagId, Source.TagId, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(Source.ConnId) && !string.IsNullOrEmpty(Source.Address))
            {
                var generatedId = ConfigMigrationService.GenerateTagId(Source.ConnId, Source.Address);
                if (string.Equals(s.TagId, generatedId, StringComparison.OrdinalIgnoreCase))
                    return true;

                var registered = DataService.TagManager?.FindTagByAddress(Source.ConnId, Source.Address);
                if (registered != null && string.Equals(s.TagId, registered.TagId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public virtual void ApplySnapshot(TagSnapshot snapshot)
        {
            Quality = snapshot.Quality;
            IsOverridden = snapshot.IsOverridden;
            IsStale = (Quality == TagQuality.Stale || Quality == TagQuality.Timeout);
            HasCommError = (Quality == TagQuality.Bad || Quality == TagQuality.CommFailure);

            // ISA-101 High-Performance HMI dimming & glyph feedback
            QualityOpacity = (IsStale || HasCommError) ? 0.55 : 1.0;

            if (IsOverridden)
            {
                QualityGlyph = "[M]";
                QualityGlyphColor = "#FF00FF";
                QualityToolTip = $"[M] Ручное переопределение оператором (Значение: {snapshot.Value})";
            }
            else if (IsStale)
            {
                QualityGlyph = "⏳";
                QualityGlyphColor = "#FFB300";
                QualityToolTip = $"⏳ Данные устарели (Stale): нет ответа от контроллера с {snapshot.Timestamp:HH:mm:ss}";
            }
            else if (HasCommError)
            {
                QualityGlyph = "⚠️";
                QualityGlyphColor = "#FF3B30";
                QualityToolTip = $"⚠️ Ошибка связи ({Quality}): {snapshot.ErrorMessage ?? "Контроллер недоступен"}";
            }
            else
            {
                QualityGlyph = string.Empty;
                QualityGlyphColor = "Transparent";
                QualityToolTip = $"Качество: Норма (Good) [Обновлено: {snapshot.Timestamp:HH:mm:ss}]";
            }

            OnTagSnapshotUpdated(snapshot);

            if (snapshot.Value != null)
            {
                OnTagValueUpdated(snapshot.Value);
            }
        }

        protected virtual void OnTagSnapshotUpdated(TagSnapshot snapshot)
        {
        }

        /// <summary>
        /// Called automatically on the UI thread when the specific tag for this widget is updated.
        /// Derived classes should override this to handle data changes.
        /// </summary>
        /// <param name="newValue">The newly received value from the network/mock.</param>
        protected virtual void OnTagValueUpdated(object newValue)
        {
        }

        #region Operator Force / Manual Override Commands

        [RelayCommand]
        public async Task ForceOverrideAsync()
        {
            if (Source == null) return;
            string tagId = Source.TagId ?? ConfigMigrationService.GenerateTagId(Source.ConnId, Source.Address);

            object? overrideVal = null;
            if (DialogService != null)
            {
                bool confirmed = await DialogService.ShowConfirmAsync(
                    "Подтверждение переопределения тега",
                    $"Вы действительно хотите принудительно задать значение для тега '{tagId}'?");
                if (!confirmed) return;

                var input = await DialogService.ShowNumpadAsync($"Ручное переопределение: {Title}", "1");
                if (string.IsNullOrEmpty(input)) return;

                if (double.TryParse(input, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double dVal))
                    overrideVal = dVal;
                else
                    overrideVal = input;
            }
            else
            {
                overrideVal = 1.0;
            }

            DataService.ForceOverride(tagId, overrideVal);
        }

        [RelayCommand]
        public void ClearOverride()
        {
            if (Source == null) return;
            string tagId = Source.TagId ?? ConfigMigrationService.GenerateTagId(Source.ConnId, Source.Address);
            DataService.ClearOverride(tagId);
        }

        #endregion

        public virtual void Dispose()
        {
            Disposables.Dispose(); // Clear all reactive subscriptions
            GC.SuppressFinalize(this);
        }
    }
}
