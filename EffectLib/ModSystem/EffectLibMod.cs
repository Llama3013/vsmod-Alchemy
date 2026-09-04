using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

#pragma warning disable IDE0130
namespace EffectLib
#pragma warning restore IDE0130
{
    public class EffectLibMod : ModSystem
    {
        private const string HarmonyId = "llama3013.EffectLib";

        private ICoreServerAPI sapi;
        private Harmony harmony;

        public override double ExecuteOrder() => 0.2;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            EffectPrimitives.RegisterAll();

            UtilityEffects.PlayerModelLibPresent = api.ModLoader.IsModEnabled("playermodellib");

            api.RegisterCollectibleBehaviorClass(
                "EffectItem",
                typeof(CollectibleBehaviorEffectItem)
            );

            api.RegisterCollectibleBehaviorClass(
                "EffectLiquid",
                typeof(CollectibleBehaviorEffectLiquid)
            );

            api.RegisterCollectibleBehaviorClass("Coatable", typeof(CollectibleBehaviorCoatable));
            api.RegisterCollectibleBehaviorClass(
                "CoatSource",
                typeof(CollectibleBehaviorCoatSource)
            );
            api.RegisterCollectibleBehaviorClass(
                "CoatSourceLiquid",
                typeof(CollectibleBehaviorCoatSourceLiquid)
            );

            if (!Harmony.HasAnyPatches(HarmonyId))
            {
                harmony = new Harmony(HarmonyId);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            EffectHandlers.Register(UtilityEffectHandler.Instance);

            api.Event.PlayerNowPlaying += OnPlayerReady;
            api.Event.PlayerDisconnect += OnPlayerDisconnect;
            api.Event.PlayerDeath += OnPlayerDeath;

            base.StartServerSide(api);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            api.Event.LevelFinalize += () =>
            {
                if (api.World.Player?.Entity is EntityPlayer sizedPlayer)
                    UtilityEffects.ApplySizeToEntity(sizedPlayer);
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
                player.WatchedAttributes.RegisterModifiedListener(EffectAttr.CanClimb, SyncClimb);
            };
        }

        private static void OnPlayerReady(IServerPlayer player)
        {
            EffectManager manager = EntityBehaviorPlayerEffects.ManagerFor(player?.Entity);
            if (manager == null)
                return;

            if (EffectPolicy.IsAllowed(EffectCapability.RetainOnDisconnect))
                manager.RestoreEffects();
            else
                manager.ResetAll();
        }

        private static void OnPlayerDisconnect(IServerPlayer player)
        {
            EntityPlayer entity = player?.Entity;
            if (entity?.Properties == null || !entity.HasBehavior<EntityBehaviorPlayerEffects>())
                return;

            EffectManager manager = entity.GetBehavior<EntityBehaviorPlayerEffects>()?.Manager;
            if (manager == null)
                return;

            if (EffectPolicy.IsAllowed(EffectCapability.RetainOnDisconnect))
                manager.Suspend();
            else
                manager.ResetAll();
        }

        private static void OnPlayerDeath(IServerPlayer player, DamageSource damageSource)
        {
            EntityPlayer entity = player?.Entity;
            if (entity?.Properties == null || !entity.HasBehavior<EntityBehaviorPlayerEffects>())
                return;

            entity.GetBehavior<EntityBehaviorPlayerEffects>()?.Manager?.ResetAll();
        }

        public override void Dispose()
        {
            if (sapi != null)
            {
                sapi.Event.PlayerNowPlaying -= OnPlayerReady;
                sapi.Event.PlayerDisconnect -= OnPlayerDisconnect;
                sapi.Event.PlayerDeath -= OnPlayerDeath;
                sapi = null;
            }

            harmony?.UnpatchAll(HarmonyId);
            harmony = null;

            base.Dispose();
        }
    }

    public class EffectLibHudMod : ModSystem
    {
        private ICoreClientAPI capi;
        private GuiHudEffects hud;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            capi = api;
            hud = new GuiHudEffects(api);

            api.Input.RegisterHotKey(
                GuiHudEffects.HotkeyToggle,
                Lang.Get("effectlib:hotkey-toggle-hud"),
                GlKeys.LBracket,
                HotkeyType.GUIOrOtherControls
            );
            api.Input.SetHotKeyHandler(GuiHudEffects.HotkeyToggle, ToggleGui);

            api.Input.RegisterHotKey(
                GuiHudEffects.HotkeyMove,
                Lang.Get("effectlib:hotkey-move-hud"),
                GlKeys.RBracket,
                HotkeyType.GUIOrOtherControls
            );
            api.Input.SetHotKeyHandler(GuiHudEffects.HotkeyMove, MoveGui);

            api.Input.RegisterHotKey(
                GuiHudEffects.HotkeyStyle,
                Lang.Get("effectlib:hotkey-style-hud"),
                GlKeys.BackSlash,
                HotkeyType.GUIOrOtherControls
            );
            api.Input.SetHotKeyHandler(GuiHudEffects.HotkeyStyle, StyleGui);

            api.Event.LevelFinalize += () =>
            {
                hud.HookPlayer();

                if (!capi.Settings.Bool.Exists(GuiHudEffects.SettingAutoEnabled))
                {
                    if (!capi.Settings.Bool[GuiHudEffects.SettingEnabled])
                    {
                        capi.Settings.Bool[GuiHudEffects.SettingEnabled] = true;
                    }
                    capi.Settings.Bool[GuiHudEffects.SettingAutoEnabled] = true;
                }

                if (capi.Settings.Bool[GuiHudEffects.SettingEnabled])
                {
                    hud.TryOpen();
                }
            };
        }

        private bool ToggleGui(KeyCombination comb)
        {
            if (hud.IsOpened())
            {
                hud.TryClose();
                capi.Settings.Bool[GuiHudEffects.SettingEnabled] = false;
            }
            else
            {
                hud.TryOpen();
                capi.Settings.Bool[GuiHudEffects.SettingEnabled] = true;
            }

            return true;
        }

        private bool MoveGui(KeyCombination comb)
        {
            if (hud.IsOpened() && hud.SingleComposer.Composed)
            {
                hud.CyclePosition();
            }
            return true;
        }

        private bool StyleGui(KeyCombination comb)
        {
            hud.ToggleStyle();
            return true;
        }
    }
}
