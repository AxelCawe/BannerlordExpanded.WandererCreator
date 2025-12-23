using System;
using System.Collections.Generic;


namespace BannerlordExpanded.WandererCreator.Models
{
    [Serializable]
    public class WandererDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "New Wanderer";
        public string Culture { get; set; } = "Empire";
        public bool IsFemale { get; set; } = false;
        public int Age { get; set; } = 22;

        // This will hold the BodyProperties code string
        public string BodyPropertiesString { get; set; } = "";

        // Dictionary of TraitId -> Value (e.g. "Honor" -> 1, "Mercy" -> -1)
        public Dictionary<string, int> Traits { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> Skills { get; set; } = new Dictionary<string, int>();

        // Custom Equipment Sets (Legacy/Override)
        public Dictionary<string, string> EquipmentBattle { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> EquipmentCivilian { get; set; } = new Dictionary<string, string>();

        // New Template System References
        public List<string> CivilianTemplateIds { get; set; } = new List<string>(); // If empty and EquipmentCivilian has items, use that?
        // Legacy support could be handled via property but for now we replace.

        public List<string> BattleTemplateIds { get; set; } = new List<string>(); // If empty, use EquipmentBattle

        // Legacy/Fallback Property
        public Dictionary<string, string> Equipment
        {
            get => EquipmentBattle;
            set => EquipmentBattle = value;
        }

        public string CivilianTemplate { get; set; } = "NPCCharacter.villager_empire";
        public string BattleTemplate { get; set; } = "NPCCharacter.villager_empire";

        // Dialogs
        public WandererDialogData Dialogs { get; set; } = new WandererDialogData();

        public WandererDefinition()
        {
        }
    }

    [Serializable]
    public class WandererDialogData
    {
        // 1. prebackstory
        public string Intro { get; set; } = "{=!}Hello there. I am a wanderer.";
        // 2. backstory_a
        public string LifeStory { get; set; } = "{=!}I have seen many things...";
        // 3. backstory_b
        public string LifeStoryB { get; set; } = "{=!}...";
        // 4. backstory_c
        public string LifeStoryC { get; set; } = "{=!}...";
        // 5. backstory_d (The proposal)
        public string Recruitment { get; set; } = "{=!}I can join you for a price.";

        // 6. generic_backstory (Rumor)
        public string GenericBackstory { get; set; } = "{=!}A wandering warrior.";

        // 7. response_1 (Player: "Tell me more")
        public string Response1 { get; set; } = "{=!}Most interesting. Go on.";

        // 8. response_2 (Player: "Good bye")
        public string Response2 { get; set; } = "{=!}I have heard enough.";

        public string Cost { get; set; } = "500";
    }

    [Serializable]
    public class EquipmentTemplate
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "New Template";
        public bool IsCivilian { get; set; } = false;
        public Dictionary<string, string> Items { get; set; } = new Dictionary<string, string>();
    }

    [Serializable]
    public class WandererProject
    {
        public string ProjectName { get; set; } = "MyWandererMod";
        public string ModuleId { get; set; } = "MyWandererMod";
        public string Version { get; set; } = "1.0.0";
        public List<WandererDefinition> Wanderers { get; set; } = new List<WandererDefinition>();
        public List<EquipmentTemplate> SharedTemplates { get; set; } = new List<EquipmentTemplate>();
    }
}

