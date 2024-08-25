using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Alchemy
{
    public class GuiHudPotion : HudElement
    {
        public override string ToggleKeyCombinationCode => "hudpotion";
        public override bool Focusable => false;
        private long activeId = 0;
        private long inactiveId = 0;
        private static readonly AssetLocation activeAlchemyHUDTexture = new("alchemy:textures/hud/activealchemyhud.png");
        private static readonly AssetLocation inactiveAlchemyHUDTexture = new("alchemy:textures/hud/inactivealchemyhud.png");
        private GuiComposer activeComposer;
        private GuiComposer inactiveComposer;

        public GuiHudPotion(ICoreClientAPI capi) : base(capi)
        {
            this.capi = capi;
            SetupDialog();
        }

        private void SetupDialog()
        {
            ElementBounds hudBounds = ElementBounds.Fixed(
                EnumDialogArea.RightBottom,
                0,
                0,
                100,
                100
            );
            CairoFont font = CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.2);

            inactiveComposer = capi.Gui
                .CreateCompo("potionhud", hudBounds)
                .AddImage(
                    hudBounds.ForkChild(),
                    inactiveAlchemyHUDTexture
                )
                .AddHoverText(
                    "shouldn't see this!",
                    font,
                    250,
                    hudBounds.ForkChild(),
                    "potionstatus"
                );
            activeComposer = capi.Gui
                .CreateCompo("potionhud", hudBounds)
                .AddImage(
                    hudBounds.ForkChild(),
                    activeAlchemyHUDTexture
                )
                .AddHoverText(
                    "shouldn't see this!",
                    font,
                    250,
                    hudBounds.ForkChild(),
                    "potionstatus"
                );
            SingleComposer = inactiveComposer.Compose();
        }

        public override bool TryOpen()
        {
            if (!CheckForEffects())
            {
                inactiveId = capi.World.RegisterGameTickListener(dt => CheckForEffects(), 5000);
            }
            return base.TryOpen();
        }

        public override bool TryClose()
        {
            if (activeId != 0)
            {
                //capi.Logger.Debug("unregister activeHUD");
                capi.World.UnregisterGameTickListener(activeId);
                activeId = 0;
            }
            if (inactiveId != 0)
            {
                //capi.Logger.Debug("unregister inactiveHUD");
                capi.World.UnregisterGameTickListener(inactiveId);
                inactiveId = 0;
            }
            return base.TryClose();
        }

        private void ActivateReadEffects()
        {
            Dispose();
            SingleComposer = activeComposer;
            SingleComposer.Compose();
            activeId = capi.World.RegisterGameTickListener(dt => ReadEffects(), 2000);
        }

        private void DeactivateReadEffects()
        {
            Dispose();
            SingleComposer = inactiveComposer;
            SingleComposer.Compose();
            inactiveId = capi.World.RegisterGameTickListener(dt => CheckForEffects(), 4000);
        }

        public bool CheckForEffects()
        {
            //capi.Logger.Debug("checking for effects active");
            bool activePotion = false;
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine(Lang.GetIfExists("alchemy:potioneffectTrue"));
            EntityPlayer entity = capi.World.Player.Entity;
            foreach (KeyValuePair<string, EntityFloatStats> stat in entity.Stats)
            {
                if (stat.Value.ValuesByKey.ContainsKey("potionmod"))
                {
                    ActivateReadEffects();
                    activePotion = true;
                }
            }
            if (entity.WatchedAttributes.HasAttribute("glow"))
            {
                ActivateReadEffects();
                activePotion = true;
            }
            if (entity.WatchedAttributes.HasAttribute("regentickpotionid"))
            {
                ActivateReadEffects();
                activePotion = true;
            }
            if (entity.WatchedAttributes.HasAttribute("poisontickpotionid"))
            {
                ActivateReadEffects();
                activePotion = true;
            }
            return activePotion;
        }

        public bool ReadEffects()
        {
            //capi.Logger.Debug("readingeffects active");
            bool activePotion = false;
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine(Lang.GetIfExists("alchemy:potioneffectTrue"));
            EntityPlayer entity = capi.World.Player.Entity;
            foreach (KeyValuePair<string, EntityFloatStats> stat in entity.Stats)
            {
                if (stat.Value.ValuesByKey.TryGetValue("potionmod", out EntityStat<float> value))
                {
                    stringBuilder.AppendLine(
                        string.Format("{0}: {1}", Lang.GetIfExists("alchemy:" + stat.Key), value?.Value)
                    );
                    activePotion = true;
                }
            }
            if (entity.WatchedAttributes.HasAttribute("glow"))
            {
                bool value = capi.World.Player.Entity.WatchedAttributes.GetBool("glow");
                stringBuilder.AppendLine(string.Format(Lang.GetIfExists("alchemy:glow") + ": {0}", value));
                activePotion = true;
            }
            if (entity.WatchedAttributes.HasAttribute("regentickpotionid"))
            {
                stringBuilder.AppendLine(string.Format(Lang.GetIfExists("alchemy:regen") + ": {0}", true));
                activePotion = true;
            }
            if (entity.WatchedAttributes.HasAttribute("poisontickpotionid"))
            {
                stringBuilder.AppendLine(string.Format(Lang.GetIfExists("alchemy:poison") + ": {0}", true));
                activePotion = true;
            }
            if (activePotion)
            {
                SingleComposer.GetHoverText("potionstatus").SetNewText(stringBuilder.ToString());
                SingleComposer.ReCompose();
            }
            else
            {
                DeactivateReadEffects();
            }
            return activePotion;
        }

        public override void OnOwnPlayerDataReceived()
        {
            base.OnOwnPlayerDataReceived();
            SetupDialog();
        }

        public override void Dispose()
        {
            base.Dispose();
            SingleComposer?.Dispose();
            if (activeId != 0)
            {
                //capi.Logger.Debug("unregister active");
                capi.World.UnregisterGameTickListener(activeId);
                activeId = 0;
            }
            if (inactiveId != 0)
            {
                //capi.Logger.Debug("unregister inactive");
                capi.World.UnregisterGameTickListener(inactiveId);
                inactiveId = 0;
            }
        }
    }

    public class ModSystemHud : ModSystem
    {
        private GuiDialog alchemyHUD;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            alchemyHUD = new GuiHudPotion(api);

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
            if (alchemyHUD.IsOpened())
            {
                alchemyHUD.TryClose();
            }
            else
            {
                alchemyHUD.TryOpen();
            }

            return true;
        }

        private bool MoveGui(KeyCombination comb)
        {
            if ((alchemyHUD.IsOpened() && alchemyHUD.SingleComposer.Composed))
            {
                EnumDialogArea newPosition = alchemyHUD.SingleComposer.Bounds.Alignment + 1;
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
                alchemyHUD.SingleComposer.Bounds.Alignment = newPosition;
            }
            return true;
        }
    }
}