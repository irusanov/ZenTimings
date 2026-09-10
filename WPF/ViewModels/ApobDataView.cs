using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using ZenStates.Core.Hardware.Apob;

namespace ZenTimings.ViewModels
{
    public sealed class ApobDataView : DynamicObject
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

        public ApobDataView(ApobData main, ApobData extended)
        {
            var props = typeof(ApobData).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var p in props)
            {
                if (!p.CanRead) continue;

                var mainVal = main != null ? p.GetValue(main) : null;
                var extendedVal = extended != null ? p.GetValue(extended) : null;

                _values[p.Name] = mainVal ?? extendedVal;
            }
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return _values.TryGetValue(binder.Name, out result);
        }
    }
}
