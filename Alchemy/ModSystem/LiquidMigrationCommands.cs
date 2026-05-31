using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class LiquidMigrationCommands : ModSystem
    {
        private const int LiquidMultiplier = 25;

        public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            IChatCommand cmd = api
                .ChatCommands.Create("alchemymigrate")
                .WithDescription(
                    "Multiply all alchemy liquid stack sizes by 25 (itemsPerLitre migration from 4 to 100). Subcommands: all, barrels [radius], blockentities [radius], players, player <name>."
                )
                .RequiresPrivilege(Privilege.controlserver);

            cmd.BeginSubCommand("all")
                .WithDescription(
                    "Migrate all block entities in loaded chunks and all online players."
                )
                .HandleWith(args =>
                {
                    int n =
                        MigrateChunkBlockEntities(api, null, 0, false)
                        + MigratePlayerInventories(api, null);
                    return TextCommandResult.Success($"Done. Modified {n} liquid stack(s).");
                })
                .EndSubCommand();

            cmd.BeginSubCommand("barrels")
                .WithDescription("Migrate only barrels. Optional radius (blocks) around you.")
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("radius"))
                .HandleWith(args =>
                {
                    if (!TryGetRadius(args, out int radius, out TextCommandResult err))
                        return err;
                    BlockPos center = radius > 0 ? args.Caller.Entity?.Pos?.AsBlockPos : null;
                    if (radius > 0 && center == null)
                        return TextCommandResult.Error(
                            "Radius requires an in-world caller (not console)."
                        );
                    int n = MigrateChunkBlockEntities(api, center, radius, barrelsOnly: true);
                    return TextCommandResult.Success($"Done. Modified {n} barrel liquid stack(s).");
                })
                .EndSubCommand();

            cmd.BeginSubCommand("blockentities")
                .WithDescription(
                    "Migrate all block entities (flasks, chests, barrels). Optional radius (blocks) around you."
                )
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("radius"))
                .HandleWith(args =>
                {
                    if (!TryGetRadius(args, out int radius, out TextCommandResult err))
                        return err;
                    BlockPos center = radius > 0 ? args.Caller.Entity?.Pos?.AsBlockPos : null;
                    if (radius > 0 && center == null)
                        return TextCommandResult.Error(
                            "Radius requires an in-world caller (not console)."
                        );
                    int n = MigrateChunkBlockEntities(api, center, radius, barrelsOnly: false);
                    return TextCommandResult.Success(
                        $"Done. Modified {n} liquid stack(s) in block entities."
                    );
                })
                .EndSubCommand();

            cmd.BeginSubCommand("players")
                .WithDescription("Migrate all online survival players' inventories.")
                .HandleWith(args =>
                {
                    int n = MigratePlayerInventories(api, null);
                    return TextCommandResult.Success(
                        $"Done. Modified {n} liquid stack(s) in player inventories."
                    );
                })
                .EndSubCommand();

            cmd.BeginSubCommand("player")
                .WithDescription("Migrate a specific online player's inventory.")
                .WithArgs(api.ChatCommands.Parsers.Word("playerName"))
                .HandleWith(args =>
                {
                    string name = args[0] as string;
                    IServerPlayer target = api
                        .World.AllOnlinePlayers.Cast<IServerPlayer>()
                        .FirstOrDefault(p =>
                            p.PlayerName.Equals(name, StringComparison.OrdinalIgnoreCase)
                        );
                    if (target == null)
                        return TextCommandResult.Error($"Player '{name}' is not online.");
                    int n = MigratePlayerInventories(api, target);
                    return TextCommandResult.Success(
                        $"Done. Modified {n} liquid stack(s) in {target.PlayerName}'s inventory."
                    );
                })
                .EndSubCommand();

            // No subcommand = migrate everything
            cmd.HandleWith(args =>
            {
                int n =
                    MigrateChunkBlockEntities(api, null, 0, false)
                    + MigratePlayerInventories(api, null);
                return TextCommandResult.Success(
                    $"Done. Modified {n} liquid stack(s) across loaded chunks and online player inventories."
                );
            });
        }

        private static bool TryGetRadius(
            TextCommandCallingArgs args,
            out int radius,
            out TextCommandResult error
        )
        {
            radius = 0;
            error = null;
            string raw = args[0] as string;
            if (string.IsNullOrEmpty(raw))
                return true;
            if (!int.TryParse(raw, out radius) || radius < 0)
            {
                error = TextCommandResult.Error(
                    $"Invalid radius '{raw}'. Must be a positive integer."
                );
                return false;
            }
            return true;
        }

        private static int MigrateChunkBlockEntities(
            ICoreServerAPI api,
            BlockPos center,
            int radius,
            bool barrelsOnly
        )
        {
            int modified = 0;
            foreach (IServerChunk chunk in api.WorldManager.AllLoadedChunks.Values)
            {
                if (chunk?.BlockEntities == null)
                    continue;
                foreach (BlockEntity be in chunk.BlockEntities.Values)
                {
                    if (be == null)
                        continue;
                    if (center != null && radius > 0 && !IsWithinRadius(be.Pos, center, radius))
                        continue;
                    try
                    {
                        modified += MigrateBlockEntity(be, barrelsOnly);
                    }
                    catch (Exception ex)
                    {
                        api.Logger.Warning(
                            $"[AlchemyMigrate] Skipped block entity at {be.Pos}: {ex.Message}"
                        );
                    }
                }
            }
            return modified;
        }

        private static int MigrateBlockEntity(
            Vintagestory.API.Common.BlockEntity be,
            bool barrelsOnly
        )
        {
            if (be is BlockEntityBarrel barrel)
            {
                if (be.Block is not BlockLiquidContainerBase barrelBlock)
                    return 0;
                if (barrel.Inventory == null || barrel.Inventory.Count < 2)
                    return 0;
                ItemSlot liquidSlot = barrel.Inventory[1];
                if (
                    liquidSlot == null
                    || liquidSlot.Empty
                    || liquidSlot.Itemstack?.Collectible == null
                )
                    return 0;
                if (!IsAlchemyLiquid(liquidSlot.Itemstack))
                    return 0;
                ScaleLiquid(liquidSlot.Itemstack, barrelBlock);
                barrel.MarkDirty();
                return 1;
            }

            if (barrelsOnly)
                return 0;

            if (be is BlockEntityLiquidContainer liquidBe)
            {
                if (be.Block is not BlockLiquidContainerBase flaskBlock)
                    return 0;
                ItemStack liquid = liquidBe.GetContent();
                if (liquid?.Collectible == null || !IsAlchemyLiquid(liquid))
                    return 0;
                ScaleLiquid(liquid, flaskBlock);
                liquidBe.SetContent(liquid);
                return 1;
            }

            if (be is not IBlockEntityContainer container)
                return 0;
            if (container.Inventory == null)
                return 0;
            int count = 0;
            bool changed = false;
            foreach (ItemSlot slot in container.Inventory)
            {
                if (slot == null || slot.Empty || slot.Itemstack?.Collectible == null)
                    continue;
                if (slot.Itemstack.Block is not BlockLiquidContainerBase containerBlock)
                    continue;
                ItemStack liquid = containerBlock.GetContent(slot.Itemstack);
                if (liquid?.Collectible == null || !IsAlchemyLiquid(liquid))
                    continue;
                ScaleLiquid(liquid, containerBlock);
                containerBlock.SetContent(slot.Itemstack, liquid);
                slot.MarkDirty();
                changed = true;
                count++;
            }
            if (changed)
                be.MarkDirty();
            return count;
        }

        private static bool IsWithinRadius(BlockPos bePos, BlockPos center, int radius)
        {
            if (bePos == null || center == null)
                return false;
            int dx = bePos.X - center.X;
            int dy = bePos.Y - center.Y;
            int dz = bePos.Z - center.Z;
            return dx * dx + dy * dy + dz * dz <= radius * radius;
        }

        private static int MigratePlayerInventories(ICoreServerAPI api, IServerPlayer target)
        {
            int modified = 0;
            IServerPlayer[] players =
                target != null
                    ? [target]
                    : api.World.AllOnlinePlayers.Cast<IServerPlayer>().ToArray();

            foreach (IServerPlayer player in players)
            {
                if (player?.WorldData == null)
                    continue;
                if (player.WorldData.CurrentGameMode == EnumGameMode.Creative)
                    continue;
                if (player.InventoryManager?.Inventories == null)
                    continue;
                foreach (IInventory inv in player.InventoryManager.Inventories.Values)
                {
                    if (inv == null)
                        continue;
                    try
                    {
                        foreach (ItemSlot slot in inv)
                        {
                            if (slot == null || slot.Empty || slot.Itemstack?.Collectible == null)
                                continue;
                            if (slot.Itemstack.Block is not BlockLiquidContainerBase containerBlock)
                                continue;
                            ItemStack liquid = containerBlock.GetContent(slot.Itemstack);
                            if (liquid?.Collectible == null || !IsAlchemyLiquid(liquid))
                                continue;
                            ScaleLiquid(liquid, containerBlock);
                            containerBlock.SetContent(slot.Itemstack, liquid);
                            slot.MarkDirty();
                            modified++;
                        }
                    }
                    catch (Exception ex)
                    {
                        api.Logger.Warning(
                            $"[AlchemyMigrate] Skipped inventory for {player.PlayerName}: {ex.Message}"
                        );
                    }
                }
            }
            return modified;
        }

        private static void ScaleLiquid(ItemStack liquid, BlockLiquidContainerBase containerBlock)
        {
            WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(liquid);
            int maxItems =
                props != null && props.ItemsPerLitre > 0
                    ? (int)(containerBlock.CapacityLitres * props.ItemsPerLitre)
                    : int.MaxValue;
            liquid.StackSize = Math.Min(liquid.StackSize * LiquidMultiplier, maxItems);
        }

        private static bool IsAlchemyLiquid(ItemStack stack) =>
            stack?.Collectible?.Code?.Domain == "alchemy"
            && stack.Collectible.MatterState == EnumMatterState.Liquid
            && stack.Collectible.Attributes?["waterTightContainerProps"].Exists == true;
    }
}
