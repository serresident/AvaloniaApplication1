using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace AvaloniaApplication1.Views.Controls
{
    public class ColorSwatchItem
    {
        public string Hex  { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public IBrush Brush { get; set; } = Brushes.Transparent;

        public ColorSwatchItem(string hex, string name)
        {
            Hex  = hex;
            Name = $"{name} ({hex})";
            if (Color.TryParse(hex, out var c))
                Brush = new SolidColorBrush(c);
        }
    }

    /// <summary>
    /// Color picker UserControl.
    /// The single external binding point is <see cref="SelectedColorHexProperty"/> (StyledProperty, TwoWay).
    /// Everything else binds through DataContext=this (INotifyPropertyChanged).
    /// </summary>
    public partial class ColorPickerBox : UserControl, INotifyPropertyChanged
    {
        // ══════════════════ External StyledProperty ══════════════════
        public static readonly StyledProperty<string> SelectedColorHexProperty =
            AvaloniaProperty.Register<ColorPickerBox, string>(
                nameof(SelectedColorHex), "#00FF00",
                defaultBindingMode: BindingMode.TwoWay);

        public string SelectedColorHex
        {
            get => GetValue(SelectedColorHexProperty);
            set => SetValue(SelectedColorHexProperty, value);
        }

        // ══════════════════ INotifyPropertyChanged (for DataContext=this bindings) ══════════════════
        public new event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ── Visual brush shown on swatch button ──
        private IBrush _selectedBrush = new SolidColorBrush(Color.FromRgb(0, 255, 0));
        public IBrush SelectedBrush
        {
            get => _selectedBrush;
            private set { _selectedBrush = value; Notify(); }
        }

        // ── Sliders / spinners use double (Avalonia Slider.Value = double) ──
        private double _red   = 0;
        private double _green = 255;
        private double _blue  = 0;
        private double _hue   = 120;

        public double RedDouble
        {
            get => _red;
            set { _red = value; Notify(); if (!_busy) OnRgbChanged(); }
        }
        public double GreenDouble
        {
            get => _green;
            set { _green = value; Notify(); if (!_busy) OnRgbChanged(); }
        }
        public double BlueDouble
        {
            get => _blue;
            set { _blue = value; Notify(); if (!_busy) OnRgbChanged(); }
        }
        public double Hue
        {
            get => _hue;
            set { _hue = value; Notify(); Notify(nameof(HueText)); if (!_busy) OnHueChanged(); }
        }
        public string HueText => $"{_hue:0}°";

        // ══════════════════ Swatches ══════════════════
        public static readonly IReadOnlyList<ColorSwatchItem> Swatches = new List<ColorSwatchItem>
        {
            // Green — Норма / Работа
            new("#00FF00","Ярко-зеленый"), new("#00E676","Неоново-зеленый"), new("#2ECC71","Изумрудный"),
            new("#76FF03","Салатовый"),    new("#AEEA00","Лайм"),            new("#64DD17","Светло-зеленый"),
            // Red — Авария / Останов
            new("#FF0000","Ярко-красный"), new("#E74C3C","Коралловый"), new("#FF5252","Алый"),
            new("#D50000","Рубиновый"),    new("#880000","Бордовый"),   new("#440000","Темно-красный"),
            // Yellow/Orange — Предупреждение
            new("#FFFF00","Желтый"),  new("#FFD700","Золотой"),         new("#FFC107","Янтарный"),
            new("#FFA000","Охра"),    new("#FF6D00","Оранжевый"),       new("#FF9800","Светло-оранжевый"),
            // Blue/Cyan — Процесс / Вода
            new("#00AAFF","Небесно-голубой"), new("#007ACC","Синий"),  new("#0050EF","Индиго"),
            new("#00FFFF","Cyan"),            new("#00FFCC","Бирюзовый"), new("#29B6F6","Голубой"),
            // Purple/Magenta — Химия / Спец
            new("#9C27B0","Фиолетовый"), new("#BA68C8","Сиреневый"),  new("#E040FB","Маджента"),
            new("#D80073","Пурпурный"),  new("#E91E63","Розовый"),    new("#FF4081","Неоново-розовый"),
            // Monochrome
            new("#FFFFFF","Белый"),     new("#CCCCCC","Светло-серый"), new("#888888","Серый"),
            new("#555555","Темно-серый"),new("#222222","Антрацит"),    new("#000000","Черный"),
        };

        private bool _busy;

        // ══════════════════ Constructor ══════════════════
        public ColorPickerBox()
        {
            DataContext = this;          // <── must come before Load!
            InitializeComponent();

            // Populate swatches imperatively
            if (this.FindControl<ItemsControl>("SwatchesItemsControl") is { } ic)
                ic.ItemsSource = Swatches;

            // React to external SelectedColorHex changes (VM → control)
            SelectedColorHexProperty.Changed.AddClassHandler<ColorPickerBox>((x, e) =>
            {
                if (!x._busy && e.NewValue is string hex)
                    x.SyncFromHex(hex);
            });

            SyncFromHex(SelectedColorHex);
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        // ══════════════════ Sync helpers ══════════════════

        private void SyncFromHex(string? hex)
        {
            if (!TryParseHex(hex, out var c)) return;
            _busy = true;
            try
            {
                SelectedBrush = new SolidColorBrush(c);
                _red   = c.R; Notify(nameof(RedDouble));
                _green = c.G; Notify(nameof(GreenDouble));
                _blue  = c.B; Notify(nameof(BlueDouble));
                var (h, _, _) = RgbToHsv(c.R, c.G, c.B);
                _hue = Math.Round(h); Notify(nameof(Hue)); Notify(nameof(HueText));
            }
            finally { _busy = false; }
        }

        private void OnRgbChanged()
        {
            _busy = true;
            try
            {
                byte r = ClipToByte(_red), g = ClipToByte(_green), b = ClipToByte(_blue);
                var color = Color.FromRgb(r, g, b);
                SelectedBrush = new SolidColorBrush(color);
                var hex = $"#{r:X2}{g:X2}{b:X2}";
                SetValue(SelectedColorHexProperty, hex);   // propagate to VM
                Notify(nameof(SelectedColorHex));
                var (h, _, _) = RgbToHsv(r, g, b);
                _hue = Math.Round(h); Notify(nameof(Hue)); Notify(nameof(HueText));
            }
            finally { _busy = false; }
        }

        private void OnHueChanged()
        {
            _busy = true;
            try
            {
                byte r0 = ClipToByte(_red), g0 = ClipToByte(_green), b0 = ClipToByte(_blue);
                var (_, s, v) = RgbToHsv(r0, g0, b0);
                if (s < 0.1 && v < 0.1) { s = 1; v = 1; }
                else if (s < 0.1) s = 1;
                var (r, g, b) = HsvToRgb(_hue, s, v);
                _red = r; _green = g; _blue = b;
                Notify(nameof(RedDouble)); Notify(nameof(GreenDouble)); Notify(nameof(BlueDouble));
                var color = Color.FromRgb(r, g, b);
                SelectedBrush = new SolidColorBrush(color);
                var hex = $"#{r:X2}{g:X2}{b:X2}";
                SetValue(SelectedColorHexProperty, hex);
                Notify(nameof(SelectedColorHex));
            }
            finally { _busy = false; }
        }

        private static byte ClipToByte(double v) => (byte)Math.Clamp(Math.Round(v), 0, 255);

        // ══════════════════ Event handlers ══════════════════

        private void Swatch_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string hex })
            {
                SetValue(SelectedColorHexProperty, hex);
                SyncFromHex(hex);
                Notify(nameof(SelectedColorHex));
            }
        }

        private void PaletteButton_Click(object? sender, RoutedEventArgs e)
        {
            var btn = this.FindControl<Button>("SwatchButton");
            btn?.Flyout?.ShowAt(btn);
        }

        private void CloseFlyout_Click(object? sender, RoutedEventArgs e)
            => this.FindControl<Button>("SwatchButton")?.Flyout?.Hide();

        private void HexBox_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                var hex = tb.Text ?? string.Empty;
                if (TryParseHex(hex, out _))
                {
                    if (!hex.StartsWith('#')) hex = '#' + hex;
                    SetValue(SelectedColorHexProperty, hex);
                    SyncFromHex(hex);
                    Notify(nameof(SelectedColorHex));
                }
            }
        }

        // ══════════════════ Color math ══════════════════

        public static bool TryParseHex(string? hex, out Color color)
        {
            color = Colors.Transparent;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            var s = hex.Trim();
            if (!s.StartsWith('#')) s = '#' + s;
            return Color.TryParse(s, out color);
        }

        public static (double h, double s, double v) RgbToHsv(byte r, byte g, byte b)
        {
            double rN = r / 255.0, gN = g / 255.0, bN = b / 255.0;
            double max = Math.Max(rN, Math.Max(gN, bN));
            double min = Math.Min(rN, Math.Min(gN, bN));
            double delta = max - min;
            double h = 0;
            if (delta > 0.0001)
            {
                if (Math.Abs(max - rN) < 0.0001)      h = 60 * (((gN - bN) / delta) % 6);
                else if (Math.Abs(max - gN) < 0.0001) h = 60 * (((bN - rN) / delta) + 2);
                else                                   h = 60 * (((rN - gN) / delta) + 4);
                if (h < 0) h += 360;
            }
            return (h, max > 0.0001 ? delta / max : 0, max);
        }

        public static (byte r, byte g, byte b) HsvToRgb(double h, double s, double v)
        {
            double c = v * s, x = c * (1 - Math.Abs((h / 60) % 2 - 1)), m = v - c;
            double rP = 0, gP = 0, bP = 0;
            if      (h < 60)  { rP = c; gP = x; }
            else if (h < 120) { rP = x; gP = c; }
            else if (h < 180) { gP = c; bP = x; }
            else if (h < 240) { gP = x; bP = c; }
            else if (h < 300) { rP = x; bP = c; }
            else              { rP = c; bP = x; }
            return (ClipToByte((rP + m) * 255), ClipToByte((gP + m) * 255), ClipToByte((bP + m) * 255));
        }
    }
}
