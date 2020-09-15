using OpenLibSys;
using System;
using System.Diagnostics;
using System.Threading;

namespace ZenStates
{
    public class Ops : IDisposable
    {
        private static Mutex amdSmuMutex;
        private const ushort SMU_TIMEOUT = 8192;

        public Ops()
        {
            amdSmuMutex = new Mutex();

            Ols = new Ols();
            CheckOlsStatus();
            CpuType = GetCPUType(GetPkgType());
            Smu = GetMaintainedSettings.GetByType(CpuType);
            Smu.Version = GetSmuVersion();
        }

        public SMU Smu { get; }
        public Ols Ols { get; }
        public SMU.CPUType CpuType { get; }

        public void CheckOlsStatus()
        {
            // Check support library status
            switch (Ols.GetStatus())
            {
                case (uint)Ols.Status.NO_ERROR:
                    break;
                case (uint)Ols.Status.DLL_NOT_FOUND:
                    throw new ApplicationException("WinRing DLL_NOT_FOUND");
                case (uint)Ols.Status.DLL_INCORRECT_VERSION:
                    throw new ApplicationException("WinRing DLL_INCORRECT_VERSION");
                case (uint)Ols.Status.DLL_INITIALIZE_ERROR:
                    throw new ApplicationException("WinRing DLL_INITIALIZE_ERROR");
            }

            // Check WinRing0 status
            switch (Ols.GetDllStatus())
            {
                case (uint)Ols.OlsDllStatus.OLS_DLL_NO_ERROR:
                    break;
                case (uint)Ols.OlsDllStatus.OLS_DLL_DRIVER_NOT_LOADED:
                    throw new ApplicationException("WinRing OLS_DRIVER_NOT_LOADED");
                case (uint)Ols.OlsDllStatus.OLS_DLL_UNSUPPORTED_PLATFORM:
                    throw new ApplicationException("WinRing OLS_UNSUPPORTED_PLATFORM");
                case (uint)Ols.OlsDllStatus.OLS_DLL_DRIVER_NOT_FOUND:
                    throw new ApplicationException("WinRing OLS_DLL_DRIVER_NOT_FOUND");
                case (uint)Ols.OlsDllStatus.OLS_DLL_DRIVER_UNLOADED:
                    throw new ApplicationException("WinRing OLS_DLL_DRIVER_UNLOADED");
                case (uint)Ols.OlsDllStatus.OLS_DLL_DRIVER_NOT_LOADED_ON_NETWORK:
                    throw new ApplicationException("WinRing DRIVER_NOT_LOADED_ON_NETWORK");
                case (uint)Ols.OlsDllStatus.OLS_DLL_UNKNOWN_ERROR:
                    throw new ApplicationException("WinRing OLS_DLL_UNKNOWN_ERROR");
            }
        }

        public uint SetBits(uint val, int offset, int n, uint newValue)
        {
            return val & ~(((1U << n) - 1) << offset) | (newValue << offset);
        }

        public uint GetBits(uint val, int offset, int n)
        {
            return (val >> offset) & ~(~0U << n);
        }

        public bool SmuWriteReg(uint addr, uint data)
        {
            bool res = false;
            amdSmuMutex.WaitOne(5000);
            if (Ols.WritePciConfigDwordEx(Smu.SMU_PCI_ADDR, Smu.SMU_OFFSET_ADDR, addr) == 1)
                res = (Ols.WritePciConfigDwordEx(Smu.SMU_PCI_ADDR, Smu.SMU_OFFSET_DATA, data) == 1);
            amdSmuMutex.ReleaseMutex();
            return res;
        }

        public bool SmuReadReg(uint addr, ref uint data)
        {
            bool res = false;
            amdSmuMutex.WaitOne(5000);
            if (Ols.WritePciConfigDwordEx(Smu.SMU_PCI_ADDR, Smu.SMU_OFFSET_ADDR, addr) == 1)
                res = (Ols.ReadPciConfigDwordEx(Smu.SMU_PCI_ADDR, Smu.SMU_OFFSET_DATA, ref data) == 1);
            amdSmuMutex.ReleaseMutex();
            return res;
        }

        public bool SmuWaitDone()
        {
            bool res;
            ushort timeout = SMU_TIMEOUT;
            uint data = 0;

            do
                res = SmuReadReg(Smu.SMU_ADDR_RSP, ref data);
            while ((!res || data != 1) && --timeout > 0);

            if (timeout == 0 || data != 1) res = false;

            return res;
        }

