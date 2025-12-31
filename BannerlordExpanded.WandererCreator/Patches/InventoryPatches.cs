using System;
using HarmonyLib;
using SandBox.GauntletUI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;

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
        /// Postfix patch for SPInventoryVM constructor to force the equipment mode
        /// and hide the mode selector buttons.
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

            // Try to hide the equipment mode selector buttons using reflection
            // The VM may have properties that control visibility of the mode selector
            TryHideEquipmentModeSelector(__instance);
        }

        /// <summary>
        /// Attempts to hide the equipment mode selector buttons by setting visibility-related properties.
        /// </summary>
        private static void TryHideEquipmentModeSelector(SPInventoryVM vm)
        {
            try
            {
                // Try to find and set properties that might hide the mode buttons
                // Common patterns in Gauntlet UI: Is[X]Visible, Is[X]Enabled, Is[X]Active
                var vmType = vm.GetType();

                // Try setting IsEquipmentSetFiltersHighlighted to false (if it affects visibility)
                var highlightProp = vmType.GetProperty("IsEquipmentSetFiltersHighlighted");
                if (highlightProp != null && highlightProp.CanWrite)
                {
                    highlightProp.SetValue(vm, false);
                }

                // The mode buttons IsCivilianMode, IsBattleMode, IsStealthMode control the "selected" state
                // but the buttons themselves are visible. We'll block their functionality via prefix patches.

                FileLogger.Log("SPInventoryVM Patch: Equipment mode selector patches applied");
            }
            catch (Exception ex)
            {
                FileLogger.Log($"SPInventoryVM Patch: Error hiding mode selector: {ex.Message}");
            }
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

        /// <summary>
        /// Blocks the civilian outfit button when the Wanderer Creator is active.
        /// Mode switching has no purpose in the equipment template editor.
        /// </summary>
        [HarmonyPatch(typeof(SPInventoryVM), "ExecuteSelectCivilianOutfit")]
        [HarmonyPrefix]
        public static bool ExecuteSelectCivilianOutfit_Prefix()
        {
            if (!IsCreatorActive) return true;
            // Block mode switching - it has no purpose in the creator
            return false;
        }

        /// <summary>
        /// Blocks the battle outfit button when the Wanderer Creator is active.
        /// Mode switching has no purpose in the equipment template editor.
        /// </summary>
        [HarmonyPatch(typeof(SPInventoryVM), "ExecuteSelectBattleOutfit")]
        [HarmonyPrefix]
        public static bool ExecuteSelectBattleOutfit_Prefix()
        {
            if (!IsCreatorActive) return true;
            // Block mode switching - it has no purpose in the creator
            return false;
        }

        /// <summary>
        /// Blocks the stealth outfit button when the Wanderer Creator is active.
        /// Mode switching has no purpose in the equipment template editor.
        /// </summary>
        [HarmonyPatch(typeof(SPInventoryVM), "ExecuteSelectStealthOutfit")]
        [HarmonyPrefix]
        public static bool ExecuteSelectStealthOutfit_Prefix()
        {
            if (!IsCreatorActive) return true;
            // Block mode switching - it has no purpose in the creator
            return false;
        }

        /// <summary>
        /// Postfix patch for GauntletInventoryScreen.OnActivate to hide the equipment mode selector buttons.
        /// This hides the "TopItems" widget that contains the Civilian/Battle/Stealth mode buttons.
        /// </summary>
        [HarmonyPatch(typeof(GauntletInventoryScreen), "OnActivate")]
        [HarmonyPostfix]
        public static void GauntletInventoryScreen_OnActivate_Postfix(GauntletInventoryScreen __instance)
        {
            if (!IsCreatorActive) return;

            try
            {
                var screenType = __instance.GetType();

                // Get the _gauntletMovie field (GauntletMovieIdentifier)
                var movieField = screenType.GetField("_gauntletMovie", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (movieField == null)
                {
                    FileLogger.Log("GauntletInventoryScreen Patch: _gauntletMovie field not found");
                    return;
                }

                var movieIdentifier = movieField.GetValue(__instance);
                if (movieIdentifier == null)
                {
                    FileLogger.Log("GauntletInventoryScreen Patch: _gauntletMovie is null");
                    return;
                }

                // Get the Movie property (IGauntletMovie) from GauntletMovieIdentifier
                var movieIdentifierType = movieIdentifier.GetType();
                var movieProp = movieIdentifierType.GetProperty("Movie");
                if (movieProp == null)
                {
                    FileLogger.Log("GauntletInventoryScreen Patch: Movie property not found");
                    return;
                }

                var movie = movieProp.GetValue(movieIdentifier);
                if (movie == null)
                {
                    FileLogger.Log("GauntletInventoryScreen Patch: Movie is null");
                    return;
                }

                // Get the RootWidget property (Widget) from IGauntletMovie
                var movieType = movie.GetType();
                var rootWidgetProp = movieType.GetProperty("RootWidget");
                if (rootWidgetProp == null)
                {
                    FileLogger.Log("GauntletInventoryScreen Patch: RootWidget property not found");
                    return;
                }

                var rootWidget = rootWidgetProp.GetValue(movie) as Widget;
                if (rootWidget == null)
                {
                    FileLogger.Log("GauntletInventoryScreen Patch: RootWidget is null");
                    return;
                }

                // Find the TopItems widget by ID recursively
                var topItemsWidget = FindWidgetById(rootWidget, "TopItems");
                if (topItemsWidget != null)
                {
                    topItemsWidget.IsVisible = false;
                    FileLogger.Log("GauntletInventoryScreen Patch: Successfully hid TopItems widget (mode buttons)");
                }
                else
                {
                    FileLogger.Log("GauntletInventoryScreen Patch: TopItems widget not found in widget tree");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log($"GauntletInventoryScreen Patch: Error hiding mode buttons: {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively searches for a widget with the specified ID.
        /// </summary>
        private static Widget FindWidgetById(Widget parent, string id)
        {
            if (parent == null) return null;
            if (parent.Id == id) return parent;

            foreach (var child in parent.Children)
            {
                var result = FindWidgetById(child, id);
                if (result != null) return result;
            }

            return null;
        }
    }
}
