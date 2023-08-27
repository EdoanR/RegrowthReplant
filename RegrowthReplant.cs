using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

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

        public enum GemTreeStyle
        {
            Topaz = 0,
            Amethyst = 1,
            Sapphire = 2,
            Emerald = 3,
            Ruby = 4,
            Diamond = 5,
            Amber = 6
        }

        public override void Load()
        {
            On_Player.PlaceThing_Tiles_BlockPlacementForAssortedThings += Player_PlaceThing_Tiles_BlockPlacementForAssortedThings;
            On_WorldGen.KillTile_GetItemDrops += WorldGen_KillTile_GetItemDrops;
            On_Player.ItemCheck_UseMiningTools_ActuallyUseMiningTool += On_Player_ItemCheck_UseMiningTools_ActuallyUseMiningTool;
        }

        private void On_Player_ItemCheck_UseMiningTools_ActuallyUseMiningTool(On_Player.orig_ItemCheck_UseMiningTools_ActuallyUseMiningTool orig, Player self, Item sItem, out bool canHitWalls, int x, int y)
        {
            // Check if should replant gemcorns.

            Tile tile = Main.tile[x, y];
            int cachedTileType = tile.TileType;

            bool flag = TileID.Sets.CountsAsGemTree[tile.TileType] && Main.tileAxe[tile.TileType] && sItem.type == ItemID.AcornAxe;

            orig.Invoke(self, sItem, out canHitWalls, x, y);

            if (flag && tile.TileType == 0)
            {
                int gemStyle = GetGemTreeStyleFromTile(cachedTileType);
                if (gemStyle == -1) return;

                TryReplatingGemTree(self, x, y, gemStyle);
            }
        }

        private static int GetGemTreeStyleFromTile(int tileType)
        {
            switch (tileType)
            {
                case TileID.TreeTopaz:
                    return (int)GemTreeStyle.Topaz;
                case TileID.TreeAmethyst:
                    return (int)GemTreeStyle.Amethyst;
                case TileID.TreeSapphire:
                    return (int)GemTreeStyle.Sapphire;
                case TileID.TreeEmerald:
                    return (int)GemTreeStyle.Emerald;
                case TileID.TreeRuby:
                    return (int)GemTreeStyle.Ruby;
                case TileID.TreeDiamond:
                    return (int)GemTreeStyle.Diamond;
                case TileID.TreeAmber:
                    return (int)GemTreeStyle.Amber;
                default:
                    return -1;
            }
        }

        private static void TryReplatingGemTree(Player player, int x, int y, int gemStyle = 0)
        {
            int type = TileID.GemSaplings;
            int style = gemStyle;

            PlantLoader.CheckAndInjectModSapling(x, y, ref type, ref style);
            if (!TileObject.CanPlace(Player.tileTargetX, Player.tileTargetY, type, style, player.direction, out var objectData))
            {
                return;
            }
            bool num = TileObject.Place(objectData);
            WorldGen.SquareTileFrame(Player.tileTargetX, Player.tileTargetY);
            if (num)
            {
                TileObjectData.CallPostPlacementPlayerHook(Player.tileTargetX, Player.tileTargetY, type, style, player.direction, objectData.alternate, objectData);
                if (Main.netMode == NetmodeID.MultiplayerClient)
                {
                    NetMessage.SendObjectPlacement(-1, Player.tileTargetX, Player.tileTargetY, objectData.type, objectData.style, objectData.alternate, objectData.random, player.direction);
                }
            }
        }

        // This is to change the amount of seeds that herbs can drop.
        private void WorldGen_KillTile_GetItemDrops(Terraria.On_WorldGen.orig_KillTile_GetItemDrops orig, int x, int y, Terraria.Tile tileCache, out int dropItem, out int dropItemStack, out int secondaryItem, out int secondaryItemStack, bool includeLargeObjectDrops)
        {
            secondaryItem = 0;
            secondaryItemStack = 1;

            var player = GetPlayerForTile(x, y);
            bool shouldModifyDrop = IsHarvestable(tileCache, player);

            if (shouldModifyDrop)
            {
                int style = tileCache.TileFrameX / 18;
                dropItem = ItemID.Daybloom + style;
                int seedID = ItemID.DaybloomSeeds + style;
                if (style == (int)HerbStyle.Shiverthorn)
                {
                    dropItem = ItemID.Shiverthorn;
                    seedID = ItemID.ShiverthornSeeds;
                }

                dropItemStack = Main.rand.Next(1, 3);

                int seedAmount = Main.rand.Next(0, 5); // Original value: 1, 6
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
        private bool Player_PlaceThing_Tiles_BlockPlacementForAssortedThings(Terraria.On_Player.orig_PlaceThing_Tiles_BlockPlacementForAssortedThings orig, Terraria.Player self, bool canPlace)
        {

            int targetX = Player.tileTargetX;
            int targetY = Player.tileTargetY;

            var tile = Main.tile[targetX, targetY];
            int style = tile.TileFrameX / 18;

            bool isImmature = tile.TileType == TileID.ImmatureHerbs;
            bool isUsingRegrowthItems = self.inventory[self.selectedItem].type == ItemID.StaffofRegrowth || self.inventory[self.selectedItem].type == ItemID.AcornAxe;

            // Prevent staff of regrowth breaking immature herbs.
            // This will prevent from placing the herb and breaking it at the same time on tiles that are not pots or planter box.
            if (isImmature && isUsingRegrowthItems)
            {
                return false;
            }

            var shouldPlace = IsHarvestable(tile, self);

            // After this Invoke the tile might be null, because might be destroyed.
            // Because of that is important to get the info and check before this.
            canPlace = orig.Invoke(self, canPlace);

            if (!canPlace && shouldPlace)
            {
                // Place the seed.
                WorldGen.PlaceTile(i: targetX, j: targetY, Type: TileID.ImmatureHerbs, style: style);

                if (Main.netMode == NetmodeID.MultiplayerClient)
                {
                    NetMessage.SendTileSquare(-1, targetX, targetY, TileChangeType.None);
                }
                
            }

            return canPlace;
        }

        public static bool IsHarvestable(Terraria.Tile tile, Terraria.Player player)
        {
            if (tile == null) return false;
            if (player == null) return false;

            int style = tile.TileFrameX / 18;

            bool isUsingRegrowthItems = player.inventory[player.selectedItem].type == ItemID.StaffofRegrowth || player.inventory[player.selectedItem].type == ItemID.AcornAxe;

            // Player is not using a regrowth item.
            if (!isUsingRegrowthItems) return false;

            // Checking if the tile is a herb and if is ready to harvest.
            if (tile.TileType != TileID.MatureHerbs && tile.TileType != TileID.BloomingHerbs)
            {
                return false;
            }
            bool harvestable = WorldGen.IsHarvestableHerbWithSeed(tile.TileType, style);
            if (!harvestable) return false;

            // Can safely replant the seed!
            return true;
        }

        // This is the same as the Terraria.Worldgen.GetPlayerForTile, but inaccessible for being private.
        private static Terraria.Player GetPlayerForTile(int x, int y) => Main.player[Player.FindClosest(new Vector2(x, y) * 16f, 16, 16)];
    }
}
