using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Client;

[assembly: ModInfo(
    "AlchemyMod",
	Version = "1.6.10",
    Description = "An alchemy mod that adds a couple of player enhancing potions.",
    Website = "https://github.com/llama3013/vsmod-Alchemy",
    Authors = new[] { "Llama3013" },
    RequiredOnClient = true,
    RequiredOnServer = true,
    IconPath = "modicon.png"
)]
[assembly: ModDependency("game", "1.17.3")]

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
        public GuiHudPotion hud;

        public override void Start(ICoreAPI api)
        {
            //api.Logger.Debug("[Potion] Start");
            base.Start(api);

            var harmony = new Harmony("llama3013.Alchemy");
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
                if (iServerPlayer.Entity is EntityPlayer)
                {
                    Entity entity = iServerPlayer.Entity;
                    entity.AddBehavior(new PotionFixBehavior(entity));

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

    public class ModSystemHud : ModSystem
    {
        GuiDialog dialog;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            dialog = new GuiHudPotion(api);
            dialog.TryOpen();

            api.Input.RegisterHotKey(
                "togglepotionhud",
                "Toggle potion hud",
                GlKeys.LBracket,
                HotkeyType.GUIOrOtherControls
            );
            api.Input.SetHotKeyHandler("togglepotionhud", ToggleGui);
            api.Input.RegisterHotKey(
                "movepotionhud",
                "Move potion hud position",
                GlKeys.RBracket,
                HotkeyType.GUIOrOtherControls
            );
            api.Input.SetHotKeyHandler("movepotionhud", MoveGui);
        }

        private bool ToggleGui(KeyCombination comb)
        {
            if (dialog.IsOpened())
                dialog.TryClose();
            else
                dialog.TryOpen();

            return true;
        }

        private bool MoveGui(KeyCombination comb)
        {
            EnumDialogArea newPosition = dialog.SingleComposer.Bounds.Alignment + 1;
            switch (newPosition)
            {
                case EnumDialogArea.LeftFixed:
                    newPosition = EnumDialogArea.RightTop;
                    break;
                case EnumDialogArea.RightFixed:
                    newPosition = EnumDialogArea.LeftTop;
                    break;
                default:
                    break;
            }
            dialog.SingleComposer.Bounds.Alignment = newPosition;

            return true;
        }
    }
}
