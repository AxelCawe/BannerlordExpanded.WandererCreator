using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace BannerlordExpanded.WandererCreator.Patches
{
    /// <summary>
    /// Minimal Harmony patches to enable inventory screen functionality in the Wanderer Creator.
    /// These patches only activate when the Wanderer Creator is in use.
    /// </summary>
    [HarmonyPatch]
    public static class InventoryPatches
    {
        /// <summary>
        /// Flag to indicate if the Wanderer Creator is currently active.
        /// Only when true will the patches modify behavior.
        /// </summary>
        public static bool IsCreatorActive { get; set; } = false;

        /// <summary>
        /// Flag to indicate if the inventory should start in civilian mode.
        /// Only used when IsCreatorActive is true.
        /// </summary>
        public static bool IsCivilianMode { get; set; } = false;


        /// <summary>
        /// Patches DefaultInformationRestrictionModel.DoesPlayerKnowDetailsOf(Hero) to prevent
        /// null reference crashes when Campaign isn't fully initialized.
        /// </summary>
        [HarmonyPatch(typeof(DefaultInformationRestrictionModel), "DoesPlayerKnowDetailsOf", new Type[] { typeof(Hero) })]
        [HarmonyPrefix]
        public static bool DoesPlayerKnowDetailsOf_Hero_Prefix(Hero hero, ref bool __result)
        {
            if (!IsCreatorActive) return true;

            try
            {
                if (Clan.PlayerClan == null || hero == null)
                {
                    __result = true;
                    return false;
                }
                var _ = hero.Clan;
            }
            catch
            {
                __result = true;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Patches CampaignUIHelper.IsHeroInformationHidden to prevent crashes.
        /// </summary>
        [HarmonyPatch(typeof(CampaignUIHelper), "IsHeroInformationHidden")]
        [HarmonyPrefix]
        public static bool IsHeroInformationHidden_Prefix(Hero hero, ref TextObject disableReason, ref bool __result)
        {
            if (!IsCreatorActive) return true;

            try
            {
                if (hero == null)
                {
                    disableReason = new TextObject(string.Empty);
                    __result = false;
                    return false;
                }
            }
            catch
            {
                disableReason = new TextObject(string.Empty);
                __result = false;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Patches HeroViewModel.FillFrom to safely handle incomplete hero data.
        /// </summary>
        [HarmonyPatch(typeof(HeroViewModel), "FillFrom")]
        [HarmonyPrefix]
        public static bool FillFrom_Prefix(HeroViewModel __instance, Hero hero)
        {
            if (!IsCreatorActive) return true;

            if (hero == null) return false;

            try
            {
                var _ = hero.Age;
                var __ = hero.CharacterObject;
            }
            catch
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Postfix patch for SPInventoryVM constructor to force the equipment mode.
        /// This is necessary because GauntletInventoryScreen hardcodes the initial mode.
        /// </summary>
        [HarmonyPatch(typeof(SPInventoryVM), MethodType.Constructor, new Type[] { typeof(InventoryLogic), typeof(bool), typeof(Func<TaleWorlds.Core.WeaponComponentData, TaleWorlds.Core.ItemObject.ItemUsageSetFlags>) })]
        [HarmonyPostfix]
        public static void SPInventoryVM_Constructor_Postfix(SPInventoryVM __instance)
        {
            if (!IsCreatorActive) return;

            // 0 = Civilian, 1 = Battle, 2 = Stealth
            __instance.EquipmentMode = IsCivilianMode ? 0 : 1;
            FileLogger.Log($"SPInventoryVM Patch: Forced EquipmentMode to {__instance.EquipmentMode} (IsCivilianMode: {IsCivilianMode})");
        }

        // NOTE: Previously had patches for UpdateCharacterEquipment, UpdateRightCharacter, and UpdateLeftCharacter

        // that blocked these methods from running. These were removed as they prevented equipment from displaying.
        // If null reference crashes occur in these methods, we should add defensive null checks instead of blocking.

        /// <summary>
        /// Patches InventoryLogic.GetItemPrice to return default price and avoid market calculations.
        /// </summary>
        [HarmonyPatch(typeof(InventoryLogic), "GetItemPrice")]
        [HarmonyPrefix]
        public static bool GetItemPrice_Prefix(EquipmentElement equipmentElement, bool isBuying, ref int __result)
        {
            if (!IsCreatorActive) return true;

            // Return item valus as price, ignoring market data
            if (equipmentElement.Item != null)
            {
                __result = equipmentElement.Item.Value;
            }
            else
            {
                __result = 0;
            }
            return false;
        }
    }
}
