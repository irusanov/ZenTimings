namespace ZenTimings.Common
{
    public class Sensor : ISensor
    {
        private float? currentValue;

        public int Index { get; }
        public string Name { get; }
        public float? Value
        {
            get => currentValue;
            set
            {
                if (value != null && value != currentValue)
                {
                    currentValue = value;

                    if (Max == null || currentValue > Max)
                        Max = currentValue;
                    else if (Min == null || currentValue < Min)
                        Min = currentValue;
                }
            }
        }
        public float? Min { get; private set; }
        public float? Max { get; private set; }

        public Sensor(string name, int index)
        {
            Name = name;
            Index = index;
        }

        public void ResetMin() => Min = null;
        public void ResetMax() => Max = null;
    }
}
