using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZenStates.Core;

namespace ZenTimings
{
    internal sealed class CpuSingleton
    {
        private static Cpu instance = null;
        private CpuSingleton() { }

        public static Cpu Instance
        {
            get
            {
                if (instance == null)
                    instance = new Cpu();

                return instance;
            }
        }
    }
}
