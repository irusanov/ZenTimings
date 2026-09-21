using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ZenTimings.Export
{
    /// <summary>
    /// Serializes a snapshot tree (SnapshotObject, List of object, scalars) to JSON or to flat "path = value" text.
    /// </summary>
    public static class SnapshotWriter
    {
        public const string TextHeader = "# ZenTimings snapshot";

        // Every format names the schema near the start of the file, that is what marks the file as ours
        public const int SignatureLength = 512;

        private const int InlineObjectLimit = 8;

        public static string Write(SnapshotObject root, SnapshotFormat format)
        {
            switch (format)
            {
                case SnapshotFormat.Text:
                    return ToText(root);
                case SnapshotFormat.Html:
                    return ToHtml(root);
                default:
                    return ToJson(root);
            }
        }

        public static bool HasSignature(string head)
        {
            return head != null && head.IndexOf(SnapshotBuilder.SchemaName, StringComparison.Ordinal) >= 0;
        }

        public static string ToJson(SnapshotObject root)
        {
            var sb = new StringBuilder(16 * 1024);
            WriteJson(sb, root, 0);
            sb.AppendLine();
            return sb.ToString();
        }

        private static void WriteJson(StringBuilder sb, object value, int indent)
        {
            if (value is SnapshotObject obj)
            {
                if (obj.Count == 0)
                {
                    sb.Append("{}");
                    return;
                }

                bool inline = IsFlat(obj);
                sb.Append(inline ? "{ " : "{");
                for (int i = 0; i < obj.Items.Count; i++)
                {
                    if (!inline)
                        NewLine(sb, indent + 1);

                    sb.Append('"').Append(Escape(obj.Items[i].Key)).Append("\": ");
                    WriteJson(sb, obj.Items[i].Value, indent + 1);

                    if (i < obj.Items.Count - 1)
                        sb.Append(inline ? ", " : ",");
                }

                if (inline)
                    sb.Append(" }");
                else
                {
                    NewLine(sb, indent);
                    sb.Append('}');
                }

                return;
            }

            if (value is List<object> list)
            {
                if (list.Count == 0)
                {
                    sb.Append("[]");
                    return;
                }

                bool inline = list.All(IsScalar);
                sb.Append('[');
                for (int i = 0; i < list.Count; i++)
                {
                    if (!inline)
                        NewLine(sb, indent + 1);

                    WriteJson(sb, list[i], indent + 1);

                    if (i < list.Count - 1)
                        sb.Append(inline ? ", " : ",");
                }

                if (!inline)
                    NewLine(sb, indent);
                sb.Append(']');
                return;
            }

            if (value is string text)
            {
                sb.Append('"').Append(Escape(text)).Append('"');
                return;
            }

            sb.Append(Scalar(value));
        }

        private static void NewLine(StringBuilder sb, int indent)
        {
            sb.AppendLine();
            sb.Append(' ', indent * 2);
        }

        private static bool IsScalar(object value)
        {
            return !(value is SnapshotObject) && !(value is List<object>);
        }

        private static bool IsFlat(SnapshotObject obj)
        {
            return obj.Count <= InlineObjectLimit && obj.Items.All(i => IsScalar(i.Value));
        }

        /// <summary>
        /// Culture independent text of a scalar. Strings are returned as is, null and non finite numbers as "null".
        /// </summary>
        public static string Scalar(object value)
        {
            if (value == null)
                return "null";

            if (value is string text)
                return text;

            if (value is bool flag)
                return flag ? "true" : "false";

            // A float converted to text with full precision shows noise like 3.29600024, the UI shows four decimals at most
            if (value is float f)
                return float.IsNaN(f) || float.IsInfinity(f) ? "null" : Math.Round((double)f, 4).ToString("R", CultureInfo.InvariantCulture);

            if (value is double d)
                return double.IsNaN(d) || double.IsInfinity(d) ? "null" : d.ToString("R", CultureInfo.InvariantCulture);

            if (value is IFormattable formattable)
                return formattable.ToString(null, CultureInfo.InvariantCulture);

            return value.ToString();
        }

        private static string Escape(string value)
        {
            var sb = new StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        public static string ToHtml(SnapshotObject root)
        {
            return SnapshotHtmlWriter.Write(root);
        }

        public static string ToText(SnapshotObject root)
        {
            var reasons = new Dictionary<string, string>();
            if (root.TryGet("unavailable", out object unavailable) && unavailable is List<object> entries)
            {
                foreach (SnapshotObject entry in entries.OfType<SnapshotObject>())
                {
                    entry.TryGet("path", out object path);
                    entry.TryGet("reason", out object reason);
                    if (path is string key && !reasons.ContainsKey(key))
                        reasons[key] = reason as string;
                }
            }

            var lines = new List<TextLine>(512);
            foreach (var item in root.Items)
            {
                if (item.Key != "unavailable")
                    Flatten(lines, item.Key, item.Value, reasons);
            }

            var sb = new StringBuilder(16 * 1024);
            sb.Append(TextHeader).AppendLine(", one value per line: path = value");
            sb.AppendLine("# [*] means the value is the same for every memory controller (DCT), null means not available");

            foreach (TextLine line in lines)
            {
                sb.Append(line.Path).Append(" = ").Append(line.Value);
                if (line.Comment != null)
                    sb.Append("  # ").Append(line.Comment);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private struct TextLine
        {
            public string Path;
            public string Value;
            public string Comment;
        }

        private static void Flatten(List<TextLine> output, string path, object value, Dictionary<string, string> reasons)
        {
            if (value is SnapshotObject obj)
            {
                foreach (var item in obj.Items)
                    Flatten(output, $"{path}.{item.Key}", item.Value, reasons);
                return;
            }

            if (value is List<object> list)
            {
                if (list.Count == 0)
                {
                    output.Add(new TextLine { Path = path, Value = "[]" });
                    return;
                }

                if (list.All(IsScalar))
                {
                    output.Add(new TextLine { Path = path, Value = string.Join(", ", list.Select(Scalar)) });
                    return;
                }

                if (list.Count > 1 && path.EndsWith(".per_dct", StringComparison.Ordinal))
                {
                    FlattenMerged(output, path, list, reasons);
                    return;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is SnapshotObject element && IsFlat(element))
                        output.Add(new TextLine
                        {
                            Path = $"{path}[{i}]",
                            Value = string.Join("; ", element.Items.Select(e => $"{e.Key}={Scalar(e.Value)}"))
                        });
                    else
                        Flatten(output, $"{path}[{i}]", list[i], reasons);
                }

                return;
            }

            string comment = null;
            if (value == null)
                reasons.TryGetValue(path, out comment);

            output.Add(new TextLine { Path = path, Value = Scalar(value), Comment = comment });
        }

        // Identical values of all array elements are written once with a [*] index
        private static void FlattenMerged(List<TextLine> output, string path, List<object> list, Dictionary<string, string> reasons)
        {
            var flattened = new List<List<TextLine>>(list.Count);
            foreach (object element in list)
            {
                var lines = new List<TextLine>();
                Flatten(lines, string.Empty, element, reasons);
                flattened.Add(lines);
            }

            var others = new List<Dictionary<string, string>>(list.Count - 1);
            for (int i = 1; i < flattened.Count; i++)
            {
                var values = new Dictionary<string, string>(flattened[i].Count);
                foreach (TextLine line in flattened[i])
                    values[line.Path] = line.Value;
                others.Add(values);
            }

            var same = new HashSet<string>();
            foreach (TextLine line in flattened[0])
            {
                bool equal = true;
                foreach (var values in others)
                {
                    if (!values.TryGetValue(line.Path, out string other) || other != line.Value)
                    {
                        equal = false;
                        break;
                    }
                }

                if (equal)
                {
                    same.Add(line.Path);
                    output.Add(new TextLine { Path = $"{path}[*]{line.Path}", Value = line.Value });
                }
            }

            for (int i = 0; i < flattened.Count; i++)
            {
                foreach (TextLine line in flattened[i])
                {
                    if (!same.Contains(line.Path))
                        output.Add(new TextLine { Path = $"{path}[{i}]{line.Path}", Value = line.Value, Comment = line.Comment });
                }
            }
        }
    }
}
