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

                    // Face - resolve body properties from template or direct values
                    string bodyPropsMin = "";
                    string bodyPropsMax = "";

                    // Check if wanderer uses a body template
                    if (!string.IsNullOrEmpty(w.BodyPropertiesTemplateId))
                    {
                        var bodyTemplate = project.SharedBodyPropertiesTemplates.FirstOrDefault(t => t.Id == w.BodyPropertiesTemplateId);
                        if (bodyTemplate != null)
                        {
                            bodyPropsMin = bodyTemplate.BodyPropertiesString;
                            bodyPropsMax = !string.IsNullOrEmpty(bodyTemplate.BodyPropertiesMaxString)
                                ? bodyTemplate.BodyPropertiesMaxString
                                : bodyTemplate.BodyPropertiesString;
                        }
                    }
                    else
                    {
                        // Use direct values
                        bodyPropsMin = w.BodyPropertiesString;
                        bodyPropsMax = !string.IsNullOrEmpty(w.BodyPropertiesMaxString)
                            ? w.BodyPropertiesMaxString
                            : w.BodyPropertiesString;
                    }

                    writer.WriteStartElement("face");
                    if (!string.IsNullOrEmpty(bodyPropsMin))
                    {
                        // Write BodyProperties element
                        WriteBodyPropertiesElement(writer, "BodyProperties", bodyPropsMin, w.Age);
                        // Write BodyPropertiesMax element
                        WriteBodyPropertiesElement(writer, "BodyPropertiesMax", bodyPropsMax, w.Age);
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
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

            // Track all localization strings for the language file
            var localizationStrings = new List<(string id, string text)>();

            // Helper: Parse {=Key}Text format and extract key and text
            // Returns (key, text). If no key found, generates one and returns (generatedKey, originalText)
            (string key, string text) ParseLocalizationString(string input, string autoKeyPrefix)
            {
                if (string.IsNullOrEmpty(input)) return (autoKeyPrefix, "...");

                // Pattern: {=key}text
                var match = System.Text.RegularExpressions.Regex.Match(input, @"^\{=([^}]+)\}(.*)$");
                if (match.Success)
                {
                    return (match.Groups[1].Value, match.Groups[2].Value.Trim());
                }
                // No localization key - generate one automatically
                return (autoKeyPrefix, input);
            }

            using (var writer = XmlWriter.Create(Path.Combine(dataDir, "wanderer_strings.xml"), new XmlWriterSettings { Indent = true }))
            {
                writer.WriteStartElement("strings");
                foreach (var w in project.Wanderers)
                {
                    void WriteDialogString(string dialogType, string rawText)
                    {
                        string autoKey = $"str_{project.ModuleId}_{w.Id}_{dialogType}";
                        var (key, text) = ParseLocalizationString(rawText, autoKey);

                        // Add to localization list
                        localizationStrings.Add((key, text));

                        // Write to strings.xml with the full {=key}text format (as game expects)
                        writer.WriteStartElement("string");
                        writer.WriteAttributeString("id", dialogType + "." + w.Id);
                        writer.WriteAttributeString("text", "{=" + key + "}" + text);
                        writer.WriteEndElement();
                    }

                    WriteDialogString("prebackstory", w.Dialogs.Intro);
                    WriteDialogString("backstory_a", w.Dialogs.LifeStory);
                    WriteDialogString("backstory_b", w.Dialogs.LifeStoryB);
                    WriteDialogString("backstory_c", w.Dialogs.LifeStoryC);
                    WriteDialogString("backstory_d", w.Dialogs.Recruitment);
                    WriteDialogString("generic_backstory", w.Dialogs.GenericBackstory);
                    WriteDialogString("response_1", w.Dialogs.Response1);
                    WriteDialogString("response_2", w.Dialogs.Response2);
                }
                writer.WriteEndElement();
            }

            // Generate the language localization file
            CreateLanguageFile(dataDir, localizationStrings);
        }

        /// <summary>
        /// Creates the standard Bannerlord language localization XML file.
        /// </summary>
        private static void CreateLanguageFile(string dataDir, List<(string id, string text)> strings)
        {
            string langDir = Path.Combine(dataDir, "Languages");
            if (!Directory.Exists(langDir)) Directory.CreateDirectory(langDir);

            using (var writer = XmlWriter.Create(Path.Combine(langDir, "std_module_strings_xml.xml"), new XmlWriterSettings { Indent = true }))
            {
                writer.WriteStartElement("base");
                writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
                writer.WriteAttributeString("xmlns", "xsd", null, "http://www.w3.org/2001/XMLSchema");
                writer.WriteAttributeString("type", "string");

                writer.WriteStartElement("tags");
                writer.WriteStartElement("tag");
                writer.WriteAttributeString("language", "English");
                writer.WriteEndElement(); // tag
                writer.WriteEndElement(); // tags

                writer.WriteStartElement("strings");
                foreach (var (id, text) in strings)
                {
                    writer.WriteStartElement("string");
                    writer.WriteAttributeString("id", id);
                    writer.WriteAttributeString("text", text);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement(); // strings

                writer.WriteEndElement(); // base
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

        /// <summary>
        /// Writes a BodyProperties or BodyPropertiesMax XML element with all required attributes.
        /// Parses the body string to extract version, age, weight, build, and key values.
        /// </summary>
        private static void WriteBodyPropertiesElement(XmlWriter writer, string elementName, string bodyPropertiesString, int wandererAge)
        {
            // BodyProperties string format from game: 
            // "<BodyProperties version=\"4\" age=\"22.79\" weight=\"0.5\" build=\"0.5\" key=\"0000040C80004001...\" />"
            // We need to parse this and output individual attributes

            writer.WriteStartElement(elementName);

            // Try to parse the body properties string if it looks like XML
            if (bodyPropertiesString.Trim().StartsWith("<"))
            {
                try
                {
                    // Parse the XML snippet to extract attributes
                    var doc = new System.Xml.XmlDocument();
                    doc.LoadXml(bodyPropertiesString);
                    var root = doc.DocumentElement;

                    if (root != null)
                    {
                        // Copy all attributes from the parsed element
                        foreach (System.Xml.XmlAttribute attr in root.Attributes)
                        {
                            writer.WriteAttributeString(attr.Name, attr.Value);
                        }
                    }
                }
                catch
                {
                    // If parsing fails, write as key with defaults
                    writer.WriteAttributeString("version", "4");
                    writer.WriteAttributeString("age", wandererAge.ToString("F2"));
                    writer.WriteAttributeString("weight", "0.5");
                    writer.WriteAttributeString("build", "0.5");
                    writer.WriteAttributeString("key", bodyPropertiesString);
                }
            }
            else
            {
                // Plain key string - wrap with default attributes
                writer.WriteAttributeString("version", "4");
                writer.WriteAttributeString("age", wandererAge.ToString("F2"));
                writer.WriteAttributeString("weight", "0.5");
                writer.WriteAttributeString("build", "0.5");
                writer.WriteAttributeString("key", bodyPropertiesString);
            }

            writer.WriteEndElement();
        }
    }
}
