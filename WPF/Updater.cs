using AdonisUI.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Xml.Serialization;
using ZenTimings.Encryption;
using ZenTimings.Windows;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;
using MessageBoxResult = AdonisUI.Controls.MessageBoxResult;

namespace ZenTimings
{
    public class Updater
    {
        public event EventHandler UpdateCheckCompleteEvent;

        private static bool manual;
        private static string ChangelogText { get; set; }
        private static readonly UpdaterPersistenceProvider persistence = new UpdaterPersistenceProvider();

#if DEBUG
        private const string updateUrl = "https://zentimings.com/Update_debug.xml";
        private const string signatureUrl = "https://zentimings.com/Update_debug.xml.sig";
#else
        private const string updateUrl = "https://zentimings.com/Update.xml";
        private const string signatureUrl = "https://zentimings.com/Update.xml.sig";
#endif

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

        public void CheckForUpdate(bool manualUpdate = false)
        {
            if (!manualUpdate) SplashWindow.Loading("Checking for updates...");

            manual = manualUpdate;

            try
            {
                UpdaterArgs updaterArgs = FetchUpdateInfo();
                if (updaterArgs == null)
                {
                    if (manual)
                        OnUpdateCheckCompleteEvent(new EventArgs());
                    return;
                }

                ProcessUpdateInfo(updaterArgs);
            }
            catch (WebException ex)
            {
                string message = "There is a problem reaching the update server. Please check your internet connection and try again later.";
                Debug.WriteLine($"Update check WebException: {ex.Message}");

                //if (manual)
                {
                    MessageBox.Show(message, @"Update Check Failed",
                        MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show(ex.Message, @"Update Check Failed",
                        MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show(
                        $"An error occurred while checking for updates:\n{ex.Message}",
                        @"Update Check Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                //else
                //{
                //    SplashWindow.Loading("Update check failed");
                //}
            }
        }

        private UpdaterArgs FetchUpdateInfo()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (var client = new WebClient())
            {
                byte[] xmlData = client.DownloadData(updateUrl);
                byte[] signature = client.DownloadData(signatureUrl);

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

        private void ProcessUpdateInfo(UpdaterArgs updaterArgs)
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
                var zipFileName = $"ZenTimings_v{shortVersion}.zip";
                var downloadUrl = $"{GitHubReleaseBaseUrl}/v{shortVersion}/{zipFileName}";
                var checksumUrl = $"{GitHubReleaseBaseUrl}/v{shortVersion}/{zipFileName}.sha256";
                var zipSignatureUrl = $"{GitHubReleaseBaseUrl}/v{shortVersion}/{zipFileName}.sig";

                var messageBox = new MessageBoxModel
                {
                    Text = $"There is new version {updaterArgs.Version} available.{Environment.NewLine}" +
                           $"You are using version {installedVersion}.{Environment.NewLine}" +
                           $"{ChangelogText}{Environment.NewLine}" +
                           "Do you want to update the application now?",
                    Caption = @"Update Available",
                    Buttons = MessageBoxButtons.YesNo(yesLabel: "Update", noLabel: "Skip"),
                };

                if (!manual)
                {
                    messageBox.CheckBoxes = new[]
                    {
                        new MessageBoxCheckBoxModel("Don't ask for this update again")
                        {
                            IsChecked = false,
                            Placement = MessageBoxCheckBoxPlacement.BelowText,
                        },
                    };
                }

                MessageBox.Show(messageBox);

                if (!manual) SplashWindow.splash.Hide();

                if (messageBox.Result.Equals(MessageBoxResult.Yes))
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
                    var enumerator = messageBox.CheckBoxes.GetEnumerator();
                    enumerator.MoveNext();
                    if (enumerator.Current.IsChecked)
                    {
                        persistence.SetSkippedVersion(remoteVersion);
                    }
                    SplashWindow.splash.Show();
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

            try
            {
                return DownloadAndApplyUpdateCore(downloadUrl, checksumUrl, zipSignatureUrl, installedVersion, progressWindow);
            }
            catch
            {
                if (!progressWindow.IsCancelled)
                {
                    try { progressWindow.Close(); }
                    catch { }
                }
                throw;
            }
        }

        private bool DownloadAndApplyUpdateCore(string downloadUrl, string checksumUrl, string zipSignatureUrl,
            Version installedVersion, UpdateProgressWindow progressWindow)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ZenTimings_Update");
            string zipPath = Path.Combine(tempDir, "update.zip");
            string extractDir = Path.Combine(tempDir, "extracted");

            // Clean up any previous update attempt
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);

            Directory.CreateDirectory(tempDir);

            using (var client = new WebClient())
            {
                // Download checksum and signature first (small files, fail early)
                progressWindow.SetIndeterminate("Downloading verification files...");

                string expectedHash;
                try
                {
                    // Checksum file format: "<hex_hash>" or "<hex_hash>  <filename>"
                    expectedHash = client.DownloadString(checksumUrl).Trim().Split(' ')[0].Trim();
                }
                catch (WebException)
                {
                    CleanupTempDir(tempDir);
                    progressWindow.Close();
                    MessageBox.Show(
                        "Could not download the checksum file.\n" +
                        "The update has been cancelled for security reasons.",
                        "Update Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }

                if (progressWindow.IsCancelled)
                {
                    CleanupTempDir(tempDir);
                    return false;
                }

                byte[] zipSignature;
                try
                {
                    zipSignature = client.DownloadData(zipSignatureUrl);
                }
                catch (WebException)
                {
                    CleanupTempDir(tempDir);
                    progressWindow.Close();
                    MessageBox.Show(
                        "Could not download the signature file.\n" +
                        "The update has been cancelled for security reasons.",
                        "Update Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }

                if (progressWindow.IsCancelled)
                {
                    CleanupTempDir(tempDir);
                    return false;
                }

                // Download the update zip with progress reporting
                progressWindow.SetStatus("Downloading update...");
                progressWindow.SetProgress(0, "Connecting...");

                var downloadComplete = new ManualResetEvent(false);
                Exception downloadError = null;

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

                client.DownloadFileCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                        downloadError = e.Error;
                    downloadComplete.Set();
                };

                client.DownloadFileAsync(new Uri(downloadUrl), zipPath);

                // Pump WPF messages while waiting for download to complete
                while (!downloadComplete.WaitOne(50))
                {
                    Application.Current.Dispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new Action(delegate { }));
                }

                if (progressWindow.IsCancelled)
                {
                    CleanupTempDir(tempDir);
                    return false;
                }

                if (downloadError != null)
                {
                    CleanupTempDir(tempDir);
                    progressWindow.Close();
                    MessageBox.Show(
                        $"Failed to download the update:\n{downloadError.Message}",
                        "Update Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }

                // Verify checksum
                progressWindow.SetIndeterminate("Verifying checksum...");

                string actualHash = ComputeFileHash(zipPath, "SHA256");
                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    CleanupTempDir(tempDir);
                    progressWindow.Close();
                    MessageBox.Show(
                        "Update verification failed. The downloaded file checksum does not match the expected value.\n" +
                        "The update has been cancelled for security reasons.",
                        "Update Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }

                // Verify RSA signature
                progressWindow.SetIndeterminate("Verifying digital signature...");

                byte[] zipData = File.ReadAllBytes(zipPath);
                if (!UpdaterSignature.Verify(zipData, zipSignature))
                {
                    CleanupTempDir(tempDir);
                    progressWindow.Close();
                    MessageBox.Show(
                        "Update signature verification failed. The downloaded file may have been tampered with.\n" +
                        "The update has been cancelled for security reasons.",
                        "Update Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }
            }

            // Extract zip
            progressWindow.SetIndeterminate("Extracting update...");

            Directory.CreateDirectory(extractDir);
            ZipFile.ExtractToDirectory(zipPath, extractDir);

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
                CleanupTempDir(tempDir);
                progressWindow.Close();
                MessageBox.Show(
                    "The downloaded update does not contain ZenTimings.exe.\n" +
                    "The update has been cancelled.",
                    "Update Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            var extractedVersion = new Version(FileVersionInfo.GetVersionInfo(extractedExe).FileVersion);
            if (extractedVersion <= installedVersion)
            {
                CleanupTempDir(tempDir);
                progressWindow.Close();
                MessageBox.Show(
                    $"The downloaded version ({extractedVersion}) is not newer than the installed version ({installedVersion}).\n" +
                    "The update has been cancelled for security reasons.",
                    "Update Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            // Create and run update batch script
            progressWindow.SetIndeterminate("Applying update...");

            string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            int pid = Process.GetCurrentProcess().Id;
            string batchPath = Path.Combine(tempDir, "update.cmd");

            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine(":wait");
            sb.AppendLine($"tasklist /fi \"PID eq {pid}\" 2>NUL | find /I \"ZenTimings\" >NUL");
            sb.AppendLine("if %errorlevel%==0 (");
            sb.AppendLine("    timeout /t 1 /nobreak >NUL");
            sb.AppendLine("    goto wait");
            sb.AppendLine(")");
            sb.AppendLine($"xcopy /s /y /q \"{sourceDir}\\*\" \"{appDir}\\\"");
            sb.AppendLine($"start \"\" \"{Path.Combine(appDir, "ZenTimings.exe")}\"");
            sb.AppendLine($"rmdir /s /q \"{tempDir}\"");

            File.WriteAllText(batchPath, sb.ToString());

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batchPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            });

            progressWindow.Close();
            return true;
        }

        private static void CleanupTempDir(string tempDir)
        {
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            catch { }
        }

        private static string ComputeFileHash(string filePath, string algorithm)
        {
            using (var ha = HashAlgorithm.Create(algorithm))
            {
                if (ha == null)
                    throw new NotSupportedException($"Hash algorithm '{algorithm}' is not supported.");

                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = ha.ComputeHash(stream);
                    var sb = new StringBuilder(hash.Length * 2);
                    foreach (byte b in hash)
                        sb.Append(b.ToString("x2"));
                    return sb.ToString();
                }
            }
        }
    }
}