using System.Collections;

namespace ZenTimings
{
    class MemoryModule: IEnumerable
    {
        public string PartNumber { get; set; } = "";
        public ulong Capacity { get; set; } = 0;
        public uint ClockSpeed { get; set; } = 0;

        public MemoryModule(string partNumber, ulong capacity, uint clockSpeed)
        {
            PartNumber = partNumber;
            Capacity = capacity;
            ClockSpeed = clockSpeed;
        }

        public IEnumerator GetEnumerator()
        {
            return ((IEnumerable)PartNumber).GetEnumerator();
        }
    }
}
