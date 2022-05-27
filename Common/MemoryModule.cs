using System.Collections;

namespace ZenTimings
{
    public enum MemRank
    {
        SR = 0,
        DR = 1,
        QR = 2,
    }

    public class MemoryModule : IEnumerable
    {
        public string BankLabel { get; set; }
        public string PartNumber { get; set; }
        public string Manufacturer { get; set; }
        public string DeviceLocator { get; set; }
        public ulong Capacity { get; set; }
        public uint ClockSpeed { get; set; }
        public MemRank Rank { get; set; }
        public string Slot { get; set; } = "";
        public uint DctOffset { get; set; } = 0;

        public MemoryModule(string partNumber, string bankLabel, string manufacturer,
            string deviceLocator, ulong capacity, uint clockSpeed)
        {
            PartNumber = partNumber;
            Capacity = capacity;
            ClockSpeed = clockSpeed;
            BankLabel = bankLabel;
            Manufacturer = manufacturer;
            DeviceLocator = deviceLocator;
        }

        public IEnumerator GetEnumerator()
        {
            return ((IEnumerable) PartNumber).GetEnumerator();
        }
    }
}