using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class GuiHudPotion : HudElement
    {
        public override string ToggleKeyCombinationCode => "hudpotion";
        public override bool Focusable => false;
        private long activeId = 0;
        private long inactiveId = 0;
        private bool isActive;

        private static readonly AssetLocation activeAlchemyHUDTexture = new(
            "alchemy:textures/hud/activealchemyhud.png"
        );
        private static readonly AssetLocation inactiveAlchemyHUDTexture = new(
            "alchemy:textures/hud/inactivealchemyhud.png"
        );
        private GuiComposer activeComposer;
        private GuiComposer inactiveComposer;

        public GuiHudPotion(ICoreClientAPI capi)
            : base(capi)
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

            inactiveComposer = capi
                .Gui.CreateCompo("potionhud", hudBounds)
                .AddImage(hudBounds.ForkChild(), inactiveAlchemyHUDTexture)
                .AddHoverText(
                    "shouldn't see this!",
                    font,
                    250,
                    hudBounds.ForkChild(),
                    "potionstatus"
                );
            activeComposer = capi
                .Gui.CreateCompo("potionhud", hudBounds)
                .AddImage(hudBounds.ForkChild(), activeAlchemyHUDTexture)
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
                capi.Logger.Debug("unregister activeHUD");
                capi.World.UnregisterGameTickListener(activeId);
                activeId = 0;
            }
            if (inactiveId != 0)
            {
                capi.Logger.Debug("unregister inactiveHUD");
                capi.World.UnregisterGameTickListener(inactiveId);
                inactiveId = 0;
            }
            return base.TryClose();
        }

        private void ActivateReadEffects()
        {
            if (isActive)
                return;

            isActive = true;

            UnregisterInactive();
            SingleComposer = activeComposer;
            SingleComposer.Compose();
            ReadEffects();

            activeId = capi.World.RegisterGameTickListener(_ => ReadEffects(), 2000);
        }

        private void DeactivateReadEffects()
        {
            if (!isActive)
                return;

            isActive = false;

            UnregisterActive();
            SingleComposer = inactiveComposer;
            SingleComposer.Compose();

            inactiveId = capi.World.RegisterGameTickListener(_ => CheckForEffects(), 4000);
        }

        private void UnregisterActive()
        {
            if (activeId != 0)
            {
                capi.World.UnregisterGameTickListener(activeId);
                activeId = 0;
            }
        }

        private void UnregisterInactive()
        {
            if (inactiveId != 0)
            {
                capi.World.UnregisterGameTickListener(inactiveId);
                inactiveId = 0;
            }
        }

        public bool CheckForEffects()
        {
            capi.Logger.Debug("checking for effects active");
            EntityPlayer entity = capi.World.Player.Entity;
            if (
                entity.Stats.Any(stat =>
                    stat.Value.ValuesByKey.Keys.Any(key => key.StartsWith("potionmod"))
                )
            )
            {
                ActivateReadEffects();
                return true;
            }
            if (entity.WatchedAttributes.HasAttribute("glowpotionid"))
            {
                ActivateReadEffects();
                return true;
            }
            if (entity.WatchedAttributes.HasAttribute("waterbreathepotionid"))
            {
                ActivateReadEffects();
                return true;
            }
            if (entity.WatchedAttributes.HasAttribute("coldresistpotionid"))
            {
                ActivateReadEffects();
                return true;
            }
            if (entity.WatchedAttributes.HasAttribute("regentickpotionid"))
            {
                ActivateReadEffects();
                return true;
            }
            if (entity.WatchedAttributes.HasAttribute("poisontickpotionid"))
            {
                ActivateReadEffects();
                return true;
            }
            if (entity.WatchedAttributes.HasAttribute("fallpotionid"))
            {
                ActivateReadEffects();
                return true;
            }
            if (entity.WatchedAttributes.HasAttribute("climbpotionid"))
            {
                ActivateReadEffects();
                return true;
            }
            if (entity.WatchedAttributes.HasAttribute("flightpotionid"))
            {
                ActivateReadEffects();
                return true;
            }
            if (Math.Abs(entity.WatchedAttributes.GetFloat("potionSizeDelta")) > 0.001f)
            {
                ActivateReadEffects();
                return true;
            }
            return false;
        }

        public bool ReadEffects()
        {
            capi.Logger.Debug("readingeffects active");
            bool activePotion = false;
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine(Lang.GetIfExists("alchemy:potioneffectTrue"));
            EntityPlayer entity = capi.World.Player.Entity;
            foreach (KeyValuePair<string, EntityFloatStats> stat in entity.Stats)
            {
                List<float> potionValues =
                [
                    .. stat
                        .Value.ValuesByKey.Where(kv => kv.Key.StartsWith("potionmod"))
                        .Select(kv => kv.Value.Value),
                ];
                if (potionValues.Count == 0)
                    continue;

                float value = potionValues.Sum();

                if (stat.Key == "maxhealthExtraPoints")
                {
                    float hp = (float)Math.Round(value, MidpointRounding.AwayFromZero);

                    stringBuilder.AppendLine($"{Lang.GetIfExists("alchemy:" + stat.Key)}: +{hp}");
                }
                else
                {
                    float percent = (float)Math.Round(value * 100, MidpointRounding.AwayFromZero);

                    stringBuilder.AppendLine(
                        $"{Lang.GetIfExists("alchemy:" + stat.Key)}: {percent:+0;-0;0}%"
                    );
                }

                activePotion = true;
            }

            if (entity.WatchedAttributes.HasAttribute("glowpotionid"))
            {
                stringBuilder.AppendLine(
                    string.Format(Lang.GetIfExists("alchemy:glow") + ": {0}", true)
                );
                activePotion = true;
            }
            if (entity.WatchedAttributes.HasAttribute("waterbreathepotionid"))
            {
                stringBuilder.AppendLine(
                    string.Format(Lang.GetIfExists("alchemy:waterbreathe") + ": {0}", true)
                );
                activePotion = true;
            }
            if (entity.WatchedAttributes.HasAttribute("coldresistpotionid"))
            {
                stringBuilder.AppendLine(
                    string.Format(Lang.GetIfExists("alchemy:coldresist") + ": {0}", true)
                );
                activePotion = true;
            }
            if (entity.WatchedAttributes.HasAttribute("regentickpotionid"))
            {
                stringBuilder.AppendLine(
                    string.Format(Lang.GetIfExists("alchemy:regen") + ": {0}", true)
                );
                activePotion = true;
            }
            if (entity.WatchedAttributes.HasAttribute("poisontickpotionid"))
            {
                stringBuilder.AppendLine(
                    string.Format(Lang.GetIfExists("alchemy:poison") + ": {0}", true)
                );
                activePotion = true;
            }
            if (entity.WatchedAttributes.HasAttribute("fallpotionid"))
            {
                stringBuilder.AppendLine(
                    string.Format(Lang.GetIfExists("alchemy:fall") + ": {0}", true)
                );
                activePotion = true;
            }
            if (entity.WatchedAttributes.HasAttribute("climbpotionid"))
            {
                stringBuilder.AppendLine(
                    string.Format(Lang.GetIfExists("alchemy:climb") + ": {0}", true)
                );
                activePotion = true;
            }
            if (entity.WatchedAttributes.HasAttribute("flightpotionid"))
            {
                stringBuilder.AppendLine(
                    string.Format(Lang.GetIfExists("alchemy:flight") + ": {0}", true)
                );
                activePotion = true;
            }
            float sizeDelta = entity.WatchedAttributes.GetFloat("potionSizeDelta");
            if (Math.Abs(sizeDelta) > 0.001f)
            {
                string sizeKey = sizeDelta > 0 ? "alchemy:grow" : "alchemy:shrink";
                stringBuilder.AppendLine($"{Lang.GetIfExists(sizeKey)}: {sizeDelta:+0.0#;-0.0#}");
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
            UnregisterActive();
            UnregisterInactive();

            activeComposer?.Dispose();
            inactiveComposer?.Dispose();

            base.Dispose();
        }
    }

    public class ModSystemHud : ModSystem
    {
        private GuiHudPotion alchemyHUD;

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
            if (alchemyHUD.IsOpened() && alchemyHUD.SingleComposer.Composed)
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
