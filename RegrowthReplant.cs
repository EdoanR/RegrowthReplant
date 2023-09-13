using Microsoft.Xna.Framework;
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
            On_Player.PlaceThing_Tiles_BlockPlacementForAssortedThings += On_Player_PlaceThing_Tiles_BlockPlacementForAssortedThings;
            On_WorldGen.KillTile_GetItemDrops += OnWorldKilledTileDrops;
            On_Player.ItemCheck_UseMiningTools_ActuallyUseMiningTool += OnPlayerMiningToolUse;
        }

        private void OnPlayerMiningToolUse(On_Player.orig_ItemCheck_UseMiningTools_ActuallyUseMiningTool orig, Player self, Item sItem, out bool canHitWalls, int x, int y)
        {
            // Check if should replant gemcorns.

            var config = ModContent.GetInstance<RegrowthReplantServerConfig>();
            if (!config.ReplantGemcorns)
            {
                orig.Invoke(self, sItem, out canHitWalls, x, y);
                return;
            }

            Tile tile = Main.tile[x, y];
            int cachedTileType = tile.TileType;

            WorldGen.GetTreeBottom(x, y, out var treeX, out var treeY);

            orig.Invoke(self, sItem, out canHitWalls, x, y);

            if (x != treeX || y != treeY - 1) return; // only try to replant in the tree bottom.
            if (tile.TileType != 0) return; // only if tile was removed.

            var player = GetPlayerForTile(x, y);
            var shouldReplantGemcorn = ShouldReplantGemcorn(cachedTileType, player, config);

            if (shouldReplantGemcorn)
            {
                var gemSeed = GetSeedItemFromGemTile(cachedTileType);

                int gemStyle = GetGemTreeStyleFromTile(cachedTileType);
                if (gemStyle == -1)
                {
                    Logger.Warn($"Could not get gem tree style from tile type {cachedTileType}");
                    return;
                }

                if (config.AxeUseSeedFromInventory)
                {
                    var itemConsumed = player.ConsumeItem(gemSeed, includeVoidBag: true);
                    if (!itemConsumed) return;
                }

                TryReplatingGemTree(self, x, y, gemStyle);
            }
        }

        private static void TryReplatingGemTree(Player player, int x, int y, int gemStyle = 0)
        {
            // This is a copy from terraria code, I needed to use but it was privated.

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

        private void OnWorldKilledTileDrops(Terraria.On_WorldGen.orig_KillTile_GetItemDrops orig, int x, int y, Terraria.Tile tileCache, out int dropItem, out int dropItemStack, out int secondaryItem, out int secondaryItemStack, bool includeLargeObjectDrops)
        {
            // Change the amount of seeds that herbs can drop.
            var config = ModContent.GetInstance<RegrowthReplantServerConfig>();

            secondaryItem = 0;
            secondaryItemStack = 1;

            var player = GetPlayerForTile(x, y);
            bool shouldRemoveHerbSeed = ShouldReplantHerb(tileCache, player, config);

            if (!shouldRemoveHerbSeed)
            {
                orig.Invoke(x, y, tileCache, out dropItem, out dropItemStack, out secondaryItem, out secondaryItemStack, includeLargeObjectDrops);
                return;
            }

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

        private bool On_Player_PlaceThing_Tiles_BlockPlacementForAssortedThings(Terraria.On_Player.orig_PlaceThing_Tiles_BlockPlacementForAssortedThings orig, Terraria.Player self, bool canPlace)
        {
            // Change the effect of the Staff/Axe of Regrowth on herbs.
            var config = ModContent.GetInstance<RegrowthReplantServerConfig>();
            if (!config.ReplantHerbs)
            {
                return orig.Invoke(self, canPlace);
            }

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

            var shouldPlace = ShouldReplantHerb(tile, self, config);

            // After this Invoke the tile might be null, because it might be destroyed.
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

        public static bool ShouldReplantHerb(Tile tile, Player player, RegrowthReplantServerConfig config)
        {
            if (tile == null) return false;
            if (player == null) return false;
            if (!config.ReplantHerbs) return false;

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

        public static bool ShouldReplantGemcorn(int tileType, Player player, RegrowthReplantServerConfig config)
        {
            if (player == null) return false;
            if (!config.ReplantGemcorns) return false;

            if (player.inventory[player.selectedItem].type != ItemID.AcornAxe) return false;
            if (!TileID.Sets.CountsAsGemTree[tileType]) return false;

            return true;
        }

        // This is the same as the Terraria.Worldgen.GetPlayerForTile, but inaccessible for being private.
        private static Player GetPlayerForTile(int x, int y) => Main.player[Player.FindClosest(new Vector2(x, y) * 16f, 16, 16)];

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

        public static int GetSeedItemFromGemTile(int tileType)
        {
            switch (tileType)
            {
                case TileID.TreeTopaz: return ItemID.GemTreeTopazSeed;
                case TileID.TreeAmethyst: return ItemID.GemTreeAmethystSeed;
                case TileID.TreeSapphire: return ItemID.GemTreeSapphireSeed;
                case TileID.TreeEmerald: return ItemID.GemTreeEmeraldSeed;
                case TileID.TreeRuby: return ItemID.GemTreeRubySeed;
                case TileID.TreeDiamond: return ItemID.GemTreeDiamondSeed;
                case TileID.TreeAmber: return ItemID.GemTreeAmberSeed;
                default: return -1;
            }
        }

        public static bool IsPlayerLocalServerOwner(Player player)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                return Netplay.Connection.Socket.GetRemoteAddress().IsLocalHost();
            }
            for (int plr = 0; plr < 255; plr++)
            {
                if (Netplay.Clients[plr].State == 10 && Main.player[plr] == player && Netplay.Clients[plr].Socket.GetRemoteAddress().IsLocalHost())
                {
                    return true;
                }
            }
            return false;
        }
    }
}
