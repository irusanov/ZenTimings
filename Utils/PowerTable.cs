using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using ZenStates;

namespace ZenTimings.Utils
{
    public class PowerTable : INotifyPropertyChanged
    {
        public const uint tableSize = 0x7E4;
        private uint[] table = new uint[tableSize];

        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T storage, T value, PropertyChangedEventArgs args)
        {
            if (Equals(storage, value) || value == null) return false;
            storage = value;
            OnPropertyChanged(args);
            return true;
        }

        [Serializable]
        [StructLayout(LayoutKind.Explicit)]
        private struct PowerTableAPU0
        {
            [FieldOffset(0x104)] public uint VddcrSoc;
            [FieldOffset(0x298)] public uint Fclk;
            [FieldOffset(0x2C8)] public uint Uclk;
            [FieldOffset(0x2CC)] public uint Mclk;
        };

        [Serializable]
        [StructLayout(LayoutKind.Explicit)]
        private struct PowerTableAPU1
        {
            [FieldOffset(0x144)] public uint Fclk;
            [FieldOffset(0x154)] public uint Uclk;
            [FieldOffset(0x164)] public uint Mclk;
            [FieldOffset(0x198)] public uint VddcrSoc;
        };

        [Serializable]
        [StructLayout(LayoutKind.Explicit)]
        private struct PowerTableCPU0
        {
            [FieldOffset(0x044)] public uint CldoVddp;
            [FieldOffset(0x068)] public uint VddcrSoc;
            [FieldOffset(0x084)] public uint Fclk;
            [FieldOffset(0x084)] public uint Uclk;
            [FieldOffset(0x084)] public uint Mclk;
        };

        [Serializable]
        [StructLayout(LayoutKind.Explicit)]
        private struct PowerTableCPU1
        {
            [FieldOffset(0x03C)] public uint CldoVddp;
            [FieldOffset(0x060)] public uint VddcrSoc;
            [FieldOffset(0x084)] public uint Fclk;
            [FieldOffset(0x084)] public uint Uclk;
            [FieldOffset(0x084)] public uint Mclk;
        };

        [Serializable]
        [StructLayout(LayoutKind.Explicit)]
        private struct PowerTableCPU2
        {
            [FieldOffset(0x0B4)] public uint VddcrSoc;
            [FieldOffset(0x0C4)] public uint Fclk;
            [FieldOffset(0x0C8)] public uint Uclk;
            [FieldOffset(0x0CC)] public uint Mclk;
            [FieldOffset(0x1F4)] public uint CldoVddp;
            [FieldOffset(0x1F8)] public uint CldoVddg;
        };

        public PowerTable(SMU.SmuType smutype) => SmuType = smutype;

