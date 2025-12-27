using System;
using BannerlordExpanded.WandererCreator.Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace BannerlordExpanded.WandererCreator.VersionCompatibility
{
    /// <summary>
    /// Abstraction layer for game API access with fallbacks and graceful degradation.
    /// Centralizes all version-sensitive game API calls for easier maintenance.
    /// </summary>
    public static class GameApiWrapper
    {
        // Default equipment template ID if Wanderer has no templates set.
        public const string DefaultEquipmentTemplateId = "npc_companion_equipment_template_empire";

        #region Hero Operations

        /// <summary>
        /// Sets the CharacterObject for a Hero using reflection (private method).
        /// </summary>
        public static bool TrySetCharacterObject(Hero hero, CharacterObject character)
        {
            if (hero == null || character == null) return false;

            return ReflectionHelper.TryInvokeMethod(
                hero,
                new[] { "SetCharacterObject", "set_CharacterObject" },
                out _,
                character);
        }

        #endregion

        #region Campaign Operations

        /// <summary>
        /// Gets the PlayerDefaultFaction from Campaign (private property).
        /// </summary>
        public static bool TryGetPlayerDefaultFaction(Campaign campaign, out IFaction? faction)
        {
            faction = null;
            if (campaign == null) return false;

            return ReflectionHelper.TryGetProperty(
                campaign,
                new[] { "PlayerDefaultFaction", "_playerDefaultFaction" },
                out faction);
        }

        /// <summary>
        /// Sets the PlayerDefaultFaction on Campaign (private property).
        /// </summary>
        public static bool TrySetPlayerDefaultFaction(Campaign campaign, IFaction faction)
        {
            if (campaign == null) return false;

            return ReflectionHelper.TrySetProperty(
                campaign,
                new[] { "PlayerDefaultFaction" },
                faction);
        }

        #endregion

        #region CharacterObject Operations

        /// <summary>
        /// Sets the culture on a CharacterObject.
        /// </summary>
        public static bool TrySetCulture(CharacterObject character, CultureObject culture)
        {
            if (character == null) return false;

            // Try property first
            if (ReflectionHelper.TrySetProperty(character, new[] { "Culture" }, culture))
                return true;

            // Fall back to field
            return ReflectionHelper.TrySetField(character, new[] { "_culture" }, culture);
        }

        /// <summary>
        /// Sets the race on a BasicCharacterObject.
        /// </summary>
        public static bool TrySetRace(BasicCharacterObject character, int race)
        {
            if (character == null) return false;

            // Try property first
            if (ReflectionHelper.TrySetProperty(character, new[] { "Race" }, race))
                return true;

            // Fall back to backing field
            return ReflectionHelper.TrySetField(character, new[] { "<Race>k__BackingField", "_race" }, race);
        }

        /// <summary>
        /// Sets the name on a BasicCharacterObject.
        /// </summary>
        public static bool TrySetName(BasicCharacterObject character, TaleWorlds.Localization.TextObject name)
        {
            if (character == null) return false;

            // Try property first
            if (ReflectionHelper.TrySetProperty(character, new[] { "Name" }, name))
                return true;

            // Fall back to field
            return ReflectionHelper.TrySetField(character, new[] { "_name", "_basicName" }, name);
        }

        /// <summary>
        /// Tries to get the voice string ID from a character.
        /// </summary>
        public static bool TryGetVoice(BasicCharacterObject character, out string voiceId)
        {
            voiceId = "";
            if (character == null) return false;

            // _persona is a private field in CharacterObject (which inherits from BasicCharacterObject)
            // It holds a TraitObject
            object? personaObj = null;
            if (ReflectionHelper.TryGetField(character, new[] { "_persona" }, out personaObj))
            {
                if (personaObj != null)
                {
                    // TraitObject has StringId (via MBObjectBase)
                    if (ReflectionHelper.TryGetProperty(personaObj, new[] { "StringId" }, out object? sId) && sId != null)
                    {
                        voiceId = sId.ToString();
                        return true;
                    }
                }
            }
            return false;
        }

        #endregion

        #region BodyPropertyRange Operations

        /// <summary>
        /// Ensures a CharacterObject has a BodyPropertyRange, creating one if needed.
        /// </summary>
        public static bool EnsureBodyPropertyRange(BasicCharacterObject character)
        {
            if (character == null) return false;

            if (character.BodyPropertyRange != null) return true;

            try
            {
                var mbBodyProperty = MBObjectManager.Instance.RegisterPresumedObject<MBBodyProperty>(
                    new MBBodyProperty(character.StringId + "_facegen"));

                return ReflectionHelper.TrySetProperty(
                    character,
                    new[] { "BodyPropertyRange" },
                    mbBodyProperty);
            }
            catch (Exception ex)
            {
                FileLogger.Log($"[GameApiWrapper] Failed to create BodyPropertyRange: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Module Operations

        /// <summary>
        /// Gets the submodules list from a Module.
        /// </summary>
        public static bool TryGetSubmodules(TaleWorlds.MountAndBlade.Module module, out System.Collections.IList? submodules)
        {
            submodules = null;
            if (module == null) return false;

            return ReflectionHelper.TryGetField(
                module,
                new[] { "_submodules", "_loadedSubmodules" },
                out submodules);
        }

        #endregion

        #region FaceGen Screen Operations

        /// <summary>
        /// Gets the facegen layer from a barber screen.
        /// </summary>
        public static bool TryGetFaceGenLayer(object barberScreen, out object? facegenLayer)
        {
            facegenLayer = null;
            if (barberScreen == null) return false;

            return ReflectionHelper.TryGetField(
                barberScreen,
                new[] { "_facegenLayer", "_bodyGeneratorView" },
                out facegenLayer);
        }

        /// <summary>
        /// Tries to clean up a facegen layer (disable scene, call OnFinalize).
        /// </summary>
        public static bool TryCleanupFaceGenLayer(object facegenLayer)
        {
            if (facegenLayer == null) return false;

            bool success = true;

            // Try to get and disable SceneLayer.SceneView
            if (ReflectionHelper.TryGetProperty(facegenLayer, new[] { "SceneLayer" }, out object? sceneLayer) && sceneLayer != null)
            {
                if (ReflectionHelper.TryGetProperty(sceneLayer, new[] { "SceneView" }, out object? sceneView) && sceneView != null)
                {
                    ReflectionHelper.TryInvokeMethod(sceneView, new[] { "SetEnable" }, out _, false);
                }
            }

            // Call OnFinalize
            if (!ReflectionHelper.TryInvokeMethod(facegenLayer, new[] { "OnFinalize" }, out _))
            {
                success = false;
            }

            return success;
        }

        #endregion
    }
}
