using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZenTimings.Export
{
    internal static class SnapshotHtmlWriter
    {
        private const string Title = "ZenTimings Snapshot";
        private const string Version = "1.0";
        internal static string Write(SnapshotObject root)
        {
            var sb = new StringBuilder(96 * 1024);
            WriteDocumentStart(sb);

            WriteHeader(sb, root);
            WriteSummaryWarning(sb);

            foreach (var item in root.Items)
            {
                if (item.Key == "unavailable" || item.Key == "schema" || item.Key == "schema_version" || item.Key == "generated_at")
                    continue;

                WriteSection(sb, ToTitle(item.Key), item.Value, item.Key);
            }

            sb.AppendLine("<footer class=\"footer\">" + Title + " v" + Version + "</footer>");
            sb.AppendLine("</div></body></html>");
            return sb.ToString();
        }

        private static void WriteDocumentStart(StringBuilder sb)
        {
            sb.AppendLine("<!doctype html>");
            sb.AppendLine("<html lang=\"en\"><head><meta charset=\"UTF-8\" />");
            sb.AppendLine("<meta name=\"generator\" content=\"" + SnapshotBuilder.SchemaName + "\" />");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
            sb.AppendLine("<title>" + Title + "</title>");
            sb.AppendLine("<meta name=\"description\" content=\"ZenTimings AMD Ryzen memory configuration snapshot\" />");
            sb.AppendLine("<style>");
            sb.AppendLine(GetStyles());
            sb.AppendLine("</style></head><body><div class=\"container\">");
        }

        private static string GetStyles()
        {
            return @":root {
              --bg-main: #f8fafc;
              --bg-card: #ffffff;
              --bg-sub: #f8fafc;
              --bg-header: #f1f5f9;
              --bg-hover: #f1f5f9;
              --border: #e2e8f0;
              --border-sub: #edf2f7;
              --text: #0f172a;
              --muted: #334155;
              --dim: #64748b;
              --cyan: #0284c7;
              --cyan-light: #e0f2fe;
              --emerald: #059669;
              --emerald-light: #dcfce7;
              --bad: #dc2626;
              --font: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif;
              --mono: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, ""Liberation Mono"", ""Courier New"", monospace;
              --shadow: 0 1px 3px rgba(0, 0, 0, .05), 0 1px 2px rgba(0, 0, 0, .04);
            }

            * {
              box-sizing: border-box;
              margin: 0;
              padding: 0;
            }

            body {
              background: var(--bg-main);
              color: var(--text);
              font-family: var(--font);
              font-size: 13px;
              line-height: 1.5;
              padding: 24px 16px 48px;
              -webkit-font-smoothing: antialiased;
            }

            .container {
              max-width: 1320px;
              margin: 0 auto;
              display: flex;
              flex-direction: column;
              gap: 20px;
            }

            .header {
              background: var(--bg-card);
              border: 1px solid var(--border);
              border-radius: 12px;
              padding: 20px 24px;
              box-shadow: var(--shadow);
              display: flex;
              flex-direction: column;
              gap: 16px;
            }

            .header-top {
              display: flex;
              flex-wrap: wrap;
              align-items: center;
              justify-content: space-between;
              gap: 12px;
              padding-bottom: 14px;
              border-bottom: 1px solid var(--border-sub);
            }

            .header-brand {
              display: flex;
              align-items: center;
              gap: 12px;
            }

            .header-badge {
              background: var(--cyan);
              color: #fff;
              font-family: var(--mono);
              font-weight: 700;
              font-size: 12px;
              padding: 4px 10px;
              border-radius: 6px;
              letter-spacing: .5px;
            }

            .title {
              font-size: 20px;
              font-weight: 700;
              color: var(--text);
            }

            .meta {
              display: flex;
              flex-wrap: wrap;
              align-items: center;
              gap: 8px 16px;
              color: var(--dim);
              font-size: 11px;
              font-family: var(--mono);
            }

            .meta strong {
              color: var(--muted);
            }

            .vitals-grid {
              display: grid;
              grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
              gap: 12px;
            }

            .vital-card {
              background: var(--bg-sub);
              border: 1px solid var(--border);
              border-radius: 8px;
              padding: 10px 14px;
              display: flex;
              flex-direction: column;
              gap: 3px;
            }

            .vital-label {
              font-size: 10px;
              font-weight: 700;
              text-transform: uppercase;
              letter-spacing: .5px;
              color: var(--dim);
            }

            .vital-val {
              font-family: var(--mono);
              font-size: 15px;
              font-weight: 700;
              color: var(--cyan);
              white-space: nowrap;
            }

            .vital-sub {
              font-size: 11px;
              color: var(--muted);
              white-space: nowrap;
              overflow: hidden;
              text-overflow: ellipsis;
            }

            .vital-card.wide {
              grid-column: span 2;
            }

            .section {
              background: var(--bg-card);
              border: 1px solid var(--border);
              border-radius: 12px;
              overflow: hidden;
              box-shadow: var(--shadow);
            }

            .section-h {
              background: var(--bg-header);
              padding: 12px 18px;
              border-bottom: 1px solid var(--border);
              font-size: 14px;
              font-weight: 700;
              color: var(--text);
              display: flex;
              align-items: center;
              justify-content: space-between;
              gap: 10px;
            }

            .section-tag {
              font-size: 11px;
              font-family: var(--mono);
              font-weight: 500;
              color: var(--dim);
            }

            .section-b {
              padding: 16px 18px;
              display: flex;
              flex-direction: column;
              gap: 16px;
            }

            .grid-2col {
              display: grid;
              grid-template-columns: repeat(auto-fit, minmax(600px, 1fr));
              gap: 16px;
            }

            .sub-panel {
              background: var(--bg-card);
              border: 1px solid var(--border);
              border-radius: 8px;
              padding: 14px;
              display: flex;
              flex-direction: column;
              gap: 10px;
            }

            .sub-h {
              margin: 0;
              color: var(--cyan);
              font-size: 12px;
              font-weight: 700;
              text-transform: uppercase;
              letter-spacing: .5px;
            }

            .note {
              color: var(--dim);
              font-size: 11px;
              line-height: 1.4;
            }

            .summary-warning {
              background: #fff7ed;
              border: 1px solid #fed7aa;
              color: #9a3412;
              border-radius: 10px;
              padding: 10px 14px;
              font-size: 12px;
              font-weight: 600;
              line-height: 1.45;
            }

            .table-wrap {
              overflow-x: auto;
              border: 1px solid var(--border);
              border-radius: 8px;
              background: #fff;
              -webkit-overflow-scrolling: touch;
            }

            table {
              width: 100%;
              border-collapse: collapse;
              font-size: 12px;
              table-layout: inline-table;
            }

            th,
            td {
              border-bottom: 1px solid var(--border-sub);
              padding: 8px 12px;
              vertical-align: middle;
            }

            th {
              font-size: 10px;
              font-weight: 700;
              text-transform: uppercase;
              letter-spacing: .5px;
              color: var(--dim);
              background: var(--bg-sub);
              text-align: left;
              white-space: nowrap;
            }

            td.key {
              font-weight: 600;
              color: #334155;
              white-space: nowrap;
              position: sticky;
              left: 0;
              background: #fff;
              z-index: 1;
              box-shadow: 1px 0 0 var(--border);
              width: 25%;
            }

            th.sticky-col {
              position: sticky;
              left: 0;
              background: var(--bg-sub);
              z-index: 2;
              box-shadow: 1px 0 0 var(--border);
            }

            tr:hover td {
              background: var(--bg-hover);
            }

            tr:hover td.key {
              background: var(--bg-hover);
            }

            tr:last-child td {
              border-bottom: none;
            }

            .mono {
              font-family: var(--mono);
            }

            .mismatch td {
              background: #fef2f2 !important;
            }

            .mismatch td.key {
              color: #991b1b !important;
              font-weight: 700;
              border-left: 3px solid #ef4444;
            }

            .mismatch td.mono {
              color: #991b1b !important;
              font-weight: 700;
            }

            .val-badge {
              display: inline-block;
              padding: 2px 7px;
              border-radius: 4px;
              font-size: 11px;
              font-family: var(--mono);
              font-weight: 600;
              line-height: 1.2;
            }

            .val-true {
              background: var(--emerald-light);
              color: var(--emerald);
              border: 1px solid #bbf7d0;
            }

            .val-false {
              background: #f1f5f9;
              color: var(--dim);
              border: 1px solid #e2e8f0;
            }

            .val-null {
              color: #94a3b8;
              font-style: italic;
              font-family: var(--mono);
            }

            .val-omitted {
              background: #f8fafc;
              color: #64748b;
              border: 1px dashed #cbd5e1;
              padding: 1px 6px;
              border-radius: 4px;
              font-size: 10px;
              font-family: var(--mono);
            }

            .footer {
              text-align: center;
              font-size: 11px;
              color: var(--dim);
              font-family: var(--mono);
              padding: 12px 0 24px;
            }

            @media (max-width: 768px) {
              body {
                padding: 12px 8px 32px;
              }

              .container {
                gap: 14px;
              }

              .header {
                padding: 14px 16px;
              }

              .section-b {
                padding: 12px;
              }

              .grid-2col {
                grid-template-columns: 1fr;
              }

              .vital-card.wide {
                grid-column: auto;
              }
            }";
        }

        private static void WriteHeader(StringBuilder sb, SnapshotObject root)
        {
            var processor = GetProcessorSummary(root);
            var motherboard = FirstNonEmpty(
                TryPathScalar(root, "static", "system", "board", "name"),
                TryPathScalar(root, "static", "system", "board", "vendor"),
                TryPathScalar(root, "static", "board", "name"),
                TryPathScalar(root, "board", "name"));

            var firstTimings = TryGetFirstChannelTimings(root);
            var frequency = firstTimings != null ? TryScalar(firstTimings, "Frequency") : null;
            var primaryTiming = BuildPrimaryTiming(firstTimings);
            var clockCard = BuildClockCard(root);

            sb.AppendLine("<header class=\"header\">");
            sb.AppendLine("<div class=\"header-top\">");
            sb.AppendLine("<div class=\"header-brand\"><h1 class=\"title\">" + Title + "</h1></div>");
            sb.AppendLine("<div class=\"meta\">");

            AppendMeta(sb, "Schema", TryScalar(root, "schema"));
            AppendMeta(sb, "Version", TryScalar(root, "schema_version"));
            AppendMeta(sb, "Generated", TryScalar(root, "generated_at"));

            if (root.TryGet("app", out object appObj) && appObj is SnapshotObject app)
            {
                AppendMeta(sb, "App", TryScalar(app, "name"));
                AppendMeta(sb, "App Ver", TryScalar(app, "version"));
            }

            sb.AppendLine("</div></div>");

            sb.AppendLine("<div class=\"vitals-grid\">");
            WriteVital(sb, "Processor", FirstNonEmpty(processor.Item1, "N/A"), FirstNonEmpty(processor.Item2, Title), true);
            WriteVital(sb, "Motherboard", FirstNonEmpty(motherboard, "N/A"), "Platform Information", true);
            WriteVital(sb, "Memory & Timings", FirstNonEmpty(frequency, "N/A"),
                !string.IsNullOrEmpty(primaryTiming) ? "Primary " + primaryTiming : "Primary timing unavailable");
            WriteVital(sb, FirstNonEmpty(clockCard.Item1, "Clock Ratio"), FirstNonEmpty(clockCard.Item2, "N/A"), FirstNonEmpty(clockCard.Item3, "Readings Power Table"));
            sb.AppendLine("</div>");
            sb.AppendLine("</header>");
        }

        private static void WriteVital(StringBuilder sb, string label, string value, string sub)
        {
            WriteVital(sb, label, value, sub, false);
        }

        private static void WriteVital(StringBuilder sb, string label, string value, string sub, bool wide)
        {
            sb.Append(wide ? "<div class=\"vital-card wide\">" : "<div class=\"vital-card\">")
                .Append("<span class=\"vital-label\">")
                .Append(EscapeHtml(label))
                .Append("</span><span class=\"vital-val\">")
                .Append(EscapeHtml(value))
                .Append("</span><span class=\"vital-sub\">")
                .Append(EscapeHtml(sub))
                .AppendLine("</span></div>");
        }

        private static Tuple<string, string> GetProcessorSummary(SnapshotObject root)
        {
            var name = FirstNonEmpty(
                TryPathScalar(root, "static", "system", "cpu", "name"),
                TryPathScalar(root, "static", "cpu", "name"),
                TryPathScalar(root, "cpu", "name"));

            var code = FirstNonEmpty(
                TryPathScalar(root, "static", "system", "cpu", "code_name"),
                TryPathScalar(root, "static", "cpu", "code_name"),
                TryPathScalar(root, "cpu", "code_name"));

            return Tuple.Create(name, code);
        }

        private static string BuildPrimaryTiming(SnapshotObject firstTimings)
        {
            if (firstTimings == null)
                return null;

            var cl = TryScalar(firstTimings, "CL");
            var rcd = TryScalar(firstTimings, "RCDRD");
            var rp = TryScalar(firstTimings, "RP");
            var ras = TryScalar(firstTimings, "RAS");

            if (string.IsNullOrEmpty(cl) || string.IsNullOrEmpty(rcd) || string.IsNullOrEmpty(rp) || string.IsNullOrEmpty(ras))
                return null;

            return cl + "-" + rcd + "-" + rp + "-" + ras;
        }

        private static Tuple<string, string, string> BuildClockCard(SnapshotObject root)
        {
            var mclkRaw = FirstNonEmpty(
                TryPathScalar(root, "readings", "power_table", "mclk_mhz"),
                TryPathScalar(root, "power_table", "mclk_mhz"));
            var uclkRaw = FirstNonEmpty(
                TryPathScalar(root, "readings", "power_table", "uclk_mhz"),
                TryPathScalar(root, "power_table", "uclk_mhz"));
            var fclkRaw = FirstNonEmpty(
                TryPathScalar(root, "readings", "power_table", "fclk_mhz"),
                TryPathScalar(root, "power_table", "fclk_mhz"));

            var mclk = NormalizeClockValue(mclkRaw);
            var uclk = NormalizeClockValue(uclkRaw);
            var fclk = NormalizeClockValue(fclkRaw);

            string label = "Clock Ratio";
            string ratio = TryGetClockRatio(mclk, uclk);
            if (!string.IsNullOrEmpty(ratio))
                label += " (" + ratio + ")";

            string value = !string.IsNullOrEmpty(mclk) ? "MCLK " + mclk : null;

            var parts = new List<string>();
            if (!string.IsNullOrEmpty(uclk)) parts.Add("UCLK " + uclk);
            if (!string.IsNullOrEmpty(fclk)) parts.Add("FCLK " + fclk + " MHz");
            string sub = parts.Count > 0 ? string.Join(" • ", parts) : null;

            return Tuple.Create(label, value, sub);
        }

        private static string NormalizeClockValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            var text = value.Trim();
            if (text.EndsWith("MHz", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(0, text.Length - 3).TrimEnd();

            return text;
        }

        private static string TryGetClockRatio(string mclk, string uclk)
        {
            if (!TryParseLeadingNumber(mclk, out var mclkValue) || !TryParseLeadingNumber(uclk, out var uclkValue) || mclkValue <= 0 || uclkValue <= 0)
                return null;

            var ratio = mclkValue / uclkValue;
            if (Math.Abs(ratio - 1.0) < 0.01)
                return "1:1";
            if (Math.Abs(ratio - 2.0) < 0.01)
                return "1:2";

            return "1:" + ratio.ToString("0.##");
        }

        private static bool TryParseLeadingNumber(string text, out double value)
        {
            value = 0;
            if (string.IsNullOrEmpty(text))
                return false;

            var chars = new StringBuilder();
            foreach (var c in text.Trim())
            {
                if (char.IsDigit(c) || c == '.' || c == ',')
                    chars.Append(c == ',' ? '.' : c);
                else if (chars.Length > 0)
                    break;
                else if (!char.IsWhiteSpace(c))
                    return false;
            }

            if (chars.Length == 0)
                return false;

            return double.TryParse(chars.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        private static SnapshotObject TryGetFirstChannelTimings(SnapshotObject root)
        {
            SnapshotObject timings;
            if (!TryPathObject(root, out timings, "config", "timings"))
                return null;

            object perDctRaw;
            if (!timings.TryGet("per_dct", out perDctRaw) || !(perDctRaw is List<object> perDct))
                return null;

            var first = perDct.OfType<SnapshotObject>().FirstOrDefault();
            if (first == null)
                return null;

            object timingsRaw;
            return first.TryGet("timings", out timingsRaw) ? timingsRaw as SnapshotObject : null;
        }

        private static bool TryPathObject(SnapshotObject root, out SnapshotObject result, params string[] path)
        {
            result = root;
            foreach (var key in path)
            {
                if (result == null)
                    return false;

                object next;
                if (!result.TryGet(key, out next) || !(next is SnapshotObject))
                {
                    result = null;
                    return false;
                }

                result = (SnapshotObject)next;
            }

            return result != null;
        }

        private static string TryPathScalar(SnapshotObject root, params string[] path)
        {
            if (path == null || path.Length == 0)
                return null;

            SnapshotObject cursor = root;
            for (int i = 0; i < path.Length - 1; i++)
            {
                object next;
                if (cursor == null || !cursor.TryGet(path[i], out next) || !(next is SnapshotObject))
                    return null;

                cursor = (SnapshotObject)next;
            }

            return cursor != null ? TryScalar(cursor, path[path.Length - 1]) : null;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
                return null;

            foreach (var value in values)
            {
                if (!string.IsNullOrEmpty(value))
                    return value;
            }

            return null;
        }

        private static void AppendMeta(StringBuilder sb, string name, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            sb.Append("<span><strong>").Append(EscapeHtml(name)).Append(":</strong> <code class=\"mono\">")
              .Append(EscapeHtml(value)).AppendLine("</code></span>");
        }

        private static void WriteSummaryWarning(StringBuilder sb)
        {
            sb.AppendLine("<div class=\"summary-warning\">Warning: Not all values may be accurate and this report can contain mistakes.</div>");
        }

        private static void WriteSection(StringBuilder sb, string title, object value, string path)
        {
            sb.AppendLine("<section class=\"section\">");
            sb.Append("<div class=\"section-h\"><span>").Append(EscapeHtml(title)).AppendLine("</span></div>");
            sb.AppendLine("<div class=\"section-b\">");
            WriteNode(sb, value, path);
            sb.AppendLine("</div></section>");
        }

        private static void WriteNode(StringBuilder sb, object value, string path)
        {
            SnapshotObject obj = value as SnapshotObject;
            if (path == "config.timings" && obj != null)
            {
                WriteTimingsTable(sb, obj);
                return;
            }

            if (obj != null)
            {
                if (path == "config.apob")
                    WriteApobSideBySide(sb, obj, path);
                else if (IsCompactPath(path))
                    WriteCompactObject(sb, obj, path);
                else
                    WriteRegularObject(sb, obj, path);
                return;
            }

            var list = value as List<object>;
            if (list != null)
            {
                WriteList(sb, list, path);
                return;
            }

            sb.Append("<div>").Append(FormatScalarHtml(SnapshotWriter.Scalar(value), true)).AppendLine("</div>");
        }

        private static void WriteRegularObject(StringBuilder sb, SnapshotObject obj, string path)
        {
            var rows = new List<KeyValuePair<string, object>>();
            foreach (var item in obj.Items)
            {
                string childPath = path + "." + item.Key;
                if (item.Value is SnapshotObject || item.Value is List<object>)
                {
                    if (rows.Count > 0)
                    {
                        WriteNameValueTable(sb, rows);
                        rows.Clear();
                    }

                    sb.Append("<div class=\"sub-h\">").Append(EscapeHtml(ToTitle(item.Key))).AppendLine("</div>");
                    WriteNode(sb, item.Value, childPath);
                }
                else
                {
                    rows.Add(item);
                }
            }

            if (rows.Count > 0)
                WriteNameValueTable(sb, rows);
        }

        private static void WriteCompactObject(StringBuilder sb, SnapshotObject obj, string path)
        {
            var rows = new List<KeyValuePair<string, object>>();
            foreach (var item in obj.Items)
            {
                string childPath = path + "." + item.Key;
                var childObj = item.Value as SnapshotObject;
                if (childObj != null && !IsLeafValueObject(childObj))
                {
                    if (rows.Count > 0)
                    {
                        WriteNameValueRawTable(sb, rows);
                        rows.Clear();
                    }

                    sb.Append("<div class=\"sub-h\">").Append(EscapeHtml(ToTitle(item.Key))).AppendLine("</div>");
                    WriteCompactObject(sb, childObj, childPath);
                    continue;
                }

                if (item.Value is List<object>)
                {
                    if (rows.Count > 0)
                    {
                        WriteNameValueRawTable(sb, rows);
                        rows.Clear();
                    }

                    sb.Append("<div class=\"sub-h\">").Append(EscapeHtml(ToTitle(item.Key))).AppendLine("</div>");
                    WriteNode(sb, item.Value, childPath);
                    continue;
                }

                rows.Add(item);
            }

            if (rows.Count > 0)
                WriteNameValueRawTable(sb, rows);
        }

        private static void WriteApobSideBySide(StringBuilder sb, SnapshotObject apob, string path)
        {
            object mainRaw;
            object extendedRaw;
            var hasMain = apob.TryGet("main", out mainRaw) && mainRaw is SnapshotObject;
            var hasExtended = apob.TryGet("extended", out extendedRaw) && extendedRaw is SnapshotObject;

            if (!hasMain && !hasExtended)
            {
                WriteCompactObject(sb, apob, path);
                return;
            }

            sb.AppendLine("<div class=\"grid-2col\">");

            if (hasMain)
            {
                sb.AppendLine("<div class=\"sub-panel\">");
                sb.AppendLine("<div class=\"sub-h\">APOB: Main</div>");
                WriteCompactObject(sb, (SnapshotObject)mainRaw, path + ".main");
                sb.AppendLine("</div>");
            }

            if (hasExtended)
            {
                sb.AppendLine("<div class=\"sub-panel\">");
                sb.AppendLine("<div class=\"sub-h\">APOB: Extended</div>");
                WriteCompactObject(sb, (SnapshotObject)extendedRaw, path + ".extended");
                sb.AppendLine("</div>");
            }

            sb.AppendLine("</div>");

            foreach (var item in apob.Items)
            {
                if (item.Key == "main" || item.Key == "extended")
                    continue;

                sb.Append("<div class=\"sub-h\">").Append(EscapeHtml(ToTitle(item.Key))).AppendLine("</div>");
                WriteNode(sb, item.Value, path + "." + item.Key);
            }
        }

        private static void WriteList(StringBuilder sb, List<object> list, string path)
        {
            if (list.Count == 0)
            {
                sb.AppendLine("<div class=\"note\">null</div>");
                return;
            }

            if (list.All(x => x is SnapshotObject))
            {
                if (string.Equals(path, "readings.sensors.items", StringComparison.Ordinal))
                {
                    WriteSensorsByGroup(sb, list.Cast<SnapshotObject>());
                    return;
                }

                bool modulesGrid = string.Equals(path, "modules", StringComparison.Ordinal)
                    || path.EndsWith(".modules", StringComparison.Ordinal)
                    || string.Equals(path, "readings.dimm", StringComparison.Ordinal)
                    || path.EndsWith(".dimm", StringComparison.Ordinal);

                if (modulesGrid)
                    sb.AppendLine("<div class=\"grid-2col\">");

                int index = 1;
                bool isDimmList = string.Equals(path, "readings.dimm", StringComparison.Ordinal)
                    || path.EndsWith(".dimm", StringComparison.Ordinal);
                string itemLabel = isDimmList ? "DIMM" : ToTitle(PathLeaf(path));

                foreach (SnapshotObject item in list.Cast<SnapshotObject>())
                {
                    if (modulesGrid)
                        sb.AppendLine("<div class=\"sub-panel\">");

                    sb.Append("<div class=\"sub-h\">").Append(EscapeHtml(itemLabel)).Append(' ').Append(index++).AppendLine("</div>");
                    WriteNode(sb, item, path);

                    if (modulesGrid)
                        sb.AppendLine("</div>");
                }

                if (modulesGrid)
                    sb.AppendLine("</div>");

                return;
            }

            sb.AppendLine("<div class=\"table-wrap\"><table><thead><tr><th class=\"sticky-col\">Value</th></tr></thead><tbody>");
            foreach (object item in list)
                sb.Append("<tr><td class=\"key\">").Append(FormatScalarHtml(SnapshotWriter.Scalar(item), true)).AppendLine("</td></tr>");
            sb.AppendLine("</tbody></table></div>");
        }

        private static void WriteSensorsByGroup(StringBuilder sb, IEnumerable<SnapshotObject> sensors)
        {
            var groups = new List<KeyValuePair<string, List<SnapshotObject>>>();
            foreach (var sensor in sensors)
            {
                string groupName = FirstNonEmpty(TryScalar(sensor, "group"), "Sensors");
                var existing = groups.FirstOrDefault(g => string.Equals(g.Key, groupName, StringComparison.Ordinal));
                if (existing.Key == null)
                {
                    var list = new List<SnapshotObject> { sensor };
                    groups.Add(new KeyValuePair<string, List<SnapshotObject>>(groupName, list));
                }
                else
                {
                    existing.Value.Add(sensor);
                }
            }

            foreach (var group in groups)
            {
                sb.Append("<div class=\"sub-h\">").Append(EscapeHtml(group.Key)).AppendLine("</div>");
                sb.AppendLine("<div class=\"table-wrap\"><table><thead><tr><th class=\"sticky-col\">Name</th><th>Type</th><th>Value</th><th>Min</th><th>Max</th></tr></thead><tbody>");
                foreach (var sensor in group.Value)
                {
                    var name = FirstNonEmpty(TryScalar(sensor, "name"), "N/A");
                    var type = FirstNonEmpty(TryScalar(sensor, "type"), "null");
                    var unit = FirstNonEmpty(TryScalar(sensor, "unit"), "—");
                    var value = FirstNonEmpty(TryScalar(sensor, "value"), "null");
                    var min = FirstNonEmpty(TryScalar(sensor, "min"), "null");
                    var max = FirstNonEmpty(TryScalar(sensor, "max"), "null");

                    sb.Append("<tr><td class=\"key\">").Append(EscapeHtml(name)).Append("</td><td>")
                        .Append(FormatScalarHtml(type, true)).Append("</td><td>")
                        .Append(FormatScalarHtml(WithUnit(value, unit), true)).Append("</td><td>")
                        .Append(FormatScalarHtml(WithUnit(min, unit), true)).Append("</td><td>")
                        .Append(FormatScalarHtml(WithUnit(max, unit), true)).AppendLine("</td></tr>");
                }
                sb.AppendLine("</tbody></table></div>");
            }
        }

        private static string WithUnit(string value, string unit)
        {
            if (string.IsNullOrEmpty(value) || string.Equals(value, "null", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "—", StringComparison.Ordinal))
                return value;

            if (string.IsNullOrEmpty(unit) || string.Equals(unit, "—", StringComparison.Ordinal))
                return value;

            return value + " " + unit;
        }

        private static void WriteNameValueTable(StringBuilder sb, List<KeyValuePair<string, object>> rows)
        {
            sb.AppendLine("<div class=\"table-wrap\"><table><thead><tr><th class=\"sticky-col\">Name</th><th>Value</th></tr></thead><tbody>");
            foreach (var row in rows)
            {
                sb.Append("<tr><td class=\"key\">").Append(EscapeHtml(ToTitle(row.Key))).Append("</td><td>")
                    .Append(FormatFieldValueHtml(row.Key, SnapshotWriter.Scalar(row.Value), true)).AppendLine("</td></tr>");
            }
            sb.AppendLine("</tbody></table></div>");
        }

        private static void WriteNameValueRawTable(StringBuilder sb, List<KeyValuePair<string, object>> rows)
        {
            sb.AppendLine("<div class=\"table-wrap\"><table><thead><tr><th class=\"sticky-col\">Name</th><th>Value</th><th>Raw</th></tr></thead><tbody>");
            foreach (var row in rows)
            {
                string value;
                string raw;
                ExtractValueRaw(row.Value, out value, out raw);
                sb.Append("<tr><td class=\"key\">").Append(EscapeHtml(ToTitle(row.Key))).Append("</td><td>")
                    .Append(FormatFieldValueHtml(row.Key, value, true)).Append("</td><td>")
                    .Append(FormatScalarHtml(string.IsNullOrEmpty(raw) ? "—" : raw, true)).AppendLine("</td></tr>");
            }
            sb.AppendLine("</tbody></table></div>");
        }

        private static void ExtractValueRaw(object value, out string display, out string raw)
        {
            display = SnapshotWriter.Scalar(value);
            raw = string.Empty;

            var obj = value as SnapshotObject;
            if (obj == null)
                return;

            object field;
            if (obj.TryGet("raw", out field) && field != null)
                raw = SnapshotWriter.Scalar(field);

            if (obj.TryGet("text", out field) && field != null)
            {
                display = SnapshotWriter.Scalar(field);
                return;
            }
            if (obj.TryGet("value", out field) && field != null)
            {
                display = SnapshotWriter.Scalar(field);
                return;
            }
            if (obj.TryGet("mv", out field) && field != null)
            {
                display = SnapshotWriter.Scalar(field) + " mV";
                return;
            }
            if (obj.Items.Count == 1)
                display = SnapshotWriter.Scalar(obj.Items[0].Value);
        }

        private static void WriteTimingsTable(StringBuilder sb, SnapshotObject timings)
        {
            var mismatch = new HashSet<string>(StringComparer.Ordinal);
            object mismatchRaw;
            if (timings.TryGet("dct_mismatch", out mismatchRaw) && mismatchRaw is List<object>)
            {
                foreach (string name in ((List<object>)mismatchRaw).OfType<string>())
                    mismatch.Add(name);
            }

            object dctRaw;
            if (!timings.TryGet("per_dct", out dctRaw) || !(dctRaw is List<object>))
            {
                sb.AppendLine("<div class=\"note\">No timings available.</div>");
                return;
            }

            var channels = ((List<object>)dctRaw).OfType<SnapshotObject>().ToList();
            var sets = channels.Select(c => c.TryGet("timings", out object t) ? t as SnapshotObject : null).Where(t => t != null).ToList();
            var keys = new List<string>();
            foreach (var set in sets)
                foreach (var item in set.Items)
                    if (!keys.Contains(item.Key)) keys.Add(item.Key);

            sb.AppendLine("<div class=\"table-wrap\"><table><thead><tr><th class=\"sticky-col\">Name</th>");
            foreach (var channel in channels)
            {
                channel.TryGet("dct", out object dct);
                sb.Append("<th>DCT ").Append(EscapeHtml(SnapshotWriter.Scalar(dct))).Append("</th>");
            }
            sb.AppendLine("</tr></thead><tbody>");

            foreach (string key in keys)
            {
                sb.Append(mismatch.Contains(key) ? "<tr class=\"mismatch\">" : "<tr>");
                sb.Append("<td class=\"key\">" + EscapeHtml(ToTitle(key)) + "</td>");
                foreach (var channel in channels)
                {
                    string valueText = "null";
                    if (channel.TryGet("timings", out object t) && t is SnapshotObject set && set.TryGet(key, out object scalar))
                        valueText = SnapshotWriter.Scalar(scalar);
                    sb.Append("<td>").Append(FormatScalarHtml(valueText, true)).Append("</td>");
                }
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table></div>");
        }

        private static bool IsLeafValueObject(SnapshotObject obj)
        {
            if (obj == null || obj.Count == 0)
                return false;

            object ignored;
            if (obj.TryGet("raw", out ignored) || obj.TryGet("text", out ignored) || obj.TryGet("value", out ignored) || obj.TryGet("mv", out ignored))
                return true;

            if (obj.Count == 1)
                return !(obj.Items[0].Value is SnapshotObject) && !(obj.Items[0].Value is List<object>);

            return obj.Items.All(i => !(i.Value is SnapshotObject) && !(i.Value is List<object>));
        }

        private static bool IsCompactPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                (path.StartsWith("config.aod", StringComparison.Ordinal) ||
                 path.StartsWith("config.apob", StringComparison.Ordinal));
        }

        private static string TryScalar(SnapshotObject obj, string key)
        {
            if (obj != null && obj.TryGet(key, out object value) && value != null)
                return SnapshotWriter.Scalar(value);

            return null;
        }

        private static string PathLeaf(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "item";

            int dot = path.LastIndexOf('.');
            return dot >= 0 ? path.Substring(dot + 1) : path;
        }

        private static string FormatScalarHtml(string value, bool mono)
        {
            if (string.IsNullOrEmpty(value))
                return mono ? "<span class=\"mono\"></span>" : string.Empty;

            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                return "<span class=\"val-badge val-true\">true</span>";
            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                return "<span class=\"val-badge val-false\">false</span>";
            if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
                return "<span class=\"val-null\">null</span>";

            var escaped = EscapeHtml(value);
            return mono ? "<span class=\"mono\">" + escaped + "</span>" : escaped;
        }

        private static string FormatFieldValueHtml(string fieldName, string value, bool mono)
        {
            if (!string.IsNullOrEmpty(fieldName)
                && string.Equals(value, "null", StringComparison.OrdinalIgnoreCase)
                && (fieldName.IndexOf("serial", StringComparison.OrdinalIgnoreCase) >= 0
                    || fieldName.IndexOf("raw_dump", StringComparison.OrdinalIgnoreCase) >= 0
                    || fieldName.IndexOf("raw_spd", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return "<span class=\"val-omitted\">omitted</span>";
            }

            return FormatScalarHtml(value, mono);
        }


        private static readonly HashSet<string> TitleAcronyms = new HashSet<string>(StringComparer.Ordinal)
        {
            "acpi", "agesa", "aod", "apob", "bios", "cad", "ccd", "ccx", "cldo", "clk", "cpu", "cs", "dct",
            "cpuid", "ddr", "dimm", "dram", "ecc", "expo", "fclk", "gpu", "i2c", "imc", "iod", "lpddr", "mclk", "nb",
            "odt", "pci", "pmic", "pmu", "procodt", "ras", "rtt", "smu", "soc", "spd", "svi", "tccd", "uclk",
            "umc", "vdd", "vddcr", "vddg", "vddio", "vddp", "vddq", "vdimm", "vin", "vpp", "vsoc", "vtt",
            "wmi", "wr", "xmp"
        };

        private static string ToTitle(string key)
        {
            if (string.IsNullOrEmpty(key))
                return key;

            var parts = key.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                string lower = part.ToLowerInvariant();

                switch (lower)
                {
                    case "ghz":
                        parts[i] = "GHz";
                        continue;
                    case "mhz":
                        parts[i] = "MHz";
                        continue;
                    case "mts":
                    case "mtps":
                        parts[i] = "MT/s";
                        continue;
                    case "mv":
                        parts[i] = "mV";
                        continue;
                    case "ms":
                        parts[i] = "ms";
                        continue;
                }

                // Rail names that start with a digit, such as 1v0 / 1v8
                if (!char.IsLetter(part[0]))
                {
                    parts[i] = part.ToUpperInvariant();
                    continue;
                }

                int stemLength = lower.Length;
                while (stemLength > 0 && char.IsDigit(lower[stemLength - 1]))
                    stemLength--;

                parts[i] = TitleAcronyms.Contains(lower.Substring(0, stemLength))
                    ? part.ToUpperInvariant()
                    : char.ToUpperInvariant(part[0]) + part.Substring(1);
            }

            return string.Join(" ", parts);
        }

        private static string EscapeHtml(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }
    }
}
