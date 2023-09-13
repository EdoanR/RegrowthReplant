using Terraria.Localization;
using Terraria;
using Terraria.ModLoader.Config;
using System.ComponentModel;

namespace RegrowthReplant
{
    public class RegrowthReplantServerConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ServerSide;

        [DefaultValue(true)]
        public bool ReplantHerbs = true;

        [Header("AxeOfRegrowthSection")]
        [DefaultValue(true)]
        public bool ReplantGemcorns = true;

        [DefaultValue(true)]
        public bool AxeUseSeedFromInventory = true;

        public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref string message)
        {
            if (!RegrowthReplant.IsPlayerLocalServerOwner(Main.player[whoAmI]))
            {
                message = Language.GetTextValue("Mods.RegrowthReplant.Configs.OnlyOwner");
                return false;
            }
            return base.AcceptClientChanges(pendingConfig, whoAmI, ref message);
        }
    }
}
