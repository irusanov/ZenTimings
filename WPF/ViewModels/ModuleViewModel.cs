namespace ZenTimings.ViewModels
{
    // A memory module's section in the Sensors window. Shares the header/telemetry-grid shape of
    // SensorGroupViewModel and adds the module-info block (vendor, capacity, rank, DRAM IC, PMIC, logo).
    public class ModuleViewModel : SensorGroupViewModel
    {
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

        public override bool HasTelemetry
        {
            get => hasTelemetry;
            set
            {
                hasTelemetry = value;
                OnPropertyChanged(nameof(HasTelemetry));
                OnPropertyChanged(nameof(HasNoTelemetry));
            }
        }

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

        public override bool HasModuleInfo => true;
    }
}
