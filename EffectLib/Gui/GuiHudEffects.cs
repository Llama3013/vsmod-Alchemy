using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EffectLib
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Shows every running effect as an icon row with countdowns, or as a single compact
    /// badge with a tooltip. Driven by the persisted effect tree, plus a built-in row for an
    /// active grow/shrink (which is a size state, not a tracked effect).
    /// </summary>
    public class GuiHudEffects : HudElement
    {
        private sealed class TrackedEffect
        {
            public string Id;
            public string Name;
            public long AppliedToken;
            public long ExpiryMs;
            public float PotencyMul;
            public string[] ExtraLines;
            public float ChangeToken;
            public bool Synthetic;
            public string[] DetailLines;
            public bool Endless;
            public bool IconResolved;
            public int IconTextureId;
            public ItemSlot IconSlot;
            public ElementBounds IconBounds;
        }

        public override string ToggleKeyCombinationCode => HotkeyToggle;
        public override bool Focusable => false;

        // Hotkey codes and client setting keys predate EffectLib and are kept verbatim so
        // players upgrading from Alchemy 2.x keep their keybinds and HUD placement.
        public const string HotkeyToggle = "togglepotionhud";
        public const string HotkeyMove = "movepotionhud";
        public const string HotkeyStyle = "stylepotionhud";
        public const string SettingPosition = "alchemyHudPosition";
        public const string SettingStyle = "alchemyHudStyle";
        public const string SettingEnabled = "alchemyHudEnabled";
        public const string SettingAutoEnabled = "alchemyHudAutoEnabled";

        // Icons (a hudIcon texture, or grown/shrunk below) are blitted into this many px, which
        // GUI scale can roughly double or triple. Author them square, transparent, 128x128
        // (anything >= 64 is fine); pixel-art item-texture sizes like 32 will look soft.
        private const int IconSize = 40;
        private const int IconPad = 4;
        private const int TimerHeight = 20;
        private const int BlinkSec = 15;

        private static readonly AssetLocation activeEffectHudTexture = new(
            "effectlib:textures/hud/activeeffecthud.png"
        );

        // Synthetic row ids for an active grow/shrink - not real effect ids, never in the tree.
        // Icons: effectlib:textures/hud/effects/grown.png / shrunk.png.
        private const string GrownRowId = "effectlib:grown";
        private const string ShrunkRowId = "effectlib:shrunk";

        private readonly Dictionary<string, TrackedEffect> tracked = [];
        private readonly List<TrackedEffect> ordered = [];
        private Dictionary<string, ItemStack> iconStacks;
        private GuiComposer current;
        private long timerListenerId;
        private EntityPlayer listeningEntity;

        private long ClientNowMs => capi.InWorldEllapsedMilliseconds;
        private bool CompactStyle => capi.Settings.Int[SettingStyle] == 1;

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
            entity.WatchedAttributes.RegisterModifiedListener(
                EffectManager.PersistKey,
                OnEffectsChanged
            );
            entity.WatchedAttributes.RegisterModifiedListener(
                UtilityEffects.SizeDeltaAttr,
                OnEffectsChanged
            );

            OnEffectsChanged();
        }

        private void OnEffectsChanged()
        {
            EntityPlayer entity = capi.World.Player?.Entity;
            if (entity == null)
                return;

            bool changed = false;
            ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute(
                EffectManager.PersistKey
            );

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
                    bool endless = remainingSec < 0;
                    tracked[pair.Key] = new TrackedEffect
                    {
                        Id = pair.Key,
                        Name = record.GetString("name", ""),
                        AppliedToken = token,
                        Endless = endless,
                        ExpiryMs = endless ? long.MaxValue : ClientNowMs + remainingSec * 1000L,
                        PotencyMul = record.GetFloat("strengthMul", 1f),
                    };
                    changed = true;
                }
            }

            SyncSizeRow(entity, ref changed);

            List<string> removed = null;
            foreach (KeyValuePair<string, TrackedEffect> pair in tracked)
            {
                // Synthetic rows are added and removed by SyncSizeRow itself.
                if (pair.Value.Synthetic)
                    continue;
                if (tree?.HasAttribute(pair.Key) != true)
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

        // Adds, updates or removes the built-in grow/shrink row from the player's size delta.
        private void SyncSizeRow(EntityPlayer entity, ref bool changed)
        {
            float sizeDelta = entity.WatchedAttributes.GetFloat(UtilityEffects.SizeDeltaAttr, 0f);
            bool active = Math.Abs(sizeDelta) > 0.001f;

            string wantId = sizeDelta > 0 ? GrownRowId : ShrunkRowId;
            string dropId = sizeDelta > 0 ? ShrunkRowId : GrownRowId;

            if (tracked.Remove(dropId))
                changed = true;
            if (!active)
            {
                if (tracked.Remove(wantId))
                    changed = true;
                return;
            }

            if (
                tracked.TryGetValue(wantId, out TrackedEffect existing)
                && Math.Abs(existing.ChangeToken - sizeDelta) < 0.0001f
            )
                return;

            string label = EffectLang.GetForDomain(
                EffectRegistry.DefaultDomain,
                sizeDelta > 0 ? "grown" : "shrunk"
            );
            tracked[wantId] = new TrackedEffect
            {
                Id = wantId,
                Name = label,
                Synthetic = true,
                Endless = true,
                ExpiryMs = long.MaxValue,
                ChangeToken = sizeDelta,
                ExtraLines = [$"{sizeDelta:+0.0#;-0.0#} blocks"],
            };
            changed = true;
        }

        private void RebuildComposer()
        {
            ordered.Clear();
            ordered.AddRange(tracked.Values);
            ordered.Sort((a, b) => a.ExpiryMs.CompareTo(b.ExpiryMs));

            EnumDialogArea hudPosition = (EnumDialogArea)capi.Settings.Int[SettingPosition];
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
            GuiComposer composer = capi.Gui.CreateCompo("effecthud", hudBounds);

            if (ordered.Count == 0)
                return composer;

            CairoFont font = CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.2);
            return composer
                .AddImage(hudBounds.ForkChild(), activeEffectHudTexture)
                .AddHoverText(
                    BuildCompactTooltip(),
                    font,
                    250,
                    hudBounds.ForkChild(),
                    "effectstatus"
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

            GuiComposer composer = capi.Gui.CreateCompo("effecthud", hudBounds);

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

            // 1. The grow/shrink row uses EffectLib's own icons, if shipped.
            // 2. An explicit texture the effect declared (JSON hudIcon / Register iconTexture).
            AssetLocation texLoc = effect.Synthetic
                ? new AssetLocation(
                    EffectRegistry.DefaultDomain,
                    "textures/hud/effects/" + (effect.Id == GrownRowId ? "grown" : "shrunk") + ".png"
                )
                : EffectRegistry.IconTextureOf(effect.Id);

            if (texLoc != null && capi.Assets.TryGet(texLoc) != null)
            {
                effect.IconTextureId = capi.Render.GetOrLoadTexture(texLoc);
                return;
            }

            // 3. The collectible the effect was registered from, then a scan for whatever item
            //    carries this effect id - so a code-registered effect still shows its item.
            ItemStack stack =
                StackFor(EffectRegistry.IconSourceOf(effect.Id)) ?? StackForEffectItem(effect.Id);
            if (stack != null)
                effect.IconSlot = new DummySlot(stack);
        }

        private ItemStack StackFor(AssetLocation code)
        {
            if (code == null)
                return null;

            Item item = capi.World.GetItem(code);
            if (item != null)
                return new ItemStack(item);

            Block block = capi.World.GetBlock(code);
            return block == null ? null : new ItemStack(block);
        }

        // The first collectible whose "effectinfo" attribute names this effect id - the flask,
        // wand or herb it comes from. Built once, since collectibles do not change in a session.
        private ItemStack StackForEffectItem(string effectId)
        {
            if (iconStacks == null)
            {
                iconStacks = [];
                foreach (CollectibleObject coll in EnumerateCollectibles())
                {
                    JsonObject def = coll?.Attributes?["effectinfo"];
                    if (def?.Exists != true)
                        continue;
                    string id = def["effectId"].AsString()?.ToLowerInvariant();
                    if (!string.IsNullOrEmpty(id))
                        iconStacks.TryAdd(id, new ItemStack(coll));
                }
            }
            return iconStacks.GetValueOrDefault(effectId);
        }

        private IEnumerable<CollectibleObject> EnumerateCollectibles()
        {
            foreach (Item item in capi.World.Items)
                yield return item;
            foreach (Block block in capi.World.Blocks)
                yield return block;
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
                    lines.Add($"{Label(effect.Id, stat.Key)}: +{hp}");
                }
                else
                {
                    float percent = (float)
                        Math.Round(value.Value * 100, MidpointRounding.AwayFromZero);
                    lines.Add($"{Label(effect.Id, stat.Key)}: {percent:+0;-0;0}%");
                }
            }

            EffectContext ctx = EffectRegistry.Build(effect.Id, effect.PotencyMul);
            if (ctx != null)
            {
                string healthTick = FormatHealthTick(effect.Id, ctx);
                if (healthTick != null)
                    lines.Add(healthTick);

                if (ctx.GlowStrength > 0)
                    lines.Add(Label(effect.Id, "glow"));
                if (ctx.WaterBreathe)
                    lines.Add(Label(effect.Id, "waterbreathe"));
                if (ctx.ColdResist)
                    lines.Add(Label(effect.Id, "coldresist"));
                if (ctx.FallDamageReduction > 0)
                    lines.Add(Label(effect.Id, "fall"));
                if (ctx.CanClimbAnywhere)
                    lines.Add(Label(effect.Id, "climb"));
                if (ctx.CanFly)
                    lines.Add(Label(effect.Id, "flight"));
                if (ctx.NoGravity)
                    lines.Add(Label(effect.Id, "nogravity"));
                if (Math.Abs(ctx.KnockbackResistance) > float.Epsilon)
                    lines.Add(
                        $"{Label(effect.Id, "knockbackresist")}: {ctx.KnockbackResistance * 100:+0;-0;0}%"
                    );
                if (ctx.NoFallDamage)
                    lines.Add(Label(effect.Id, "nofalldamage"));
                if (ctx.DisableClimbing)
                    lines.Add(Label(effect.Id, "noclimb"));
                if (Math.Abs(ctx.ClimbTouchDistance) > float.Epsilon)
                    lines.Add(
                        $"{Label(effect.Id, "climbreach")}: {ctx.ClimbTouchDistance:+0.##;-0.##;0}"
                    );
                if (Math.Abs(ctx.Weight) > float.Epsilon)
                    lines.Add($"{Label(effect.Id, "weight")}: {ctx.Weight:+0.#;-0.#;0}kg");
            }

            if (effect.ExtraLines != null)
                lines.AddRange(effect.ExtraLines);

            effect.DetailLines = [.. lines];
        }

        private static string Label(string effectId, string key) =>
            EffectLang.GetIfExists(effectId, key) ?? key;

        private static string FormatHealthTick(string effectId, EffectContext ctx)
        {
            if (ctx.TickSec <= 0 || Math.Abs(ctx.Health) <= float.Epsilon)
                return null;

            string label = Label(effectId, ctx.Health < 0 ? "poison" : "regen");
            return $"{label}: {ctx.Health:+0.#;-0.#} HP / {ctx.TickSec:0.##}s";
        }

        private static string DisplayName(TrackedEffect effect) =>
            string.IsNullOrEmpty(effect.Name) ? effect.Id : effect.Name;

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
            // Header uses the first row's domain so a single-mod HUD keeps that mod's wording.
            string headerDomain =
                ordered.Count > 0
                    ? EffectRegistry.DomainOf(ordered[0].Id)
                    : EffectRegistry.DefaultDomain;
            sb.Append(EffectLang.GetForDomain(headerDomain, "effects-active"));
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
                current.GetHoverText("effectstatus")?.SetNewText(BuildCompactTooltip());
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

            capi.Settings.Int[SettingPosition] = (int)newPosition;
            RebuildComposer();
        }

        public void ToggleStyle()
        {
            capi.Settings.Int[SettingStyle] = CompactStyle ? 0 : 1;
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
}
