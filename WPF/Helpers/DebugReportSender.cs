using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ZenTimings.Helpers
{
    /// <summary>What the user chose to send with a debug report.</summary>
    internal sealed class DebugReportSubmission
    {
        public string Report { get; set; }
        public string Description { get; set; }
        public string Email { get; set; }
        /// <summary>PNG bytes of the main window, or null to send no screenshot.</summary>
        public byte[] ScreenshotPng { get; set; }
    }

    /// <summary>The server refused a report; the message is fit to show to the user.</summary>
    internal sealed class DebugReportException : Exception
    {
        public DebugReportException(string message) : base(message) { }
    }

    /// <summary>
    /// Uploads a debug report to the report endpoint (a Cloudflare Worker on zentimings.com) as multipart/form-data.
    /// The Worker answers with JSON: {"id":"..."} on success, {"error":"..."} otherwise.
    /// </summary>
    internal static class DebugReportSender
    {
        public const string Endpoint = "https://zentimings.com/api/debug-report";

        // Keep in sync with the limits in the Worker.
        public const int MaxDescriptionLength = 4000;
        public const int MaxEmailLength = 254;

        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        private static string AppVersion
        {
            get
            {
                var attr = (AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(
                    Assembly.GetExecutingAssembly(), typeof(AssemblyFileVersionAttribute), false);
                return attr?.Version ?? "0.0.0.0";
            }
        }

#if BETA
        private const string BuildChannel = "beta";
#else
        private const string BuildChannel = "release";
#endif

        /// <summary>Sends the report and returns the reference ID the server assigned to it.</summary>
        public static async Task<string> SendAsync(DebugReportSubmission submission, CancellationToken cancellationToken)
        {
            using (var content = new MultipartFormDataContent())
            {
                AddPart(content, new StringContent(AppVersion, Encoding.UTF8), "appVersion");
                AddPart(content, new StringContent(BuildChannel, Encoding.UTF8), "channel");
                AddPart(content, new StringContent(submission.Description ?? "", Encoding.UTF8), "description");
                AddPart(content, new StringContent(submission.Email ?? "", Encoding.UTF8), "email");

                // Gzipped: the hex dumps in the report compress several times over.
                var report = new ByteArrayContent(Gzip(Encoding.UTF8.GetBytes(submission.Report ?? "")));
                report.Headers.ContentType = new MediaTypeHeaderValue("application/gzip");
                AddPart(content, report, "report", "debug-report.txt.gz");

                if (submission.ScreenshotPng != null && submission.ScreenshotPng.Length > 0)
                {
                    var screenshot = new ByteArrayContent(submission.ScreenshotPng);
                    screenshot.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                    AddPart(content, screenshot, "screenshot", "screenshot.png");
                }

                using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content })
                {
                    request.Headers.UserAgent.Add(new ProductInfoHeaderValue("ZenTimings", AppVersion));
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    using (var response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            if ((int)response.StatusCode == 429)
                                throw new DebugReportException("Too many reports were sent from your connection. Please try again later.");

                            throw new DebugReportException(
                                ReadJsonString(body, "error") ?? $"The server returned an error ({(int)response.StatusCode}).");
                        }

                        string id = ReadJsonString(body, "id");
                        if (string.IsNullOrEmpty(id))
                            throw new DebugReportException("The server sent an unexpected response.");

                        return id;
                    }
                }
            }
        }

        // .NET Framework writes name=value unquoted (and adds filename*=), which the Worker's multipart
        // parser rejects as a part without a name. Write the quoted form browsers send instead.
        private static void AddPart(MultipartFormDataContent content, HttpContent part, string name, string fileName = null)
        {
            var disposition = new ContentDispositionHeaderValue("form-data") { Name = "\"" + name + "\"" };
            if (fileName != null)
                disposition.FileName = "\"" + fileName + "\"";
            part.Headers.ContentDisposition = disposition;
            content.Add(part);
        }

        private static byte[] Gzip(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
                    gzip.Write(data, 0, data.Length);
                return output.ToArray();
            }
        }

        // The responses are tiny, flat objects with plain string values, so a regex is enough here.
        private static string ReadJsonString(string json, string name)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            var match = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            return match.Success ? Regex.Unescape(match.Groups[1].Value) : null;
        }
    }
}
