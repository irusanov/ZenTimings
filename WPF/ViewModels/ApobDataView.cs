using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using ZenStates.Core.Hardware.Apob;

namespace ZenTimings.ViewModels
{
    public sealed class ApobDataView : DynamicObject
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

        /// <param name="preferExtended">
        /// Take the extended record's value first, when it is the record of the active memory P-state
        /// (Apob.ExtendedDataPerPstate): the main record only holds the P-state 0 set.
        /// </param>
        public ApobDataView(ApobData main, ApobData extended, bool preferExtended = false)
        {
            var props = typeof(ApobData).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var p in props)
            {
                if (!p.CanRead) continue;

                var mainVal = main != null ? p.GetValue(main) : null;
                var extendedVal = extended != null ? p.GetValue(extended) : null;

                _values[p.Name] = preferExtended ? (extendedVal ?? mainVal) : (mainVal ?? extendedVal);
            }
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return _values.TryGetValue(binder.Name, out result);
        }
    }
}
