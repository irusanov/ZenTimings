using System.ComponentModel;
using System.Globalization;
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
                currentAlarmLevel = value;
                OnPropertyChanged(nameof(CurrentAlarmLevel));
            }
        }

        public ThermalAlarmLevel MinAlarmLevel
        {
            get => minAlarmLevel;
            private set
            {
                minAlarmLevel = value;
                OnPropertyChanged(nameof(MinAlarmLevel));
            }
        }

        public ThermalAlarmLevel MaxAlarmLevel
        {
            get => maxAlarmLevel;
            private set
            {
                maxAlarmLevel = value;
                OnPropertyChanged(nameof(MaxAlarmLevel));
            }
        }

        public string Current => FormatValue(currentValue);
        public string Min => minValue != double.MaxValue ? FormatValue(minValue) : "-";
        public string Max => maxValue != double.MinValue ? FormatValue(maxValue) : "-";
        public string Average => count > 0 ? FormatValue(sum / count) : "-";

        // True when current, min and max values are all zero (or min/max have never been recorded).
        public bool IsAllZero =>
            !_isBoolean &&
            currentValue == 0 &&
            (minValue == double.MaxValue || minValue == 0) &&
            (maxValue == double.MinValue || maxValue == 0);

        public TelemetryItemViewModel(string name, double initialValue, string unit = "")
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

        public void UpdateValue(double value)
        {
            currentValue = value;

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

            OnPropertyChanged(nameof(Current));
            OnPropertyChanged(nameof(Min));
            OnPropertyChanged(nameof(Max));
            OnPropertyChanged(nameof(Average));
        }

        public void ResetStats()
        {
            minValue = currentValue;
            maxValue = currentValue;
            sum = currentValue;
            count = 1;

            MinAlarmLevel = CurrentAlarmLevel;
            MaxAlarmLevel = CurrentAlarmLevel;

            OnPropertyChanged(nameof(Min));
            OnPropertyChanged(nameof(Max));
            OnPropertyChanged(nameof(Average));
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

            string format = unit == "°C" ? "F2" : unit == "RPM" ? "F0" : "F3";
            return $"{value.ToString(format, CultureInfo.InvariantCulture)} {unit}";
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
