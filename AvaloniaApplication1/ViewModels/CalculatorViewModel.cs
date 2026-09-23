using System;
using System.Collections.ObjectModel;
using System.Globalization;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    public class CalculationHistoryItem
    {
        public string Expression { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string FullDisplay => $"{Expression} {Result}";
    }

    public partial class CalculatorViewModel : ViewModelBase
    {
        private readonly IHmiClipboardService? _clipboard;

        [ObservableProperty]
        private string _display = "0";

        [ObservableProperty]
        private string _expressionHistory = string.Empty;

        [ObservableProperty]
        private int _selectedTabIndex = 0; // 0: Обычный, 1: Масса/Объем, 2: Давление

        private double _firstOperand = 0;
        private string _pendingOperation = string.Empty;
        private bool _isNewNumber = true;

        #region Memory (MC, MR, M+, M-, MS)
        [ObservableProperty]
        private double _memoryValue = 0;

        [ObservableProperty]
        private bool _hasMemory = false;
        #endregion

        #region History Log
        public ObservableCollection<CalculationHistoryItem> HistoryLog { get; } = new();

        [ObservableProperty]
        private bool _isHistoryOpen = false;

        public Action<bool>? OnToggleHistory { get; set; }
        public Action? CloseAction { get; set; }

        [ObservableProperty]
        private string _clipboardStatusMessage = string.Empty;
        #endregion

        #region Tech Mode 1: Масса / Объем / Плотность
        [ObservableProperty]
        private double _techVolume = 10.0; // м³

        [ObservableProperty]
        private double _techDensity = 940.0; // кг/м³ (ММА ~940)

        [ObservableProperty]
        private double _techMass = 9400.0; // кг

        [ObservableProperty]
        private double _techMassTons = 9.4; // т
        #endregion

        #region Tech Mode 2: Давление
        [ObservableProperty]
        private double _pressureBar = 1.0;

        [ObservableProperty]
        private double _pressureKgs = 1.01972; // кгс/см²

        [ObservableProperty]
        private double _pressureKpa = 100.0; // кПа

        [ObservableProperty]
        private double _pressureMpa = 0.1; // МПа
        #endregion

        public CalculatorViewModel(IHmiClipboardService? clipboard = null)
        {
            _clipboard = clipboard;
            UpdateTechMass();
        }

        #region Standard Calculator Commands
        [RelayCommand]
        private void InputDigit(string digit)
        {
            if (_isNewNumber || Display == "0" || Display == "Ошибка")
            {
                Display = digit;
                _isNewNumber = false;
            }
            else
            {
                Display += digit;
            }
        }

        [RelayCommand]
        private void InputDecimal()
        {
            if (_isNewNumber || Display == "Ошибка")
            {
                Display = "0,";
                _isNewNumber = false;
            }
            else if (!Display.Contains(","))
            {
                Display += ",";
            }
        }

        [RelayCommand]
        private void SetOperation(string op)
        {
            if (double.TryParse(Display.Replace(",", "."), CultureInfo.InvariantCulture, out var currentVal))
            {
                if (!string.IsNullOrEmpty(_pendingOperation) && !_isNewNumber)
                {
                    CalculateResult();
                }
                else
                {
                    _firstOperand = currentVal;
                }

                _pendingOperation = op;
                ExpressionHistory = $"{_firstOperand} {op}";
                _isNewNumber = true;
            }
        }

        [RelayCommand]
        private void CalculateResult()
        {
            if (string.IsNullOrEmpty(_pendingOperation)) return;

            if (double.TryParse(Display.Replace(",", "."), CultureInfo.InvariantCulture, out var secondOperand))
            {
                double result = 0;
                bool error = false;

                switch (_pendingOperation)
                {
                    case "+": result = _firstOperand + secondOperand; break;
                    case "-": result = _firstOperand - secondOperand; break;
                    case "×":
                    case "*": result = _firstOperand * secondOperand; break;
                    case "÷":
                    case "/":
                        if (Math.Abs(secondOperand) < 1e-12)
                        {
                            error = true;
                        }
                        else
                        {
                            result = _firstOperand / secondOperand;
                        }
                        break;
                    case "%": result = _firstOperand * (secondOperand / 100.0); break;
                }

                if (error)
                {
                    Display = "Ошибка";
                    ExpressionHistory = string.Empty;
                }
                else
                {
                    var expr = $"{_firstOperand} {_pendingOperation} {secondOperand} =";
                    var resStr = result.ToString("G10", CultureInfo.CurrentCulture);
                    ExpressionHistory = expr;
                    Display = resStr;
                    _firstOperand = result;

                    AddHistoryEntry(expr, resStr);
                }

                _pendingOperation = string.Empty;
                _isNewNumber = true;
            }
        }

        [RelayCommand]
        private void Clear()
        {
            Display = "0";
            ExpressionHistory = string.Empty;
            _pendingOperation = string.Empty;
            _firstOperand = 0;
            _isNewNumber = true;
        }

        [RelayCommand]
        private void Backspace()
        {
            if (_isNewNumber || Display.Length <= 1 || Display == "Ошибка")
            {
                Display = "0";
                _isNewNumber = true;
            }
            else
            {
                Display = Display.Substring(0, Display.Length - 1);
            }
        }

        [RelayCommand]
        private void ToggleSign()
        {
            if (double.TryParse(Display.Replace(",", "."), CultureInfo.InvariantCulture, out var val))
            {
                val = -val;
                Display = val.ToString("G10", CultureInfo.CurrentCulture);
            }
        }

        [RelayCommand]
        private void Sqrt()
        {
            if (double.TryParse(Display.Replace(",", "."), CultureInfo.InvariantCulture, out var val))
            {
                if (val >= 0)
                {
                    var res = Math.Sqrt(val);
                    var expr = $"√({val}) =";
                    var resStr = res.ToString("G10", CultureInfo.CurrentCulture);
                    ExpressionHistory = expr;
                    Display = resStr;
                    _isNewNumber = true;

                    AddHistoryEntry(expr, resStr);
                }
                else
                {
                    Display = "Ошибка";
                }
            }
        }
        #endregion

        #region Memory Operations
        [RelayCommand]
        private void MemoryClear()
        {
            MemoryValue = 0;
            HasMemory = false;
        }

        [RelayCommand]
        private void MemoryRecall()
        {
            if (HasMemory)
            {
                Display = MemoryValue.ToString("G10", CultureInfo.CurrentCulture);
                _isNewNumber = true;
            }
        }

        [RelayCommand]
        private void MemoryAdd()
        {
            if (double.TryParse(Display.Replace(",", "."), CultureInfo.InvariantCulture, out var val))
            {
                MemoryValue += val;
                HasMemory = Math.Abs(MemoryValue) > 1e-12;
                _isNewNumber = true;
            }
        }

        [RelayCommand]
        private void MemorySubtract()
        {
            if (double.TryParse(Display.Replace(",", "."), CultureInfo.InvariantCulture, out var val))
            {
                MemoryValue -= val;
                HasMemory = Math.Abs(MemoryValue) > 1e-12;
                _isNewNumber = true;
            }
        }

        [RelayCommand]
        private void MemoryStore()
        {
            if (double.TryParse(Display.Replace(",", "."), CultureInfo.InvariantCulture, out var val))
            {
                MemoryValue = val;
                HasMemory = Math.Abs(MemoryValue) > 1e-12;
                _isNewNumber = true;
            }
        }
        #endregion

        #region History Operations
        private void AddHistoryEntry(string expression, string result)
        {
            HistoryLog.Insert(0, new CalculationHistoryItem
            {
                Expression = expression,
                Result = result,
                Timestamp = DateTime.Now.ToString("HH:mm:ss")
            });

            // Ограничение размера журнала
            while (HistoryLog.Count > 30)
            {
                HistoryLog.RemoveAt(HistoryLog.Count - 1);
            }
        }

        [RelayCommand]
        private void ToggleHistory()
        {
            IsHistoryOpen = !IsHistoryOpen;
            OnToggleHistory?.Invoke(IsHistoryOpen);
        }

        [RelayCommand]
        private void ClearHistory()
        {
            HistoryLog.Clear();
        }

        [RelayCommand]
        private void SelectHistoryItem(CalculationHistoryItem? item)
        {
            if (item != null)
            {
                Display = item.Result;
                _isNewNumber = true;
            }
        }
        #endregion

        #region Internal HMI Clipboard Operations
        [RelayCommand]
        public void CopyToClipboard(string? customVal = null)
        {
            var val = customVal ?? Display;
            if (!string.IsNullOrEmpty(val) && val != "Ошибка")
            {
                // Заменяем запятую на точку для удобства ввода в контроллеры
                val = val.Replace(",", ".");
                if (_clipboard != null)
                {
                    _clipboard.CurrentValue = val;
                }
                ClipboardStatusMessage = $"В буфере HMI: {val}";
            }
        }

        [RelayCommand]
        public void PasteFromClipboard()
        {
            if (_clipboard != null && !string.IsNullOrEmpty(_clipboard.CurrentValue))
            {
                Display = _clipboard.CurrentValue.Replace(".", ",");
                _isNewNumber = true;
            }
        }

        [RelayCommand]
        public void CloseWindow()
        {
            CloseAction?.Invoke();
        }
        #endregion

        #region Tech Mode Logic
        public void UpdateTechMass()
        {
            TechMass = Math.Round(TechVolume * TechDensity, 2);
            TechMassTons = Math.Round(TechMass / 1000.0, 3);
        }

        public void UpdateTechFromMass()
        {
            if (TechDensity > 0)
            {
                TechVolume = Math.Round(TechMass / TechDensity, 3);
                TechMassTons = Math.Round(TechMass / 1000.0, 3);
            }
        }

        private bool _isUpdatingPressure;

        public void UpdatePressureFromBar(double bar)
        {
            if (_isUpdatingPressure) return;
            _isUpdatingPressure = true;
            try
            {
                PressureBar = bar;
                PressureKgs = Math.Round(bar * 1.019716, 4);
                PressureKpa = Math.Round(bar * 100.0, 2);
                PressureMpa = Math.Round(bar * 0.1, 4);
            }
            finally
            {
                _isUpdatingPressure = false;
            }
        }

        public void UpdatePressureFromKgs(double kgs)
        {
            if (_isUpdatingPressure) return;
            double bar = kgs / 1.019716;
            UpdatePressureFromBar(bar);
        }
        #endregion
    }
}
