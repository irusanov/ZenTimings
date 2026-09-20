using System;
using ZenTimings.Common;

namespace ZenTimings.Export
{
    internal static class CoreOptionsScope
    {
        private static readonly object SyncRoot = new object();

        public static void Run(bool printSerialNumbers, Action action)
        {
            Run(printSerialNumbers, () =>
            {
                action();
                return true;
            });
        }

        public static T Run<T>(bool printSerialNumbers, Func<T> action)
        {
            lock (SyncRoot)
            {
                bool previousPrintSerialNumbers = CpuSingleton.Instance.Options.PrintSerialNumbers;
                CpuSingleton.Instance.Options.PrintSerialNumbers = printSerialNumbers;
                try
                {
                    return action();
                }
                finally
                {
                    CpuSingleton.Instance.Options.PrintSerialNumbers = previousPrintSerialNumbers;
                }
            }
        }
    }
}