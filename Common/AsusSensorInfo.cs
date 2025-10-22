using static ZenTimings.AsusWMI;

namespace ZenTimings
{
    /*
     * Sample data for core voltage from Crosshair VI Hero
     * 
     * Data_Type: 3
     * Location: 0
     * Name: CPU Core Voltage
     * Source: 1
     * Type: 0
     */
    public class AsusSensorInfo : ObservableObject
    {
        private byte _index;

        public byte Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }

        private AsusSensorDataType _dataType;

        public AsusSensorDataType DataType
        {
            get => _dataType;
            set => SetProperty(ref _dataType, value);
        }


        private AsusSensorLocation _location;

        public AsusSensorLocation Location
        {
            get => _location;
            set => SetProperty(ref _location, value);
        }

        private string _name;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private AsusSensorSource _source;

        public AsusSensorSource Source
        {
            get => _source;
            set => SetProperty(ref _source, value);
        }

        private AsusSensorType _type;

        public AsusSensorType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        private string _val;

        public string Value
        {
            get => _val;
            set => SetProperty(ref _val, value);
        }
    }
}