        public SMU.Status SendSmuCommand(uint msg, ref uint[] args)
        {
            ushort timeout = SMU_TIMEOUT;
            uint[] cmdArgs = new uint[6];
            int argsLength = args.Length;
            uint status = 0;

            if (argsLength > cmdArgs.Length)
                argsLength = cmdArgs.Length;

            for (int i = 0; i < argsLength; ++i)
                cmdArgs[i] = args[i];

            if (amdSmuMutex.WaitOne(5000))
            {
                // Clear response register
                bool temp;
                do
                    temp = SmuWriteReg(Smu.SMU_ADDR_RSP, 0);
                while ((!temp) && --timeout > 0);

                if (timeout == 0)
                {
                    amdSmuMutex.ReleaseMutex();
                    SmuReadReg(Smu.SMU_ADDR_RSP, ref status);
                    return (SMU.Status)status;
                }

                // Write data
                for (int i = 0; i < cmdArgs.Length; ++i)
                    SmuWriteReg(Smu.SMU_ADDR_ARG + (uint)(i * 4), cmdArgs[i]);

                // Send message
                SmuWriteReg(Smu.SMU_ADDR_MSG, msg);

                // Wait done
                if (!SmuWaitDone())
                {
                    amdSmuMutex.ReleaseMutex();
                    SmuReadReg(Smu.SMU_ADDR_RSP, ref status);
                    return (SMU.Status)status;
                }

                // Read back args
                for (int i = 0; i < args.Length; ++i)
                    SmuReadReg(Smu.SMU_ADDR_ARG + (uint)(i * 4), ref args[i]);
            }

            amdSmuMutex.ReleaseMutex();
            SmuReadReg(Smu.SMU_ADDR_RSP, ref status);

            return (SMU.Status)status;
        }

        public uint ReadDword(uint value)
        {
            Ols.WritePciConfigDword(Smu.SMU_PCI_ADDR, (byte)Smu.SMU_OFFSET_ADDR, value);
            return Ols.ReadPciConfigDword(Smu.SMU_PCI_ADDR, (byte)Smu.SMU_OFFSET_DATA);
        }

        // Function from OpenHardwareMonitor
        private void EstimateTimeStampCounterFrequency(out double frequency, out double error)
        {
            double f, e;

            // preload the function
            EstimateTimeStampCounterFrequency(0, out f, out e);
            EstimateTimeStampCounterFrequency(0, out f, out e);

            // estimate the frequency
            error = double.MaxValue;
            frequency = 0;
            for (int i = 0; i < 5; i++)
            {
                EstimateTimeStampCounterFrequency(0.025, out f, out e);
                if (e < error)
                {
                    error = e;
                    frequency = f;
                }

                if (error < 1e-4)
                    break;
            }
        }

        private void EstimateTimeStampCounterFrequency(double timeWindow,
          out double frequency, out double error)
        {
            uint eax = 0, edx = 0;
            uint eax2 = 0, edx2 = 0;

            long ticks = (long)(timeWindow * Stopwatch.Frequency);
            ulong countBegin, countEnd;

            long timeBegin = Stopwatch.GetTimestamp() + (long)Math.Ceiling(0.001 * ticks);
            long timeEnd = timeBegin + ticks;

            while (Stopwatch.GetTimestamp() < timeBegin) { }
            //Ols.Rdtsc(ref eax, ref edx);
            Ols.RdmsrTx(0x00000010, ref eax, ref edx, (UIntPtr)1);
            countBegin = eax;
            long afterBegin = Stopwatch.GetTimestamp();

            while (Stopwatch.GetTimestamp() < timeEnd) { }
            //Ols.Rdtsc(ref eax, ref edx);
            Ols.RdmsrTx(0x00000010, ref eax, ref edx, (UIntPtr)1);
            countEnd = eax;
            long afterEnd = Stopwatch.GetTimestamp();

            double delta = (timeEnd - timeBegin);
            frequency = 1e-6 *
              (((double)(countEnd - countBegin)) * Stopwatch.Frequency) / delta;

            Ols.RdmsrTx(0xc0000104, ref eax2, ref edx2, (UIntPtr)1);
            frequency *= GetBits(edx2, 0, 6);

            double beginError = (afterBegin - timeBegin) / delta;
            double endError = (afterEnd - timeEnd) / delta;
            error = beginError + endError;
        }

        public double GetTimeStampFrequency()
        {
            EstimateTimeStampCounterFrequency(
                out double estimatedTimeStampCounterFrequency,
                out double estimatedTimeStampCounterFrequencyError);

            return estimatedTimeStampCounterFrequency;
        }

