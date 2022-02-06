namespace ZenTimings.Common
{
    public class Sensor : ISensor
    {
        private float? currentValue;
        private float? currentMin;
        private float? currentMax;

        public int Index { get; }
        public string Name { get; }
        public float? Value
        {
            get => currentValue;
            set {
                if (value != null && value != currentValue)
                {
                    currentValue = value;

                    if (currentMax == null || currentValue > currentMax)
                        currentMax = currentValue;
                    else if (currentMin == null || currentValue < currentMin)
                        currentMin = currentValue;
                }
            }
        }
        public float? Min => currentMin;
        public float? Max => currentMax;

        public Sensor(string name, int index)
        {
            Name = name;
            Index = index;
        }

        public void ResetMin() => currentMin = null;
        public void ResetMax() => currentMax = null;
    }
}
