using System.ComponentModel;
using System.Globalization;
using ZenTimings.Localization;
using ZenTimings.Windows;

namespace ZenTimings.ViewModels
{
    public class TelemetryItemViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private double currentValue;
        private double minValue = double.MaxValue;
        private double maxValue = double.MinValue;
        private double sum = 0;
        private int count = 0;
        private bool hasCurrentValue;

        // The texts the view shows. Change notifications are raised only when one of them actually
        // changes, so a steady sensor costs no binding or layout work on a refresh.
        private string currentText = "-";
        private string minText = "-";
        private string maxText = "-";
        private string averageText = "-";

        private readonly string unit;
        private readonly bool _isBoolean;

        // Live alarm state
        private ThermalAlarmLevel currentAlarmLevel;

        // Alarm states captured when values were recorded
        private ThermalAlarmLevel minAlarmLevel;
        private ThermalAlarmLevel maxAlarmLevel;

        public string Name { get; }

        // Identifies the sensor's group + name for persisting hide/unhide state.
        public string GroupKey { get; set; }

        public string DisplayName => GetDisplayName(Name, GroupKey);

        internal static string GetDisplayName(string name, string groupKey)
        {
            if (groupKey == "WHEA|Errors since boot")
                return UiText.Get("WheaErrorsSinceBoot");

            if (groupKey?.StartsWith("PMIC|", System.StringComparison.Ordinal) == true)
            {
                switch (name)
                {
                    case "PMIC Temp": return UiText.Get("PmicTemperature");
                    case "PMIC High Temp": return UiText.Get("PmicHighTemperature");
                    case "SPD Hub Temp": return UiText.Get("SpdHubTemperature");
                    case "DIMM Temp": return UiText.Get("DimmTemperature");
                    case "Total Power": return UiText.Get("TotalPower");
                }
            }

            return name;
        }

        public SensorIconKind IconKind => GetIconKind(unit);

        private static SensorIconKind GetIconKind(string unit)
        {
            switch (unit)
            {
                case "V": return SensorIconKind.Voltage;
                case "°C": return SensorIconKind.Temperature;
                case "W": return SensorIconKind.Power;
                case "RPM": return SensorIconKind.Fan;
                default: return SensorIconKind.Generic;
            }
        }

        public ThermalAlarmLevel CurrentAlarmLevel
        {
            get => currentAlarmLevel;
            private set
            {
                if (currentAlarmLevel == value)
                    return;
                currentAlarmLevel = value;
                OnPropertyChanged(nameof(CurrentAlarmLevel));
            }
        }

        public ThermalAlarmLevel MinAlarmLevel
        {
            get => minAlarmLevel;
            private set
            {
                if (minAlarmLevel == value)
                    return;
                minAlarmLevel = value;
                OnPropertyChanged(nameof(MinAlarmLevel));
            }
        }

        public ThermalAlarmLevel MaxAlarmLevel
        {
            get => maxAlarmLevel;
            private set
            {
                if (maxAlarmLevel == value)
                    return;
                maxAlarmLevel = value;
                OnPropertyChanged(nameof(MaxAlarmLevel));
            }
        }

        private string valueFormat;

        /// <summary>Numeric format of the values (e.g. "F0" for a count), null for the default by unit.</summary>
        public string ValueFormat
        {
            get => valueFormat;
            set
            {
                valueFormat = value;
                UpdateTexts(hasCurrentValue);
            }
        }

        public string Current => currentText;
        public string Min => minText;
        public string Max => maxText;
        public string Average => averageText;

        // True when current, min and max values are all zero (or min/max have never been recorded).
        public bool IsAllZero =>
            !_isBoolean &&
            currentValue == 0 &&
            (minValue == double.MaxValue || minValue == 0) &&
            (maxValue == double.MinValue || maxValue == 0);

        /// <param name="initialValue">Null when the sensor has no value yet (not measured): shown as "-" and not recorded.</param>
        public TelemetryItemViewModel(string name, double? initialValue, string unit = "")
        {
            Name = name;
            this.unit = unit;
            UpdateValue(initialValue);
        }

        public TelemetryItemViewModel(string name, bool initialValue)
        {
            Name = name;
            this.unit = "";
            _isBoolean = true;
            UpdateValue(initialValue ? 1.0 : 0.0);
        }

        /// <summary>
        /// A sensor without a value (not read or measured yet) shows "-" as the current value and is left out of min, max
        /// and average, so a sensor that starts empty does not record a 0.
        /// </summary>
        public void UpdateValue(double? value)
        {
            if (value.HasValue)
            {
                UpdateValue(value.Value);
                return;
            }

            hasCurrentValue = false;
            SetText(ref currentText, "-", nameof(Current));
        }

        public void UpdateValue(double value)
        {
            currentValue = value;
            hasCurrentValue = true;

            if (value < minValue)
            {
                minValue = value;

                // Preserve alarm state at recorded minimum
                MinAlarmLevel = CurrentAlarmLevel;
            }

            if (value > maxValue)
            {
                maxValue = value;

                // Preserve alarm state at recorded maximum
                MaxAlarmLevel = CurrentAlarmLevel;
            }

            sum += value;
            count++;

            UpdateTexts(true);
        }

        // Re-formats the shown values and notifies only those whose text changed.
        private void UpdateTexts(bool includeCurrent)
        {
            if (includeCurrent)
                SetText(ref currentText, FormatValue(currentValue), nameof(Current));
            SetText(ref minText, minValue != double.MaxValue ? FormatValue(minValue) : "-", nameof(Min));
            SetText(ref maxText, maxValue != double.MinValue ? FormatValue(maxValue) : "-", nameof(Max));
            SetText(ref averageText, count > 0 ? FormatValue(sum / count) : "-", nameof(Average));
        }

        private void SetText(ref string field, string text, string propertyName)
        {
            if (string.Equals(field, text))
                return;
            field = text;
            OnPropertyChanged(propertyName);
        }

        public void ResetStats()
        {
            if (!hasCurrentValue)
            {
                // Nothing measured yet: back to "never recorded"
                minValue = double.MaxValue;
                maxValue = double.MinValue;
                sum = 0;
                count = 0;
                UpdateTexts(false);
                return;
            }

            minValue = currentValue;
            maxValue = currentValue;
            sum = currentValue;
            count = 1;

            MinAlarmLevel = CurrentAlarmLevel;
            MaxAlarmLevel = CurrentAlarmLevel;

            UpdateTexts(false);
        }

        public void UpdateThermalAlarm(bool critHigh, bool high)
        {
            if (critHigh)
                CurrentAlarmLevel = ThermalAlarmLevel.CriticalHigh;
            else if (high)
                CurrentAlarmLevel = ThermalAlarmLevel.High;
            else
                CurrentAlarmLevel = ThermalAlarmLevel.None;
        }

        private string FormatValue(double value)
        {
            if (_isBoolean)
                return value >= 0.5 ? "Yes" : "No";

            // Clocks are measured to 0.01 MHz; a third decimal would only widen the column
            string format = valueFormat ?? (unit == "°C" || unit == "MHz" ? "F2" : unit == "RPM" ? "F0" : "F3");
            string text = value.ToString(format, CultureInfo.InvariantCulture);
            return string.IsNullOrEmpty(unit) ? text : $"{text} {unit}";
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
