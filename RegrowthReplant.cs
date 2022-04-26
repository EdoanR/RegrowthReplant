using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace RegrowthReplant
{
    public class RegrowthReplant : Mod
    {
        public enum HerbStyle
        {
            Daybloom = 0,
            Moonglow = 1,
            Blinkroot = 2,
            Deathweed = 3,
            Waterleaf = 4,
            Fireblossom = 5,
            Shiverthorn = 6
        }

        public override void Load()
        {
            On.Terraria.Player.PlaceThing_Tiles_BlockPlacementForAssortedThings += Player_PlaceThing_Tiles_BlockPlacementForAssortedThings;
            On.Terraria.WorldGen.KillTile_GetItemDrops += WorldGen_KillTile_GetItemDrops;

            // Immature Herb Tile ID = 82
            // Grown Herb Tile ID    = 83 (84 for Blinkroot and Shiverthorn)

        }

        // This is to change the amount of seeds that herbs can drop.
        private void WorldGen_KillTile_GetItemDrops(On.Terraria.WorldGen.orig_KillTile_GetItemDrops orig, int x, int y, Terraria.Tile tileCache, out int dropItem, out int dropItemStack, out int secondaryItem, out int secondaryItemStack, bool includeLargeObjectDrops)
        {
            secondaryItem = 0;
            secondaryItemStack = 1;

            var player = RegrowthReplant.GetPlayerForTile(x, y);
            bool shouldModifyDrop = RegrowthReplant.IsHarvestable(tileCache, player);

            if (shouldModifyDrop)
            {
                int style = tileCache.TileFrameX / 18;
                dropItem = Terraria.ID.ItemID.Daybloom + style;
                int seedID = Terraria.ID.ItemID.DaybloomSeeds + style;
                if (style == (int)HerbStyle.Shiverthorn)
                {
                    dropItem = Terraria.ID.ItemID.Shiverthorn;
                    seedID = Terraria.ID.ItemID.ShiverthornSeeds;
                }

                dropItemStack = Terraria.Main.rand.Next(1, 3);

                int seedAmount = Terraria.Main.rand.Next(0, 5); // Original value: 1, 6
                if (seedAmount > 0)
                {
                    secondaryItem = seedID;
                    secondaryItemStack = seedAmount;
                }

            }
            else
            {
                orig.Invoke(x, y, tileCache, out dropItem, out dropItemStack, out secondaryItem, out secondaryItemStack, includeLargeObjectDrops);

            }
        }

        // This is to change the effect of the Staff of Regrowth.
        private bool Player_PlaceThing_Tiles_BlockPlacementForAssortedThings(On.Terraria.Player.orig_PlaceThing_Tiles_BlockPlacementForAssortedThings orig, Terraria.Player self, bool canPlace)
        {

            int targetX = Terraria.Player.tileTargetX;
            int targetY = Terraria.Player.tileTargetY;

            var tile = Terraria.Main.tile[targetX, targetY];
            int style = tile.TileFrameX / 18;

            bool isImmature = tile.TileType == Terraria.ID.TileID.ImmatureHerbs;

            // Prevent staff of regrowth breaking immature herbs.
            // This will prevent from breaking then placing and then breaking again the herb tile on pots or herbs plaftorms.
            if (isImmature && self.inventory[self.selectedItem].type == Terraria.ID.ItemID.StaffofRegrowth)
            {
                return false;
            }

            var shouldPlace = RegrowthReplant.IsHarvestable(tile, self);

            // After this Invoke the tile might be null, because might be destroyed.
            // Because of that is important to get the info and check before this.
            canPlace = orig.Invoke(self, canPlace);

            if (!canPlace && shouldPlace)
            {
                // Place the seed.
                Terraria.WorldGen.PlaceTile(i: targetX, j: targetY, Type: Terraria.ID.TileID.ImmatureHerbs, style: style);

                if (Terraria.Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
                {
                    Terraria.NetMessage.SendTileSquare(-1, targetX, targetY, Terraria.ID.TileChangeType.None);
                }
                
            }

            return canPlace;
        }

        public static bool IsHarvestable(Terraria.Tile tile, Terraria.Player player)
        {
            if (tile == null) return false;
            if (player == null) return false;

            int style = tile.TileFrameX / 18;

            // Player is not using the Staff of Regrowth.
            if (player.inventory[player.selectedItem].type != Terraria.ID.ItemID.StaffofRegrowth) return false;

            // Checking if the tile is a herb and if is ready to harvest.
            if (tile.TileType != Terraria.ID.TileID.MatureHerbs && tile.TileType != Terraria.ID.TileID.BloomingHerbs)
            {
                return false;
            }
            bool harvestable = Terraria.WorldGen.IsHarvestableHerbWithSeed(tile.TileType, style);
            if (!harvestable) return false;

            // Can safely replant the seed!
            return true;
        }

        // This is the same as the Terraria.Worldgen.GetPlayerForTile, but inaccessible for being private.
        private static Terraria.Player GetPlayerForTile(int x, int y) => Terraria.Main.player[Terraria.Player.FindClosest(new Vector2(x, y) * 16f, 16, 16)];
    }
}