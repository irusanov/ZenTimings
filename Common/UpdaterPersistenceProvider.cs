﻿using System;
using System.Reflection;
using AutoUpdaterDotNET;

namespace ZenTimings
{
    public sealed class UpdaterPersistenceProvider : IPersistenceProvider
    {
        internal AppSettings appSettings;

        public UpdaterPersistenceProvider(AppSettings settings)
        {
            appSettings = settings;
        }

        /// <summary>
        /// Reads the flag indicating whether a specific version should be skipped or not.
        /// </summary>
        /// <returns>Returns a version to skip. If skip value is false or not present then it will return null.</returns>
        public Version GetSkippedVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version;
        }

        /// <summary>
        /// Reads the value containing the date and time at which the user must be given again the possibility to upgrade the application.
        /// </summary>
        /// <returns>Returns a DateTime value at which the user must be given again the possibility to upgrade the application. If remind later value is not present then it will return null.</returns>
        public DateTime? GetRemindLater()
        {
            if (appSettings?.UpdaterRemindLaterAt != null)
            {
                DateTime.TryParse(appSettings?.UpdaterRemindLaterAt, out DateTime result);
                return result;
            }

            return null;
        }

        /// <summary>
        /// Sets the values indicating the specific version that must be ignored by AutoUpdater.
        /// </summary>
        /// <param name="version">Version code for the specific version that must be ignored. Set it to null if you don't want to skip any version.</param>
        public void SetSkippedVersion(Version version)
        {
            if (appSettings != null && version != null)
            {
                appSettings.UpdaterSkippedVersion = version.ToString();
                appSettings.Save();
            }
        }

        /// <summary>
        /// Sets the date and time at which the user must be given again the possibility to upgrade the application.
        /// </summary>
        /// <param name="remindLaterAt">Date and time at which the user must be given again the possibility to upgrade the application.</param>
        public void SetRemindLater(DateTime? remindLaterAt)
        {
            if (appSettings != null && remindLaterAt != null)
            {
                appSettings.UpdaterRemindLaterAt = remindLaterAt.ToString();
                appSettings.Save();
            }
        }
    }
}