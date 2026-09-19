using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using ZenStates.Core.Common;
using ZenStates.Core.Hardware.DRAM;

namespace ZenTimings.Export
{
    public sealed partial class SnapshotBuilder
    {
        private const int MaxTextLength = 128;
        private const int MaxDepth = 4;

        private sealed class Member
        {
            public string Name;
            public Type DeclaringType;
            public Func<object, object> Get;

            // A property without a setter, its value is calculated on every read
            public bool IsComputed;
        }

        // Snapshots are built both from the UI thread and from the auto refresh task
        private static readonly ConcurrentDictionary<Type, Member[]> MemberCache = new ConcurrentDictionary<Type, Member[]>();

        // Strings coming from SPD and SMBIOS are not trusted: they can be rewritten by the user or the vendor
        internal static string Text(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (!char.IsControl(c) && !char.IsSurrogate(c))
                    sb.Append(c);
            }

            string result = sb.ToString().Trim();
            if (result.Length > MaxTextLength)
                result = result.Substring(0, MaxTextLength);

            if (result.Length == 0 || result == "N/A")
                return null;

            return result;
        }

        private static Member[] GetMembers(Type type)
        {
            return MemberCache.GetOrAdd(type, CreateMembers);
        }

        private static Member[] CreateMembers(Type type)
        {
            var result = new List<Member>();
            var names = new HashSet<string>();

            // A property hidden with "new" is returned twice, the most derived one comes first
            foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0 || prop.PropertyType == typeof(byte[]))
                    continue;
                if (!names.Add(prop.Name))
                    continue;

                PropertyInfo current = prop;
                result.Add(new Member
                {
                    Name = current.Name,
                    DeclaringType = current.DeclaringType,
                    Get = obj => current.GetValue(obj, null),
                    IsComputed = current.GetSetMethod(true) == null,
                });
            }

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.FieldType == typeof(byte[]))
                    continue;

                FieldInfo current = field;
                result.Add(new Member { Name = current.Name, DeclaringType = current.DeclaringType, Get = current.GetValue });
            }

            return result.ToArray();
        }

        private SnapshotObject ReflectObject(object obj, int depth, ICollection<string> skip)
        {
            var result = new SnapshotObject();
            foreach (Member member in GetMembers(obj.GetType()))
            {
                if (skip != null && skip.Contains(member.Name))
                    continue;

                result.Add(member.Name, ReadMember(member, obj, depth + 1));
            }

            return result;
        }

        // The fields of a partially read object which were not read hold defaults that look like real values
        private static SnapshotObject WithoutDefaults(SnapshotObject source)
        {
            var result = new SnapshotObject();
            foreach (var item in source.Items)
            {
                object value = item.Value;
                bool isDefault = value == null
                    || (value is bool flag && !flag)
                    || (value is List<object> list && list.Count == 0)
                    || (value is IConvertible number && !(value is string) && !(value is bool)
                        && Convert.ToDouble(number, CultureInfo.InvariantCulture) == 0);

                if (!isDefault)
                    result.Add(item.Key, value);
            }

            return result;
        }

        private object ReadMember(Member member, object obj, int depth)
        {
            try
            {
                return ToValue(member.Get(obj), depth);
            }
            catch
            {
                return null;
            }
        }

        private object ToValue(object value, int depth)
        {
            if (value == null)
                return null;

            if (value is string text)
                return Text(text);

            if (value is bool || value is byte || value is sbyte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong
                || value is float || value is double || value is decimal)
                return value;

            if (value is Enum)
                return value.ToString();

            if (value is DateTime date)
                return date.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

            if (value is BooleanProp flag)
            {
                uint raw = flag;
                return raw > 1 ? (object)null : raw == 1;
            }

            if (value is CommandRateProp)
                return Text(value.ToString());

            if (value is BankRefreshMode refreshMode)
                return refreshMode.Name;

            if (value is EncodedValueBase encoded)
            {
                if (encoded.IsNull)
                    return null;

                return new SnapshotObject()
                    .Add("raw", encoded.RawValue)
                    .Add("text", Text(encoded.ToString()));
            }

            // A zero means the table has no value for it
            if (value is Voltage voltage)
                return voltage.RawValue == 0 ? null : new SnapshotObject().Add("mv", voltage.RawValue);

            if (value is Capacity capacity)
                return capacity.SizeInBytes;

            if (value is byte[])
                return null;

            if (value is IEnumerable enumerable)
            {
                var list = new List<object>();
                foreach (object item in enumerable)
                    list.Add(ToValue(item, depth + 1));
                return list;
            }

            string ns = value.GetType().Namespace ?? string.Empty;
            if (depth < MaxDepth && (ns.StartsWith("ZenStates.", StringComparison.Ordinal) || ns.StartsWith("ZenTimings", StringComparison.Ordinal)))
                return ReflectObject(value, depth, null);

            return Text(value.ToString());
        }
    }
}
