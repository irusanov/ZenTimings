using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ZenTimings.ViewModels
{
    public class ModuleViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string header;
        private string partNumber;
        private string manufacturer;
        private string capacity;
        private string rank;
        private string memoryChip;
        private string pmicVendor;
        private string pmicRevision;
        private bool hasPmic;
        private bool hasTelemetry;
        private bool hasLogo;
        private string logoResourceName;
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

        public string PartNumber
        {
            get => partNumber;
            set { partNumber = value; OnPropertyChanged(nameof(PartNumber)); }
        }

        public string Manufacturer
        {
            get => manufacturer;
            set { manufacturer = value; OnPropertyChanged(nameof(Manufacturer)); }
        }

        public string Capacity
        {
            get => capacity;
            set { capacity = value; OnPropertyChanged(nameof(Capacity)); }
        }

        public string Rank
        {
            get => rank;
            set { rank = value; OnPropertyChanged(nameof(Rank)); }
        }

        public string MemoryChip
        {
            get => memoryChip;
            set { memoryChip = value; OnPropertyChanged(nameof(MemoryChip)); }
        }

        public string PmicVendor
        {
            get => pmicVendor;
            set { pmicVendor = value; OnPropertyChanged(nameof(PmicVendor)); }
        }

        public string PmicRevision
        {
            get => pmicRevision;
            set { pmicRevision = value; OnPropertyChanged(nameof(PmicRevision)); }
        }

        public bool HasPmic
        {
            get => hasPmic;
            set { hasPmic = value; OnPropertyChanged(nameof(HasPmic)); }
        }

        public bool HasTelemetry
        {
            get => hasTelemetry;
            set
            {
                hasTelemetry = value;
                OnPropertyChanged(nameof(HasTelemetry));
                OnPropertyChanged(nameof(HasNoTelemetry));
            }
        }

        public bool HasNoTelemetry => !HasTelemetry;

        public bool HasLogo
        {
            get => hasLogo;
            set { hasLogo = value; OnPropertyChanged(nameof(HasLogo)); }
        }

        public string LogoResourceName
        {
            get => logoResourceName;
            set { logoResourceName = value; OnPropertyChanged(nameof(LogoResourceName)); }
        }

        public ObservableCollection<TelemetryItemViewModel> TelemetryItems { get; } = new ObservableCollection<TelemetryItemViewModel>();

        public List<string> HiddenKeys { get; } = new List<string>();

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
