using System;
using System.Runtime.InteropServices;
using ZenStates.Core.Common;

namespace ZenTimings.Common
{
    public class BiosMemController : IDisposable
    {
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
            if (biosTable == null || biosTable.Length < Marshal.SizeOf(typeof(Resistances)))
            {
                Config = default(Resistances);
                return;
            }

            GCHandle handle = GCHandle.Alloc(biosTable, GCHandleType.Pinned);
            try
            {
                Config = (Resistances)Marshal.PtrToStructure(handle.AddrOfPinnedObject(),
                    typeof(Resistances));
            }
            finally
            {
                handle.Free();
            }
        }

        // Same encoding as the DDR4 APOB memory configuration (ZenStates-Core EncodedValues)
        public string GetProcODTString(int key) => new Ddr4ProcOdt(key).ToString();
        public string GetDrvStrenString(int key) => new Ddr4DrvStren(key).ToString();
        public string GetRttString(int key) => new Ddr4Rtt(key).ToString();
        public string GetRttWrString(int key) => new Ddr4RttWr(key).ToString();
        public string GetSetupString(byte value) => new Ddr4Setup(value).ToString();

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
            [FieldOffset(31)] public ushort MemVpp;
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