using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZenTimings.Common
{
    public interface ISensor
    {
        int Index { get; }
        string Name { get; }
        float? Value { get; }
        float? Min { get; }
        float? Max { get; }
        void ResetMin();
        void ResetMax();
    }
}
