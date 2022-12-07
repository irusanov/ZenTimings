using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ZenTimings
{
    public class BiosMemController : IDisposable
    {
        private static readonly Dictionary<int, string> ProcOdtDict = new Dictionary<int, string>
        {
            {1, "480.0 Ω"},
            {2, "240.0 Ω"},
            {3, "160.0 Ω"},
            {8, "120.0 Ω"},
            {9, "96.0 Ω"},
            {10, "80.0 Ω"},
            {11, "68.6 Ω"},
            {24, "60.0 Ω"},
            {25, "53.3 Ω"},
            {26, "48.0 Ω"},
            {27, "43.6 Ω"},
            {56, "40.0 Ω"},
            {57, "36.9 Ω"},
            {58, "34.3 Ω"},
            {59, "32.0 Ω"},
            {62, "30.0 Ω"},
            {63, "28.2 Ω"}
        };

        private static readonly Dictionary<int, string> DriveStrengthDict = new Dictionary<int, string>
        {
            {0, "120.0 Ω"},
            {1, "60.0 Ω"},
            {3, "40.0 Ω"},
            {7, "30.0 Ω"},
            {15, "24.0 Ω"},
            {31, "20.0 Ω"}
        };

        private static readonly Dictionary<int, string> RttDict = new Dictionary<int, string>
        {
            {0, "Disabled"},
            {1, "RZQ/4"},
            {2, "RZQ/2"},
            {3, "RZQ/6"},
            {4, "RZQ/1"},
            {5, "RZQ/5"},
            {6, "RZQ/3"},
            {7, "RZQ/7"}
        };

        private static readonly Dictionary<int, string> RttWrDict = new Dictionary<int, string>
        {
            {0, "Off"},
            {1, "RZQ/2"},
            {2, "RZQ/1"},
            {3, "Hi-Z"},
            {4, "RZQ/3"}
        };

        private bool disposedValue;

        private byte[] table;

        public BiosMemController()
        {
        }

        public BiosMemController(byte[] table)
        {
            ParseTable(table);
        }

        public byte[] Table
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

        public Resistances Config { get; set; }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void ParseTable(byte[] biosTable)
        {
            GCHandle handle = GCHandle.Alloc(biosTable, GCHandleType.Pinned);
            try
            {
                Config = (Resistances) Marshal.PtrToStructure(handle.AddrOfPinnedObject(),
                    typeof(Resistances));
            }
            finally
            {
                handle.Free();
            }
        }

        private static string GetByKey(Dictionary<int, string> dict, int key)
        {
            return dict.TryGetValue(key, out string output) ? output : "N/A";
        }

        public string GetProcODTString(int key) => GetByKey(ProcOdtDict, key);
        public string GetDrvStrenString(int key) => GetByKey(DriveStrengthDict, key);
        public string GetRttString(int key) => GetByKey(RttDict, key);
        public string GetRttWrString(int key) => GetByKey(RttWrDict, key);
        public string GetSetupString(byte value) => $"{value / 32}/{value % 32}";

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                Table = null;
                disposedValue = true;
            }
        }

        [Serializable]
        [StructLayout(LayoutKind.Explicit)]
        public struct Resistances
        {
            [FieldOffset(27)] public ushort MemVddio;
            [FieldOffset(29)] public ushort MemVtt;
            [FieldOffset(33)] public byte ProcODT;
            [FieldOffset(65)] public byte RttNom;
            [FieldOffset(66)] public byte RttWr;
            [FieldOffset(67)] public byte RttPark;
            [FieldOffset(86)] public byte AddrCmdSetup;
            [FieldOffset(87)] public byte CsOdtSetup;
            [FieldOffset(88)] public byte CkeSetup;
            [FieldOffset(89)] public byte ClkDrvStren;
            [FieldOffset(90)] public byte AddrCmdDrvStren;
            [FieldOffset(91)] public byte CsOdtCmdDrvStren;
            [FieldOffset(92)] public byte CkeDrvStren;
        };
    }
}