        public double GetTimeStampCounterMultiplier()
        {
            uint eax = 0, edx = 0;
            Ols.Rdmsr(0xC0010064, ref eax, ref edx);
            uint cpuDfsId = (eax >> 8) & 0x3f;
            uint cpuFid = eax & 0xff;
            return 2.0 * cpuFid / cpuDfsId;
        }

        private double GetCoreMulti(int index)
        {
            uint eax = default, edx = default;
            if (Ols.RdmsrTx(0xC0010293, ref eax, ref edx, (UIntPtr)(1 << index)) != 1)
            {
                return 0;
            }

            double multi = 25 * (eax & 0xFF) / (12.5 * (eax >> 8 & 0x3F));
            return Math.Round(multi * 4, MidpointRounding.ToEven) / 4;
        }

        public float GetBaseClock()
        {
            // uint eax = default, edx = default;
            float bclk = 0;

            //Ols.RdmsrTx(0xC0010015, ref eax, ref edx, (UIntPtr)1);

            //uint prevBitValue = GetBits(eax, 21, 1);
            //eax = SetBits(eax, 21, 1, 1);

            //Ols.WrmsrTx(0xC0010015, eax, edx, (UIntPtr)1);

            double timeStampCounterMultiplier = GetCoreMulti(0);
            double timeStampCounterFrequency = GetTimeStampFrequency();
            
            //eax = SetBits(eax, 21, 1, prevBitValue);
            //Ols.WrmsrTx(0xC0010015, eax, edx, (UIntPtr)1);

            if (timeStampCounterMultiplier > 0)
            {
                bclk = (float)(timeStampCounterFrequency / timeStampCounterMultiplier);
            }

            return bclk;
        }

        public SMU.CpuFamily GetCpuFamily()
        {
            uint eax = 0, ebx = 0, ecx = 0, edx = 0;
            if (Ols.Cpuid(0x00000001, ref eax, ref ebx, ref ecx, ref edx) == 1)
            {
                SMU.CpuFamily family = (SMU.CpuFamily)(GetBits(eax, 8, 4) + GetBits(eax, 20, 7));
                return family;
            }
            return SMU.CpuFamily.UNSUPPORTED;
        }

        public SMU.CPUType GetCPUType(uint packageType)
        {
            SMU.CPUType cpuType;

            // CPU Check. Compare family, model, ext family, ext model
            switch (GetCpuId())
            {
                case 0x00800F11: // CPU \ Zen \ Summit Ridge \ ZP - B0 \ 14nm
                case 0x00800F00: // CPU \ Zen \ Summit Ridge \ ZP - A0 \ 14nm
                    if (packageType == 7)
                        cpuType = SMU.CPUType.Threadripper;
                    else
                        cpuType = SMU.CPUType.SummitRidge;
                    break;
                case 0x00800F12:
                    cpuType = SMU.CPUType.Naples;
                    break;
                case 0x00800F82: // CPU \ Zen + \ Pinnacle Ridge \ 12nm
                    if (packageType == 7)
                        cpuType = SMU.CPUType.Colfax;
                    else
                        cpuType = SMU.CPUType.PinnacleRidge;
                    break;
                case 0x00810F81: // APU \ Zen + \ Picasso \ 12nm
                    cpuType = SMU.CPUType.Picasso;
                    break;
                case 0x00810F00: // APU \ Zen \ Raven Ridge \ RV - A0 \ 14nm
                case 0x00810F10: // APU \ Zen \ Raven Ridge \ 14nm
                case 0x00820F00: // APU \ Zen \ Raven Ridge 2 \ RV2 - A0 \ 14nm
                case 0x00820F01: // APU \ Zen \ Dali
                    cpuType = SMU.CPUType.RavenRidge;
                    break;
                case 0x00870F10: // CPU \ Zen2 \ Matisse \ MTS - B0 \ 7nm + 14nm I/ O Die
                case 0x00870F00: // CPU \ Zen2 \ Matisse \ MTS - A0 \ 7nm + 14nm I/ O Die
                    cpuType = SMU.CPUType.Matisse;
                    break;
                case 0x00830F00:
                case 0x00830F10: // CPU \ Epyc 2 \ Rome \ Treadripper 2 \ Castle Peak 7nm
                    if (packageType == 7)
                        cpuType = SMU.CPUType.Rome;
                    else
                        cpuType = SMU.CPUType.CastlePeak;
                    break;
                case 0x00850F00: // Subor Z+
                    cpuType = SMU.CPUType.Fenghuang;
                    break;
                case 0x00860F01: // APU \ Renoir
                    cpuType = SMU.CPUType.Renoir;
                    break;
                default:
                    cpuType = SMU.CPUType.Unsupported;
                    break;
            }

            return cpuType;
        }

