using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;

namespace TownOfUsDraft
{
    public sealed class DraftOptions : AbstractOptionGroup
    {
        public override MenuCategory ParentMenu => MenuCategory.Roles; // Spróbujmy w Roles (bezpieczniej)
        public override string GroupName => "Draft Mode";
        public override uint GroupPriority => 0; // Najwyższy priorytet = na samej górze zakładki

        public ModdedToggleOption EnableDraftMode { get; set; } = new("Enable Draft Mode", true);

        public ModdedNumberOption DraftTimeout { get; set; } = new("Draft Timeout", 20f, 10f, 60f, 5f, MiraNumberSuffixes.Seconds);
    }
}
