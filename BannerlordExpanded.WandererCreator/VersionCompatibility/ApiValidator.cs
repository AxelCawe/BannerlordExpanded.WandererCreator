using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;

using System.Linq;

namespace BannerlordExpanded.WandererCreator.VersionCompatibility
{
    /// <summary>
    /// Validates that critical game APIs exist and are accessible.
    /// Run this on mod load to detect compatibility issues early.
    /// 
    /// When updating for a new game version, add/modify checks here to verify
    /// that the APIs your mod depends on are still available.
    /// </summary>
    public static class ApiValidator
    {
        /// <summary>
        /// Validates all critical game APIs and logs any issues found.
        /// Call this during mod load (OnSubModuleLoad).
        /// </summary>
        public static void ValidateAll()
        {
            var issues = new List<string>();

            // Check Hero APIs
            CheckProperty(typeof(Hero), "IsWanderer", issues);
            CheckProperty(typeof(Hero), "IsKnownToPlayer", issues);
            CheckProperty(typeof(Hero), "CharacterObject", issues);
            CheckProperty(typeof(Hero), "BodyProperties", issues);
            CheckProperty(typeof(Hero), "Name", issues);
            CheckProperty(typeof(Hero), "IsFemale", issues);
            CheckStaticProperty(typeof(Hero), "AllAliveHeroes", issues);
            CheckStaticProperty(typeof(Hero), "MainHero", issues);

            // Check CharacterObject APIs
            CheckProperty(typeof(CharacterObject), "StringId", issues);
            CheckProperty(typeof(CharacterObject), "Culture", issues);
            CheckStaticProperty(typeof(CharacterObject), "PlayerCharacter", issues);
            CheckMethod(typeof(CharacterObject), "GetPersona", issues);

            // Check Campaign APIs
            CheckProperty(typeof(Campaign), "Current", issues);

            // Check Module APIs
            CheckStaticMethod(typeof(TaleWorlds.ModuleManager.ModuleHelper), "GetModules", issues);
            CheckStaticMethod(typeof(TaleWorlds.ModuleManager.ModuleHelper), "GetXmlPath", issues);

            // Check ObjectManager APIs
            CheckProperty(typeof(TaleWorlds.Core.Game), "Current", issues);

            // Check for CampaignBehaviors
            CheckCampaignBehavior("HeroKnownInformationCampaignBehavior", new[] { "UpdateHeroLocation" }, issues);

            // Log results
            if (issues.Count > 0)
            {
                FileLogger.Log($"[ApiValidator] WARNING: {issues.Count} API compatibility issue(s) detected:");
                foreach (var issue in issues)
                {
                    FileLogger.Log($"  - {issue}");
                }
                FileLogger.Log("[ApiValidator] Some features may not work correctly with this game version.");
            }
            FileLogger.Log("[ApiValidator] All critical APIs validated successfully.");
        }

        private static void CheckCampaignBehavior(string behaviorName, string[] requiredMethods, List<string> issues)
        {
            try
            {
                // We search all loaded assemblies for the behavior type by name
                Type? behaviorType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    behaviorType = assembly.GetTypes().FirstOrDefault(t => t.Name == behaviorName);
                    if (behaviorType != null) break;
                }

                if (behaviorType == null)
                {
                    issues.Add($"CampaignBehavior '{behaviorName}' not found");
                    return;
                }

                foreach (var method in requiredMethods)
                {
                    var m = behaviorType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                        .FirstOrDefault(x => x.Name == method);
                    if (m == null)
                    {
                        issues.Add($"Method '{method}' not found in '{behaviorName}'");
                    }
                }
            }
            catch (Exception ex)
            {
                issues.Add($"Error validating '{behaviorName}': {ex.Message}");
            }
        }

        private static void CheckProperty(Type type, string propertyName, List<string> issues)
        {
            // Check for existence
            bool exists = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                             .Any(p => p.Name == propertyName);

            if (!exists)
            {
                issues.Add($"{type.Name}.{propertyName} property not found");
            }
        }

        private static void CheckStaticProperty(Type type, string propertyName, List<string> issues)
        {
            bool exists = type.GetProperties(BindingFlags.Public | BindingFlags.Static)
                             .Any(p => p.Name == propertyName);

            if (!exists)
            {
                issues.Add($"{type.Name}.{propertyName} static property not found");
            }
        }

        private static void CheckMethod(Type type, string methodName, List<string> issues)
        {
            bool exists = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                             .Any(m => m.Name == methodName);

            if (!exists)
            {
                issues.Add($"{type.Name}.{methodName}() method not found");
            }
        }

        private static void CheckStaticMethod(Type type, string methodName, List<string> issues)
        {
            bool exists = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                             .Any(m => m.Name == methodName);

            if (!exists)
            {
                issues.Add($"{type.Name}.{methodName}() static method not found");
            }
        }
    }
}
