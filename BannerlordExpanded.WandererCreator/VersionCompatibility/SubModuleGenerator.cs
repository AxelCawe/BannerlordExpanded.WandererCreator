using System.IO;
using System.Xml.Linq;
using System.Linq;
using BannerlordExpanded.WandererCreator.Models;

namespace BannerlordExpanded.WandererCreator.VersionCompatibility
{
    public static class SubModuleGenerator
    {
        public static void Generate(string baseDir, WandererProject project)
        {
            string path = Path.Combine(baseDir, "SubModule.xml");
            XDocument doc;

            // 1. Load or Create Document
            if (File.Exists(path))
            {
                try
                {
                    doc = XDocument.Load(path);
                }
                catch
                {
                    doc = CreateDefaultTemplate();
                }
            }
            else
            {
                doc = CreateDefaultTemplate();
            }

            // 2. Update Core Attributes (Name, Id, Version)
            var moduleNode = doc.Descendants("Module").FirstOrDefault();
            if (moduleNode != null)
            {
                SetElementAttribute(moduleNode, "Name", project.ProjectName);
                SetElementAttribute(moduleNode, "Id", project.ModuleId);

                string version = project.Version;
                // Ensure version has a prefix (e.g., v1.0.0)
                if (!version.StartsWith("v") && !version.StartsWith("e") && !version.StartsWith("b"))
                {
                    version = "v" + version;
                }
                SetElementAttribute(moduleNode, "Version", version);

                // Remove SubModules node if it exists (XML-only mod)
                var subModulesNode = moduleNode.Element("SubModules");
                if (subModulesNode != null)
                {
                    subModulesNode.Remove();
                }

                // 3. Inject XML Declarations
                var xmlsNode = moduleNode.Element("Xmls");
                if (xmlsNode == null)
                {
                    xmlsNode = new XElement("Xmls");
                    moduleNode.Add(xmlsNode);
                }

                EnsureXmlNode(xmlsNode, "NPCCharacters", "wanderers");
                EnsureXmlNode(xmlsNode, "EquipmentRosters", "wanderer_equipment");
                EnsureXmlNode(xmlsNode, "GameText", "wanderer_strings");
            }

            doc.Save(path);
        }

        private static XDocument CreateDefaultTemplate()
        {
            // Simplified template construction (XML-only, no SubModules)
            return XDocument.Parse(@"<Module>
    <Name value=""My Mod""/>
    <Id value=""MyMod""/>
    <Version value=""v1.0.0""/>
    <SingleplayerModule value=""true""/>
    <MultiplayerModule value=""false""/>
    <DependedModules>
        <DependedModule Id=""Native""/>
        <DependedModule Id=""SandBoxCore""/>
        <DependedModule Id=""Sandbox""/>
        <DependedModule Id=""StoryMode""/>
        <DependedModule Id=""CustomBattle""/>
    </DependedModules>
    <Xmls>
    </Xmls>
</Module>");
        }

        private static void SetElementAttribute(XElement parent, string elementName, string value)
        {
            var element = parent.Element(elementName);
            if (element != null)
            {
                element.SetAttributeValue("value", value);
            }
        }

        private static void EnsureXmlNode(XElement xmlsNode, string id, string path)
        {
            // Check if exists by id
            bool exists = xmlsNode.Elements("XmlNode")
                .Any(x => x.Element("XmlName")?.Attribute("id")?.Value == id);

            if (!exists)
            {
                var node = new XElement("XmlNode",
                    new XElement("XmlName", new XAttribute("id", id), new XAttribute("path", path)),
                    new XElement("IncludedGameTypes",
                        new XElement("GameType", new XAttribute("value", "Campaign")),
                        new XElement("GameType", new XAttribute("value", "CampaignStoryMode")),
                        new XElement("GameType", new XAttribute("value", "CustomGame"))
                    )
                );
                xmlsNode.Add(node);
            }
        }
    }
}
