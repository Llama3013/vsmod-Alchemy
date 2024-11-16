using HarmonyLib;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

[assembly: ModInfo(
    "AlchemyMod",
    Version = "1.6.41",
    Description = "An alchemy mod that adds a couple of player enhancing potions.",
    Website = "https://github.com/llama3013/vsmod-Alchemy",
    Authors = new[] { "Llama3013" },
    RequiredOnClient = true,
    RequiredOnServer = true,
    IconPath = "modicon.png"
)]
[assembly: ModDependency("game", "1.19.8")]
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
        public GuiHudPotion alchemyHUD;

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

        /* This override is to add the PotionFixBehavior to the player and to reset all of the potion stats to default */

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Event.PlayerNowPlaying += (IServerPlayer iServerPlayer) =>
            {
                if (iServerPlayer.Entity is not null)
                {
                    Entity entity = iServerPlayer.Entity;
                    entity.AddBehavior(new PotionFixBehavior(entity));

                    //api.Logger.Debug("[Potion] Adding PotionFixBehavior to spawned EntityPlayer");
                    EntityPlayer player = iServerPlayer.Entity;
                    TempEffect.ResetAllTempStats(player);
                    TempEffect.ResetAllAttrListeners(player, "potionid", "tickpotionid");
                   // api.Logger.Debug("potion player ready");
                }
            };
        }
    }
}