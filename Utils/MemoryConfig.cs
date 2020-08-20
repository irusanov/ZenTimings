using System;
using System.ComponentModel;

namespace ZenTimings
{
    [Serializable]
    public class MemoryConfig : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T storage, T value, PropertyChangedEventArgs args)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(args);
            return true;
        }

        float frequency;
        public float Frequency
        {
            get => frequency;
            set
            {
                SetProperty(ref frequency, value, InternalEventArgsCache.Frequency);
                double trfcns = Convert.ToDouble(RFC * 2000 / Frequency);
                RFCns = $"{trfcns:F4}";
                double trefins = Convert.ToDouble(1000 / Frequency * 2 * REFI);
                REFIns = $"{trefins:F3}";
            }
        }

        string totalCapacity;
        public string TotalCapacity
        {
            get => totalCapacity;
            set => SetProperty(ref totalCapacity, value, InternalEventArgsCache.TotalCapacity);
        }

        string bgs;
        public string BGS
        {
            get => bgs;
            set => SetProperty(ref bgs, value, InternalEventArgsCache.BGS);
        }

        string bgsAlt;
        public string BGSAlt
        {
            get => bgsAlt;
            set => SetProperty(ref bgsAlt, value, InternalEventArgsCache.BGSAlt);
        }

        string gdm;
        public string GDM
        {
            get => gdm;
            set => SetProperty(ref gdm, value, InternalEventArgsCache.GDM);
        }

        string cmd2T;
        public string Cmd2T
        {
            get => cmd2T;
            set => SetProperty(ref cmd2T, value, InternalEventArgsCache.Cmd2T);
        }

        uint cl;
        public uint CL
        {
            get => cl;
            set => SetProperty(ref cl, value, InternalEventArgsCache.CL);
        }

        uint rcdwr;
        public uint RCDWR
        {
            get => rcdwr;
            set => SetProperty(ref rcdwr, value, InternalEventArgsCache.RCDWR);
        }

        uint rcdrd;
        public uint RCDRD
        {
            get => rcdrd;
            set => SetProperty(ref rcdrd, value, InternalEventArgsCache.RCDRD);
        }

        uint rp;
        public uint RP
        {
            get => rp;
            set => SetProperty(ref rp, value, InternalEventArgsCache.RP);
        }

        uint ras;
        public uint RAS
        {
            get => ras;
            set => SetProperty(ref ras, value, InternalEventArgsCache.RAS);
        }

        uint rc;
        public uint RC
        {
            get => rc;
            set => SetProperty(ref rc, value, InternalEventArgsCache.RC);
        }

        uint rrds;
        public uint RRDS
        {
            get => rrds;
            set => SetProperty(ref rrds, value, InternalEventArgsCache.RRDS);
        }

        uint rrdl;
        public uint RRDL
        {
            get => rrdl;
            set => SetProperty(ref rrdl, value, InternalEventArgsCache.RRDL);
        }

        uint faw;
        public uint FAW
        {
            get => faw;
            set => SetProperty(ref faw, value, InternalEventArgsCache.FAW);
        }

        uint wtrs;
        public uint WTRS
        {
            get => wtrs;
            set => SetProperty(ref wtrs, value, InternalEventArgsCache.WTRS);
        }

        uint wtrl;
        public uint WTRL
        {
            get => wtrl;
            set => SetProperty(ref wtrl, value, InternalEventArgsCache.WTRL);
        }

        uint wr;
        public uint WR
        {
            get => wr;
            set => SetProperty(ref wr, value, InternalEventArgsCache.WR);
        }

        uint rdrdscl;
        public uint RDRDSCL
        {
            get => rdrdscl;
            set => SetProperty(ref rdrdscl, value, InternalEventArgsCache.RDRDSCL);
        }

        uint wrwrscl;
        public uint WRWRSCL
        {
            get => wrwrscl;
            set => SetProperty(ref wrwrscl, value, InternalEventArgsCache.WRWRSCL);
        }

        uint cwl;
        public uint CWL
        {
            get => cwl;
            set => SetProperty(ref cwl, value, InternalEventArgsCache.CWL);
        }

        uint rtp;
        public uint RTP
        {
            get => rtp;
            set => SetProperty(ref rtp, value, InternalEventArgsCache.RTP);
        }

        uint rdwr;
        public uint RDWR
        {
            get => rdwr;
            set => SetProperty(ref rdwr, value, InternalEventArgsCache.RDWR);
        }

        uint wrrd;
        public uint WRRD
        {
            get => wrrd;
            set => SetProperty(ref wrrd, value, InternalEventArgsCache.WRRD);
        }

        uint rdrdsc;
        public uint RDRDSC
        {
            get => rdrdsc;
            set => SetProperty(ref rdrdsc, value, InternalEventArgsCache.RDRDSC);
        }

        uint rdrdsd;
        public uint RDRDSD
        {
            get => rdrdsd;
            set => SetProperty(ref rdrdsd, value, InternalEventArgsCache.RDRDSD);
        }

        uint rdrddd;
        public uint RDRDDD
        {
            get => rdrddd;
            set => SetProperty(ref rdrddd, value, InternalEventArgsCache.RDRDDD);
        }

        uint wrwrsc;
        public uint WRWRSC
        {
            get => wrwrsc;
            set => SetProperty(ref wrwrsc, value, InternalEventArgsCache.WRWRSC);
        }

        uint wrwrsd;
        public uint WRWRSD
        {
            get => wrwrsd;
            set => SetProperty(ref wrwrsd, value, InternalEventArgsCache.WRWRSD);
        }

        uint wrwrdd;
        public uint WRWRDD
        {
            get => wrwrdd;
            set => SetProperty(ref wrwrdd, value, InternalEventArgsCache.WRWRDD);
        }

        uint cke;
        public uint CKE
        {
            get => cke;
            set => SetProperty(ref cke, value, InternalEventArgsCache.CKE);
        }

        uint stag;
        public uint STAG
        {
            get => stag;
            set => SetProperty(ref stag, value, InternalEventArgsCache.STAG);
        }

        uint mod;
        public uint MOD
        {
            get => mod;
            set => SetProperty(ref mod, value, InternalEventArgsCache.MOD);
        }

        uint modpda;
        public uint MODPDA
        {
            get => modpda;
            set => SetProperty(ref modpda, value, InternalEventArgsCache.MODPDA);
        }

        uint mrd;
        public uint MRD
        {
            get => mrd;
            set => SetProperty(ref mrd, value, InternalEventArgsCache.MRD);
        }

        uint mrdpda;
        public uint MRDPDA
        {
            get => mrdpda;
            set => SetProperty(ref mrdpda, value, InternalEventArgsCache.MRDPDA);
        }

        uint rfc;
        public uint RFC
        {
            get => rfc;
            set
            {
                SetProperty(ref rfc, value, InternalEventArgsCache.RFC);
                double trfcns = Convert.ToDouble(RFC * 2000 / Frequency);
                RFCns = $"{trfcns:F4}";
            }
        }

        string refns;
        public string RFCns
        {
            get => refns;
            set => SetProperty(ref refns, value, InternalEventArgsCache.RFCns);
        }

        uint rfc2;
        public uint RFC2
        {
            get => rfc2;
            set => SetProperty(ref rfc2, value, InternalEventArgsCache.RFC2);
        }

        uint rfc4;
        public uint RFC4
        {
            get => rfc4;
            set => SetProperty(ref rfc4, value, InternalEventArgsCache.RFC4);
        }

        uint refi;
        public uint REFI
        {
            get => refi;
            set
            {
                SetProperty(ref refi, value, InternalEventArgsCache.REFI);
                double trefins = Convert.ToDouble(1000 / Frequency * 2 * REFI);
                REFIns = $"{trefins:F3}";
            }
        }

        string refins;
        public string REFIns
        {
            get => refins;
            set => SetProperty(ref refins, value, InternalEventArgsCache.REFIns);
        }

        protected void OnPropertyChanged(PropertyChangedEventArgs eventArgs)
        {
            PropertyChanged?.Invoke(this, eventArgs);
        }
    }

    internal static class InternalEventArgsCache
    {
        internal static PropertyChangedEventArgs TotalCapacity = new PropertyChangedEventArgs("TotalCapacity");
        internal static PropertyChangedEventArgs Frequency = new PropertyChangedEventArgs("Frequency");
        internal static PropertyChangedEventArgs BGS = new PropertyChangedEventArgs("BGS");
        internal static PropertyChangedEventArgs BGSAlt = new PropertyChangedEventArgs("BGSAlt");
        internal static PropertyChangedEventArgs GDM = new PropertyChangedEventArgs("GDM");
        internal static PropertyChangedEventArgs Cmd2T = new PropertyChangedEventArgs("Cmd2T");
        internal static PropertyChangedEventArgs CL = new PropertyChangedEventArgs("CL");
        internal static PropertyChangedEventArgs RCDWR = new PropertyChangedEventArgs("RCDWR");
        internal static PropertyChangedEventArgs RCDRD = new PropertyChangedEventArgs("RCDRD");
        internal static PropertyChangedEventArgs RP = new PropertyChangedEventArgs("RP");
        internal static PropertyChangedEventArgs RAS = new PropertyChangedEventArgs("RAS");
        internal static PropertyChangedEventArgs RC = new PropertyChangedEventArgs("RC");
        internal static PropertyChangedEventArgs RRDS = new PropertyChangedEventArgs("RRDS");
        internal static PropertyChangedEventArgs RRDL = new PropertyChangedEventArgs("RRDL");
        internal static PropertyChangedEventArgs FAW = new PropertyChangedEventArgs("FAW");
        internal static PropertyChangedEventArgs WTRS = new PropertyChangedEventArgs("WTRS");
        internal static PropertyChangedEventArgs WTRL = new PropertyChangedEventArgs("WTRL");
        internal static PropertyChangedEventArgs WR = new PropertyChangedEventArgs("WR");
        internal static PropertyChangedEventArgs RDRDSCL = new PropertyChangedEventArgs("RDRDSCL");
        internal static PropertyChangedEventArgs WRWRSCL = new PropertyChangedEventArgs("WRWRSCL");
        internal static PropertyChangedEventArgs CWL = new PropertyChangedEventArgs("CWL");
        internal static PropertyChangedEventArgs RTP = new PropertyChangedEventArgs("RTP");
        internal static PropertyChangedEventArgs RDWR = new PropertyChangedEventArgs("RDWR");
        internal static PropertyChangedEventArgs WRRD = new PropertyChangedEventArgs("WRRD");
        internal static PropertyChangedEventArgs RDRDSC = new PropertyChangedEventArgs("RDRDSC");
        internal static PropertyChangedEventArgs RDRDSD = new PropertyChangedEventArgs("RDRDSD");
        internal static PropertyChangedEventArgs RDRDDD = new PropertyChangedEventArgs("RDRDDD");
        internal static PropertyChangedEventArgs WRWRSC = new PropertyChangedEventArgs("WRWRSC");
        internal static PropertyChangedEventArgs WRWRSD = new PropertyChangedEventArgs("WRWRSD");
        internal static PropertyChangedEventArgs WRWRDD = new PropertyChangedEventArgs("WRWRDD");
        internal static PropertyChangedEventArgs CKE = new PropertyChangedEventArgs("CKE");
        internal static PropertyChangedEventArgs STAG = new PropertyChangedEventArgs("STAG");
        internal static PropertyChangedEventArgs MOD = new PropertyChangedEventArgs("MOD");
        internal static PropertyChangedEventArgs MODPDA = new PropertyChangedEventArgs("MODPDA");
        internal static PropertyChangedEventArgs MRD = new PropertyChangedEventArgs("MRD");
        internal static PropertyChangedEventArgs MRDPDA = new PropertyChangedEventArgs("MRDPDA");
        internal static PropertyChangedEventArgs RFC = new PropertyChangedEventArgs("RFC");
        internal static PropertyChangedEventArgs RFC2 = new PropertyChangedEventArgs("RFC2");
        internal static PropertyChangedEventArgs RFC4 = new PropertyChangedEventArgs("RFC4");
        internal static PropertyChangedEventArgs REFI = new PropertyChangedEventArgs("REFI");
        internal static PropertyChangedEventArgs RFCns = new PropertyChangedEventArgs("RFCns");
        internal static PropertyChangedEventArgs REFIns = new PropertyChangedEventArgs("REFIns");
    }
}
