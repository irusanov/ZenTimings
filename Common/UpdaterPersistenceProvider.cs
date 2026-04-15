using System;

namespace ZenTimings
{
    public sealed class UpdaterPersistenceProvider
    {
        internal readonly AppSettings appSettings = AppSettings.Instance;
        public UpdaterPersistenceProvider() { }

        /// <summary>
        /// Reads the flag indicating whether a specific version should be skipped or not.
        /// </summary>
        public Version GetSkippedVersion()
        {
            if (appSettings != null && !string.IsNullOrEmpty(appSettings.UpdaterSkippedVersion))
            {
                return new Version(appSettings.UpdaterSkippedVersion);
            }

            return new Version(0, 0, 0);
        }

        /// <summary>
        /// Sets the values indicating the specific version that must be ignored.
        /// </summary>
        public void SetSkippedVersion(Version version)
        {
            if (appSettings != null && version != null)
            {
                appSettings.UpdaterSkippedVersion = version.ToString();
                appSettings.Save();
            }
        }
    }
}