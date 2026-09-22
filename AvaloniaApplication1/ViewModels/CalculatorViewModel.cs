using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    public partial class CalculatorViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _display = "0";

        [ObservableProperty]
        private string _expressionHistory = string.Empty;

        [ObservableProperty]
        private int _selectedTabIndex = 0; // 0: Обычный, 1: Масса/Объем, 2: Давление

        private double _firstOperand = 0;
        private string _pendingOperation = string.Empty;
        private bool _isNewNumber = true;

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

        public CalculatorViewModel()
        {
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
                    ExpressionHistory = $"{_firstOperand} {_pendingOperation} {secondOperand} =";
                    Display = result.ToString("G10", CultureInfo.CurrentCulture);
                    _firstOperand = result;
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
                    ExpressionHistory = $"√({val}) =";
                    Display = res.ToString("G10", CultureInfo.CurrentCulture);
                    _isNewNumber = true;
                }
                else
                {
                    Display = "Ошибка";
                }
            }
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
