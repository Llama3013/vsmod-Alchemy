using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

/*json block glow
vertexFlags: {
    glowLevel: 255
},*/
/* Quick reference to all attributes that change the characters Stats:
   healingeffectivness, maxhealthExtraPoints, walkspeed, hungerrate, rangedWeaponsAcc, rangedWeaponsSpeed
   rangedWeaponsDamage, meleeWeaponsDamage, mechanicalsDamage, animalLootDropRate, forageDropRate, wildCropDropRate
   vesselContentsDropRate, oreDropRate, rustyGearDropRate, miningSpeedMul, animalSeekingRange, armorDurabilityLoss, bowDrawingStrength, wholeVesselLootChance, temporalGearTLRepairCost, animalHarvestingTime*/

namespace Alchemy
{
    public class AlchemyMod : ModSystem
    {
        private AlchemyConfig config;

        public GuiHudPotion alchemyHUD;

        public override void StartPre(ICoreAPI api)
        {
            try
            {
                config = api.LoadModConfig<AlchemyConfig>("alchemyConfig.json");
                if (config == null)
                {
                    config = new AlchemyConfig();
                    api.StoreModConfig(config, "alchemyConfig.json");
                }
            }
            catch
            {
                api.Logger.Error("Failed to load mod config. Reverting to default settings.");
                config = new AlchemyConfig();
            }
        }

