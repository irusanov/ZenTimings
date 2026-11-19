using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Xml.Serialization;
using ZenTimings.Common;
using ZenTimings.Encryption;
using ZenTimings.Helpers;
using ZenTimings.Settings;
using ZenTimings.Windows;
using MessageBox = ZenTimings.Theming.MessageBox;
using MessageBoxButton = ZenTimings.Theming.MessageBoxButton;
using MessageBoxButtons = ZenTimings.Theming.MessageBoxButtons;
using MessageBoxImage = ZenTimings.Theming.MessageBoxImage;
using MessageBoxModel = ZenTimings.Theming.MessageBoxModel;
using MessageBoxResult = ZenTimings.Theming.MessageBoxResult;

namespace ZenTimings
{
    public class Updater
    {
        public event EventHandler UpdateCheckCompleteEvent;

        private static bool manual;
        private static readonly object DisableCheckButtonId = new object();
        private static string ChangelogText { get; set; }
        private static readonly UpdaterPersistenceProvider persistence = new UpdaterPersistenceProvider();

        private const string updateUrl = "https://zentimings.com/Update.xml";
        private const string signatureUrl = "https://zentimings.com/Update.xml.sig";
        private const string betaUpdateUrl = "https://zentimings.com/Update_beta.xml";
        private const string betaSignatureUrl = "https://zentimings.com/Update_beta.xml.sig";

        protected virtual void OnUpdateCheckCompleteEvent(EventArgs e)
        {
            UpdateCheckCompleteEvent?.Invoke(this, e);
        }

        private const string GitHubReleaseBaseUrl = "https://github.com/irusanov/ZenTimings/releases/download";

