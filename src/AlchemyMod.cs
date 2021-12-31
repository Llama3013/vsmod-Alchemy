using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

[assembly: ModInfo("AlchemyMod",
    Version = "1.4.1",
    Description = "An alchemy mod that adds a couple of player enhancing potions.",
    Website = "https://github.com/llama3013/vsmod-Alchemy",
    Authors = new[] { "Llama3013" },
    RequiredOnClient = true,
    RequiredOnServer = true,
    IconPath = "modicon.png"
    )]
[assembly: ModDependency("game", "1.16.0-pre.3")]

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
        private ModConfig config;
        public override void Start(ICoreAPI api)
        {
            api.Logger.Debug("[Potion] Start");
            base.Start(api);

            config = ModConfig.Load(api);
            
            api.RegisterBlockClass("BlockPotionFlask", typeof(BlockPotionFlask));
            api.RegisterItemClass("ItemPotion", typeof(ItemPotion));
            api.RegisterBlockClass("BlockHerbRacks", typeof(BlockHerbRacks));
            api.RegisterBlockEntityClass("HerbRacks", typeof(BlockEntityHerbRacks));
        }

        ICoreServerAPI sapi;

        /* This override is to add the PotionFixBehavior to the player and to reset all of the potion stats to default */
        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;
            api.Event.PlayerNowPlaying += (IServerPlayer iServerPlayer) =>
            {
                if (iServerPlayer.Entity is EntityPlayer)
                {
                    Entity entity = iServerPlayer.Entity;
                    entity.AddBehavior(new PotionFixBehavior(entity, config));
                    //api.Logger.Debug("[Potion] Adding PotionFixBehavior to spawned EntityPlayer");
                    TempEffect tempEffect = new TempEffect();
                    EntityPlayer player = (iServerPlayer.Entity as EntityPlayer);
                    tempEffect.resetAllTempStats(player, "potionmod");
                    tempEffect.resetAllAttrListeners(player, "potionid", "tickpotionid");
                    //api.Logger.Debug("potion player ready");
                }
            };
        }
    }
}
