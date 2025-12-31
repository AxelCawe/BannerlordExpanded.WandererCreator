using System;
using System.Collections.Generic;

namespace BannerlordExpanded.WandererCreator.Models
{
    /// <summary>
    /// Represents a detected mod dependency from third-party modules.
    /// Used to track which external mods the project depends on.
    /// </summary>
    [Serializable]
    public class ModDependencyInfo
    {
        /// <summary>
        /// The module ID (e.g., "MyArmorMod").
        /// </summary>
        public string ModuleId { get; set; } = "";

        /// <summary>
        /// The display name of the module.
        /// </summary>
        public string ModuleName { get; set; } = "";

        /// <summary>
        /// The version of the module (e.g., "v1.0.0").
        /// </summary>
        public string Version { get; set; } = "";

        /// <summary>
        /// Whether this dependency is optional.
        /// </summary>
        public bool IsOptional { get; set; } = false;

        /// <summary>
        /// List of item StringIds from this module that are used in the project.
        /// </summary>
        public List<string> UsedItems { get; set; } = new List<string>();

        public override string ToString()
        {
            string versionPart = !string.IsNullOrEmpty(Version) ? $" {Version}" : "";
            return $"{ModuleName} ({ModuleId}{versionPart}) - {UsedItems.Count} items";
        }
    }
}
