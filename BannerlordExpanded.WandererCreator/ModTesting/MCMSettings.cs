using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using System;
using BannerlordExpanded.WandererCreator.VersionCompatibility;

namespace BannerlordExpanded.WandererCreator.ModTesting
{
    public sealed class MCMSettings : AttributeGlobalSettings<MCMSettings>
    {
        public override string Id => "WandererCreatorSettings";
        public override string DisplayName => "Wanderer Creator";
        public override string FolderName => "WandererCreator";
        public override string FormatType => "json";

        [SettingPropertyButton("Reveal All Wanderers",
            HintText = "Makes all wanderers visible in the encyclopedia for testing. Must be in a campaign.",
            Content = "Reveal",
            Order = 0,
            RequireRestart = false)]
        [SettingPropertyGroup("Testing Tools")]
        public Action RevealAllWanderers { get; set; } = GameApiWrapper.RevealAllWanderers;
    }
}
