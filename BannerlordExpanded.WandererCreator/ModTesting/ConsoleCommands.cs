using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using BannerlordExpanded.WandererCreator.VersionCompatibility;

namespace BannerlordExpanded.WandererCreator.ModTesting
{
    /// <summary>
    /// Console commands for testing wanderer mods.
    /// </summary>
    public static class ConsoleCommands
    {
        private static bool IsHelpRequest(List<string> args)
        {
            return args != null && args.Count > 0 && args[0] == "?";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("reveal_all_wanderers", "wanderercreator")]
        public static string RevealAllWanderers(List<string> args)
        {
            if (IsHelpRequest(args))
                return "Reveals all wanderers in the encyclopedia so they can be found and recruited.\nUsage: wanderercreator.reveal_all_wanderers";

            if (Campaign.Current == null)
                return "Error: Must be in a campaign to use this command.";

            GameApiWrapper.RevealAllWanderers();
            return "Revealed all wanderers in encyclopedia. Check in-game message for count.";
        }
    }
}
