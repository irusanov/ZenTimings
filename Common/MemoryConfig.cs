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

        public enum MemType
        {
            DDR4 = 0,
            DDR5 = 1,
        }

        public MemType Type { get; set; }

        private float frequency;

        public float Frequency
        {
            get => frequency;
            set {
                SetProperty(ref frequency, value, InternalEventArgsCache.Frequency);
                FrequencyString = $"{Type.ToString()}-{Math.Floor(value)}";
                
            }
        }

        private string frequencyString;
        public string FrequencyString
        {
            set => SetProperty(ref frequencyString, value, InternalEventArgsCache.FrequencyString);
            get => frequencyString;
        }

        private float ratio;

        public float Ratio
        {
            get => ratio;
            set => SetProperty(ref ratio, value, InternalEventArgsCache.Ratio);
        }

        private string totalCapacity;

        public string TotalCapacity
        {
            get => totalCapacity;
            set => SetProperty(ref totalCapacity, value, InternalEventArgsCache.TotalCapacity);
        }

        private string bgs;

        public string BGS
        {
            get => bgs;
            set => SetProperty(ref bgs, value, InternalEventArgsCache.BGS);
        }

        private string bgsAlt;

        public string BGSAlt
        {
            get => bgsAlt;
            set => SetProperty(ref bgsAlt, value, InternalEventArgsCache.BGSAlt);
        }

        private string gdm;

        public string GDM
        {
            get => gdm;
            set => SetProperty(ref gdm, value, InternalEventArgsCache.GDM);
        }

        private string powerdown;

        public string PowerDown
        {
            get => powerdown;
            set => SetProperty(ref powerdown, value, InternalEventArgsCache.PowerDown);
        }

        private string cmd2T;

        public string Cmd2T
        {
            get => cmd2T;
            set => SetProperty(ref cmd2T, value, InternalEventArgsCache.Cmd2T);
        }

        private uint cl;

        public uint CL
        {
            get => cl;
            set => SetProperty(ref cl, value, InternalEventArgsCache.CL);
        }

        private uint rcdwr;

        public uint RCDWR
        {
            get => rcdwr;
            set => SetProperty(ref rcdwr, value, InternalEventArgsCache.RCDWR);
        }

        private uint rcdrd;

        public uint RCDRD
        {
            get => rcdrd;
            set => SetProperty(ref rcdrd, value, InternalEventArgsCache.RCDRD);
        }

        private uint rp;

        public uint RP
        {
            get => rp;
            set => SetProperty(ref rp, value, InternalEventArgsCache.RP);
        }

        private uint ras;

        public uint RAS
        {
            get => ras;
            set => SetProperty(ref ras, value, InternalEventArgsCache.RAS);
        }

        private uint rc;

        public uint RC
        {
            get => rc;
            set => SetProperty(ref rc, value, InternalEventArgsCache.RC);
        }

        private uint rrds;

        public uint RRDS
        {
            get => rrds;
            set => SetProperty(ref rrds, value, InternalEventArgsCache.RRDS);
        }

        private uint rrdl;

        public uint RRDL
        {
            get => rrdl;
            set => SetProperty(ref rrdl, value, InternalEventArgsCache.RRDL);
        }

        private uint faw;

        public uint FAW
        {
            get => faw;
            set => SetProperty(ref faw, value, InternalEventArgsCache.FAW);
        }

        private uint wtrs;

        public uint WTRS
        {
            get => wtrs;
            set => SetProperty(ref wtrs, value, InternalEventArgsCache.WTRS);
        }

        private uint wtrl;

        public uint WTRL
        {
            get => wtrl;
            set => SetProperty(ref wtrl, value, InternalEventArgsCache.WTRL);
        }

        private uint wr;

        public uint WR
        {
            get => wr;
            set => SetProperty(ref wr, value, InternalEventArgsCache.WR);
        }

        private uint rdrdscl;

        public uint RDRDSCL
        {
            get => rdrdscl;
            set => SetProperty(ref rdrdscl, value, InternalEventArgsCache.RDRDSCL);
        }

        private uint wrwrscl;

        public uint WRWRSCL
        {
            get => wrwrscl;
            set => SetProperty(ref wrwrscl, value, InternalEventArgsCache.WRWRSCL);
        }

        private uint cwl;

        public uint CWL
        {
            get => cwl;
            set => SetProperty(ref cwl, value, InternalEventArgsCache.CWL);
        }

        private uint rtp;

        public uint RTP
        {
            get => rtp;
            set => SetProperty(ref rtp, value, InternalEventArgsCache.RTP);
        }

        private uint rdwr;

        public uint RDWR
        {
            get => rdwr;
            set => SetProperty(ref rdwr, value, InternalEventArgsCache.RDWR);
        }

        private uint wrrd;

        public uint WRRD
        {
            get => wrrd;
            set => SetProperty(ref wrrd, value, InternalEventArgsCache.WRRD);
        }

        private uint rdrdsc;

        public uint RDRDSC
        {
            get => rdrdsc;
            set => SetProperty(ref rdrdsc, value, InternalEventArgsCache.RDRDSC);
        }

        private uint rdrdsd;

        public uint RDRDSD
        {
            get => rdrdsd;
            set => SetProperty(ref rdrdsd, value, InternalEventArgsCache.RDRDSD);
        }

        private uint rdrddd;

        public uint RDRDDD
        {
            get => rdrddd;
            set => SetProperty(ref rdrddd, value, InternalEventArgsCache.RDRDDD);
        }

        private uint wrwrsc;

        public uint WRWRSC
        {
            get => wrwrsc;
            set => SetProperty(ref wrwrsc, value, InternalEventArgsCache.WRWRSC);
        }

        private uint wrwrsd;

        public uint WRWRSD
        {
            get => wrwrsd;
            set => SetProperty(ref wrwrsd, value, InternalEventArgsCache.WRWRSD);
        }

        private uint wrwrdd;

        public uint WRWRDD
        {
            get => wrwrdd;
            set => SetProperty(ref wrwrdd, value, InternalEventArgsCache.WRWRDD);
        }

        private uint trcpage;

        public uint TRCPAGE
        {
            get => trcpage;
            set => SetProperty(ref trcpage, value, InternalEventArgsCache.TRCPAGE);
        }

        private uint cke;

        public uint CKE
        {
            get => cke;
            set => SetProperty(ref cke, value, InternalEventArgsCache.CKE);
        }

        private uint stag;

        public uint STAG
        {
            get => stag;
            set => SetProperty(ref stag, value, InternalEventArgsCache.STAG);
        }

        private uint mod;

        public uint MOD
        {
            get => mod;
            set => SetProperty(ref mod, value, InternalEventArgsCache.MOD);
        }

        private uint modpda;

        public uint MODPDA
        {
            get => modpda;
            set => SetProperty(ref modpda, value, InternalEventArgsCache.MODPDA);
        }

        private uint mrd;

        public uint MRD
        {
            get => mrd;
            set => SetProperty(ref mrd, value, InternalEventArgsCache.MRD);
        }

        private uint mrdpda;

        public uint MRDPDA
        {
            get => mrdpda;
            set => SetProperty(ref mrdpda, value, InternalEventArgsCache.MRDPDA);
        }

        private uint rfc;

        public uint RFC
        {
            get => rfc;
            set
            {
                SetProperty(ref rfc, value, InternalEventArgsCache.RFC);

                double rfcValue = Convert.ToDouble(RFC);
                double trfcns = rfcValue * 2000 / Frequency;
                if (trfcns > rfcValue) trfcns /= 2;
                RFCns = $"{trfcns:F4}".TrimEnd('0').TrimEnd('.', ',');
            }
        }

        private string refns;

        public string RFCns
        {
            get => refns;
            set => SetProperty(ref refns, value, InternalEventArgsCache.RFCns);
        }

        private uint rfc2;

        public uint RFC2
        {
            get => rfc2;
            set => SetProperty(ref rfc2, value, InternalEventArgsCache.RFC2);
        }

        private uint rfc4;

        public uint RFC4
        {
            get => rfc4;
            set => SetProperty(ref rfc4, value, InternalEventArgsCache.RFC4);
        }


        private uint rfcsb;

        public uint RFCsb
        {
            get => rfcsb;
            set => SetProperty(ref rfcsb, value, InternalEventArgsCache.RFCsb);
        }

        private uint refi;

        public uint REFI
        {
            get => refi;
            set
            {
                SetProperty(ref refi, value, InternalEventArgsCache.REFI);
                double refiValue = Convert.ToDouble(REFI);
                double trefins = 1000 / Frequency * 2 * refiValue;
                if (trefins > refiValue) trefins /= 2;
                REFIns = $"{trefins:F3}".TrimEnd('0').TrimEnd('.', ',');
            }
        }

        private string refins;

        public string REFIns
        {
            get => refins;
            set => SetProperty(ref refins, value, InternalEventArgsCache.REFIns);
        }

        private uint xp;

        public uint XP
        {
            get => xp;
            set => SetProperty(ref xp, value, InternalEventArgsCache.XP);
        }

        private uint phywrd;

        public uint PHYWRD
        {
            get => phywrd;
            set => SetProperty(ref phywrd, value, InternalEventArgsCache.PHYWRD);
        }

        private uint phywrl;

        public uint PHYWRL
        {
            get => phywrl;
            set => SetProperty(ref phywrl, value, InternalEventArgsCache.PHYWRL);
        }

        private uint phyrdl;

        public uint PHYRDL
        {
            get => phyrdl;
            set => SetProperty(ref phyrdl, value, InternalEventArgsCache.PHYRDL);
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
        internal static PropertyChangedEventArgs FrequencyString = new PropertyChangedEventArgs("FrequencyString");
        internal static PropertyChangedEventArgs Ratio = new PropertyChangedEventArgs("Ratio");
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
        internal static PropertyChangedEventArgs TRCPAGE = new PropertyChangedEventArgs("TRCPAGE");
        internal static PropertyChangedEventArgs CKE = new PropertyChangedEventArgs("CKE");
        internal static PropertyChangedEventArgs STAG = new PropertyChangedEventArgs("STAG");
        internal static PropertyChangedEventArgs MOD = new PropertyChangedEventArgs("MOD");
        internal static PropertyChangedEventArgs MODPDA = new PropertyChangedEventArgs("MODPDA");
        internal static PropertyChangedEventArgs MRD = new PropertyChangedEventArgs("MRD");
        internal static PropertyChangedEventArgs MRDPDA = new PropertyChangedEventArgs("MRDPDA");
        internal static PropertyChangedEventArgs RFC = new PropertyChangedEventArgs("RFC");
        internal static PropertyChangedEventArgs RFC2 = new PropertyChangedEventArgs("RFC2");
        internal static PropertyChangedEventArgs RFC4 = new PropertyChangedEventArgs("RFC4");
        internal static PropertyChangedEventArgs RFCsb = new PropertyChangedEventArgs("RFCsb");
        internal static PropertyChangedEventArgs REFI = new PropertyChangedEventArgs("REFI");
        internal static PropertyChangedEventArgs RFCns = new PropertyChangedEventArgs("RFCns");
        internal static PropertyChangedEventArgs REFIns = new PropertyChangedEventArgs("REFIns");
        internal static PropertyChangedEventArgs XP = new PropertyChangedEventArgs("XP");
        internal static PropertyChangedEventArgs PowerDown = new PropertyChangedEventArgs("PowerDown");
        internal static PropertyChangedEventArgs PHYWRD = new PropertyChangedEventArgs("PHYWRD");
        internal static PropertyChangedEventArgs PHYWRL = new PropertyChangedEventArgs("PHYWRL");
        internal static PropertyChangedEventArgs PHYRDL = new PropertyChangedEventArgs("PHYRDL");
    }
}