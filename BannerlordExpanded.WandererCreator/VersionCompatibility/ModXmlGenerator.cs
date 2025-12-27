using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using BannerlordExpanded.WandererCreator.Models;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace BannerlordExpanded.WandererCreator.VersionCompatibility
{
    public static class ModXmlGenerator
    {
        public static void CreateWanderersXml(string baseDir, WandererProject project)
        {
            string dataDir = Path.Combine(baseDir, "ModuleData");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

            using (var writer = XmlWriter.Create(Path.Combine(dataDir, "wanderers.xml"), new XmlWriterSettings { Indent = true }))
            {
                writer.WriteStartElement("NPCCharacters");
                foreach (var w in project.Wanderers)
                {
                    writer.WriteStartElement("NPCCharacter");
                    writer.WriteAttributeString("id", w.Id);
                    writer.WriteAttributeString("name", w.Name);
                    writer.WriteAttributeString("voice", !string.IsNullOrEmpty(w.Voice) ? w.Voice : (w.IsFemale ? "softspoken" : "earnest"));
                    writer.WriteAttributeString("age", w.Age.ToString());
                    writer.WriteAttributeString("default_group", "Infantry");
                    writer.WriteAttributeString("is_template", "true");
                    writer.WriteAttributeString("is_hero", "false");
                    writer.WriteAttributeString("culture", "Culture." + w.Culture.ToLower());
                    writer.WriteAttributeString("occupation", "Wanderer");

                    // Face
                    writer.WriteStartElement("face");
                    if (!string.IsNullOrEmpty(w.BodyPropertiesString))
                    {
                        writer.WriteStartElement("face_key_template");
                        writer.WriteAttributeString("value", w.BodyPropertiesString);
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();

                    // Traits (from Template)
                    if (!string.IsNullOrEmpty(w.TraitTemplate))
                    {
                        var template = project.SharedTraitTemplates.FirstOrDefault(t => t.Id == w.TraitTemplate);
                        if (template != null && template.Traits.Count > 0)
                        {
                            writer.WriteStartElement("Traits");
                            foreach (var trait in template.Traits)
                            {
                                writer.WriteStartElement("Trait");
                                writer.WriteAttributeString("id", trait.Key);
                                writer.WriteAttributeString("value", trait.Value.ToString());
                                writer.WriteEndElement();
                            }
                            writer.WriteEndElement();
                        }
                    }

                    // Skills
                    if (!string.IsNullOrEmpty(w.SkillTemplate))
                    {
                        // Resolve from Shared Skill Templates
                        string cleanId = w.SkillTemplate;
                        var tmpl = project.SharedSkillTemplates.FirstOrDefault(x => x.Id == cleanId);
                        if (tmpl != null && tmpl.Skills.Count > 0)
                        {
                            writer.WriteStartElement("skills");
                            foreach (var skill in tmpl.Skills)
                            {
                                writer.WriteStartElement("skill");
                                writer.WriteAttributeString("id", skill.Key);
                                writer.WriteAttributeString("value", skill.Value.ToString());
                                writer.WriteEndElement();
                            }
                            writer.WriteEndElement();
                        }
                    }

                    // Equipments Linkage
                    writer.WriteStartElement("Equipments");

                    // Civilian
                    if (w.CivilianTemplateIds.Any())
                    {
                        foreach (var id in w.CivilianTemplateIds)
                        {
                            writer.WriteStartElement("EquipmentSet");
                            writer.WriteAttributeString("id", id);
                            writer.WriteAttributeString("civilian", "true");
                            writer.WriteEndElement();
                        }
                    }
                    else
                    {
                        // Default Game Template
                        string defId = GameApiWrapper.DefaultEquipmentTemplateId;
                        if (MBObjectManager.Instance.GetObject<MBEquipmentRoster>(defId) == null)
                        {
                            MessageBox.Show($"Warning: Default Civilian Template '{defId}' not found in game data! Wanderer '{w.Name}' might spawn without equipment.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        writer.WriteStartElement("EquipmentSet");
                        writer.WriteAttributeString("id", defId);
                        writer.WriteAttributeString("civilian", "true");
                        writer.WriteEndElement();
                    }


                    // Battle (Templates)
                    if (w.BattleTemplateIds != null && w.BattleTemplateIds.Any())
                    {
                        foreach (var id in w.BattleTemplateIds)
                        {
                            writer.WriteStartElement("EquipmentSet");
                            writer.WriteAttributeString("id", id);
                            writer.WriteEndElement();
                        }
                    }
                    else
                    {
                        // Default Game Template
                        string defId = GameApiWrapper.DefaultEquipmentTemplateId;
                        if (MBObjectManager.Instance.GetObject<MBEquipmentRoster>(defId) == null)
                        {
                            MessageBox.Show($"Warning: Default Battle Template '{defId}' not found in game data! Wanderer '{w.Name}' might spawn without equipment.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        writer.WriteStartElement("EquipmentSet");
                        writer.WriteAttributeString("id", defId);
                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement(); // Equipments

                    writer.WriteEndElement(); // NPCCharacter
                }
                writer.WriteEndElement(); // NPCCharacters
            }
        }

        public static void CreateEquipmentXml(string baseDir, WandererProject project)
        {
            string dataDir = Path.Combine(baseDir, "ModuleData");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

            using (var writer = XmlWriter.Create(Path.Combine(dataDir, "wanderer_equipment.xml"), new XmlWriterSettings { Indent = true }))
            {
                writer.WriteStartElement("EquipmentRosters");

                // 1. Export Shared Templates
                if (project.SharedTemplates != null)
                {
                    foreach (var tmpl in project.SharedTemplates)
                    {
                        WriteEquipmentSet(writer, tmpl.Id, tmpl.Items, tmpl.IsCivilian);
                    }
                }
                writer.WriteEndElement();
            }
        }

        public static void CreateStringsXml(string baseDir, WandererProject project)
        {
            string dataDir = Path.Combine(baseDir, "ModuleData");
            using (var writer = XmlWriter.Create(Path.Combine(dataDir, "wanderer_strings.xml"), new XmlWriterSettings { Indent = true }))
            {
                writer.WriteStartElement("strings");
                foreach (var w in project.Wanderers)
                {
                    void WriteString(string id, string text)
                    {
                        if (string.IsNullOrEmpty(text)) text = "{=!}...";
                        writer.WriteStartElement("string");
                        writer.WriteAttributeString("id", id + "." + w.Id);
                        writer.WriteAttributeString("text", text);
                        writer.WriteEndElement();
                    }

                    WriteString("prebackstory", w.Dialogs.Intro);
                    WriteString("backstory_a", w.Dialogs.LifeStory);
                    WriteString("backstory_b", w.Dialogs.LifeStoryB);
                    WriteString("backstory_c", w.Dialogs.LifeStoryC);
                    WriteString("backstory_d", w.Dialogs.Recruitment);
                    WriteString("generic_backstory", w.Dialogs.GenericBackstory);

                    // Responses
                    WriteString("response_1", w.Dialogs.Response1);
                    WriteString("response_2", w.Dialogs.Response2);
                }
                writer.WriteEndElement();
            }
        }

        private static void WriteEquipmentSet(XmlWriter writer, string id, Dictionary<string, string> equipment, bool civilian)
        {
            writer.WriteStartElement("EquipmentRoster");
            writer.WriteAttributeString("id", id);
            if (civilian) writer.WriteAttributeString("civilian", "true");

            foreach (var kvp in equipment)
            {
                if (string.IsNullOrEmpty(kvp.Value)) continue;

                writer.WriteStartElement("Equipment");
                writer.WriteAttributeString("slot", kvp.Key);
                writer.WriteAttributeString("id", kvp.Value);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
    }
}
