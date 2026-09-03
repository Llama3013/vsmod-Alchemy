using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EffectLib
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Owns the per-player effect lifecycle: attaching the effect behavior, resuming saved
    /// effects on login, suspending them on disconnect and clearing them on death.
    /// Mods built on EffectLib do not need to wire any of this up themselves.
    /// </summary>
    public class EffectLibMod : ModSystem
    {
        private const string HarmonyId = "llama3013.EffectLib";

        private ICoreServerAPI sapi;
        private Harmony harmony;

        // Run after the default 0.1, so mods have registered their effects, policy gate and
        // handlers by the time this starts listening for player events.
        public override double ExecuteOrder() => 0.2;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            // Both sides: the client rebuilds contexts too, for HUD tooltips.
            EffectPrimitives.RegisterAll();

            UtilityEffects.PlayerModelLibPresent = api.ModLoader.IsModEnabled("playermodellib");

            // Lets a JSON-only mod grant an effect with zero code: add this behavior and an
            // "effectinfo" attribute to an item and it works.
            api.RegisterCollectibleBehaviorClass(
                "EffectItem",
                typeof(CollectibleBehaviorEffectItem)
            );

            // Same, for a liquid container - the effect is declared on whatever it currently
            // holds rather than on the container itself.
            api.RegisterCollectibleBehaviorClass(
                "EffectLiquid",
                typeof(CollectibleBehaviorEffectLiquid)
            );

            // Weapon/arrow coating: apply an effect to a weapon now, deliver it on a later hit.
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

            // Effect handlers only ever fire server-side (from EffectManager), so this is the
            // only side that registers them. Utility effects - respawn, reshape, nutrition,
            // temporal stability, size - are EffectLib's own; no dependent mod registers them.
            EffectHandlers.Register(UtilityEffectHandler.Instance);

            api.Event.PlayerNowPlaying += OnPlayerReady;
            api.Event.PlayerDisconnect += OnPlayerDisconnect;
            api.Event.PlayerDeath += OnPlayerDeath;

            base.StartServerSide(api);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            // A size change applied before the client is fully loaded (e.g. resumed on login)
            // can be missed by PlayerSizePatch's WatchedAttributes listener, since sync order
            // isn't guaranteed relative to Entity.Initialize. Force one resync once loading is
            // definitely done.
            api.Event.LevelFinalize += () =>
            {
                if (api.World.Player?.Entity is EntityPlayer sizedPlayer)
                    UtilityEffects.ApplySizeToEntity(sizedPlayer);
            };

            // CanClimbAnywhere is set directly on the server's own EntityProperties by
            // EffectManager, which never reaches the client - properties are not synced,
            // only WatchedAttributes are. Mirror it client-side from EffectAttr.CanClimb instead.
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

            // Deliberately not unregistering from EffectHandlers: it is a process-wide static
            // that Start re-establishes, and dropping it here would disable every utility effect
            // for any world loaded later in the same session.
            base.Dispose();
        }
    }

    /// <summary>Registers and drives the shared effect HUD. Client side only.</summary>
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

                // Turn the HUD on once for players upgrading from a version that shipped it off.
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