        public uint GetCpuId()
        {
            uint eax = 0, ebx = 0, ecx = 0, edx = 0;
            if (Ols.Cpuid(0x00000001, ref eax, ref ebx, ref ecx, ref edx) == 1)
            {
                return eax;
            }
            return 0;
        }

        public uint GetPkgType()
        {
            uint eax = 0, ebx = 0, ecx = 0, edx = 0;
            if (Ols.Cpuid(0x80000001, ref eax, ref ebx, ref ecx, ref edx) == 1)
            {
                return ebx >> 28;
            }
            return 0;
        }

        public int GetCpuNodes()
        {
            uint eax = 0, ebx = 0, ecx = 0, edx = 0;
            if (Ols.Cpuid(0x8000001E, ref eax, ref ebx, ref ecx, ref edx) == 1)
            {
                return Convert.ToInt32(ecx >> 8 & 0x7) + 1;
            }
            return 1;
        }

        // Return [realCores, logicalCores] 
        public int[] GetCoreCount()
        {
            uint eax = 0, ebx = 0, ecx = 0, edx = 0;
            int logicalCores = 0;
            int threadsPerCore = 1;
            int[] count = { 0, 1 };

            if (Ols.Cpuid(0x00000001, ref eax, ref ebx, ref ecx, ref edx) == 1)
            {
                logicalCores = Convert.ToInt32((ebx >> 16) & 0xFF);

                if (Ols.Cpuid(0x8000001E, ref eax, ref ebx, ref ecx, ref edx) == 1)
                    threadsPerCore = Convert.ToInt32(ebx >> 8 & 0xF) + 1;
            }
            if (threadsPerCore == 0)
                count[0] = logicalCores;
            else
                count[0] = logicalCores / threadsPerCore;

            count[1] = logicalCores;

            return count;
        }

        public int GetCCDCount()
        {
            uint value1 = 0, value2 = 0, value3 = 0;
            int ccdCount = 0;
            uint reg1 = 0x5D22A;
            uint reg2 = 0x5D22B;
            uint reg3 = 0x5D22C;

            if (CpuType == SMU.CPUType.Matisse)
            {
                reg1 = 0x5D21A;
                reg2 = 0x5D21B;
                reg3 = 0x5D21C;
            }

            if (!SmuReadReg(reg1, ref value1) ||
                !SmuReadReg(reg2, ref value2) || 
                !SmuReadReg(reg3, ref value3))
                return ccdCount;

            value1 = (value1 >> 22) & 0xff;
            value2 = (value2 >> 30) & 0xff;
            value3 &= 0x3f;

            uint value4 = value2 | 4 * value3;

            if (!((value1 & 1) == 0 || (value4 & 1) == 1))
                ccdCount += 1;

            int i = 0;
            do {
                if ((value1 & (1 << i)) == 1 && (value4 & (1 << i)) == 0)
                    ccdCount += 1;
            } while (++i < 8);

            return ccdCount;
        }

        private string GetStringPart(uint val)
        {
            return val != 0 ? Convert.ToChar(val).ToString() : "";
        }

        private string IntToStr(uint val)
        {
            uint part1 = val & 0xff;
            uint part2 = val >> 8 & 0xff;
            uint part3 = val >> 16 & 0xff;
            uint part4 = val >> 24 & 0xff;

            return string.Format("{0}{1}{2}{3}", GetStringPart(part1), GetStringPart(part2), GetStringPart(part3), GetStringPart(part4));
        }

        public string GetCpuName()
        {
            string model = "";
            uint eax = 0, ebx = 0, ecx = 0, edx = 0;

            if (Ols.Cpuid(0x80000002, ref eax, ref ebx, ref ecx, ref edx) == 1)
                model = model + IntToStr(eax) + IntToStr(ebx) + IntToStr(ecx) + IntToStr(edx);

            if (Ols.Cpuid(0x80000003, ref eax, ref ebx, ref ecx, ref edx) == 1)
                model = model + IntToStr(eax) + IntToStr(ebx) + IntToStr(ecx) + IntToStr(edx);

            if (Ols.Cpuid(0x80000004, ref eax, ref ebx, ref ecx, ref edx) == 1)
                model = model + IntToStr(eax) + IntToStr(ebx) + IntToStr(ecx) + IntToStr(edx);

            return model.Trim();
        }

