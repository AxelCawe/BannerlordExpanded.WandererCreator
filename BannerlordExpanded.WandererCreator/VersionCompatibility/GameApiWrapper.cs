using System;
using System.Collections.Generic;
using BannerlordExpanded.WandererCreator.Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
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
        /// Tries to get the voice string ID from a character using GetPersona().
        /// </summary>
        public static bool TryGetVoice(BasicCharacterObject character, out string voiceId)
        {
            voiceId = "";
            if (character == null) return false;

            try
            {
                // Try to cast to CharacterObject (CampaignSystem)
                if (character is CharacterObject charObj)
                {
                    // Direct access since GetPersona is public
                    var traitObj = charObj.GetPersona();
                    if (traitObj != null)
                    {
                        voiceId = traitObj.StringId;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log($"[GameApiWrapper] Error getting voice: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Tries to get the FaceGen voice index from BodyProperties.
        /// This is the audio voice preset (0, 1, 2, etc.) encoded in the body properties.
        /// </summary>
        public static bool TryGetFaceGenVoiceIndex(BodyProperties bodyProperties, out int voiceIndex)
        {
            voiceIndex = 0;
            try
            {
                FaceGenerationParams faceGenParams = FaceGenerationParams.Create();
                MBBodyProperties.GetParamsFromKey(ref faceGenParams, bodyProperties, false, false);
                voiceIndex = faceGenParams.CurrentVoice;
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Log($"[GameApiWrapper] Error getting FaceGen voice index: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Maps a FaceGen voice index to a persona string ID.
        /// Voice indices are gender-dependent and map to different audio presets.
        /// Index 0 = curt, 1 = earnest, 2 = ironic, 3 = softspoken (approximate mapping)
        /// </summary>
        public static string GetPersonaFromVoiceIndex(int voiceIndex, bool isFemale)
        {
            // The game has different voice counts per gender/race/age.
            // This is an approximate mapping based on the 4 persona types.
            // Voice indices cycle through available voices.
            string[] personaIds = { "curt", "earnest", "ironic", "softspoken" };

            // Wrap the index to the available personas
            int personaIndex = voiceIndex % personaIds.Length;
            return personaIds[personaIndex];
        }

        /// <summary>
        /// Gets the persona string ID from a Hero's BodyProperties.
        /// Combines TryGetFaceGenVoiceIndex and GetPersonaFromVoiceIndex.
        /// </summary>
        public static bool TryGetPersonaFromBodyProperties(Hero hero, out string personaId)
        {
            personaId = "curt"; // Default
            if (hero == null) return false;

            try
            {
                if (TryGetFaceGenVoiceIndex(hero.BodyProperties, out int voiceIndex))
                {
                    personaId = GetPersonaFromVoiceIndex(voiceIndex, hero.IsFemale);
                    return true;
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log($"[GameApiWrapper] Error getting persona from body properties: {ex.Message}");
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

        #region Game Data Operations

        /// <summary>
        /// Gets all skill IDs from the game's object manager.
        /// </summary>
        public static List<string> GetSkillIds()
        {
            var list = new List<string>();
            try
            {
                var skills = Game.Current.ObjectManager.GetObjectTypeList<SkillObject>();
                foreach (var s in skills)
                {
                    if (!string.IsNullOrEmpty(s.StringId))
                        list.Add(s.StringId);
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log($"[GameApiWrapper] Error getting skill IDs: {ex.Message}");
            }
            return list;
        }

        /// <summary>
        /// Gets all trait IDs from the game's object manager.
        /// </summary>
        public static List<string> GetTraitIds()
        {
            var list = new List<string>();
            try
            {
                var traits = Game.Current.ObjectManager.GetObjectTypeList<TraitObject>();
                foreach (var t in traits)
                {
                    if (!string.IsNullOrEmpty(t.StringId))
                        list.Add(t.StringId);
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log($"[GameApiWrapper] Error getting trait IDs: {ex.Message}");
            }
            return list;
        }

        /// <summary>
        /// Gets culture IDs from the game. Only includes major cultures (CanHaveSettlement = true).
        /// </summary>
        public static List<string> GetCultureIds()
        {
            var list = new List<string>();
            try
            {
                var cultures = Game.Current.ObjectManager.GetObjectTypeList<CultureObject>();
                foreach (var c in cultures)
                {
                    if (!string.IsNullOrEmpty(c.StringId) && c.CanHaveSettlement)
                        list.Add(c.StringId);
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log($"[GameApiWrapper] Error getting culture IDs: {ex.Message}");
            }
            return list;
        }

        #endregion
    }
}
