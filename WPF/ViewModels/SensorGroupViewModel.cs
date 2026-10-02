using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ZenTimings.ViewModels
{
    // Base for every section shown in the Sensors window
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
            set { hiddenCount = value; OnPropertyChanged(nameof(HiddenCount)); OnPropertyChanged(nameof(HeaderDisplay)); OnPropertyChanged(nameof(HiddenDisplay)); }
        }

        public string HeaderDisplay => HiddenCount > 0 ? $"{Header} ({HiddenCount} hidden)" : Header;

        public string HiddenDisplay => HiddenCount > 0 ? $"({HiddenCount} hidden)" : string.Empty;

        private bool isExpanded = true;
        private bool showModuleInfo = true;

        // Stable key the collapsed state is saved under; defaults to the header.
        private string sectionKey;
        public string SectionKey
        {
            get => sectionKey ?? header;
            set => sectionKey = value;
        }

        public bool ShowModuleInfo
        {
            get => showModuleInfo;
            set { showModuleInfo = value; OnPropertyChanged(nameof(ShowModuleInfo)); OnPropertyChanged(nameof(IsModuleInfoVisible)); }
        }

        public bool IsModuleInfoVisible => HasModuleInfo && ShowModuleInfo;

        public bool IsExpanded
        {
            get => isExpanded;
            set { isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); }
        }

        public virtual bool HasTelemetry { get; set; } = true;

        public bool HasNoTelemetry => !HasTelemetry;

        // Drives the module info block
        public virtual bool HasModuleInfo => false;

        public ObservableCollection<TelemetryItemViewModel> TelemetryItems { get; } = new ObservableCollection<TelemetryItemViewModel>();

        public List<string> HiddenKeys { get; } = new List<string>();

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
