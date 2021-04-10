using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Common.Entities;

[assembly: ModInfo("AlchemyMod",
    Description = "An alchemy mod that adds a couple of player enhancing potions.",
    Website = "https://github.com/llama3013/vsmod-AlchemyMod",
    Authors = new[] { "Llama3013" })]

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
            
            api.RegisterItemClass("ItemRegenPotion", typeof(ItemRegenPotion));
            api.RegisterItemClass("ItemSpeedPotion", typeof(ItemSpeedPotion));
            api.RegisterItemClass("ItemMiningPotion", typeof(ItemMiningPotion));
            api.RegisterItemClass("ItemMeleePotion", typeof(ItemMeleePotion));
            api.RegisterItemClass("ItemHungerSupressPotion", typeof(ItemHungerSupressPotion));
            api.RegisterItemClass("ItemHungerEnhancePotion", typeof(ItemHungerEnhancePotion));
            api.RegisterItemClass("ItemArcherPotion", typeof(ItemArcherPotion));
            api.RegisterItemClass("ItemPoisonPotion", typeof(ItemPoisonPotion));
            api.RegisterItemClass("ItemHealingEffectPotion", typeof(ItemHealingEffectPotion));
        }

        /* This override is to add the PotionFixBehavior to the player and to reset all of the potion stats to default */
        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Event.OnEntitySpawn += (Entity entity) =>
            {
                if (entity is EntityPlayer)
                {
                    entity.AddBehavior(new PotionFixBehavior(entity, config));
                    //api.Logger.Debug("[Potion] Adding PotionFixBehavior to spawned EntityPlayer");
                    entity.Stats.Set("rangedWeaponsDamage", "potionmod", 0, false);
                    entity.Stats.Set("rangedWeaponsAcc", "potionmod", 0, false);
                    entity.Stats.Set("rangedWeaponsSpeed", "potionmod", 0, false);
                    entity.Stats.Set("hungerrate", "potionmod", 0, false);
                    entity.Stats.Set("meleeWeaponsDamage", "potionmod", 0, false);
                    entity.Stats.Set("miningSpeedMul", "potionmod", 0, false);
                    entity.Stats.Set("walkspeed", "potionmod", 0, false);
                    entity.Stats.Set("healingeffectivness", "potionmod", 0, false);
                    entity.Stats.Set("archerpotionid", "potionmod", 0, false);
                    entity.Stats.Set("healingeffectpotionid", "potionmod", 0, false);
                    entity.Stats.Set("hungerenhancepotionid", "potionmod", 0, false);
                    entity.Stats.Set("hungersupresspotionid", "potionmod", 0, false);
                    entity.Stats.Set("meleepotionid", "potionmod", 0, false);
                    entity.Stats.Set("miningpotionid", "potionmod", 0, false);
                    entity.Stats.Set("poisonpotionid", "potionmod", 0, false);
                    entity.Stats.Set("regenpotionid", "potionmod", 0, false);
                    entity.Stats.Set("speedpotionid", "potionmod", 0, false);
                }
            };
        }

        /* This override is to reset all of the potion stats to default (might not be needed since already done serverside) */
        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Event.OnEntitySpawn += (Entity entity) =>
            {
                if (entity is EntityPlayer)
                {
                    entity.Stats.Set("rangedWeaponsDamage", "potionmod", 0, false);
                    entity.Stats.Set("rangedWeaponsAcc", "potionmod", 0, false);
                    entity.Stats.Set("rangedWeaponsSpeed", "potionmod", 0, false);
                    entity.Stats.Set("hungerrate", "potionmod", 0, false);
                    entity.Stats.Set("meleeWeaponsDamage", "potionmod", 0, false);
                    entity.Stats.Set("miningSpeedMul", "potionmod", 0, false);
                    entity.Stats.Set("walkspeed", "potionmod", 0, false);
                    entity.Stats.Set("healingeffectivness", "potionmod", 0, false);
                    entity.Stats.Set("archerpotionid", "potionmod", 0, false);
                    entity.Stats.Set("healingeffectpotionid", "potionmod", 0, false);
                    entity.Stats.Set("hungerenhancepotionid", "potionmod", 0, false);
                    entity.Stats.Set("hungersupresspotionid", "potionmod", 0, false);
                    entity.Stats.Set("meleepotionid", "potionmod", 0, false);
                    entity.Stats.Set("miningpotionid", "potionmod", 0, false);
                    entity.Stats.Set("poisonpotionid", "potionmod", 0, false);
                    entity.Stats.Set("regenpotionid", "potionmod", 0, false);
                    entity.Stats.Set("speedpotionid", "potionmod", 0, false);
                }
            };
        }
    }
}
