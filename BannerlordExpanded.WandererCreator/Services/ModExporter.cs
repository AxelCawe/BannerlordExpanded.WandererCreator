using System;
using System.IO;
using System.Text;
using System.Xml;
using BannerlordExpanded.WandererCreator.Models;
using TaleWorlds.Library;
using System.Collections.Generic;
using System.Linq;

namespace BannerlordExpanded.WandererCreator.Services
{
    public class ModExporter
    {
        public static string Export(WandererProject project)
        {
            string baseDir = Path.Combine(Environment.CurrentDirectory, "ExportedMods", project.ModuleId);
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, true);

            // 1. Copy Template
            string templateDir = Path.Combine(Environment.CurrentDirectory, "_Module", "ModuleData", "CompanionModTemplate");
            if (Directory.Exists(templateDir))
            {
                CopyDirectory(templateDir, baseDir);
            }
            else
            {
                Directory.CreateDirectory(baseDir);
            }

            // 2. Process SubModule.xml
            ProcessSubModuleXml(baseDir, project);

            // 3. Generate XMLs
            CreateEquipmentXml(baseDir, project);
            CreateStringsXml(baseDir, project);
            CreateWanderersXml(baseDir, project);

            return baseDir;
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile);
            }
            foreach (var subdir in Directory.GetDirectories(sourceDir))
            {
                string destSubdir = Path.Combine(destDir, Path.GetFileName(subdir));
                CopyDirectory(subdir, destSubdir);
            }
        }

        private static void ProcessSubModuleXml(string baseDir, WandererProject project)
        {
            string path = Path.Combine(baseDir, "SubModule.xml");
            if (File.Exists(path))
            {
                string content = File.ReadAllText(path);
                content = content.Replace("{{ModuleId}}", project.ModuleId)
                                 .Replace("{{ModuleName}}", project.ProjectName)
                                 .Replace("{{Version}}", project.Version);

                // Inject Equipment Rosters logic
                if (!content.Contains("wanderer_equipment"))
                {
                    string eqEntry = @"
        <XmlNode>
            <XmlName id=""EquipmentRosters"" path=""wanderer_equipment""/>
            <IncludedGameTypes>
                <GameType value=""Campaign""/>
                <GameType value=""CampaignStoryMode""/>
                <GameType value=""CustomGame""/>
            </IncludedGameTypes>
        </XmlNode>";
                    content = content.Replace("</Xmls>", eqEntry + "\n            </Xmls>");
                }

                // Inject Strings logic
                if (!content.Contains("wanderer_strings"))
                {
                    string strEntry = @"
        <XmlNode>
            <XmlName id=""GameText"" path=""wanderer_strings""/>
            <IncludedGameTypes>
                <GameType value=""Campaign""/>
                <GameType value=""CampaignStoryMode""/>
                <GameType value=""CustomGame""/>
            </IncludedGameTypes>
        </XmlNode>";
                    content = content.Replace("</Xmls>", strEntry + "\n            </Xmls>");
                }

                File.WriteAllText(path, content);
            }
        }

        private static void CreateWanderersXml(string baseDir, WandererProject project)
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
                    writer.WriteAttributeString("voice", w.IsFemale ? "female" : "male");
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

                    // Traits
                    if (w.Traits != null && w.Traits.Count > 0)
                    {
                        writer.WriteStartElement("Traits");
                        foreach (var trait in w.Traits)
                        {
                            writer.WriteStartElement("Trait");
                            writer.WriteAttributeString("id", trait.Key);
                            writer.WriteAttributeString("value", trait.Value.ToString());
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
                    }

                    // Skills
                    if (w.Skills != null && w.Skills.Count > 0)
                    {
                        writer.WriteStartElement("skills");
                        foreach (var skill in w.Skills)
                        {
                            writer.WriteStartElement("skill");
                            writer.WriteAttributeString("id", skill.Key);
                            writer.WriteAttributeString("value", skill.Value.ToString());
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
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
                    else if (w.EquipmentCivilian != null && w.EquipmentCivilian.Count > 0)
                    {
                        // Fallback Custom
                        writer.WriteStartElement("EquipmentSet");
                        writer.WriteAttributeString("id", $"eq_civ_custom_{w.Id}");
                        writer.WriteAttributeString("civilian", "true");
                        writer.WriteEndElement();
                    }

                    // Battle (Templates)
                    if (w.BattleTemplateIds != null)
                    {
                        foreach (var id in w.BattleTemplateIds)
                        {
                            writer.WriteStartElement("EquipmentSet");
                            writer.WriteAttributeString("id", id);
                            writer.WriteEndElement();
                        }
                    }

                    // Battle (Custom Legacy)
                    if (w.EquipmentBattle != null && w.EquipmentBattle.Count > 0)
                    {
                        writer.WriteStartElement("EquipmentSet");
                        writer.WriteAttributeString("id", $"eq_battle_custom_{w.Id}");
                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement(); // Equipments

                    writer.WriteEndElement(); // NPCCharacter
                }
                writer.WriteEndElement(); // NPCCharacters
            }
        }

        private static void CreateEquipmentXml(string baseDir, WandererProject project)
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

                // 2. Export Custom Sets (Civilian)
                foreach (var w in project.Wanderers)
                {
                    // If no templates assigned, export custom set logic
                    if (!w.CivilianTemplateIds.Any() && w.EquipmentCivilian != null && w.EquipmentCivilian.Count > 0)
                    {
                        WriteEquipmentSet(writer, $"eq_civ_custom_{w.Id}", w.EquipmentCivilian, true);
                    }

                    // Custom Battle Set (Legacy/Manual)
                    if (w.EquipmentBattle != null && w.EquipmentBattle.Count > 0)
                    {
                        WriteEquipmentSet(writer, $"eq_battle_custom_{w.Id}", w.EquipmentBattle, false);
                    }
                }
                writer.WriteEndElement();
            }
        }

        private static void CreateStringsXml(string baseDir, WandererProject project)
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
