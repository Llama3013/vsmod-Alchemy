using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class GuiHudEffects : HudElement
    {
        private sealed class TrackedEffect
        {
            public string Id;
            public string Name;
            public long AppliedToken;
            public long ExpiryMs;
            public float PotencyMul;
            public float SizeDelta;
            public string[] DetailLines;
            public bool Endless;
            public bool IconResolved;
            public int IconTextureId;
            public ItemSlot IconSlot;
            public ElementBounds IconBounds;
        }

        public override string ToggleKeyCombinationCode => "hudpotion";
        public override bool Focusable => false;

        private const string PersistKey = "alchemyEffects";
        private const int IconSize = 40;
        private const int IconPad = 4;
        private const int TimerHeight = 20;
        private const int BlinkSec = 15;

        private static readonly AssetLocation activeAlchemyHUDTexture = new(
            "alchemy:textures/hud/activealchemyhud.png"
        );

        private readonly Dictionary<string, TrackedEffect> tracked = [];
        private readonly List<TrackedEffect> ordered = [];
        private GuiComposer current;
        private long timerListenerId;
        private EntityPlayer listeningEntity;
        private Dictionary<string, ItemStack> iconStacks;

        private long ClientNowMs => capi.InWorldEllapsedMilliseconds;
        private bool CompactStyle => capi.Settings.Int["alchemyHudStyle"] == 1;

        public GuiHudEffects(ICoreClientAPI capi)
            : base(capi)
        {
            this.capi = capi;
            RebuildComposer();
        }

        public void HookPlayer()
        {
            EntityPlayer entity = capi.World.Player?.Entity;
            if (entity == null || ReferenceEquals(entity, listeningEntity))
                return;

            listeningEntity = entity;
            entity.WatchedAttributes.RegisterModifiedListener(PersistKey, OnEffectsChanged);
            entity.WatchedAttributes.RegisterModifiedListener("potionSizeDelta", OnEffectsChanged);
            OnEffectsChanged();
        }

        private void OnEffectsChanged()
        {
            EntityPlayer entity = capi.World.Player?.Entity;
            if (entity == null)
                return;

            bool changed = false;
            ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute(PersistKey);

            if (tree != null)
            {
                foreach (KeyValuePair<string, IAttribute> pair in tree)
                {
                    if (pair.Value is not ITreeAttribute record)
                        continue;

                    long token = record.GetLong("appliedAt");
                    if (
                        tracked.TryGetValue(pair.Key, out TrackedEffect existing)
                        && existing.AppliedToken == token
                    )
                        continue;

                    int remainingSec = record.GetInt("remainingSec");
                    tracked[pair.Key] = new TrackedEffect
                    {
                        Id = pair.Key,
                        Name = record.GetString("name", ""),
                        AppliedToken = token,
                        ExpiryMs = ClientNowMs + remainingSec * 1000L,
                        PotencyMul = record.GetFloat("strengthMul", 1f),
                    };
                    changed = true;
                }
            }

            float sizeDelta = entity.WatchedAttributes.GetFloat("potionSizeDelta");
            string sizeId = sizeDelta > 0 ? "growpotionid" : "shrinkpotionid";
            bool sizeActive = Math.Abs(sizeDelta) > 0.001f;
            if (sizeActive)
            {
                if (
                    !tracked.TryGetValue(sizeId, out TrackedEffect existingSize)
                    || existingSize.SizeDelta != sizeDelta
                )
                {
                    tracked[sizeId] = new TrackedEffect
                    {
                        Id = sizeId,
                        Name = Lang.Get(sizeDelta > 0 ? "alchemy:grow" : "alchemy:shrink"),
                        SizeDelta = sizeDelta,
                        ExpiryMs = long.MaxValue,
                        Endless = true,
                    };
                    changed = true;
                }
            }

            List<string> removed = null;
            foreach (KeyValuePair<string, TrackedEffect> pair in tracked)
            {
                bool stale = pair.Value.Endless
                    ? !sizeActive || pair.Key != sizeId
                    : tree?.HasAttribute(pair.Key) != true;
                if (stale)
                    (removed ??= []).Add(pair.Key);
            }
            if (removed != null)
            {
                foreach (string id in removed)
                    tracked.Remove(id);
                changed = true;
            }

            if (changed && IsOpened())
                RebuildComposer();
            UpdateTimerListener();
        }

        private void RebuildComposer()
        {
            ordered.Clear();
            ordered.AddRange(tracked.Values);
            ordered.Sort((a, b) => a.ExpiryMs.CompareTo(b.ExpiryMs));

            EnumDialogArea hudPosition = (EnumDialogArea)capi.Settings.Int["alchemyHudPosition"];
            if (hudPosition == EnumDialogArea.None)
            {
                hudPosition = EnumDialogArea.RightBottom;
            }

            GuiComposer previous = current;

            if (ordered.Count > 0 && !CompactStyle)
                current = ComposeExpanded(hudPosition);
            else
                current = ComposeCompact(hudPosition);

            current.Compose();
            SingleComposer = current;
            previous?.Dispose();

            RefreshTimerVisuals();
        }

        private GuiComposer ComposeCompact(EnumDialogArea hudPosition)
        {
            ElementBounds hudBounds = ElementBounds.Fixed(hudPosition, 0, 0, 100, 100);
            GuiComposer composer = capi.Gui.CreateCompo("potionhud", hudBounds);

            if (ordered.Count == 0)
                return composer;

            CairoFont font = CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.2);
            return composer
                .AddImage(hudBounds.ForkChild(), activeAlchemyHUDTexture)
                .AddHoverText(
                    BuildCompactTooltip(),
                    font,
                    250,
                    hudBounds.ForkChild(),
                    "potionstatus"
                );
        }

        private GuiComposer ComposeExpanded(EnumDialogArea hudPosition)
        {
            int count = ordered.Count;
            double totalWidth = count * IconSize + (count - 1) * IconPad;
            ElementBounds hudBounds = ElementBounds.Fixed(
                hudPosition,
                0,
                0,
                totalWidth,
                IconSize + TimerHeight
            );
            CairoFont hoverFont = CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.2);
            CairoFont timerFont = CairoFont
                .WhiteSmallText()
                .WithOrientation(EnumTextOrientation.Center);

            GuiComposer composer = capi.Gui.CreateCompo("potionhud", hudBounds);

            for (int i = 0; i < count; i++)
            {
                TrackedEffect effect = ordered[i];
                double x = i * (IconSize + IconPad);

                ElementBounds iconBounds = ElementBounds.Fixed(x, 0, IconSize, IconSize);
                ElementBounds timerBounds = ElementBounds.Fixed(
                    x - IconPad / 2.0,
                    IconSize,
                    IconSize + IconPad,
                    TimerHeight
                );
                hudBounds.WithChild(iconBounds);
                hudBounds.WithChild(timerBounds);

                effect.IconBounds = iconBounds;
                ResolveIcon(effect);
                BuildDetails(effect);

                composer
                    .AddHoverText(
                        BuildExpandedTooltip(effect),
                        hoverFont,
                        250,
                        iconBounds,
                        "hover-" + effect.Id
                    )
                    .AddDynamicText("", timerFont, timerBounds, "timer-" + effect.Id);
            }

            return composer;
        }

        private void ResolveIcon(TrackedEffect effect)
        {
            if (effect.IconResolved)
                return;
            effect.IconResolved = true;

            string shortId = effect.Id.EndsWith("potionid")
                ? effect.Id[..^"potionid".Length]
                : effect.Id;
            AssetLocation texLoc = new("alchemy", "textures/hud/effects/" + shortId + ".png");
            if (capi.Assets.TryGet(texLoc) != null)
            {
                effect.IconTextureId = capi.Render.GetOrLoadTexture(texLoc);
                return;
            }

            ItemStack stack = ResolveIconStack(effect.Id);
            if (stack != null)
            {
                effect.IconSlot = new DummySlot(stack);
            }
        }

        private ItemStack ResolveIconStack(string potionId)
        {
            if (iconStacks == null)
            {
                iconStacks = [];
                foreach (Item item in capi.World.Items)
                {
                    JsonObject info = item?.Attributes?["potioninfo"];
                    if (info == null || !info.Exists)
                        continue;
                    string pid = info["potionId"].AsString();
                    if (string.IsNullOrEmpty(pid) || iconStacks.ContainsKey(pid))
                        continue;
                    iconStacks[pid] = new ItemStack(item);
                }
            }
            return iconStacks.TryGetValue(potionId, out ItemStack stack) ? stack : null;
        }

        private void BuildDetails(TrackedEffect effect)
        {
            if (effect.DetailLines != null)
                return;

            EntityPlayer entity = capi.World.Player?.Entity;
            if (entity == null)
                return;

            List<string> lines = [];
            string modCode = "potionmod-" + effect.Id;

            foreach (KeyValuePair<string, EntityFloatStats> stat in entity.Stats)
            {
                if (!stat.Value.ValuesByKey.TryGetValue(modCode, out EntityStat<float> value))
                    continue;

                if (stat.Key == "maxhealthExtraPoints")
                {
                    float hp = (float)Math.Round(value.Value, MidpointRounding.AwayFromZero);
                    lines.Add($"{Lang.GetIfExists("alchemy:" + stat.Key)}: +{hp}");
                }
                else
                {
                    float percent = (float)
                        Math.Round(value.Value * 100, MidpointRounding.AwayFromZero);
                    lines.Add($"{Lang.GetIfExists("alchemy:" + stat.Key)}: {percent:+0;-0;0}%");
                }
            }

            EffectContext ctx = EffectRegistry.Build(effect.Id, effect.PotencyMul);
            if (ctx != null)
            {
                string healthTick = FormatHealthTick(ctx);
                if (healthTick != null)
                    lines.Add(healthTick);

                if (ctx.GlowStrength > 0)
                    lines.Add(Lang.GetIfExists("alchemy:glow"));
                if (ctx.WaterBreathe)
                    lines.Add(Lang.GetIfExists("alchemy:waterbreathe"));
                if (ctx.ColdResist)
                    lines.Add(Lang.GetIfExists("alchemy:coldresist"));
                if (ctx.FallDamageReduction > 0)
                    lines.Add(Lang.GetIfExists("alchemy:fall"));
                if (ctx.CanClimbAnywhere)
                    lines.Add(Lang.GetIfExists("alchemy:climb"));
                if (ctx.CanFly)
                    lines.Add(Lang.GetIfExists("alchemy:flight"));
            }

            if (Math.Abs(effect.SizeDelta) > 0.001f)
            {
                string sizeKey = effect.SizeDelta > 0 ? "alchemy:grow" : "alchemy:shrink";
                lines.Add($"{Lang.GetIfExists(sizeKey)}: {effect.SizeDelta:+0.0#;-0.0#}");
            }

            effect.DetailLines = [.. lines];
        }

        private static string FormatHealthTick(EffectContext ctx)
        {
            if (ctx.TickSec <= 0 || Math.Abs(ctx.Health) <= float.Epsilon)
                return null;

            string label = Lang.GetIfExists(ctx.Health < 0 ? "alchemy:poison" : "alchemy:regen");
            return $"{label}: {ctx.Health:+0.#;-0.#} HP / {ctx.TickSec}s";
        }

        private static string DisplayName(TrackedEffect effect)
        {
            if (!string.IsNullOrEmpty(effect.Name))
                return effect.Name;
            return effect.Id.EndsWith("potionid") ? effect.Id[..^"potionid".Length] : effect.Id;
        }

        private static string BuildExpandedTooltip(TrackedEffect effect)
        {
            StringBuilder sb = new();
            sb.Append(DisplayName(effect));
            foreach (string line in effect.DetailLines ?? [])
            {
                sb.AppendLine();
                sb.Append(line);
            }
            return sb.ToString();
        }

        private string BuildCompactTooltip()
        {
            long now = ClientNowMs;
            StringBuilder sb = new();
            sb.Append(Lang.GetIfExists("alchemy:potioneffectTrue"));
            foreach (TrackedEffect effect in ordered)
            {
                BuildDetails(effect);
                sb.AppendLine();
                sb.Append(
                    effect.Endless
                        ? DisplayName(effect)
                        : $"{DisplayName(effect)} - {FormatTime(RemainingSec(effect, now))}"
                );
                foreach (string line in effect.DetailLines ?? [])
                {
                    sb.AppendLine();
                    sb.Append("  ").Append(line);
                }
            }
            return sb.ToString();
        }

        private static int RemainingSec(TrackedEffect effect, long nowMs)
        {
            if (effect.ExpiryMs == long.MaxValue)
                return int.MaxValue;
            return (int)Math.Max(0L, (effect.ExpiryMs - nowMs + 999) / 1000);
        }

        private static string FormatTime(int sec)
        {
            return sec >= 3600
                ? $"{sec / 3600}:{sec / 60 % 60:00}:{sec % 60:00}"
                : $"{sec / 60}:{sec % 60:00}";
        }

        private void UpdateTimerListener()
        {
            bool needed = IsOpened() && tracked.Count > 0;
            if (needed && timerListenerId == 0)
            {
                timerListenerId = capi.World.RegisterGameTickListener(OnTimerTick, 1000);
            }
            else if (!needed && timerListenerId != 0)
            {
                capi.World.UnregisterGameTickListener(timerListenerId);
                timerListenerId = 0;
            }
        }

        private void OnTimerTick(float dt)
        {
            RefreshTimerVisuals();
        }

        private void RefreshTimerVisuals()
        {
            if (ordered.Count == 0)
                return;

            long now = ClientNowMs;
            if (CompactStyle)
            {
                current.GetHoverText("potionstatus")?.SetNewText(BuildCompactTooltip());
            }
            else
            {
                foreach (TrackedEffect effect in ordered)
                {
                    current
                        .GetDynamicText("timer-" + effect.Id)
                        ?.SetNewText(effect.Endless ? "" : FormatTime(RemainingSec(effect, now)));
                }
            }
        }

        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);

            if (CompactStyle || ordered.Count == 0)
                return;

            capi.Render.GlPushMatrix();
            capi.Render.GlTranslate(0, 0, -150);

            long now = ClientNowMs;
            foreach (TrackedEffect effect in ordered)
            {
                ElementBounds bounds = effect.IconBounds;
                if (bounds == null)
                    continue;

                if (
                    !capi.IsGamePaused
                    && effect.ExpiryMs - now < BlinkSec * 1000L
                    && now / 400 % 2 == 0
                )
                    continue;

                if (effect.IconTextureId != 0)
                {
                    capi.Render.Render2DTexture(
                        effect.IconTextureId,
                        (float)bounds.renderX,
                        (float)bounds.renderY,
                        (float)bounds.OuterWidth,
                        (float)bounds.OuterHeight,
                        50f
                    );
                }
                else if (effect.IconSlot != null)
                {
                    capi.Render.RenderItemstackToGui(
                        effect.IconSlot,
                        bounds.renderX + bounds.OuterWidth / 2,
                        bounds.renderY + bounds.OuterHeight / 2,
                        100,
                        (float)(bounds.OuterWidth * 0.55),
                        ColorUtil.WhiteArgb,
                        true,
                        false,
                        false
                    );
                }
            }

            capi.Render.GlPopMatrix();
        }

        public void CyclePosition()
        {
            EnumDialogArea newPosition = SingleComposer.Bounds.Alignment + 1;
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

            capi.Settings.Int["alchemyHudPosition"] = (int)newPosition;
            RebuildComposer();
        }

        public void ToggleStyle()
        {
            capi.Settings.Int["alchemyHudStyle"] = CompactStyle ? 0 : 1;
            if (IsOpened())
                RebuildComposer();
        }

        public override bool TryOpen()
        {
            bool opened = base.TryOpen();
            if (opened)
            {
                HookPlayer();
                RebuildComposer();
                UpdateTimerListener();
            }
            return opened;
        }

        public override bool TryClose()
        {
            if (timerListenerId != 0)
            {
                capi.World.UnregisterGameTickListener(timerListenerId);
                timerListenerId = 0;
            }
            return base.TryClose();
        }

        public override void OnOwnPlayerDataReceived()
        {
            base.OnOwnPlayerDataReceived();
            HookPlayer();
            RebuildComposer();
        }

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize

        public override void Dispose()
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize

        {
            if (timerListenerId != 0)
            {
                capi.World.UnregisterGameTickListener(timerListenerId);
                timerListenerId = 0;
            }

            current?.Dispose();
            current = null;

            base.Dispose();
        }
    }

    public class ModSystemHud : ModSystem
    {
        private ICoreClientAPI capi;
        private GuiHudEffects alchemyHUD;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            capi = api;
            alchemyHUD = new GuiHudEffects(api);

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
            api.Input.RegisterHotKey(
                "stylepotionhud",
                "Toggle potion hud style (icon row / compact)",
                GlKeys.BackSlash,
                HotkeyType.GUIOrOtherControls
            );
            api.Input.SetHotKeyHandler("stylepotionhud", StyleGui);

            api.Event.LevelFinalize += () =>
            {
                alchemyHUD.HookPlayer();

                if (!capi.Settings.Bool.Exists("alchemyHudAutoEnabled"))
                {
                    if (!capi.Settings.Bool["alchemyHudEnabled"])
                    {
                        capi.Settings.Bool["alchemyHudEnabled"] = true;
                    }
                    capi.Settings.Bool["alchemyHudAutoEnabled"] = true;
                }

                if (capi.Settings.Bool["alchemyHudEnabled"])
                {
                    alchemyHUD.TryOpen();
                }
            };
        }

        private bool ToggleGui(KeyCombination comb)
        {
            if (alchemyHUD.IsOpened())
            {
                alchemyHUD.TryClose();
                capi.Settings.Bool["alchemyHudEnabled"] = false;
            }
            else
            {
                alchemyHUD.TryOpen();
                capi.Settings.Bool["alchemyHudEnabled"] = true;
            }

            return true;
        }

        private bool MoveGui(KeyCombination comb)
        {
            if (alchemyHUD.IsOpened() && alchemyHUD.SingleComposer.Composed)
            {
                alchemyHUD.CyclePosition();
            }
            return true;
        }

        private bool StyleGui(KeyCombination comb)
        {
            alchemyHUD.ToggleStyle();
            return true;
        }
    }
}