        public uint GetSmuVersion()
        {
            uint[] args = new uint[6];
            if (SendSmuCommand(Smu.SMU_MSG_GetSmuVersion, ref args) == SMU.Status.OK)
                return args[0];

            return 0;
        }

        public uint GetPatchLevel()
        {
            uint eax = 0, edx = 0;
            if (Ols.Rdmsr(0x8b, ref eax, ref edx) != 1)
                return 0;

            return eax;
        }

        public bool GetOcMode()
        {
            /*
            uint eax = 0;
            uint edx = 0;

            if (ols.Rdmsr(MSR_PStateStat, ref eax, ref edx) == 1)
            {
                // Summit Ridge, Raven Ridge
                return Convert.ToBoolean((eax >> 1) & 1);
            }
            return false;
            */

            return GetPBOScalar() == 0;
        }

        public float GetPBOScalar()
        {
            uint[] args = new uint[6];
            if (SendSmuCommand(Smu.SMU_MSG_GetPBOScalar, ref args) == SMU.Status.OK)
            {
                byte[] bytes = BitConverter.GetBytes(args[0]);

                return BitConverter.ToSingle(bytes, 0);
            }
            return 0f;
        }

        public SMU.Status TransferTableToDram()
        {
            uint[] args = { 1, 1, 0, 0, 0, 0 };

            if (Smu.SMU_TYPE == SMU.SmuType.TYPE_APU0)
            {
                args[0] = 3;
                args[1] = 0;
            }

            return SendSmuCommand(Smu.SMU_MSG_TransferTableToDram, ref args);
        }


        public ulong GetDramBaseAddress()
        {
            uint[] args = new uint[6];
            ulong address = 0;

            SMU.Status status = SMU.Status.FAILED;

            switch (Smu.SMU_TYPE)
            {
                // SummitRidge, PinnacleRidge
                case SMU.SmuType.TYPE_CPU0:
                case SMU.SmuType.TYPE_CPU1:
                    args[0] = 0;
                    status = SendSmuCommand(Smu.SMU_MSG_GetDramBaseAddress - 1, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    status = SendSmuCommand(Smu.SMU_MSG_GetDramBaseAddress, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    address = args[0];

                    args[0] = 0;
                    status = SendSmuCommand(Smu.SMU_MSG_GetDramBaseAddress + 2, ref args);
                    if (status != SMU.Status.OK)
                        return 0;
                    break;

                // Matisse, CastlePeak, Rome
                case SMU.SmuType.TYPE_CPU2:
                    status = SendSmuCommand(Smu.SMU_MSG_GetDramBaseAddress, ref args);
                    if (status != SMU.Status.OK)
                        return 0;
                    address = args[0];
                    break;

                // Renoir
                case SMU.SmuType.TYPE_APU1:
                    status = SendSmuCommand(Smu.SMU_MSG_GetDramBaseAddress, ref args);
                    if (status != SMU.Status.OK)
                        return 0;
                    address = args[0] | ((ulong)args[1] << 32);
                    break;

                // RavenRidge, RavenRidge2, Picasso
                case SMU.SmuType.TYPE_APU0:
                    uint[] parts = new uint[2];

                    args[0] = 3;
                    status = SendSmuCommand(Smu.SMU_MSG_GetDramBaseAddress - 1, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    args[0] = 3;
                    status = SendSmuCommand(Smu.SMU_MSG_GetDramBaseAddress, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    // First base
                    parts[0] = args[0];

                    args[0] = 5;
                    status = SendSmuCommand(Smu.SMU_MSG_GetDramBaseAddress - 1, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    status = SendSmuCommand(Smu.SMU_MSG_GetDramBaseAddress, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    // Second base
                    parts[1] = args[0];
                    address = (ulong)parts[1] << 32 | parts[0];
                    break;

                default:
                    break;
            }

            if (status == SMU.Status.OK)
                return address;

            return 0;
        }

        public bool SendTestMessage()
        {
            uint[] args = new uint[6];
            return SendSmuCommand(Smu.SMU_MSG_TestMessage, ref args) == SMU.Status.OK;
        }

        public bool IsProchotEnabled()
        {
            uint data = ReadDword(0x59804);
            return (data & 1) == 1;
        }

        public double VidToVoltage(uint vid)
        {
            return 1.55 - vid * 0.00625;
        }

        public void Dispose()
        {
            amdSmuMutex.ReleaseMutex();
            Ols.Dispose();
        }
    }
}
