using Vintagestory.API.Client;
using Vintagestory.API.Common;
using System;
using System.Collections.Generic;
using System.Text;
using Cairo;

namespace Alchemy
{
    public class HudPotion : HudElement
    {
        public override string ToggleKeyCombinationCode => "hudpotion";
        public override bool Focusable => false;
        long id;
        Dictionary<string, float> maxEssenceDic;
        AssetLocation imageLocation;

        public HudPotion(ICoreClientAPI capi) : base(capi)
        {
            SetupDialog();
        }

        private void SetupDialog()
        {
            try
            {
                IAsset maxEssences = capi.Assets.TryGet("alchemy:config/essences.json");
                imageLocation = capi.Assets.TryGet("alchemy:textures/hud/alchemyhud.png").Location;
                if (maxEssences != null)
                {
                    maxEssenceDic = maxEssences.ToObject<Dictionary<string, float>>();
                }
                maxEssenceDic.Remove("recall");
                maxEssenceDic.Remove("duration");
                maxEssenceDic.Remove("health");
            }
            catch (Exception e)
            {
                capi.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", e);
            }

            ElementBounds textBounds = ElementBounds.Fixed(EnumDialogArea.RightBottom, 0, 0, 100, 100);
            CairoFont font = CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.2);

            SingleComposer = capi.Gui.CreateCompo("inactive", textBounds)
                .AddDynamicCustomDraw(textBounds.ForkChild(), new DrawDelegateWithBounds(OnDraw), "potioninactive")
                .AddHoverText("shouldn't see this!", font, 400, textBounds.ForkChild(), "potionstatus")
            ;
            
            id = capi.World.RegisterGameTickListener(dt => UpdateText(), 100);
        }
        bool inactive = true;

        private void OnDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            BitmapRef bmp = capi.Assets.Get("alchemy:textures/hud/alchemyhud.png").ToBitmap(capi);
            if (inactive) bmp.MulAlpha(30);
            Vintagestory.API.Common.SurfaceDrawImage.Image(surface, ((Vintagestory.API.Common.BitmapExternal)bmp), (int)currentBounds.drawX, (int)currentBounds.drawY, (int)currentBounds.InnerWidth, (int)currentBounds.InnerHeight);
            bmp.Dispose();
        }

        public void UpdateText()
        {
            if (capi.World.Player.Entity.WatchedAttributes.TryGetLong("potionid") == null)
            {
                SingleComposer.GetHoverText("potionstatus").SetNewText("Off");
                inactive = true;
                SingleComposer.ReCompose();
            }
            else
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("On");
                EntityPlayer entity = capi.World.Player.Entity;
                foreach (var stats in entity.Stats)
                {
                    if (entity.Stats[stats.Key].ValuesByKey.ContainsKey("potionmod"))
                    {
                        var value = entity.Stats[stats.Key]?.ValuesByKey["potionmod"]?.Value.ToString();
                        stringBuilder.AppendLine(string.Format("{0}: {1}", stats.Key, value));
                    }
                }
                if (entity.WatchedAttributes.HasAttribute("glow"))
                {
                    var value = capi.World.Player.Entity.WatchedAttributes.GetBool("glow").ToString();
                    stringBuilder.AppendLine(string.Format("Glow: {0}", value));
                }
                SingleComposer.GetHoverText("potionstatus").SetNewText(stringBuilder.ToString());
                inactive = false;
                SingleComposer.ReCompose();
            }
        }

        public override void OnOwnPlayerDataReceived()
        {
            base.OnOwnPlayerDataReceived();
            SetupDialog();
        }

        public override void Dispose()
        {
            base.Dispose();
            capi.World.UnregisterGameTickListener(id);
        }
    }
}