        public override void Start(ICoreAPI api)
        {
            //api.Logger.Debug("[Potion] Start");
            base.Start(api);

            Harmony harmony = new("llama3013.Alchemy");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            api.RegisterBlockClass("BlockPotionFlask", typeof(BlockPotionFlask));
            api.RegisterBlockEntityClass("BlockEntityPotionFlask", typeof(BlockEntityPotionFlask));
            api.RegisterItemClass("ItemPotion", typeof(ItemPotion));
            api.RegisterBlockClass("BlockHerbRacks", typeof(BlockHerbRacks));
            api.RegisterBlockEntityClass("HerbRacks", typeof(BlockEntityHerbRacks));
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            if (api.Side == EnumAppSide.Server)
            {
                var potionRemovals = new List<(bool enabled, List<(string code, bool isWildcard)> codesToRemove, string logMessage)>
                {
                    (config.DisableRecallPotion, new List<(string code, bool isWildcard)> { ("utilitypotionportion-recall", false), ("utilitypotionbase-recall", false) }, "Successfully removed the 'Utility Potion Portion - Recall' item and base."),
                    (config.DisableGlowPotion, new List<(string code, bool isWildcard)> { ("utilitypotionportion-glow", false), ("utilitypotionbase-glow", false) }, "Successfully removed the 'Utility Potion Portion - Glow' item and base."),
                    (config.DisableWaterBreathePotion, new List<(string code, bool isWildcard)> { ("utilitypotionportion-waterbreathe", false), ("utilitypotionbase-waterbreathe", false) }, "Successfully removed the 'Utility Potion Portion - Water breathe' item and base."),
                    (config.DisableNutritionPotion, new List<(string code, bool isWildcard)> { ("utilitypotionportion-nutrition", false), ("utilitypotionbase-nutrition", false) }, "Successfully removed the 'Utility Potion Portion - Nutrition' item and base."),
                    (config.DisableTemporalPotion, new List<(string code, bool isWildcard)> { ("utilitypotionportion-temporal", false), ("utilitypotionbase-temporal", false) }, "Successfully removed the 'Utility Potion Portion - Temporal' item and base."),

                    (config.DisableArcherPotion, new List<(string code, bool isWildcard)> { ("potionportion-archer-", true), ("potionbase-archer-", true), ("herbball-archer", false) }, "Successfully removed 'Potion Portion - Archer' items and bases."),
                    (config.DisableHealingEffectPotion, new List<(string code, bool isWildcard)> { ("potionportion-healingeffect-", true), ("potionbase-healingeffect-", true), ("herbball-healingeffect", false) }, "Successfully removed 'Potion Portion - Healing Effect' items and bases."),
                    (config.DisableHungerEnhancePotion, new List<(string code, bool isWildcard)> { ("potionportion-hungerenhance-", true), ("potionbase-hungerenhance-", true), ("herbball-hungerenhance", false) }, "Successfully removed 'Potion Portion - Hunger Enhance' items and bases."),
                    (config.DisableHungerSupressPotion, new List<(string code, bool isWildcard)> { ("potionportion-hungersupress-", true), ("potionbase-hungersupress-", true), ("herbball-hungersupress", false) }, "Successfully removed 'Potion Portion - Hunger Supress' items and bases."),
                    (config.DisableHunterPotion, new List<(string code, bool isWildcard)> { ("potionportion-hunter-", true), ("potionbase-hunter-", true), ("herbball-hunter", false) }, "Successfully removed 'Potion Portion - Hunter' items and bases."),
                    (config.DisableLooterPotion, new List<(string code, bool isWildcard)> { ("potionportion-looter-", true), ("potionbase-looter-", true), ("herbball-looter", false) }, "Successfully removed 'Potion Portion - Looter' items and bases."),
                    (config.DisableMeleePotion, new List<(string code, bool isWildcard)> { ("potionportion-melee-", true), ("potionbase-melee-", true), ("herbball-melee", false) }, "Successfully removed 'Potion Portion - Melee' items and bases."),
                    (config.DisableMiningPotion, new List<(string code, bool isWildcard)> { ("potionportion-mining-", true), ("potionbase-mining-", true), ("herbball-mining", false) }, "Successfully removed 'Potion Portion - Mining' items and bases."),
                    (config.DisablePoisonPotion, new List<(string code, bool isWildcard)> { ("potionportion-poison-", true), ("potionbase-poison-", true), ("herbball-poison", false) }, "Successfully removed 'Potion Portion - Poison' items and bases."),
                    (config.DisablePredatorPotion, new List<(string code, bool isWildcard)> { ("potionportion-predator-", true), ("potionbase-predator-", true), ("herbball-predator", false) }, "Successfully removed 'Potion Portion - Predator' items and bases."),
                    (config.DisableRegenPotion, new List<(string code, bool isWildcard)> { ("potionportion-regen-", true), ("potionbase-regen-", true), ("herbball-regen", false) }, "Successfully removed 'Potion Portion - Regen' items and bases."),
                    (config.DisableScentMaskPotion, new List<(string code, bool isWildcard)> { ("potionportion-scentmask-", true), ("potionbase-scentmask-", true), ("herbball-scentmask", false) }, "Successfully removed 'Potion Portion - Scent Mask' items and bases."),
                    (config.DisableSpeedPotion, new List<(string code, bool isWildcard)> { ("potionportion-speed-", true), ("potionbase-speed-", true), ("herbball-speed", false) }, "Successfully removed 'Potion Portion - Speed' items and bases."),
                    (config.DisableVitalityPotion, new List<(string code, bool isWildcard)> { ("potionportion-vitality-", true), ("potionbase-vitality-", true), ("herbball-vitality", false) }, "Successfully removed 'Potion Portion - Vitality' items and bases."),
                    (config.DisableDebugPotions, new List<(string code, bool isWildcard)> { ("potionportion-all-", true), ("potionbase-alltick-", true), ("herbball-all", false), ("herbball-alltick", false) }, ""),
                   
                    //(config.DisableClayFlask, new List<(string code, bool isWildcard)> { ("claypotionflask-*", true) }, "Successfully removed 'Clay Potion Flask'."),
                    //(config.DisableLargeFlask, new List<(string code, bool isWildcard)> { ("potionflask-normal-*", true) }, "Successfully removed 'Glass Potion Flask Large'."),
                    //(config.DisableMediumFlask, new List<(string code, bool isWildcard)> { ("potionflask-round-*", true) }, "Successfully removed 'Glass Potion Flask Medium'."),
                    //(config.DisableSmallFlask, new List<(string code, bool isWildcard)> { ("potionflask-tube-*", true) }, "Successfully removed 'Glass Potion Flask Small'."),

                    //(config.DisableHerbRack, new List<(string code, bool isWildcard)> { ("herbrack-*", true) }, "Successfully removed 'Herb Rack'.")
                };

                foreach (var (enabled, codesToRemove, logMessage) in potionRemovals)
                {
                    if (enabled)
                    {
                        if (codesToRemove != null)
                        {
                            bool removedCode = false;
                            foreach (var (code, isWildcard) in codesToRemove)
                            {
                                List<Item> items = [.. api.World.Items.Where(item =>
                                    item != null && item.Code != null && item.Code.Domain == "alchemy" && (isWildcard ? item.Code.Path.StartsWith(code) : item.Code.Path == code)
                                )];

                                foreach (Item item in items)
                                {
                                    api.Logger.VerboseDebug($"Item variant of {item.Code.GetName()} removed!");
                                    api.World.Items.Remove(item);
                                    removedCode = true;
                                }

                                List<Block> blocks = [.. api.World.Blocks.Where(block =>
                                    block != null && block.Code != null && block.Code.Domain == "alchemy" && (isWildcard ? block.Code.Path.StartsWith(code) : block.Code.Path == code)
                                )];

                                foreach (Block block in blocks)
                                {
                                    api.Logger.VerboseDebug($"Block variant of {block.Code.GetName()} removed!");
                                    api.World.Blocks.Remove(block);
                                    removedCode = true;
                                }

                                for (int i = api.World.GridRecipes.Count - 1; i >= 0; i--)
                                {
                                    GridRecipe recipe = api.World.GridRecipes[i];
                                    if (recipe.Output?.Code != null && (isWildcard ? recipe.Output.Code.Path.StartsWith(code) : recipe.Output.Code.Path == code))
                                    {
                                        api.World.GridRecipes.RemoveAt(i);
                                        api.Logger.VerboseDebug($"Grid recipe for {recipe.Output.Code.Path} removed!");
                                        removedCode = true;
                                    }
                                }

                                List<BarrelRecipe> barrelRecipes = api.GetBarrelRecipes();
                                if (barrelRecipes != null)
                                {
                                    for (int i = barrelRecipes.Count - 1; i >= 0; i--)
                                    {
                                        BarrelRecipe recipe = barrelRecipes[i];
                                        if (recipe.Output?.Code != null && (isWildcard ? recipe.Output.Code.Path.StartsWith(code) : recipe.Output.Code.Path == code))
                                        {
                                            barrelRecipes.RemoveAt(i);
                                            api.Logger.VerboseDebug($"Recipe for {recipe.Output.Code.Path} removed from barrel registry!");
                                            removedCode = true;
                                        }
                                    }
                                }
                                //var cookingRecipes = api.GetCookingRecipes();
                                //if (cookingRecipes != null)
                                //{
                                //    for (int i = cookingRecipes.Count - 1; i >= 0; i--)
                                //    {
                                //        var recipe = cookingRecipes[i];
                                //        foreach (var (code, isWildcard) in allCodesToRemove)
                                //        {
                                //            if (recipe.Output?.Code != null && (isWildcard ? recipe.Output.Code.Path.StartsWith(code) : recipe.Output.Code.Path == code))
                                //            {
                                //                recipes.RemoveAt(i);
                                //                api.Logger.Debug($"Recipe for {recipe.Output.Code.Path} removed from {registryName} registry!");
                                //            }
                                //        }
                                //    }
                                //}
                            }

                            if (removedCode && logMessage != "")
                            {
                                api.Logger.Event(logMessage);
                            }
                        }
                    }
                }
            }
            base.AssetsFinalize(api);
        }

        /* This override is to add the PotionFixBehavior to the player and to reset all of the potion stats to default */
        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            api.Event.PlayerNowPlaying += iServerPlayer =>
            {
                if (iServerPlayer.Entity is not null)
                {
                    EntityPlayer entity = iServerPlayer.Entity;
                    entity.AddBehavior(new PotionFixBehavior(entity));

                    api.Logger.VerboseDebug("[Potion] Adding PotionFixBehavior to spawned EntityPlayer");
                    EntityPlayer player = iServerPlayer.Entity;
                    TempEffect.ResetAllTempStats(player);
                    TempEffect.ResetAllAttrListeners(player, "potionid", "tickpotionid");
                    api.Logger.VerboseDebug("potion player ready");
                }
            };
        }
    }
}
