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

        private static void WriteHtmlObject(StringBuilder sb, SnapshotObject obj, string path, ISet<string> timingMismatch)
        {
            var rows = new List<KeyValuePair<string, object>>();
            foreach (var item in obj.Items)
            {
                string childPath = path.Length == 0 ? item.Key : path + "." + item.Key;
                if (childPath == "config.timings" && item.Value is SnapshotObject timings)
                {
                    WriteTimingsTable(sb, timings);
                    continue;
                }

                if (item.Value is SnapshotObject || item.Value is List<object>)
                {
                    if (IsCompactPath(path))
                    {
                        if (item.Value is SnapshotObject compactChild && !IsLeafValueObject(compactChild))
                        {
                            if (rows.Count > 0)
                            {
                                WriteHtmlRows(sb, rows, timingMismatch, path);
                                rows.Clear();
                            }

                            sb.Append("<h2>").Append(EscapeHtml(ToSectionTitle(item.Key))).AppendLine("</h2>");
                            WriteHtmlRows(sb, compactChild.Items, timingMismatch, childPath);
                            continue;
                        }

                        if (item.Value is List<object>)
                        {
                            if (rows.Count > 0)
                            {
                                WriteHtmlRows(sb, rows, timingMismatch, path);
                                rows.Clear();
                            }

                            sb.Append("<h2>").Append(EscapeHtml(ToSectionTitle(item.Key))).AppendLine("</h2>");
                            WriteHtmlValue(sb, item.Value, childPath, timingMismatch);
                            continue;
                        }

                        rows.Add(item);
                        continue;
                    }

                    if (rows.Count > 0)
                    {
                        WriteHtmlRows(sb, rows, timingMismatch, path);
                        rows.Clear();
                    }

                    sb.Append("<h2>").Append(EscapeHtml(ToSectionTitle(item.Key))).AppendLine("</h2>");
                    WriteHtmlValue(sb, item.Value, childPath, timingMismatch);
                    continue;
                }

                rows.Add(item);
            }

            if (rows.Count > 0)
                WriteHtmlRows(sb, rows, timingMismatch, path);
        }

        private static void WriteHtmlRows(StringBuilder sb, List<KeyValuePair<string, object>> rows, ISet<string> timingMismatch, string path)
        {
            bool compact = IsCompactPath(path) && rows.Count > 2;
            sb.AppendLine(compact ? "<table class=\"compact\">" : "<table>");

            if (compact)
                sb.AppendLine("<tr><th>Name</th><th>Value</th><th>Raw</th></tr>");

            foreach (var row in rows)
            {
                bool mismatch = timingMismatch != null && timingMismatch.Contains(row.Key);
                sb.Append(mismatch ? "<tr class=\"mismatch\">" : "<tr>");
                sb.Append("<td class=\"key\">").Append(EscapeHtml(row.Key)).Append("</td>");

                if (!compact)
                {
                    sb.Append("<td>").Append(EscapeHtml(FormatHtmlValue(row.Value))).AppendLine("</td></tr>");
                    continue;
                }

                string valueText;
                string rawText;
                ExtractCompactColumns(row.Value, out valueText, out rawText);
                sb.Append("<td>").Append(EscapeHtml(valueText)).Append("</td><td>").Append(EscapeHtml(rawText)).AppendLine("</td></tr>");
            }

            sb.AppendLine("</table>");
        }

        private static void ExtractCompactColumns(object value, out string display, out string raw)
        {
            display = Scalar(value);
            raw = string.Empty;

            var obj = value as SnapshotObject;
            if (obj == null)
                return;

            if (obj.TryGet("raw", out object rawValue) && rawValue != null)
                raw = Scalar(rawValue);

            if (obj.TryGet("text", out object textValue) && textValue != null)
            {
                display = Scalar(textValue);
                return;
            }

            if (obj.TryGet("value", out object valueValue) && valueValue != null)
            {
                display = Scalar(valueValue);
                return;
            }

            if (obj.TryGet("mv", out object mvValue) && mvValue != null)
            {
                display = Scalar(mvValue);
                return;
            }

            if (obj.TryGet("name", out object nameValue) && nameValue != null)
            {
                display = Scalar(nameValue);
                return;
            }

            if (obj.Items.Count == 1)
            {
                display = Scalar(obj.Items[0].Value);
                return;
            }

            display = ToInlineJson(obj);
        }

        private static bool IsCompactPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                (path.StartsWith("config.aod", StringComparison.Ordinal) ||
                 path.StartsWith("config.apob", StringComparison.Ordinal));
        }

        private static bool IsLeafValueObject(SnapshotObject obj)
        {
            if (obj == null || obj.Count == 0)
                return false;

            if (obj.TryGet("raw", out object raw) || obj.TryGet("text", out object text) || obj.TryGet("value", out object value) || obj.TryGet("mv", out object mv))
                return true;

            if (obj.Count == 1)
            {
                object only = obj.Items[0].Value;
                return IsScalar(only);
            }

            return obj.Items.All(i => IsScalar(i.Value));
        }

        private static void WriteHtmlValue(StringBuilder sb, object value, string path, ISet<string> timingMismatch)
        {
            if (value is SnapshotObject child)
            {
                WriteHtmlObject(sb, child, path, timingMismatch);
                return;
            }

            if (value is List<object> list)
            {
                WriteHtmlList(sb, list, path, timingMismatch);
                return;
            }

            sb.Append("<div>").Append(EscapeHtml(Scalar(value))).AppendLine("</div>");
        }

        private static void WriteHtmlList(StringBuilder sb, List<object> list, string path, ISet<string> timingMismatch)
        {
            if (list.Count == 0)
            {
                sb.AppendLine("<div>null</div>");
                return;
            }

            if (list.All(item => item is SnapshotObject))
            {
                int index = 1;
                foreach (SnapshotObject item in list)
                {
                    sb.Append("<h2>").Append(EscapeHtml(ToSectionTitle(PathLeaf(path)))).Append(' ').Append(index++).AppendLine("</h2>");
                    WriteHtmlObject(sb, item, path, timingMismatch);
                }
                return;
            }

            sb.AppendLine("<table>");
            foreach (object item in list)
                sb.Append("<tr><td>").Append(EscapeHtml(Scalar(item))).AppendLine("</td></tr>");
            sb.AppendLine("</table>");
        }

        private static void WriteTimingsTable(StringBuilder sb, SnapshotObject timings)
        {
            var mismatch = new HashSet<string>(StringComparer.Ordinal);
            if (timings.TryGet("dct_mismatch", out object mismatchRaw) && mismatchRaw is List<object> mismatchList)
            {
                foreach (string name in mismatchList.OfType<string>())
                    mismatch.Add(name);
            }

            if (!timings.TryGet("per_dct", out object dctRaw) || !(dctRaw is List<object> dctEntries))
            {
                sb.AppendLine("<h2>Timings</h2><div>null</div>");
                return;
            }

            var channels = dctEntries.OfType<SnapshotObject>().ToList();
            var timingSets = channels
                .Select(c => c.TryGet("timings", out object t) ? t as SnapshotObject : null)
                .Where(t => t != null)
                .ToList();

            var keys = new List<string>();
            foreach (SnapshotObject set in timingSets)
            {
                foreach (var item in set.Items)
                {
                    if (!keys.Contains(item.Key))
                        keys.Add(item.Key);
                }
            }

            sb.AppendLine("<h2>Timings</h2>");
            sb.AppendLine("<table>");
            sb.Append("<tr><th>timing</th>");
            foreach (SnapshotObject channel in channels)
            {
                channel.TryGet("dct", out object dct);
                sb.Append("<th>DCT ").Append(EscapeHtml(Scalar(dct))).Append("</th>");
            }
            sb.AppendLine("</tr>");

            foreach (string key in keys)
            {
                bool rowMismatch = mismatch.Contains(key);
                sb.Append(rowMismatch ? "<tr class=\"mismatch\">" : "<tr>");
                sb.Append("<td class=\"key\">").Append(EscapeHtml(key)).Append("</td>");
                foreach (SnapshotObject channel in channels)
                {
                    var value = "null";
                    if (channel.TryGet("timings", out object t) && t is SnapshotObject set && set.TryGet(key, out object scalar))
                        value = Scalar(scalar);

                    sb.Append("<td>").Append(EscapeHtml(value)).Append("</td>");
                }
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</table>");
        }

        private static string PathLeaf(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "item";

            int dot = path.LastIndexOf('.');
            return dot >= 0 ? path.Substring(dot + 1) : path;
        }

        private static string FormatHtmlValue(object value)
        {
            if (value is SnapshotObject || value is List<object>)
                return ToInlineJson(value);

            return Scalar(value);
        }

        private static string ToInlineJson(object value)
        {
            var sb = new StringBuilder(128);
            WriteJson(sb, value, 0);
            return sb.ToString().Replace("\r", "").Replace("\n", " ").Trim();
        }

        private static string ToSectionTitle(string key)
        {
            if (string.IsNullOrEmpty(key))
                return key;

            var parts = key.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                parts[i] = part.Length <= 4 ? part.ToUpperInvariant() : char.ToUpperInvariant(part[0]) + part.Substring(1);
            }

            return string.Join(" ", parts);
        }

        private static string EscapeHtml(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
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
