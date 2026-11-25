using System;
using ZenStates.Core;

namespace ZenTimings.Common
{
    internal sealed class CpuSingleton
    {
        private static Cpu instance = null;
        private CpuSingleton() { }
        public static Action<string> InitProgress { get; set; }

        /// <summary>The core when it has been created, null otherwise; never creates it.</summary>
        public static Cpu Existing => instance;

        public static Cpu Instance
        {
            get
            {
                if (instance == null)
                    instance = new Cpu(new CoreOptions { InitProgress = InitProgress });

                return instance;
            }
        }
    }
}