        private void ParseTable(uint[] pt)
        {
            if (pt == null)
                return;

            GCHandle handle = GCHandle.Alloc(pt, GCHandleType.Pinned);
            try
            {
                dynamic powerTable;
                byte[] bytes = new byte[4];

                switch (SmuType)
                {
                    case SMU.SmuType.TYPE_CPU0:
                        powerTable = (PowerTableCPU0)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PowerTableCPU0));
                        break;

                    case SMU.SmuType.TYPE_CPU1:
                        powerTable = (PowerTableCPU1)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PowerTableCPU1));
                        break;

                    default:
                    case SMU.SmuType.TYPE_CPU2:
                        powerTable = (PowerTableCPU2)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PowerTableCPU2));
                        break;

                    case SMU.SmuType.TYPE_APU0:
                        powerTable = (PowerTableAPU0)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PowerTableAPU0));
                        break;

                    case SMU.SmuType.TYPE_APU1:
                        powerTable = (PowerTableAPU1)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PowerTableAPU1));
                        break;
                }

                //Type typeOfTable = powerTable.GetType();
                float bclkCorrection = 1.00f;

                try
                {
                    bytes = BitConverter.GetBytes(powerTable.Mclk);
                    float mclk = BitConverter.ToSingle(bytes, 0);

                    // Compensate for lack of BCLK detection, based on configuredClockSpeed
                    if ((ConfiguredClockSpeed / 2) % mclk > 1)
                        bclkCorrection = ConfiguredClockSpeed / 2 / mclk;

                    MCLK = $"{(mclk * bclkCorrection):F2}";
                }
                catch { }

                try
                {
                    bytes = BitConverter.GetBytes(powerTable.Fclk);
                    float fclk = BitConverter.ToSingle(bytes, 0);
                    FCLK = $"{(fclk * bclkCorrection):F2}";
                }
                catch { }

                try
                {
                    bytes = BitConverter.GetBytes(powerTable.Uclk);
                    float uclk = BitConverter.ToSingle(bytes, 0);
                    UCLK = $"{(uclk * bclkCorrection):F2}";
                }
                catch { }

                try
                {
                    bytes = BitConverter.GetBytes(powerTable.VddcrSoc);
                    VDDCR_SOC = $"{BitConverter.ToSingle(bytes, 0):F4}V";
                }
                catch { }

                try
                {
                    bytes = BitConverter.GetBytes(powerTable.CldoVddp);
                    CLDO_VDDP = $"{BitConverter.ToSingle(bytes, 0):F4}V";
                }
                catch { }

                try
                {
                    bytes = BitConverter.GetBytes(powerTable.CldoVddg);
                    CLDO_VDDG = $"{BitConverter.ToSingle(bytes, 0):F4}V";
                }
                catch { }
            }
            finally
            {
                handle.Free();
            }
        }

        public SMU.SmuType SmuType { get; protected set; }
        public uint[] Table
        {
            get => table;
            set
            {
                if (value != null)
                {
                    table = value;
                    ParseTable(value);
                }
            }
        }

        string fclk;
        public string FCLK
        {
            get => fclk ?? "N/A";
            set => SetProperty(ref fclk, value, InternalEventArgsCache.FCLK);
        }

        string mclk;
        public string MCLK
        {
            get => mclk ?? "N/A";
            set => SetProperty(ref mclk, value, InternalEventArgsCache.MCLK);
        }

        string uclk;
        public string UCLK
        {
            get => uclk ?? "N/A";
            set => SetProperty(ref uclk, value, InternalEventArgsCache.UCLK);
        }

        string vddcr_soc;
        public string VDDCR_SOC
        {
            get => vddcr_soc ?? "N/A";
            set => SetProperty(ref vddcr_soc, value, InternalEventArgsCache.VDDCR_SOC);
        }

        string cldo_vddp;
        public string CLDO_VDDP
        {
            get => cldo_vddp ?? "N/A";
            set => SetProperty(ref cldo_vddp, value, InternalEventArgsCache.CLDO_VDDP);
        }

        string cldo_vddg;
        public string CLDO_VDDG
        {
            get => cldo_vddg ?? "N/A";
            set => SetProperty(ref cldo_vddg, value, InternalEventArgsCache.CLDO_VDDG);
        }
        protected void OnPropertyChanged(PropertyChangedEventArgs eventArgs)
        {
            PropertyChanged?.Invoke(this, eventArgs);
        }

        public float ConfiguredClockSpeed { get; set; }
    }

    internal static class InternalEventArgsCache
    {
        internal static PropertyChangedEventArgs FCLK = new PropertyChangedEventArgs("FCLK");
        internal static PropertyChangedEventArgs MCLK = new PropertyChangedEventArgs("MCLK");
        internal static PropertyChangedEventArgs UCLK = new PropertyChangedEventArgs("UCLK");

        internal static PropertyChangedEventArgs VDDCR_SOC = new PropertyChangedEventArgs("VDDCR_SOC");
        internal static PropertyChangedEventArgs CLDO_VDDP = new PropertyChangedEventArgs("CLDO_VDDP");
        internal static PropertyChangedEventArgs CLDO_VDDG = new PropertyChangedEventArgs("CLDO_VDDG");
    }
}
