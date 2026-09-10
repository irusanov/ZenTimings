using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ZenTimings.ViewModels
{
    public class SensorGroupViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string header;
        private int hiddenCount;

        public string Header
        {
            get => header;
            set { header = value; OnPropertyChanged(nameof(Header)); OnPropertyChanged(nameof(HeaderDisplay)); }
        }

        public int HiddenCount
        {
            get => hiddenCount;
            set { hiddenCount = value; OnPropertyChanged(nameof(HiddenCount)); OnPropertyChanged(nameof(HeaderDisplay)); }
        }

        public string HeaderDisplay => HiddenCount > 0 ? $"{Header} ({HiddenCount} hidden)" : Header;

        public ObservableCollection<TelemetryItemViewModel> TelemetryItems { get; } = new ObservableCollection<TelemetryItemViewModel>();

        public List<string> HiddenKeys { get; } = new List<string>();

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
