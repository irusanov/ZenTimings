using System.ComponentModel;
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
    public class AsusSensorInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(PropertyChangedEventArgs eventArgs)
        {
            PropertyChanged?.Invoke(this, eventArgs);
        }

        protected bool SetProperty<T>(ref T storage, T value, PropertyChangedEventArgs args)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(args);
            return true;
        }

        byte index;
        public byte Index
        {
            get => index;
            set => SetProperty(ref index, value, InternalEventArgsCache.Index);
        }

        AsusSensorDataType dataType;
        public AsusSensorDataType DataType
        {
            get => dataType;
            set => SetProperty(ref dataType, value, InternalEventArgsCache.DataType);
        }


        AsusSensorLocation location;
        public AsusSensorLocation Location
        {
            get => location;
            set => SetProperty(ref location, value, InternalEventArgsCache.Location);
        }

        string name;
        public string Name
        {
            get => name;
            set => SetProperty(ref name, value, InternalEventArgsCache.Name);
        }

        AsusSensorSource source;
        public AsusSensorSource Source
        {
            get => source;
            set => SetProperty(ref source, value, InternalEventArgsCache.Source);
        }

        AsusSensorType type;
        public AsusSensorType Type
        {
            get => type;
            set => SetProperty(ref type, value, InternalEventArgsCache.Type);
        }

        string val;
        public string Value
        {
            get => val;
            set => SetProperty(ref val, value, InternalEventArgsCache.Value);
        }

        internal static class InternalEventArgsCache
        {
            internal static PropertyChangedEventArgs Index = new PropertyChangedEventArgs("Index");
            internal static PropertyChangedEventArgs DataType = new PropertyChangedEventArgs("DataType");
            internal static PropertyChangedEventArgs Location = new PropertyChangedEventArgs("Location");
            internal static PropertyChangedEventArgs Name = new PropertyChangedEventArgs("Name");
            internal static PropertyChangedEventArgs Source = new PropertyChangedEventArgs("Source");
            internal static PropertyChangedEventArgs Type = new PropertyChangedEventArgs("Type");
            internal static PropertyChangedEventArgs Value = new PropertyChangedEventArgs("Value");
        }
    }
}
