using System;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace AvaloniaApplication1.Views.Controls
{
    public partial class FormatEditorBox : UserControl
    {
        public static readonly StyledProperty<string> FormatProperty =
            AvaloniaProperty.Register<FormatEditorBox, string>(
                nameof(Format),
                "{0}",
                defaultBindingMode: BindingMode.TwoWay);

        public string Format
        {
            get => GetValue(FormatProperty);
            set => SetValue(FormatProperty, value);
        }

        private static readonly Regex FormatPatternRegex = new(@"\{0(?::[^}]+)?\}", RegexOptions.Compiled);

        public FormatEditorBox()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void PresetItem_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string preset)
            {
                Format = preset;
            }
        }

        private void PrecisionButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                string precision = btn.Tag as string ?? string.Empty;
                ApplyPrecision(precision);
            }
        }

        public void ApplyPrecision(string precision)
        {
            string targetSpecifier = string.IsNullOrEmpty(precision) ? "{0}" : $"{{0:{precision}}}";
            string current = Format ?? string.Empty;

            if (string.IsNullOrWhiteSpace(current))
            {
                Format = targetSpecifier;
                return;
            }

            if (FormatPatternRegex.IsMatch(current))
            {
                Format = FormatPatternRegex.Replace(current, targetSpecifier, 1);
            }
            else
            {
                // Current string contains custom unit without {0} (e.g. "°C")
                Format = $"{targetSpecifier} {current.Trim()}";
            }
        }
    }
}
