using System;
using TaleWorlds.Library;

namespace BannerlordExpanded.WandererCreator.VersionCompatibility
{
    /// <summary>
    /// Handles game version detection and compatibility checks.
    /// </summary>
    public static class VersionCheck
    {
        /// <summary>
        /// Minimum game version the mod was developed for.
        /// </summary>
        public static readonly ApplicationVersion MinSupportedVersion = new ApplicationVersion(
            ApplicationVersionType.Release, 1, 3, 15, 0);

        /// <summary>
        /// Maximum game version the mod has been tested with.
        /// </summary>
        public static readonly ApplicationVersion MaxTestedVersion = new ApplicationVersion(
            ApplicationVersionType.Release, 1, 3, 15, 0);

        /// <summary>
        /// Current game version.
        /// </summary>
        public static ApplicationVersion GameVersion => ApplicationVersion.FromParametersFile();

        /// <summary>
        /// Whether the current game version is within the tested range.
        /// </summary>
        public static bool IsVersionTested
        {
            get
            {
                try
                {
                    var current = GameVersion;
                    return current >= MinSupportedVersion && current <= MaxTestedVersion;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Whether the current game version meets minimum requirements.
        /// </summary>
        public static bool IsVersionSupported
        {
            get
            {
                try
                {
                    return GameVersion >= MinSupportedVersion;
                }
                catch
                {
                    return true; // Assume supported if we can't detect
                }
            }
        }

        /// <summary>
        /// Gets a formatted string describing the current version status.
        /// </summary>
        public static string GetVersionStatusMessage()
        {
            try
            {
                var current = GameVersion;

                if (!IsVersionSupported)
                {
                    return $"WARNING: Game version {current} is below minimum supported version {MinSupportedVersion}. " +
                           "The mod may not work correctly.";
                }

                if (!IsVersionTested)
                {
                    return $"NOTE: Game version {current} is newer than the tested version {MaxTestedVersion}. " +
                           "The mod should work but some features may behave unexpectedly.";
                }

                return $"Game version {current} is supported.";
            }
            catch (Exception ex)
            {
                return $"Could not determine game version compatibility: {ex.Message}";
            }
        }

        /// <summary>
        /// Logs the version compatibility status.
        /// </summary>
        public static void LogVersionStatus()
        {
            FileLogger.Log($"[VersionCompatibility] {GetVersionStatusMessage()}");
        }
    }
}
