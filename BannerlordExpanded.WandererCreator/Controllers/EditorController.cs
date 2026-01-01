using BannerlordExpanded.WandererCreator.Models;
using BannerlordExpanded.WandererCreator.Services;
using BannerlordExpanded.WandererCreator.UI;
using BannerlordExpanded.WandererCreator.VersionCompatibility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace BannerlordExpanded.WandererCreator.Controllers
{
    public class EditorController
    {
        private CreatorForm _form;
        private Thread _formThread;
        private List<string> _cachedSkillIds = new List<string>(); // Dynamic Skills
        private List<string> _cachedTraitIds = new List<string>(); // Dynamic Traits
        private List<string> _cachedCultureIds = new List<string>(); // Dynamic Cultures

        public static EditorController Instance { get; private set; }
        public bool ShouldExit { get; private set; }

        public EditorController()
        {
            Instance = this;
        }

        public bool CanStart()
        {
            // Detect exclusive fullscreen using NativeOptions
            // DisplayMode values: 0 = Fullscreen (exclusive), 1 = Windowed, 2 = Borderless
            bool isExclusive = TaleWorlds.Engine.Options.NativeOptions.GetConfig(
                TaleWorlds.Engine.Options.NativeOptions.NativeOptionsType.DisplayMode) == 2f;

            if (isExclusive)
            {
                InformationManager.ShowInquiry(new InquiryData("Error", "You cannot use the Wanderer Creator in Exclusive Fullscreen mode. Please switch to Windowed or Borderless.", true, false, "Ok", "", null, null));
                return false;
            }

            return true;
        }

        public void Start()
        {
            try
            {
                _cachedSkillIds = GameApiWrapper.GetSkillIds();
                _cachedTraitIds = GameApiWrapper.GetTraitIds();
                _cachedCultureIds = GameApiWrapper.GetCultureIds();
            }
            catch (Exception ex)
            {
                FileLogger.Log($"Error fetching skills/traits/cultures: {ex}");
                _cachedSkillIds = new List<string>();
                _cachedTraitIds = new List<string>();
                _cachedCultureIds = new List<string>();
            }
            LaunchEditorThread();
        }

        public void OnScreenActivated()
        {
            // Called when the CreatorScreen is reactivated (e.g. returning from FaceGen)
            if (_form != null)
            {
                _form.Invoke(new Action(() =>
                {
                    if (!_form.Visible) _form.Show();
                    _form.BringToFront();
                }));
            }
        }

        private void LaunchEditorThread()
        {
            // Windows Forms must run in a single-threaded apartment (STA) thread
            _formThread = new Thread(() =>
            {
                Application.EnableVisualStyles();
                _form = new CreatorForm();
                if (_cachedSkillIds != null) _form.AvailableSkills = _cachedSkillIds;
                if (_cachedTraitIds != null) _form.AvailableTraits = _cachedTraitIds;
                if (_cachedCultureIds != null) _form.AvailableCultures = _cachedCultureIds;

                // Center the form
                _form.StartPosition = FormStartPosition.CenterScreen;

                _form.OnEditTemplateRequest += HandleEditTemplate; // Fix: Subscribe to template edits
                _form.OnEditBodyTemplateRequest += HandleEditBodyTemplate; // Body template editing
                _form.OnCreateBodyTemplateRequest += HandleCreateBodyTemplate; // Generate body properties for new template
                // Note: OnSaveRequest is handled entirely by CreatorForm.SaveProject() which shows its own dialog
                _form.OnExportRequest += HandleExportMod;

                // Handle Exit (return to Main Menu)
                _form.FormClosed += (s, e) =>
                {
                    ShouldExit = true;
                    Application.ExitThread(); // Ensure thread exits
                };

                // Parent to Game Window (so it stays on top of game but not other apps)
                try
                {
                    var handle = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                    if (handle != IntPtr.Zero)
                    {
                        var wrapper = new WindowWrapper(handle);
                        _form.Show(wrapper);
                    }
                    else
                    {
                        _form.Show();
                    }
                }
                catch
                {
                    _form.Show();
                }

                // Keep the form thread alive
                Application.Run();
            });
            _formThread.SetApartmentState(ApartmentState.STA);
            _formThread.Start();
        }

        // Helper class to parent the form to the game window
        private class WindowWrapper : System.Windows.Forms.IWin32Window
        {
            public WindowWrapper(IntPtr handle) { _hwnd = handle; }
            public IntPtr Handle { get { return _hwnd; } }
            private IntPtr _hwnd;
        }

        // P/Invoke to focus game window after hiding form
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// Focuses the game window (used after hiding the editor form).
        /// </summary>
        private void FocusGameWindow()
        {
            try
            {
                var handle = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                if (handle != IntPtr.Zero)
                {
                    SetForegroundWindow(handle);
                }
            }
            catch { /* Silently ignore focus errors */ }
        }

        // Store reference to the dummy character passed to FaceGen (so we can read from it on pop)
        private CharacterObject? _currentFaceGenCharacter;
        // Track if we're editing max appearance (vs min)
        private bool _isEditingMaxAppearance;
        // Track if we're editing a body template directly (vs wanderer appearance)
        private BodyPropertiesTemplate? _currentEditingBodyTemplate;
        private void HandleEditAppearance(WandererDefinition wanderer, bool isEditingMax)
        {
            FileLogger.Log($"HandleEditAppearance called (isEditingMax: {isEditingMax})");
            FileLogger.Log($"  Wanderer Name: {wanderer.Name}");
            FileLogger.Log($"  BodyPropertiesString: '{wanderer.BodyPropertiesString}'");
            FileLogger.Log($"  BodyPropertiesString IsNullOrEmpty: {string.IsNullOrEmpty(wanderer.BodyPropertiesString)}");

            _currentEditingWanderer = wanderer; // Store for later callback
            _isEditingMaxAppearance = isEditingMax; // Store which property we're editing
            _form.Invoke(new Action(() => _form.Hide()));
            FocusGameWindow();

            try
            {
                CharacterObject dummy = CreateDummyCharacter(wanderer);

                // Critical Check: Ensure Monster can be derived from Race (prevents crash in FaceGen)
                Monster? derivedMonster = null;
                try
                {
                    derivedMonster = FaceGen.GetBaseMonsterFromRace(dummy.Race);
                }
                catch (Exception monsterEx)
                {
                    FileLogger.Log($"Exception getting Monster from Race: {monsterEx.Message}");
                }

                if (derivedMonster == null)
                {
                    FileLogger.Log($"Error: Cannot derive Monster from Race {dummy.Race}. Aborting FaceGen.");
                    InformationManager.ShowInquiry(new InquiryData("Error", $"Critical Error: Cannot derive Monster from Race {dummy.Race}.\nGame XMLs (Native/Sandbox) may not be loaded.\n\nPlease check if 'Native' module is enabled.", true, false, "Ok", "", () => OnScreenActivated(), null));
                    return;
                }

                FileLogger.Log($"Dummy Character created: {dummy.Name} (Race: {dummy.Race}, Monster: {derivedMonster.StringId})");

                // Capture the body properties string for use in the main thread action
                // Use the correct string based on whether we're editing min or max
                string bodyPropsString = isEditingMax
                    ? wanderer.BodyPropertiesMaxString
                    : wanderer.BodyPropertiesString;

                // Push BarberState via Main Thread
                SubModule.EnqueueMainThreadAction(() =>
                {
                    try
                    {
                        var heroCharacter = Hero.MainHero.CharacterObject;
                        if (heroCharacter == null)
                        {
                            FileLogger.Log("[MainThread] ERROR: Hero.MainHero.CharacterObject is null!");
                            return;
                        }

                        // Always set IsFemale first (affects skeleton selection)
                        Hero.MainHero.IsFemale = wanderer.IsFemale;
                        FileLogger.Log($"[MainThread] Set IsFemale = {wanderer.IsFemale}");

                        // Get or generate body properties
                        TaleWorlds.Core.BodyProperties bodyProps;
                        if (!string.IsNullOrEmpty(bodyPropsString) &&
                            TaleWorlds.Core.BodyProperties.FromString(bodyPropsString, out var savedBp))
                        {
                            // Existing wanderer - use saved properties
                            FileLogger.Log($"[MainThread] Loading saved BodyProperties");
                            bodyProps = savedBp;
                        }
                        else
                        {
                            // New wanderer - generate random properties
                            FileLogger.Log($"[MainThread] Generating new BodyProperties");
                            var bpMin = heroCharacter.GetBodyPropertiesMin(true);
                            var bpMax = heroCharacter.GetBodyPropertiesMax(true);
                            bodyProps = TaleWorlds.Core.FaceGen.GetRandomBodyProperties(
                                heroCharacter.Race,
                                wanderer.IsFemale,
                                bpMin, bpMax,
                                0, // HairCoverType.None
                                new Random().Next(),
                                heroCharacter.BodyPropertyRange?.HairTags ?? "",
                                heroCharacter.BodyPropertyRange?.BeardTags ?? "",
                                heroCharacter.BodyPropertyRange?.TattooTags ?? "",
                                0.1f);
                        }

                        // Apply body properties to Hero.MainHero
                        Hero.MainHero.StaticBodyProperties = bodyProps.StaticProperties;
                        Hero.MainHero.Weight = bodyProps.Weight;
                        Hero.MainHero.Build = bodyProps.Build;

                        // Ensure BodyPropertyRange exists on CharacterObject
                        EnsureBodyPropertyRange(heroCharacter);
                        heroCharacter.BodyPropertyRange?.Init(bodyProps, bodyProps);

                        FileLogger.Log($"[MainThread] Applied BodyProperties to Hero.MainHero");

                        // Push the FaceGen state
                        _currentFaceGenCharacter = dummy;
                        GameStateManager.Current.PushState(
                            GameStateManager.Current.CreateState<BarberState>(dummy, new FaceGenCustomFilter()));
                        FileLogger.Log("Pushed BarberState");
                    }
                    catch (Exception ex)
                    {
                        FileLogger.Log($"Error in FaceGen setup: {ex}");
                    }
                });
            }
            catch (Exception ex)
            {
                FileLogger.Log($"Error in HandleEditAppearance: {ex}");
                InformationManager.ShowInquiry(new InquiryData("Error", "Could not open FaceGen. (Requires Campaign?)\n" + ex.Message, true, false, "Ok", "", () => OnScreenActivated(), null));
            }
        }

        private void HandleCreateBodyTemplate(BodyPropertiesTemplate template)
        {
            FileLogger.Log($"HandleCreateBodyTemplate called (template: {template.Name}, IsFemale: {template.IsFemale})");

            // Generate random body properties on the main thread
            SubModule.EnqueueMainThreadAction(() =>
            {
                try
                {
                    var heroCharacter = Hero.MainHero.CharacterObject;
                    if (heroCharacter == null)
                    {
                        FileLogger.Log("[MainThread] ERROR: Hero.MainHero.CharacterObject is null!");
                        return;
                    }

                    var bpMin = heroCharacter.GetBodyPropertiesMin(true);
                    var bpMax = heroCharacter.GetBodyPropertiesMax(true);
                    var bodyProps = TaleWorlds.Core.FaceGen.GetRandomBodyProperties(
                        heroCharacter.Race,
                        template.IsFemale,
                        bpMin, bpMax,
                        0,
                        new Random().Next(),
                        heroCharacter.BodyPropertyRange?.HairTags ?? "",
                        heroCharacter.BodyPropertyRange?.BeardTags ?? "",
                        heroCharacter.BodyPropertyRange?.TattooTags ?? "",
                        0.1f);

                    template.BodyPropertiesString = bodyProps.ToString();
                    template.BodyPropertiesMaxString = bodyProps.ToString();
                    FileLogger.Log($"[MainThread] Generated BodyProperties for new template: {template.Id}");

                    // Refresh the UI to show the new body properties
                    _form?.Invoke(new Action(() => _form.RefreshBodyTemplateList()));
                }
                catch (Exception ex)
                {
                    FileLogger.Log($"Error generating body properties for template: {ex}");
                }
            });
        }

        private void HandleEditBodyTemplate(BodyPropertiesTemplate template, bool isEditingMax)
        {
            FileLogger.Log($"HandleEditBodyTemplate called (template: {template.Name}, isEditingMax: {isEditingMax})");

            _currentEditingBodyTemplate = template;
            _currentEditingWanderer = null; // Not editing a wanderer directly
            _isEditingMaxAppearance = isEditingMax;
            _form.Invoke(new Action(() => _form.Hide()));
            FocusGameWindow();

            try
            {
                // Use the template's body properties
                string bodyPropsString = isEditingMax
                    ? template.BodyPropertiesMaxString
                    : template.BodyPropertiesString;

                // Push BarberState via Main Thread
                SubModule.EnqueueMainThreadAction(() =>
                {
                    try
                    {
                        var heroCharacter = Hero.MainHero.CharacterObject;
                        if (heroCharacter == null)
                        {
                            FileLogger.Log("[MainThread] ERROR: Hero.MainHero.CharacterObject is null!");
                            return;
                        }

                        // Set IsFemale first - this affects the visual model displayed
                        Hero.MainHero.IsFemale = template.IsFemale;
                        FileLogger.Log($"[MainThread] Set Hero.MainHero.IsFemale = {template.IsFemale}");

                        // Get or generate body properties
                        TaleWorlds.Core.BodyProperties bodyProps;
                        if (!string.IsNullOrEmpty(bodyPropsString) &&
                            TaleWorlds.Core.BodyProperties.FromString(bodyPropsString, out var savedBp))
                        {
                            FileLogger.Log($"[MainThread] Loading saved BodyProperties for template");
                            bodyProps = savedBp;
                        }
                        else
                        {
                            FileLogger.Log($"[MainThread] Generating new BodyProperties for template (IsFemale: {template.IsFemale})");
                            var bpMin = heroCharacter.GetBodyPropertiesMin(true);
                            var bpMax = heroCharacter.GetBodyPropertiesMax(true);
                            bodyProps = TaleWorlds.Core.FaceGen.GetRandomBodyProperties(
                                heroCharacter.Race,
                                template.IsFemale, // Use template's gender setting
                                bpMin, bpMax,
                                0,
                                new Random().Next(),
                                heroCharacter.BodyPropertyRange?.HairTags ?? "",
                                heroCharacter.BodyPropertyRange?.BeardTags ?? "",
                                heroCharacter.BodyPropertyRange?.TattooTags ?? "",
                                0.1f);
                        }

                        // Apply body properties to Hero.MainHero
                        Hero.MainHero.StaticBodyProperties = bodyProps.StaticProperties;
                        Hero.MainHero.Weight = bodyProps.Weight;
                        Hero.MainHero.Build = bodyProps.Build;

                        // Ensure BodyPropertyRange exists on CharacterObject
                        EnsureBodyPropertyRange(heroCharacter);
                        heroCharacter.BodyPropertyRange?.Init(bodyProps, bodyProps);

                        FileLogger.Log($"[MainThread] Applied BodyProperties for template editing");

                        // Push the FaceGen state
                        GameStateManager.Current.PushState(
                            GameStateManager.Current.CreateState<BarberState>(heroCharacter, new FaceGenCustomFilter()));
                        FileLogger.Log("Pushed BarberState for template editing");
                    }
                    catch (Exception ex)
                    {
                        FileLogger.Log($"Error in FaceGen setup for template: {ex}");
                    }
                });
            }
            catch (Exception ex)
            {
                FileLogger.Log($"Error in HandleEditBodyTemplate: {ex}");
                InformationManager.ShowInquiry(new InquiryData("Error", "Could not open FaceGen.\n" + ex.Message, true, false, "Ok", "", () => OnScreenActivated(), null));
            }
        }

        /// <summary>
        /// Ensures the given CharacterObject has a BodyPropertyRange. Creates one if null.
        /// </summary>
        private void EnsureBodyPropertyRange(BasicCharacterObject character)
        {
            GameApiWrapper.EnsureBodyPropertyRange(character);
        }

        // Store reference to the wanderer currently being edited (for FaceGen callback)
        private WandererDefinition? _currentEditingWanderer;


        /// <summary>
        /// Called by CreatorGameManager when BarberState is popped (user clicked Done in FaceGen)
        /// </summary>
        public void OnFaceGenComplete()
        {
            string actualBp = "";
            try
            {
                var bp = Hero.MainHero.BodyProperties;
                actualBp = bp.ToString();
                FileLogger.Log($"Read BodyProperties from Hero.MainHero: {actualBp}");
            }
            catch (Exception ex)
            {
                FileLogger.Log($"Error reading BodyProperties from Hero.MainHero: {ex.Message}");
            }

            // Handle body template editing
            if (_currentEditingBodyTemplate != null && !string.IsNullOrEmpty(actualBp))
            {
                if (_isEditingMaxAppearance)
                {
                    _currentEditingBodyTemplate.BodyPropertiesMaxString = actualBp;
                    FileLogger.Log($"Updated BodyPropertiesMax for template: {_currentEditingBodyTemplate.Name}");
                }
                else
                {
                    _currentEditingBodyTemplate.BodyPropertiesString = actualBp;
                    // Auto-copy to max if empty
                    if (string.IsNullOrEmpty(_currentEditingBodyTemplate.BodyPropertiesMaxString))
                    {
                        _currentEditingBodyTemplate.BodyPropertiesMaxString = actualBp;
                        FileLogger.Log($"Updated BodyPropertiesMax (auto-copied from min)");
                    }
                    FileLogger.Log($"Updated BodyPropertiesString for template: {_currentEditingBodyTemplate.Name}");
                }

                // Refresh template list
                _form.Invoke(new Action(() => _form.RefreshBodyTemplateList()));
                _currentEditingBodyTemplate = null;
            }
            // Handle wanderer editing
            else if (_currentEditingWanderer != null && !string.IsNullOrEmpty(actualBp))
            {
                // Save to the correct property based on which button was clicked
                if (_isEditingMaxAppearance)
                {
                    _currentEditingWanderer.BodyPropertiesMaxString = actualBp;
                    FileLogger.Log($"Updated BodyPropertiesMax for wanderer: {_currentEditingWanderer.Name}");
                }
                else
                {
                    _currentEditingWanderer.BodyPropertiesString = actualBp;
                    // If editing min and max is empty, also set max to the same value
                    if (string.IsNullOrEmpty(_currentEditingWanderer.BodyPropertiesMaxString))
                    {
                        _currentEditingWanderer.BodyPropertiesMaxString = actualBp;
                        FileLogger.Log($"Updated BodyPropertiesMax (auto-copied from min)");
                    }
                    FileLogger.Log($"Updated BodyPropertiesString for wanderer: {_currentEditingWanderer.Name}");
                }

                // Get voice/persona from the BodyProperties (FaceGen voice selection) - only for min
                if (!_isEditingMaxAppearance && GameApiWrapper.TryGetPersonaFromBodyProperties(Hero.MainHero, out string personaId))
                {
                    _currentEditingWanderer.Voice = personaId;
                    FileLogger.Log($"Updated Voice to '{personaId}' from BodyProperties");
                }
                else if (!_isEditingMaxAppearance)
                {
                    MessageBox.Show("Could not get Voice from BodyProperties. Mod is broken! Report to mod author ASAP!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            // refresh UI
            if (_currentEditingWanderer != null)
            {
                _form.Invoke(new Action(() => _form.SelectWanderer(_currentEditingWanderer)));
            }

            // Clear stored refs
            _currentFaceGenCharacter = null;

            // Show the form again
            OnScreenActivated();
        }

        // Store reference to the template currently being edited
        private EquipmentTemplate? _currentEditingTemplate;

        // State to track which set we are editing (Civilian vs Battle)
        private bool _isEditingCivilianEquipment;
        private TaleWorlds.Localization.TextObject? _originalCharacterName;
        private TaleWorlds.Localization.TextObject? _originalHeroName;
        private TaleWorlds.Localization.TextObject? _originalHeroFirstName;

        public void RegisterFormEvents()
        {
            if (_form == null) return;
            _form.OnEditTemplateRequest += HandleEditTemplate;
            _form.OnEditBodyTemplateRequest += HandleEditBodyTemplate;
            _form.OnSaveRequest += (p) => { /* Auto-save handled by form serialization */ };
            _form.OnExportRequest += HandleExportMod;
        }

        private void HandleEditTemplate(EquipmentTemplate template)
        {
            _form.Invoke(new Action(() => _form.Hide()));
            FocusGameWindow();
            try
            {
                _currentEditingTemplate = template;
                _currentEditingWanderer = null; // Clear wanderer context
                                                // Template determines civilian status? Or we assume Battle slots for editing?
                                                // Editor always uses Battle slots 0-4 for visual editing convenience usually, 
                                                // but native Civilian toggle exists in Inventory logic.
                                                // Let's rely on _isEditingCivilianEquipment logic or pass it to InventoryLogic?
                                                // Actually InventoryLogic takes 'isCivilian' bool.
                _isEditingCivilianEquipment = false; // Default to Battle mode visuals unless template is strictly civilian?
                                                     // NOTE: Template has IsCivilian property. We should use it.
                _isEditingCivilianEquipment = template.IsCivilian;

                CharacterObject dummy = Hero.MainHero.CharacterObject;


                // CRITICAL: InventoryLogic uses CharacterObject.PlayerCharacter, NOT Hero.MainHero.CharacterObject!
                var playerCharacter = CharacterObject.PlayerCharacter;

                // Temporarily rename for the inventory screen
                _originalCharacterName = playerCharacter.Name;
                if (GameApiWrapper.TrySetName(playerCharacter, new TaleWorlds.Localization.TextObject(template.Name)))
                {
                    FileLogger.Log($"Temporarily renamed player to {template.Name}");
                }

                // Also rename Hero.MainHero if it exists (Inventory UI reads from Hero)
                if (Hero.MainHero != null)
                {
                    _originalHeroName = Hero.MainHero.Name;
                    _originalHeroFirstName = Hero.MainHero.FirstName;
                    var newName = new TaleWorlds.Localization.TextObject(template.Name);
                    // Hero.SetName takes (fullName, firstName)
                    Hero.MainHero.SetName(newName, newName);
                    FileLogger.Log($"Temporarily renamed Hero.MainHero to {template.Name}");
                }

                FileLogger.Log($"Ref Check: Hero.MainHero.CharacterObject == CharacterObject.PlayerCharacter? {dummy == playerCharacter}");
                FileLogger.Log($"Ref Check: CharacterObject.PlayerCharacter.HeroObject == Hero.MainHero? {playerCharacter.HeroObject == Hero.MainHero}");

                // Set equipment on the correct equipment set based on template type
                // For civilian templates, use CivilianEquipment; for battle, use BattleEquipment
                bool isCivilian = template.IsCivilian;
                var heroEquipment = isCivilian ? Hero.MainHero.CivilianEquipment : Hero.MainHero.BattleEquipment;
                var charEquipment = isCivilian ? playerCharacter.FirstCivilianEquipment : playerCharacter.Equipment;

                // First clear all slots on both
                for (int i = 0; i < 12; i++)
                {
                    heroEquipment[i] = EquipmentElement.Invalid;
                    charEquipment[i] = EquipmentElement.Invalid;
                }

                // Then set each template item directly on BOTH equipment objects
                FileLogger.Log($"Setting {template.Items.Count} items on {(isCivilian ? "CivilianEquipment" : "BattleEquipment")}");
                foreach (var kvp in template.Items)
                {
                    if (string.IsNullOrEmpty(kvp.Value)) continue;
                    try
                    {
                        EquipmentIndex index = ParseEquipmentIndex(kvp.Key);
                        if (index != EquipmentIndex.None)
                        {
                            ItemObject item = Game.Current.ObjectManager.GetObject<ItemObject>(kvp.Value);
                            if (item != null)
                            {
                                var element = new EquipmentElement(item);
                                heroEquipment[index] = element;
                                charEquipment[index] = element;
                                FileLogger.Log($"  Set: {index} = {kvp.Value}");
                            }
                        }
                    }
                    catch (Exception ex) { FileLogger.Log($"Error setting template item: {ex.Message}"); }
                }

                // Log what's now equipped
                int heroCount = 0, charCount = 0;
                for (int i = 0; i < 12; i++)
                {
                    if (!heroEquipment[i].IsEmpty) heroCount++;
                    if (!charEquipment[i].IsEmpty) charCount++;
                }
                FileLogger.Log($"Hero equipment has {heroCount} items, CharacterObject equipment has {charCount} items (IsCivilian={isCivilian})");

                // Open inventory in correct mode based on template type
                OpenInventory(playerCharacter, isCivilian);
            }
            catch (Exception ex)
            {
                FileLogger.Log($"Error starting template edit: {ex}");
                OnScreenActivated();
            }
        }

        private void OpenInventory(CharacterObject dummy, bool isCivilian)
        {
            // Log pre-open equipment state
            var preEq = isCivilian ? Hero.MainHero.CivilianEquipment : Hero.MainHero.BattleEquipment;
            int preCount = 0;
            for (int i = 0; i < 12; i++) if (!preEq[i].IsEmpty) preCount++;
            FileLogger.Log($"OpenInventory: Hero.MainHero {(isCivilian ? "CivilianEquipment" : "BattleEquipment")} has {preCount} items before init");

            // Force Civilian Mode via Harmony Patch if necessary
            Patches.InventoryPatches.IsCivilianMode = isCivilian;


            // Create ItemRoster (All items) - Same as TroopDesigner
            var itemRoster = new TaleWorlds.CampaignSystem.Roster.ItemRoster();
            var allItems = Game.Current.ObjectManager.GetObjectTypeList<TaleWorlds.Core.ItemObject>();
            foreach (var item in allItems)
            {
                if (!item.IsTradeGood && !item.IsAnimal)
                {
                    itemRoster.AddToCounts(item, 5);
                }
            }

            // TroopDesigner pattern: Use CharacterObject.PlayerCharacter instead of Hero.MainHero.CharacterObject
            var playerCharacter = CharacterObject.PlayerCharacter;

            var inventoryLogic = new TaleWorlds.CampaignSystem.Inventory.InventoryLogic(
                TaleWorlds.CampaignSystem.Party.MobileParty.MainParty,
                playerCharacter,
                null);

            // Initialize inventory in correct mode (civilian or battle)
            inventoryLogic.Initialize(
                itemRoster,
                TaleWorlds.CampaignSystem.Party.MobileParty.MainParty,
                false,
                true,
                playerCharacter,
                global::Helpers.InventoryScreenHelper.InventoryCategoryType.All,
                null,
                false,
                global::Helpers.InventoryScreenHelper.InventoryMode.Default
            );

            var inventoryState = Game.Current.GameStateManager.CreateState<TaleWorlds.CampaignSystem.GameState.InventoryState>();
            inventoryState.InventoryLogic = inventoryLogic;
            Game.Current.GameStateManager.PushState(inventoryState, 0);

            FileLogger.Log("Pushed InventoryState");

            FileLogger.Log("Pushed InventoryState");
        }

        public void OnInventoryComplete()
        {
            FileLogger.Log("OnInventoryComplete called.");

            // CRITICAL: Read from CharacterObject.PlayerCharacter (same as what we used in OpenInventory)
            // NOT Hero.MainHero - these are DIFFERENT objects!
            var playerCharacter = CharacterObject.PlayerCharacter;

            // Restore name
            if (_originalCharacterName != null)
            {
                if (GameApiWrapper.TrySetName(playerCharacter, _originalCharacterName))
                {
                    FileLogger.Log($"Restored player name to {_originalCharacterName}");
                }
                _originalCharacterName = null;
            }

            // Restore Hero name
            if (Hero.MainHero != null && _originalHeroName != null && _originalHeroFirstName != null)
            {
                Hero.MainHero.SetName(_originalHeroName, _originalHeroFirstName);
                FileLogger.Log($"Restored Hero.MainHero name to {_originalHeroName}");
                _originalHeroName = null;
                _originalHeroFirstName = null;
            }

            var equipment = _isEditingCivilianEquipment
                ? playerCharacter.FirstCivilianEquipment
                : playerCharacter.Equipment;


            // Handle Template Save
            if (_currentEditingTemplate != null)
            {
                _currentEditingTemplate.Items.Clear();
                FileLogger.Log($"Saving to template: {_currentEditingTemplate.Id}");

                for (int i = 0; i < 12; i++)
                {
                    var eqElement = equipment[i];
                    if (!eqElement.IsEmpty && eqElement.Item != null)
                    {
                        string slotName = ((EquipmentIndex)i).ToString();
                        _currentEditingTemplate.Items[slotName] = eqElement.Item.StringId;
                        FileLogger.Log($"  Added: {slotName} = {eqElement.Item.StringId}");
                    }
                }

                FileLogger.Log($"Template {_currentEditingTemplate.Id} now has {_currentEditingTemplate.Items.Count} items.");
            }
            // Handle Wanderer Save


            // Refresh UI
            if (_currentEditingWanderer != null)
            {
                _form.Invoke(new Action(() => _form.SelectWanderer(_currentEditingWanderer)));
            }
            else
            {
                // Template edited
                _form.Invoke(new Action(() => _form.RefreshTemplateList()));
            }

            _currentEditingTemplate = null;
            _currentEditingWanderer = null;

            OnScreenActivated();
        }


        private EquipmentIndex ParseEquipmentIndex(string key)
        {
            if (Enum.TryParse<EquipmentIndex>(key, true, out var result))
                return result;
            // Fallback for integers if stored that way
            if (int.TryParse(key, out int intVal))
                return (EquipmentIndex)intVal;
            return EquipmentIndex.None;
        }

        private CharacterObject CreateDummyCharacter(WandererDefinition wanderer)
        {
            // Create a basic CharacterObject via ObjectManager
            CharacterObject character = MBObjectManager.Instance.CreateObject<CharacterObject>($"wanderer_temp_{Guid.NewGuid()}");

            // Populate basic data
            CultureObject culture = Game.Current.ObjectManager.GetObject<CultureObject>(wanderer.Culture) ?? Game.Current.ObjectManager.GetObject<CultureObject>("empire");

            // Set Culture using abstraction layer
            if (culture != null)
            {
                if (GameApiWrapper.TrySetCulture(character, culture))
                    FileLogger.Log("Set Culture via GameApiWrapper");
                else
                    FileLogger.Log("Failed to set Culture via GameApiWrapper");
            }

            // 2. Set Race to 0 (Human) using abstraction layer
            const int HUMAN_RACE_ID = 0;
            if (GameApiWrapper.TrySetRace(character, HUMAN_RACE_ID))
                FileLogger.Log($"Race set to {character.Race} via GameApiWrapper");
            else
                FileLogger.Log("Failed to set Race via GameApiWrapper");

            // 3. Set other properties
            character.Age = wanderer.Age;
            character.IsFemale = wanderer.IsFemale;
            FileLogger.Log($"Set IsFemale to {wanderer.IsFemale}");

            // 4. Set Name using abstraction layer
            var nameText = new TaleWorlds.Localization.TextObject(wanderer.Name);
            if (GameApiWrapper.TrySetName(character, nameText))
                FileLogger.Log("Set Name via GameApiWrapper");
            else
                FileLogger.Log("Failed to set Name via GameApiWrapper");

            // 5. BodyProperties - Must create BodyPropertyRange first since it's null on new CharacterObject
            // BodyPropertyRange = MBObjectManager.Instance.RegisterPresumedObject<MBBodyProperty>(new MBBodyProperty(stringId))
            // Then call BodyPropertyRange.Init(bodyProps, bodyProps)
            try
            {
                TaleWorlds.Core.BodyProperties bodyPropsToSet;

                if (!string.IsNullOrEmpty(wanderer.BodyPropertiesString) && TaleWorlds.Core.BodyProperties.FromString(wanderer.BodyPropertiesString, out var existingBp))
                {
                    bodyPropsToSet = existingBp;
                    FileLogger.Log($"CreateDummyCharacter: Loaded existing BodyProperties from string");
                }
                else
                {
                    // Generate random body properties
                    var mn = new TaleWorlds.Core.BodyProperties();
                    var mx = new TaleWorlds.Core.BodyProperties();

                    bodyPropsToSet = TaleWorlds.Core.FaceGen.GetRandomBodyProperties(
                         character.Race,
                         wanderer.IsFemale,
                         mn,
                         mx,
                         0, // HairCoverType.None
                         (int)DateTime.Now.Ticks,
                         "", "", "",
                         0.1f);
                    FileLogger.Log($"CreateDummyCharacter: Generated random BodyProperties for IsFemale: {wanderer.IsFemale}");
                }

                // Create BodyPropertyRange if it doesn't exist (it's null on fresh CharacterObject)
                if (character.BodyPropertyRange == null)
                {
                    FileLogger.Log($"CreateDummyCharacter: BodyPropertyRange is null, creating new MBBodyProperty...");

                    // Create the MBBodyProperty object
                    var mbBodyProperty = TaleWorlds.ObjectSystem.MBObjectManager.Instance.RegisterPresumedObject<TaleWorlds.Core.MBBodyProperty>(
                        new TaleWorlds.Core.MBBodyProperty(character.StringId));

                    // Set BodyPropertyRange via reflection (protected setter)
                    var bodyPropertyRangeProp = typeof(BasicCharacterObject).GetProperty("BodyPropertyRange",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (bodyPropertyRangeProp != null)
                    {
                        bodyPropertyRangeProp.SetValue(character, mbBodyProperty);
                        FileLogger.Log($"CreateDummyCharacter: Set BodyPropertyRange via reflection");
                    }
                }

                // Now initialize BodyPropertyRange with our body properties
                if (character.BodyPropertyRange != null)
                {
                    character.BodyPropertyRange.Init(bodyPropsToSet, bodyPropsToSet);
                    FileLogger.Log($"CreateDummyCharacter: Called BodyPropertyRange.Init() with body properties");

                    // Set race and gender
                    character.Race = 0; // Human
                    character.IsFemale = wanderer.IsFemale;

                    // Verify
                    var verifyBp = character.GetBodyPropertiesMin(false);
                    FileLogger.Log($"CreateDummyCharacter: Verification - GetBodyPropertiesMin: {verifyBp}");
                }
                else
                {
                    FileLogger.Log($"CreateDummyCharacter: ERROR - BodyPropertyRange is still null after creation attempt");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log($"Error setting BodyProperties on CharacterObject: {ex.Message}");
                FileLogger.Log($"Stack: {ex.StackTrace}");
            }

            return character;
        }

        public class FaceGenCustomFilter : IFaceGeneratorCustomFilter
        {
            // Return -1 to indicate "use all available"
            public int GetHairCount() => -1;
            public int GetBeardCount() => -1;
            public int GetFaceTextureCount() => -1;
            public int GetMouthTextureCount() => -1;
            public int GetTattooCount() => -1;
            public int GetVoiceCount() => -1;

            // CRITICAL: Return empty arrays, NOT null
            // The game's FaceGenVM calls Contains() on these, which throws on null
            public int[] GetHairIndices() => Array.Empty<int>();
            public int[] GetBeardIndices() => Array.Empty<int>();
            public int[] GetFaceTextureIndices() => Array.Empty<int>();
            public int[] GetMouthTextureIndices() => Array.Empty<int>();
            public int[] GetTattooIndices() => Array.Empty<int>();
            public int[] GetVoiceIndices() => Array.Empty<int>();

            // These are called by FaceGenVM.UpdateRaceAndGenderBasedResources()
            // Returning empty array = "allow all indices" (see game code logic)
            public int[] GetHaircutIndices(BasicCharacterObject character) => Array.Empty<int>();
            public int[] GetFacialHairIndices(BasicCharacterObject character) => Array.Empty<int>();

            public FaceGeneratorStage[] GetAvailableStages()
            {
                return new FaceGeneratorStage[] {
                    FaceGeneratorStage.Body,
                    FaceGeneratorStage.Face,
                    FaceGeneratorStage.Hair,
                    FaceGeneratorStage.Eyes
                };
            }
        }

        private void HandleSave(WandererProject project)
        {
            try
            {
                // Create save directory if it doesn't exist
                string saveDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Mount and Blade II Bannerlord",
                    "WandererCreator"
                );
                Directory.CreateDirectory(saveDir);

                // Save as JSON (with .wcproj extension)
                string fileName = SanitizeFileName(project.ProjectName) + ".wcproj";
                string savePath = Path.Combine(saveDir, fileName);

                string json = Newtonsoft.Json.JsonConvert.SerializeObject(project, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(savePath, json);

                MessageBox.Show($"Project saved to:\n{savePath}", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save Failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string SanitizeFileName(string name)
        {
            // Remove invalid file name characters
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        private void HandleExportMod(WandererProject project)
        {
            _form?.Invoke(new Action(() =>
            {
                // Validate project before export
                var validationResult = ValidateProjectForExport(project);
                if (!validationResult.IsValid)
                {
                    MessageBox.Show($"Export Failed - Missing Required Fields:\n\n{validationResult.ErrorMessage}", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Show warnings if any
                if (!string.IsNullOrEmpty(validationResult.WarningMessage))
                {
                    var result = MessageBox.Show($"Warnings:\n\n{validationResult.WarningMessage}\n\nDo you still want to continue with the export?", "Export Warnings", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (result != DialogResult.Yes) return;
                }

                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Select Export Destination (e.g. Game's Modules Folder)";

                    string potentialModules = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "../../Modules"));
                    if (Directory.Exists(potentialModules))
                        fbd.SelectedPath = potentialModules;
                    else if (Directory.Exists(Environment.CurrentDirectory))
                        fbd.SelectedPath = Environment.CurrentDirectory;

                    if (fbd.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        try
                        {
                            string path = ModExporter.Export(project, fbd.SelectedPath);
                            MessageBox.Show($"Mod Exported to: {path}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            System.Diagnostics.Process.Start("explorer.exe", path);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Export Failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }));
        }

        private (bool IsValid, string ErrorMessage, string WarningMessage) ValidateProjectForExport(WandererProject project)
        {
            var errors = new List<string>();
            var wandererWarnings = new Dictionary<string, List<string>>();

            if (project.Wanderers.Count == 0)
            {
                errors.Add("No wanderers defined in the project.");
            }

            foreach (var w in project.Wanderers)
            {
                string wandererName = w.Name ?? "(unnamed)";
                var currentWarnings = new List<string>();

                if (string.IsNullOrWhiteSpace(w.Id))
                    errors.Add($"• {wandererName}: Missing ID");

                if (string.IsNullOrWhiteSpace(w.Name))
                    errors.Add($"• (unnamed): Missing Name");

                bool hasBodyTemplate = !string.IsNullOrEmpty(w.BodyPropertiesTemplateId) &&
                    project.SharedBodyPropertiesTemplates.Any(t => t.Id == w.BodyPropertiesTemplateId &&
                        !string.IsNullOrEmpty(t.BodyPropertiesString));
                bool hasDirectBody = !string.IsNullOrEmpty(w.BodyPropertiesString);

                if (!hasBodyTemplate && !hasDirectBody)
                    errors.Add($"• {wandererName}: Missing Body Properties (Set or Create a body template)");

                // Check for localization placeholder IDs ({=!} pattern)
                var dialogsWithPlaceholder = new List<string>();
                void CheckDialogLocalization(string dialogName, string dialogText)
                {
                    if (!string.IsNullOrEmpty(dialogText) && dialogText.Contains("{=!}"))
                        dialogsWithPlaceholder.Add(dialogName);
                }
                CheckDialogLocalization("Intro", w.Dialogs.Intro);
                CheckDialogLocalization("LifeStory", w.Dialogs.LifeStory);
                CheckDialogLocalization("LifeStoryB", w.Dialogs.LifeStoryB);
                CheckDialogLocalization("LifeStoryC", w.Dialogs.LifeStoryC);
                CheckDialogLocalization("Recruitment", w.Dialogs.Recruitment);
                CheckDialogLocalization("GenericBackstory", w.Dialogs.GenericBackstory);
                CheckDialogLocalization("Response1", w.Dialogs.Response1);
                CheckDialogLocalization("Response2", w.Dialogs.Response2);

                if (dialogsWithPlaceholder.Count > 0)
                {
                    currentWarnings.Add($"  ⚠ Placeholder localization IDs ({{=!}}) in: {string.Join(", ", dialogsWithPlaceholder)} — Modders cannot create translations!");
                }

                // Check equipment sets - provide specific warnings for civilian vs battle
                bool hasCivilian = w.CivilianTemplateIds != null && w.CivilianTemplateIds.Count > 0;
                bool hasBattle = w.BattleTemplateIds != null && w.BattleTemplateIds.Count > 0;

                if (!hasCivilian && !hasBattle)
                {
                    currentWarnings.Add("  ⚠ Missing both civilian and battle equipment sets — Default game template will be used");
                }
                else if (!hasCivilian)
                {
                    currentWarnings.Add("  ⚠ Missing civilian equipment set — Default game template will be used");
                }
                else if (!hasBattle)
                {
                    currentWarnings.Add("  ⚠ Missing battle equipment set — Default game template will be used");
                }

                if (currentWarnings.Count > 0)
                {
                    wandererWarnings[wandererName] = currentWarnings;
                }
            }

            // Format errors
            string errorMsg = errors.Count > 0 ? string.Join("\n", errors) : "";

            // Format warnings with grouping by wanderer
            var warnBuilder = new System.Text.StringBuilder();
            foreach (var kvp in wandererWarnings)
            {
                warnBuilder.AppendLine($"► {kvp.Key}:");
                foreach (var warning in kvp.Value)
                {
                    warnBuilder.AppendLine(warning);
                }
                warnBuilder.AppendLine(); // Empty line between wanderers
            }
            string warnMsg = warnBuilder.ToString().TrimEnd();

            return (errors.Count == 0, errorMsg, warnMsg);
        }
    }
}
