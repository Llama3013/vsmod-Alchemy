// Maybe change recipe for archer flask to not require orange mallow since its semi rare for normal climate worlds
/*json block glow
vertexFlags: {
    glowLevel: 255
},*/
/* Quick reference to all attributes that change the characters Stats:
   healingeffectivness, maxhealthExtraPoints, walkspeed, hungerrate, rangedWeaponsAcc, rangedWeaponsSpeed
   rangedWeaponsDamage, meleeWeaponsDamage, mechanicalsDamage, animalLootDropRate, forageDropRate, wildCropDropRate
   vesselContentsDropRate, oreDropRate, rustyGearDropRate, miningSpeedMul, animalSeekingRange, armorDurabilityLoss, bowDrawingStrength, wholeVesselLootChance, temporalGearTLRepairCost, animalHarvestingTime*/
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class AlchemyMod : ModSystem
    {
        // private static GuiDialogCreateCharacter createCharDlg;
        private ICoreAPI api;
        private const string HarmonyId = "llama3013.Alchemy";
        private Harmony harmony;

        public static bool PlayerModelLibPresent { get; private set; }

        public override void Start(ICoreAPI api)
        {
            this.api = api;
            base.Start(api);
            api.Logger.Debug("[Potion] Start");

            PlayerModelLibPresent = api.ModLoader.IsModEnabled("playermodellib");

            if (!Harmony.HasAnyPatches(HarmonyId))
            {
                harmony = new Harmony(HarmonyId);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            CombatOverhaulCompat.Init(api);
            RegisterClasses(api);
            RegisterEffectsOnce(api.Logger);
        }

        private static readonly object effectRegistrationLock = new();
        private static bool effectsRegistered;

        private static void RegisterEffectsOnce(ILogger logger)
        {
            lock (effectRegistrationLock)
            {
                if (effectsRegistered)
                    return;

                PotionEffects.RegisterAll();
                PotionDefinitions.Validate(logger);
                effectsRegistered = true;
            }
        }

        public static void RegisterClasses(ICoreAPI api)
        {
            api.RegisterBlockClass("BlockPotionFlask", typeof(BlockPotionFlask));
            api.RegisterBlockEntityClass("BlockEntityPotionFlask", typeof(BlockEntityPotionFlask));
            api.RegisterItemClass("ItemPotion", typeof(ItemPotion));
            api.RegisterBlockClass("BlockHerbRacks", typeof(BlockHerbRacks));
            api.RegisterBlockEntityClass("HerbRacks", typeof(BlockEntityHerbRacks));
            api.RegisterCollectibleBehaviorClass(
                "PotionConsumable",
                typeof(PotionConsumableBehavior)
            );
            api.RegisterCollectibleBehaviorClass("PotionCoat", typeof(CollectibleBehaviorCoat));
            api.RegisterCollectibleBehaviorClass(
                "PotionCoatSource",
                typeof(PotionCoatSourceBehavior)
            );
            api.RegisterItemClass("ItemStirringSpoon", typeof(ItemStirringSpoon));
            api.RegisterBlockClass("BlockCauldronFirepit", typeof(BlockCauldronFirepit));
            api.RegisterBlockClass("BlockThrowablePotionFlask", typeof(BlockThrowablePotionFlask));
            api.RegisterEntity("EntityThrownPotionFlask", typeof(EntityThrownPotionFlask));
            api.RegisterBlockEntityClass(
                "BlockEntityCauldronFirepit",
                typeof(BlockEntityCauldronFirepit)
            );
        }

        public override void StartPre(ICoreAPI api)
        {
            string cfgFileName = "alchemy.json";
            try
            {
                AlchemyConfig fromDisk;
                if ((fromDisk = api.LoadModConfig<AlchemyConfig>(cfgFileName)) == null)
                {
                    api.StoreModConfig(AlchemyConfig.Loaded, cfgFileName);
                }
                else
                {
                    AlchemyConfig.Loaded = fromDisk;
                }
            }
            catch
            {
                api.Logger.Error("Failed to load mod config. Reverting to default settings.");
                api.StoreModConfig(AlchemyConfig.Loaded, cfgFileName);
            }

            AlchemyConfig.Loaded.WriteToWorldConfig(api.World.Config);

            api.Logger.Debug("Loaded alchemy mod config into world properties.");

            base.StartPre(api);
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            if (api.Side != EnumAppSide.Client)
                return;

            List<string> tagList =
            [
                .. AlchemyConfig
                    .Loaded.CoatableWeaponTags.Split(',')
                    .Select(t => t.Trim())
                    .Where(t => t.Length > 0),
            ];

            api.CollectibleTagRegistry.TryCreateTagSet(out TagSet coatableTags, tagList);

            foreach (CollectibleObject obj in api.World.Collectibles)
            {
                if (obj?.Code == null)
                    continue;

                // Coatable weapons are tag-matched; coatable projectiles are wildcard-code matched
                // against the admin-configured CoatableProjectilesCodes (default "*arrow*").
                bool isCoatable =
                    obj.Tags.Overlaps(coatableTags)
                    || PotionConsumableLogic.IsCoatableProjectile(obj);

                if (!isCoatable)
                    continue;

                obj.CollectibleBehaviors =
                [
                    .. obj.CollectibleBehaviors,
                    new CollectibleBehaviorCoat(obj),
                ];
            }
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Event.LevelFinalize += () =>
            {
                AlchemyConfig.Loaded.ReadFromWorldConfig(api.World.Config);

                if (api.World.Player?.Entity is EntityPlayer sizedPlayer)
                    EntityPlayerSizePatch.ApplySize(sizedPlayer);
            };

            api.Event.PlayerEntitySpawn += iPlayer =>
            {
                if (iPlayer.Entity is not EntityPlayer player)
                    return;

                bool baselineCanClimbAnywhere = player.Properties.CanClimbAnywhere;

                void SyncClimb() =>
                    player.Properties.CanClimbAnywhere =
                        player.WatchedAttributes.GetBool(EffectAttr.CanClimb)
                        || baselineCanClimbAnywhere;

                SyncClimb();
                player.WatchedAttributes.RegisterModifiedListener(
                    EffectAttr.CanClimb,
                    SyncClimb
                );
            };

        }

        /* This override is to add the PotionFixBehavior to the player and to reset all of the potion stats to default */
        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Event.PlayerNowPlaying += OnPlayerReady; // add method so we can remove it in dispose to prevent memory leaks
            api.Event.PlayerDisconnect += OnPlayerDisconnect;
            api.Event.PlayerDeath += OnPlayerDeath;

            base.StartServerSide(api);
        }

        private static void OnPlayerReady(IServerPlayer player)
        {
            EntityPlayer entity = player.Entity;
            if (entity?.Properties == null)
                return;

            if (!entity.HasBehavior<EntityBehaviorEffects>())
            {
                entity.AddBehavior(new EntityBehaviorEffects(entity));
            }

            if (AlchemyConfig.Loaded.RetainEffectsOnDisconnect)
            {
                entity.GetBehavior<EntityBehaviorEffects>().Manager?.RestoreEffects();

                float sizeDelta = entity.WatchedAttributes.GetFloat("potionSizeDelta", 0f);
                if (sizeDelta is > 0.001f or < -0.001f)
                {
                    entity.WatchedAttributes.MarkPathDirty("potionSizeDelta");
                }
            }
            else
            {
                UtilityEffects.ResetPlayerSize(entity);
                entity.GetBehavior<EntityBehaviorEffects>().Manager?.RemoveAll();
            }
        }

        private static void OnPlayerDisconnect(IServerPlayer player)
        {
            EntityPlayer entity = player.Entity;
            if (entity?.Properties == null)
                return;

            if (!entity.HasBehavior<EntityBehaviorEffects>())
                return;

            if (AlchemyConfig.Loaded.RetainEffectsOnDisconnect)
            {
                entity.GetBehavior<EntityBehaviorEffects>().Manager?.Suspend();
            }
            else
            {
                UtilityEffects.ResetPlayerSize(entity);
                entity.GetBehavior<EntityBehaviorEffects>().Manager?.RemoveAll();
            }
        }

        private static void OnPlayerDeath(IServerPlayer player, DamageSource damageSource)
        {
            EntityPlayer entity = player.Entity;
            if (entity?.Properties == null)
                return;

            UtilityEffects.ResetPlayerSize(entity);
            if (entity.HasBehavior<EntityBehaviorEffects>())
                entity.GetBehavior<EntityBehaviorEffects>().Manager?.RemoveAll();
        }

        public override void Dispose()
        {
            CombatOverhaulCompat.Shutdown();
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            // remove our player join listener so we don't create memory leaks
            if (api is ICoreServerAPI sapi)
            {
                sapi.Event.PlayerNowPlaying -= OnPlayerReady;
                sapi.Event.PlayerDisconnect -= OnPlayerDisconnect;
                sapi.Event.PlayerDeath -= OnPlayerDeath;
            }
        }
    }
}
