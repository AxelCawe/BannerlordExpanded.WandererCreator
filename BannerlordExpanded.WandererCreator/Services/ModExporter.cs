using System;
using System.IO;
using System.Text;
using System.Xml;
using BannerlordExpanded.WandererCreator.Models;
using TaleWorlds.Library;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.ObjectSystem;
using TaleWorlds.Core;
using System.Windows.Forms;

namespace BannerlordExpanded.WandererCreator.Services
{
    using BannerlordExpanded.WandererCreator.VersionCompatibility;
    public class ModExporter
    {
        public static string Export(WandererProject project, string outputFolder)
        {
            string baseDir = Path.Combine(outputFolder, project.ModuleId);
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, true);

            // Copy Template
            string templateDir = Path.Combine(Environment.CurrentDirectory, "_Module", "ModuleData", "CompanionModTemplate");
            if (Directory.Exists(templateDir))
            {
                CopyDirectory(templateDir, baseDir);
            }
            else
            {
                Directory.CreateDirectory(baseDir);
            }

            // Process SubModule.xml
            SubModuleGenerator.Generate(baseDir, project);

            // Generate XMLs
            ModXmlGenerator.CreateEquipmentXml(baseDir, project);
            ModXmlGenerator.CreateStringsXml(baseDir, project);
            ModXmlGenerator.CreateWanderersXml(baseDir, project);

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
    }
}