        private static Version InstalledVersion
        {
            get
            {
                var attr = (AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(
                    Assembly.GetExecutingAssembly(), typeof(AssemblyFileVersionAttribute), false);
                return new Version(attr.Version);
            }
        }

        private static AppSettings.ReleaseChannel Channel => AppSettings.Instance.UpdateReleaseChannel;

        /// <summary>What the update feeds returned.</summary>
        private sealed class FetchResult
        {
            public UpdaterArgs Args;
            public bool IsBeta;
            /// <summary>Set when the beta feed was skipped; shown to the user before the result is used.</summary>
            public string BetaWarning;
        }

        /// <summary>Checks for an update and waits for the answer of the server. Used on startup.</summary>
        public void CheckForUpdate(bool manualUpdate = false, bool suppressNetworkErrorDialog = false)
        {
            if (!manualUpdate) SplashWindow.Loading("Checking for updates...");

            manual = manualUpdate;

            FetchResult result = null;
            Exception error = null;
            try
            {
                result = FetchLatestUpdateInfo();
            }
            catch (Exception ex)
            {
                error = ex;
            }

            CompleteCheck(result, error, suppressNetworkErrorDialog);
        }

        /// <summary>
        /// The manual check. The server is asked on a background thread, so the UI stays responsive while it
        /// answers or times out; the result and the errors are shown the same way as by <see cref="CheckForUpdate"/>.
        /// </summary>
        public async Task CheckForUpdateAsync()
        {
            manual = true;

            FetchResult result = null;
            Exception error = null;
            try
            {
                result = await Task.Run(() => FetchLatestUpdateInfo());
            }
            catch (Exception ex)
            {
                error = ex;
            }

            CompleteCheck(result, error, false);
        }

        // Runs on the UI thread: it shows the dialogs
        private void CompleteCheck(FetchResult result, Exception error, bool suppressNetworkErrorDialog)
        {
            try
            {
                // Rethrown here, so a failed download ends in the same handlers as an error of the steps below
                if (error != null)
                    ExceptionDispatchInfo.Capture(error).Throw();

                if (result.BetaWarning != null)
                {
                    MessageBox.Show(
                        result.BetaWarning,
                        @"Beta Update Check Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                if (result.Args == null)
                {
                    if (manual)
                        OnUpdateCheckCompleteEvent(new EventArgs());
                    return;
                }

                ProcessUpdateInfo(result.Args, result.IsBeta);
            }
            catch (WebException ex)
            {
                Debug.WriteLine($"Update check WebException: {ex.Message}");

                if (!suppressNetworkErrorDialog)
                {
                    string message = "There is a problem reaching the update server. Please check your internet connection and try again later.";
                    ShowCheckFailed(message);
                }
                //else
                //{
                //    SplashWindow.Loading("Update check failed: no connection");
                //}
            }
            catch (CryptographicException ex)
            {
                Debug.WriteLine($"Update check crypto error: {ex.Message}");

                //if (manual)
                {
                    ShowCheckFailed(ex.Message);
                }
                //else
                //{
                //    SplashWindow.Loading("Update check failed: verification error");
                //}
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update check error: {ex.Message}");

                //if (manual)
                {
                    ShowCheckFailed($"An error occurred while checking for updates:\n{ex.Message}");
                }
                //else
                //{
                //    SplashWindow.Loading("Update check failed");
                //}
            }
        }

        /// <summary>
        /// Shows an update check error. After the automatic check on startup the dialog also offers to turn that
        /// check off, e.g. on a PC without a network connection, and saves the setting when the user takes it.
        /// </summary>
        private static void ShowCheckFailed(string message)
        {
            const string caption = "Update Check Failed";
            AppSettings settings = AppSettings.Instance;

            if (manual || !settings.CheckForUpdates)
            {
                MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var model = new MessageBoxModel
            {
                Text = message + "\n\nThe check on startup can be turned off here, and back on in Options.",
                Caption = caption,
                Icon = MessageBoxImage.Error,
                Buttons = new[]
                {
                    MessageBoxButtons.Custom("Don't check on startup", DisableCheckButtonId),
                    MessageBoxButtons.Ok(),
                },
            };

            MessageBox.Show(model);

            if (model.ButtonPressed == null || !ReferenceEquals(model.ButtonPressed.Id, DisableCheckButtonId))
                return;

            settings.CheckForUpdates = false;
            settings.Save();
        }

        // Release checks the stable feed only and Beta the beta feed only. Any checks both and the newer one wins,
        // so those users also get a stable release that is newer than the last beta.
        // May run on a background thread: it shows nothing itself.
        private FetchResult FetchLatestUpdateInfo()
        {
            var result = new FetchResult();
            var channel = Channel;

            if (channel == AppSettings.ReleaseChannel.Beta)
            {
                result.IsBeta = true;
                result.Args = FetchUpdateInfo(betaUpdateUrl, betaSignatureUrl);
                return result;
            }

            bool checkBeta = channel == AppSettings.ReleaseChannel.Any;

            // The feeds are independent, so they are fetched together: the check waits for the slower one, not for both
            Task<UpdaterArgs> betaTask = checkBeta
                ? Task.Run(() => FetchUpdateInfo(betaUpdateUrl, betaSignatureUrl))
                : null;

            Exception stableError = null;
            UpdaterArgs stable = null;

            try
            {
                stable = FetchUpdateInfo(updateUrl, signatureUrl);
            }
            catch (Exception ex) when (checkBeta && !(ex is CryptographicException))
            {
                // Beta may still be available, report the stable error only if that fails as well
                stableError = ex;
            }

            result.Args = stable;

            if (!checkBeta)
                return result;

            UpdaterArgs beta = null;
            try
            {
                // GetResult rethrows the original exception, not an AggregateException
                beta = betaTask.GetAwaiter().GetResult();
            }
            catch (CryptographicException ex)
            {
                if (stable == null)
                    throw;

                result.BetaWarning = ex.Message + Environment.NewLine + Environment.NewLine +
                    "The beta channel was skipped; checking the stable channel only.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Beta update check failed: {ex.Message}");
                if (stableError != null)
                    throw stableError;
            }

            if (beta != null && (stable == null || new Version(beta.Version) > new Version(stable.Version)))
            {
                result.IsBeta = true;
                result.Args = beta;
            }

            return result;
        }

        private UpdaterArgs FetchUpdateInfo(string xmlUrl, string sigUrl)
        {
            // After upgrading to .NET 4.7.2, the best supported TLS version is automatically negotiated, so we don't need to force TLS 1.2 anymore.
            //ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            // A WebClient runs one request at a time, so the signature gets its own. It is fetched on a pool thread:
            // an async WebClient call started here would complete on the UI thread, which is waiting for it.
            using (var client = new TimeoutWebClient())
            using (var signatureClient = new TimeoutWebClient())
            {
                Task<byte[]> signatureTask = Task.Run(() => signatureClient.DownloadData(sigUrl));
                byte[] xmlData = client.DownloadData(xmlUrl);
                byte[] signature = signatureTask.GetAwaiter().GetResult();

                if (!UpdaterSignature.Verify(xmlData, signature))
                {
                    throw new CryptographicException(
                        "Update metadata signature verification failed. " +
                        "The update XML may have been tampered with. " +
                        Environment.NewLine +
                        "The update has been cancelled for security reasons.");
                }

                string xml = Encoding.UTF8.GetString(xmlData);
                using (var reader = new StringReader(xml))
                {
                    var serializer = new XmlSerializer(typeof(UpdaterArgs));
                    return serializer.Deserialize(reader) as UpdaterArgs;
                }
            }
        }

        private void ProcessUpdateInfo(UpdaterArgs updaterArgs, bool isBeta)
        {
            var remoteVersion = new Version(updaterArgs.Version);
            var installedVersion = InstalledVersion;

            ChangelogText = $"\nChangelog{Environment.NewLine}";
            if (updaterArgs.Changes != null)
            {
                foreach (string change in updaterArgs.Changes)
                    ChangelogText += $" - {change}{Environment.NewLine}";
            }

            bool isUpdateAvailable = remoteVersion > installedVersion;

            if (isUpdateAvailable && (manual || !persistence.GetSkippedVersion().Equals(remoteVersion)))
            {
                var shortVersion = string.Join(".", updaterArgs.Version.Split('.'), 0, 2);
                var zipSuffix = isBeta ? "_beta" : string.Empty;
                var zipFileName = $"ZenTimings_v{shortVersion}{zipSuffix}.zip";
                var downloadUrl = $"{GitHubReleaseBaseUrl}/v{shortVersion}/{zipFileName}";
                var checksumUrl = $"{GitHubReleaseBaseUrl}/v{shortVersion}/{zipFileName}.sha256";
                var zipSignatureUrl = $"{GitHubReleaseBaseUrl}/v{shortVersion}/{zipFileName}.sig";

                var dialog = new UpdateAvailableDialog(updaterArgs.Version, installedVersion, updaterArgs.Changes, !manual, isBeta);
                if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsLoaded && Application.Current.MainWindow != dialog)
                {
                    dialog.Owner = Application.Current.MainWindow;
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                bool accepted = dialog.ShowDialog() == true;

                if (!manual) SplashWindow.HideIfOpen();

                if (accepted)
                {
                    try
                    {
                        if (DownloadAndApplyUpdate(downloadUrl, checksumUrl, zipSignatureUrl, installedVersion))
                        {
                            if (!manual) SplashWindow.Stop();
                            Application.Current.Shutdown();
                            Environment.Exit(0);
                        }
                    }
                    catch (Exception exception)
                    {
                        MessageBox.Show(
                            exception.Message,
                            exception.GetType().ToString(),
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else if (!manual)
                {
                    if (dialog.DontAskAgain)
                    {
                        persistence.SetSkippedVersion(remoteVersion);
                    }
                    SplashWindow.ShowIfOpen();
                }
            }
            else if (manual)
            {
                OnUpdateCheckCompleteEvent(new EventArgs());
            }
        }

        private bool DownloadAndApplyUpdate(string downloadUrl, string checksumUrl, string zipSignatureUrl, Version installedVersion)
        {
            var progressWindow = new UpdateProgressWindow();
            progressWindow.Show();

            // The download waits in a message loop, so the main window would take input in the middle of the update
            Window owner = Application.Current?.MainWindow;
            bool lockOwner = owner != null && owner.IsLoaded && owner.IsEnabled;
            if (lockOwner)
                owner.IsEnabled = false;

            try
            {
                return DownloadAndApplyUpdateCore(downloadUrl, checksumUrl, zipSignatureUrl, installedVersion, progressWindow);
            }
            catch
            {
                progressWindow.CloseFromUpdater();
                throw;
            }
            finally
            {
                if (lockOwner)
                    owner.IsEnabled = true;
            }
        }

        private static void ShowUpdateFailed(UpdateProgressWindow progressWindow, string message)
        {
            progressWindow.CloseFromUpdater();
            MessageBox.Show(
                message,
                "Update Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private bool DownloadAndApplyUpdateCore(string downloadUrl, string checksumUrl, string zipSignatureUrl,
            Version installedVersion, UpdateProgressWindow progressWindow)
        {
            string expectedHash;
            byte[] zipSignature;
            byte[] zipData;

            using (var client = new TimeoutWebClient())
            {
                // Download checksum and signature first (small files, fail early)
                progressWindow.SetIndeterminate("Downloading verification files...");

                try
                {
                    // Checksum file format: "<hex_hash>" or "<hex_hash>  <filename>"
                    expectedHash = client.DownloadString(checksumUrl).Trim().Split(' ')[0].Trim();
                }
                catch (WebException)
                {
                    ShowUpdateFailed(progressWindow,
                        "Could not download the checksum file.\n" +
                        "The update has been cancelled for security reasons.");
                    return false;
                }

                if (progressWindow.IsCancelled)
                    return false;

                try
                {
                    zipSignature = client.DownloadData(zipSignatureUrl);
                }
                catch (WebException)
                {
                    ShowUpdateFailed(progressWindow,
                        "Could not download the signature file.\n" +
                        "The update has been cancelled for security reasons.");
                    return false;
                }

                if (progressWindow.IsCancelled)
                    return false;

                // Download the update zip into memory with progress reporting.
                // The same bytes are verified and extracted, so nothing can be
                // swapped on disk between verification and use.
                progressWindow.SetStatus("Downloading update...");
                progressWindow.SetProgress(0, "Connecting...");

                var downloadFrame = new DispatcherFrame();
                Exception downloadError = null;
                bool downloadCancelled = false;
                byte[] downloadedData = null;

                client.DownloadProgressChanged += (sender, e) =>
                {
                    if (progressWindow.IsCancelled)
                    {
                        client.CancelAsync();
                        return;
                    }

                    string detail = $"{e.BytesReceived / 1024:N0} KB";
                    if (e.TotalBytesToReceive > 0)
                        detail += $" / {e.TotalBytesToReceive / 1024:N0} KB";

                    progressWindow.SetProgress(e.ProgressPercentage, detail);
                };

                client.DownloadDataCompleted += (sender, e) =>
                {
                    if (e.Cancelled)
                        downloadCancelled = true;
                    else if (e.Error != null)
                        downloadError = e.Error;
                    else
                        downloadedData = e.Result;
                    downloadFrame.Continue = false;
                };

                // Cancelling is also checked on a timer, as the progress events stop when the connection stalls
                bool cancelRequested = false;
                var cancelTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                cancelTimer.Tick += (sender, e) =>
                {
                    if (progressWindow.IsCancelled && !cancelRequested)
                    {
                        cancelRequested = true;
                        client.CancelAsync();
                    }
                };
                cancelTimer.Start();

                client.DownloadDataAsync(new Uri(downloadUrl));

                // A message loop until the download completes; it ends the frame
                Dispatcher.PushFrame(downloadFrame);
                cancelTimer.Stop();

                if (progressWindow.IsCancelled || downloadCancelled)
                {
                    progressWindow.CloseFromUpdater();
                    return false;
                }

                if (downloadError != null || downloadedData == null)
                {
                    ShowUpdateFailed(progressWindow,
                        $"Failed to download the update:\n{downloadError?.Message ?? "No data received."}");
                    return false;
                }

                zipData = downloadedData;
            }

            // Verify checksum
            progressWindow.SetIndeterminate("Verifying checksum...");

            string actualHash = ComputeHash(zipData, "SHA256");
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                ShowUpdateFailed(progressWindow,
                    "Update verification failed. The downloaded file checksum does not match the expected value.\n" +
                    "The update has been cancelled for security reasons.");
                return false;
            }

            // Verify RSA signature
            progressWindow.SetIndeterminate("Verifying digital signature...");

            if (!UpdaterSignature.Verify(zipData, zipSignature))
            {
                ShowUpdateFailed(progressWindow,
                    "Update signature verification failed. The downloaded file may have been tampered with.\n" +
                    "The update has been cancelled for security reasons.");
                return false;
            }

            // Extract the verified bytes into a fresh directory that only
            // Administrators/SYSTEM can modify (the update script runs elevated).
            progressWindow.SetIndeterminate("Extracting update...");

            string workDir = SecureDirectoryHelper.CreateAdminOnlyDirectory("Update");
            string extractDir = Path.Combine(workDir, "extracted");

            try
            {
                Directory.CreateDirectory(extractDir);
                SecureDirectoryHelper.ExtractZip(zipData, extractDir);
            }
            catch
            {
                CleanupTempDir(workDir);
                throw;
            }

            // Find the actual content directory (zip may contain a single root folder)
            string sourceDir = extractDir;
            string[] subDirs = Directory.GetDirectories(extractDir);
            if (subDirs.Length == 1 && Directory.GetFiles(extractDir).Length == 0)
            {
                sourceDir = subDirs[0];
            }

            // Anti-downgrade: verify the extracted exe is actually newer than what's installed
            progressWindow.SetIndeterminate("Verifying update...");

            string extractedExe = Path.Combine(sourceDir, "ZenTimings.exe");
            if (!File.Exists(extractedExe))
            {
                CleanupTempDir(workDir);
                ShowUpdateFailed(progressWindow,
                    "The downloaded update does not contain ZenTimings.exe.\n" +
                    "The update has been cancelled.");
                return false;
            }

            // An unreadable version counts as not newer (and must not leak the work directory via an exception).
            Version extractedVersion;
            if (!Version.TryParse(FileVersionInfo.GetVersionInfo(extractedExe).FileVersion, out extractedVersion) ||
                extractedVersion <= installedVersion)
            {
                CleanupTempDir(workDir);
                ShowUpdateFailed(progressWindow,
                    $"The downloaded version ({extractedVersion}) is not newer than the installed version ({installedVersion}).\n" +
                    "The update has been cancelled for security reasons.");
                return false;
            }

            // Create and run update batch script (in the same protected directory)
            progressWindow.SetIndeterminate("Applying update...");

            string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            int pid = Process.GetCurrentProcess().Id;
            string batchPath = Path.Combine(workDir, "update.cmd");

            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine(":wait");
            sb.AppendLine($"tasklist /fi \"PID eq {pid}\" 2>NUL | find /I \"ZenTimings\" >NUL");
            sb.AppendLine("if %errorlevel%==0 (");
            sb.AppendLine("    timeout /t 1 /nobreak >NUL");
            sb.AppendLine("    goto wait");
            sb.AppendLine(")");
            sb.AppendLine($"xcopy /s /y /q \"{sourceDir}\\*\" \"{appDir}\\\"");
            // A failed copy keeps the downloaded files (and says so) instead of deleting them
            sb.AppendLine("if errorlevel 1 (");
            sb.AppendLine($"    echo xcopy failed with exit code %errorlevel% > \"{Path.Combine(workDir, "update_failed.txt")}\"");
            sb.AppendLine($"    msg * /time:60 \"ZenTimings could not be updated, copying the files failed. The downloaded update was kept in {workDir}\" 2>NUL");
            sb.AppendLine($"    start \"\" \"{Path.Combine(appDir, "ZenTimings.exe")}\"");
            sb.AppendLine("    exit /b 1");
            sb.AppendLine(")");
            sb.AppendLine($"start \"\" \"{Path.Combine(appDir, "ZenTimings.exe")}\"");
            sb.AppendLine($"rmdir /s /q \"{workDir}\"");

            try
            {
                File.WriteAllText(batchPath, sb.ToString());

                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                    Arguments = $"/c \"{batchPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                });
            }
            catch
            {
                CleanupTempDir(workDir);
                throw;
            }

            progressWindow.CloseFromUpdater();
            return true;
        }

        private static void CleanupTempDir(string tempDir)
        {
            SecureDirectoryHelper.TryDelete(tempDir);
        }

        private static string ComputeHash(byte[] data, string algorithm)
        {
            using (var ha = HashAlgorithm.Create(algorithm))
            {
                if (ha == null)
                    throw new NotSupportedException($"Hash algorithm '{algorithm}' is not supported.");

                byte[] hash = ha.ComputeHash(data);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>
        /// WebClient with a bounded timeout for synchronous requests, so an
        /// unreachable update server does not block the UI for the default ~100 s.
        /// </summary>
        private sealed class TimeoutWebClient : WebClient
        {
            private const int DefaultTimeoutMs = 15000;

            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = base.GetWebRequest(address);
                if (request != null)
                {
                    request.Timeout = DefaultTimeoutMs;
                    if (request is HttpWebRequest httpRequest)
                        httpRequest.ReadWriteTimeout = DefaultTimeoutMs;
                }
                return request;
            }
        }
    }
}