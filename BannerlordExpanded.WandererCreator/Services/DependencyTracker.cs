using System;
using System.Collections.Generic;
using System.Linq;
using BannerlordExpanded.WandererCreator.Models;
using BannerlordExpanded.WandererCreator.VersionCompatibility;

namespace BannerlordExpanded.WandererCreator.Services
{
    /// <summary>
    /// Service for scanning a WandererProject and detecting third-party mod dependencies.
    /// </summary>
    public static class DependencyTracker
    {
        /// <summary>
        /// Scans a WandererProject and identifies all third-party mod dependencies
        /// based on the equipment items used in templates.
        /// </summary>
        public static List<ModDependencyInfo> ScanProject(WandererProject project)
        {
            var dependencies = new Dictionary<string, ModDependencyInfo>(StringComparer.OrdinalIgnoreCase);

            if (project == null) return new List<ModDependencyInfo>();

            // Scan all shared equipment templates
            foreach (var template in project.SharedTemplates)
            {
                ScanEquipmentTemplate(template, dependencies);
            }

            return dependencies.Values.ToList();
        }

        /// <summary>
        /// Scans a single equipment template for non-core module items.
        /// </summary>
        private static void ScanEquipmentTemplate(EquipmentTemplate template, Dictionary<string, ModDependencyInfo> dependencies)
        {
            if (template?.Items == null) return;

            foreach (var kvp in template.Items)
            {
                string itemId = kvp.Value;
                if (string.IsNullOrEmpty(itemId)) continue;

                // Get the source module for this item
                if (GameApiWrapper.TryGetItemSourceModule(itemId, out string moduleId))
                {
                    // Skip core modules
                    if (GameApiWrapper.IsCoreModule(moduleId)) continue;

                    // Add to or update dependencies
                    if (!dependencies.ContainsKey(moduleId))
                    {
                        var moduleInfo = GameApiWrapper.GetModuleInfo(moduleId);
                        string version = "";

                        // Get the version from ModuleInfo
                        if (moduleInfo != null)
                        {
                            try
                            {
                                version = moduleInfo.Version.ToString();
                            }
                            catch { /* Version not available */ }
                        }

                        dependencies[moduleId] = new ModDependencyInfo
                        {
                            ModuleId = moduleId,
                            ModuleName = moduleInfo?.Name ?? moduleId,
                            Version = version,
                            IsOptional = false,
                            UsedItems = new List<string>()
                        };
                    }

                    // Track which items are from this module
                    if (!dependencies[moduleId].UsedItems.Contains(itemId))
                    {
                        dependencies[moduleId].UsedItems.Add(itemId);
                    }
                }
            }
        }

        /// <summary>
        /// Updates a project's DetectedDependencies by scanning all templates.
        /// </summary>
        public static void UpdateProjectDependencies(WandererProject project)
        {
            if (project == null) return;
            project.DetectedDependencies = ScanProject(project);
        }
    }
}
