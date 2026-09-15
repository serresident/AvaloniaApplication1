using System;
using System.Collections.Generic;
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
        public string Hex { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public IBrush Brush { get; set; } = Brushes.Transparent;

        public ColorSwatchItem(string hex, string name)
        {
            Hex = hex;
            Name = $"{name} ({hex})";
            if (Color.TryParse(hex, out var c))
            {
                Brush = new SolidColorBrush(c);
            }
        }
    }

    public partial class ColorPickerBox : UserControl
    {
        public static readonly StyledProperty<string> SelectedColorHexProperty =
            AvaloniaProperty.Register<ColorPickerBox, string>(
                nameof(SelectedColorHex),
                "#00FF00",
                defaultBindingMode: BindingMode.TwoWay);

        public static readonly StyledProperty<Color> SelectedColorProperty =
            AvaloniaProperty.Register<ColorPickerBox, Color>(
                nameof(SelectedColor),
                Color.FromRgb(0, 255, 0));

        public static readonly StyledProperty<IBrush> SelectedBrushProperty =
            AvaloniaProperty.Register<ColorPickerBox, IBrush>(
                nameof(SelectedBrush),
                new SolidColorBrush(Color.FromRgb(0, 255, 0)));

        public static readonly StyledProperty<byte> RedProperty =
            AvaloniaProperty.Register<ColorPickerBox, byte>(nameof(Red), 0);

        public static readonly StyledProperty<byte> GreenProperty =
            AvaloniaProperty.Register<ColorPickerBox, byte>(nameof(Green), 255);

        public static readonly StyledProperty<byte> BlueProperty =
            AvaloniaProperty.Register<ColorPickerBox, byte>(nameof(Blue), 0);

        public static readonly StyledProperty<double> HueProperty =
            AvaloniaProperty.Register<ColorPickerBox, double>(nameof(Hue), 120.0);

        public string SelectedColorHex
        {
            get => GetValue(SelectedColorHexProperty);
            set => SetValue(SelectedColorHexProperty, value);
        }

        public Color SelectedColor
        {
            get => GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        public IBrush SelectedBrush
        {
            get => GetValue(SelectedBrushProperty);
            set => SetValue(SelectedBrushProperty, value);
        }

        public byte Red
        {
            get => GetValue(RedProperty);
            set => SetValue(RedProperty, value);
        }

        public byte Green
        {
            get => GetValue(GreenProperty);
            set => SetValue(GreenProperty, value);
        }

        public byte Blue
        {
            get => GetValue(BlueProperty);
            set => SetValue(BlueProperty, value);
        }

        public double Hue
        {
            get => GetValue(HueProperty);
            set => SetValue(HueProperty, value);
        }

        public List<ColorSwatchItem> Swatches { get; } = new()
        {
            // Green (Норма / Работа)
            new ColorSwatchItem("#00FF00", "Ярко-зеленый"),
            new ColorSwatchItem("#00E676", "Неоново-зеленый"),
            new ColorSwatchItem("#2ECC71", "Изумрудный"),
            new ColorSwatchItem("#76FF03", "Салатовый"),
            new ColorSwatchItem("#AEEA00", "Лайм"),
            new ColorSwatchItem("#64DD17", "Светло-зеленый"),

            // Red (Авария / Останов / Закрыто)
            new ColorSwatchItem("#FF0000", "Ярко-красный"),
            new ColorSwatchItem("#E74C3C", "Коралловый"),
            new ColorSwatchItem("#FF5252", "Алый"),
            new ColorSwatchItem("#D50000", "Рубиновый"),
            new ColorSwatchItem("#880000", "Бордовый"),
            new ColorSwatchItem("#440000", "Темно-красный"),

            // Yellow / Orange (Предупреждение / Внимание)
            new ColorSwatchItem("#FFFF00", "Желтый"),
            new ColorSwatchItem("#FFD700", "Золотой"),
            new ColorSwatchItem("#FFC107", "Янтарный"),
            new ColorSwatchItem("#FFA000", "Охра"),
            new ColorSwatchItem("#FF6D00", "Оранжевый"),
            new ColorSwatchItem("#FF9800", "Светло-оранжевый"),

            // Blue / Cyan (Процесс / Вода / Инфо)
            new ColorSwatchItem("#00AAFF", "Небесно-голубой"),
            new ColorSwatchItem("#007ACC", "Синий"),
            new ColorSwatchItem("#0050EF", "Индиго"),
            new ColorSwatchItem("#00FFFF", "Cyan"),
            new ColorSwatchItem("#00FFCC", "Бирюзовый"),
            new ColorSwatchItem("#29B6F6", "Голубой"),

            // Purple / Magenta (Спец / Химия)
            new ColorSwatchItem("#9C27B0", "Фиолетовый"),
            new ColorSwatchItem("#BA68C8", "Сиреневый"),
            new ColorSwatchItem("#E040FB", "Маджента"),
            new ColorSwatchItem("#D80073", "Пурпурный"),
            new ColorSwatchItem("#E91E63", "Розовый"),
            new ColorSwatchItem("#FF4081", "Неоново-розовый"),

            // Monochrome (Металл / Корпуса / Фон)
            new ColorSwatchItem("#FFFFFF", "Белый"),
            new ColorSwatchItem("#CCCCCC", "Светло-серый"),
            new ColorSwatchItem("#888888", "Серый"),
            new ColorSwatchItem("#555555", "Темно-серый"),
            new ColorSwatchItem("#222222", "Антрацит"),
            new ColorSwatchItem("#000000", "Черный")
        };

        private bool _isInternalUpdate;

        public ColorPickerBox()
        {
            InitializeComponent();

            SelectedColorHexProperty.Changed.AddClassHandler<ColorPickerBox>((x, e) => x.OnSelectedColorHexChanged(e));
            RedProperty.Changed.AddClassHandler<ColorPickerBox>((x, e) => x.OnRgbComponentChanged());
            GreenProperty.Changed.AddClassHandler<ColorPickerBox>((x, e) => x.OnRgbComponentChanged());
            BlueProperty.Changed.AddClassHandler<ColorPickerBox>((x, e) => x.OnRgbComponentChanged());
            HueProperty.Changed.AddClassHandler<ColorPickerBox>((x, e) => x.OnHueChanged());

            UpdateFromHex(SelectedColorHex);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnSelectedColorHexChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_isInternalUpdate) return;
            if (e.NewValue is string hex)
            {
                UpdateFromHex(hex);
            }
        }

        private void UpdateFromHex(string? hex)
        {
            if (TryParseHex(hex, out var c))
            {
                _isInternalUpdate = true;
                try
                {
                    SelectedColor = c;
                    SelectedBrush = new SolidColorBrush(c);
                    Red = c.R;
                    Green = c.G;
                    Blue = c.B;
                    var (h, _, _) = RgbToHsv(c.R, c.G, c.B);
                    Hue = Math.Round(h);
                }
                finally
                {
                    _isInternalUpdate = false;
                }
            }
        }

        private void OnRgbComponentChanged()
        {
            if (_isInternalUpdate) return;

            _isInternalUpdate = true;
            try
            {
                var color = Color.FromRgb(Red, Green, Blue);
                SelectedColor = color;
                SelectedBrush = new SolidColorBrush(color);
                SelectedColorHex = $"#{Red:X2}{Green:X2}{Blue:X2}";

                var (h, _, _) = RgbToHsv(Red, Green, Blue);
                Hue = Math.Round(h);
            }
            finally
            {
                _isInternalUpdate = false;
            }
        }

        private void OnHueChanged()
        {
            if (_isInternalUpdate) return;

            _isInternalUpdate = true;
            try
            {
                // Convert current Hue to RGB using current saturation & value if non-zero, or default S=1, V=1
                var (_, currentS, currentV) = RgbToHsv(Red, Green, Blue);
                if (currentS < 0.1 && currentV < 0.1)
                {
                    currentS = 1.0;
                    currentV = 1.0;
                }
                else if (currentS < 0.1)
                {
                    currentS = 1.0;
                }

                var (r, g, b) = HsvToRgb(Hue, currentS, currentV);
                Red = r;
                Green = g;
                Blue = b;

                var color = Color.FromRgb(r, g, b);
                SelectedColor = color;
                SelectedBrush = new SolidColorBrush(color);
                SelectedColorHex = $"#{r:X2}{g:X2}{b:X2}";
            }
            finally
            {
                _isInternalUpdate = false;
            }
        }

        private void Swatch_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                SelectedColorHex = hex;
                UpdateFromHex(hex);
            }
        }

        private void PaletteButton_Click(object? sender, RoutedEventArgs e)
        {
            var swatchBtn = this.FindControl<Button>("SwatchButton");
            if (swatchBtn?.Flyout != null)
            {
                swatchBtn.Flyout.ShowAt(swatchBtn);
            }
        }

        private void CloseFlyout_Click(object? sender, RoutedEventArgs e)
        {
            var swatchBtn = this.FindControl<Button>("SwatchButton");
            swatchBtn?.Flyout?.Hide();
        }

        public static bool TryParseHex(string? hex, out Color color)
        {
            color = Colors.Transparent;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            var clean = hex.Trim();
            if (!clean.StartsWith("#")) clean = "#" + clean;
            return Color.TryParse(clean, out color);
        }

        public static (double h, double s, double v) RgbToHsv(byte r, byte g, byte b)
        {
            double rNorm = r / 255.0;
            double gNorm = g / 255.0;
            double bNorm = b / 255.0;

            double max = Math.Max(rNorm, Math.Max(gNorm, bNorm));
            double min = Math.Min(rNorm, Math.Min(gNorm, bNorm));
            double delta = max - min;

            double h = 0;
            if (delta > 0.0001)
            {
                if (Math.Abs(max - rNorm) < 0.0001)
                    h = 60 * (((gNorm - bNorm) / delta) % 6);
                else if (Math.Abs(max - gNorm) < 0.0001)
                    h = 60 * (((bNorm - rNorm) / delta) + 2);
                else
                    h = 60 * (((rNorm - gNorm) / delta) + 4);

                if (h < 0) h += 360;
            }

            double s = max > 0.0001 ? delta / max : 0;
            double v = max;

            return (h, s, v);
        }

        public static (byte r, byte g, byte b) HsvToRgb(double h, double s, double v)
        {
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = v - c;

            double rPrime = 0, gPrime = 0, bPrime = 0;
            if (h < 60) { rPrime = c; gPrime = x; bPrime = 0; }
            else if (h < 120) { rPrime = x; gPrime = c; bPrime = 0; }
            else if (h < 180) { rPrime = 0; gPrime = c; bPrime = x; }
            else if (h < 240) { rPrime = 0; gPrime = x; bPrime = c; }
            else if (h < 300) { rPrime = x; gPrime = 0; bPrime = c; }
            else { rPrime = c; gPrime = 0; bPrime = x; }

            return ((byte)Math.Clamp(Math.Round((rPrime + m) * 255), 0, 255),
                    (byte)Math.Clamp(Math.Round((gPrime + m) * 255), 0, 255),
                    (byte)Math.Clamp(Math.Round((bPrime + m) * 255), 0, 255));
        }
    }
}
