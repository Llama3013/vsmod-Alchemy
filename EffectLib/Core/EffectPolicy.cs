using System;

namespace EffectLib
{
    /// <summary>
    /// Capability names understood by <see cref="EffectPolicy"/>. A capability covers every
    /// effect field that grants the same kind of power, so a server owner can switch off
    /// "flight" once rather than per effect.
    /// </summary>
    public static class EffectCapability
    {
        /// <summary><see cref="EffectContext.CanFly"/> and <see cref="EffectContext.NoGravity"/>.</summary>
        public const string Fly = "fly";

        /// <summary><see cref="EffectContext.CanClimbAnywhere"/>, <see cref="EffectContext.DisableClimbing"/>
        /// and <see cref="EffectContext.ClimbTouchDistance"/>.</summary>
        public const string Climb = "climb";

        /// <summary><see cref="EffectContext.FallDamageReduction"/> and <see cref="EffectContext.NoFallDamage"/>.</summary>
        public const string Fall = "fall";

        /// <summary>
        /// Whether re-applying an already running effect restarts it. When denied, the second
        /// application fails instead. Ticking health effects are never refreshed regardless.
        /// </summary>
        public const string Refresh = "refresh";

        /// <summary>
        /// Whether effects survive a disconnect. When denied, effects are cleared on
        /// disconnect and on login instead of being suspended and resumed.
        /// </summary>
        public const string RetainOnDisconnect = "retainOnDisconnect";

        /// <summary><see cref="EffectContext.SizeChange"/>, growing or shrinking the player.</summary>
        public const string Resize = "resize";
    }

    /// <summary>
    /// Decides whether a capability may take effect. EffectLib ships no config of its own, so
    /// the owning mod installs a gate that reads whatever config it already has.
    /// </summary>
    public static class EffectPolicy
    {
        private static Func<string, bool> gate;

        /// <summary>
        /// Installs the gate consulted before a capability is applied. Pass null to allow
        /// everything. The gate is read on every state refresh, so it must be cheap and must
        /// reflect config changes immediately rather than caching a snapshot.
        /// </summary>
        public static void SetGate(Func<string, bool> allow) => gate = allow;

        /// <summary>
        /// True when <paramref name="capability"/> may be applied. Unknown capabilities are
        /// allowed, so a newer EffectLib never silently disables an effect on an older gate.
        /// </summary>
        public static bool IsAllowed(string capability)
        {
            Func<string, bool> allow = gate;
            if (allow == null)
                return true;

            try
            {
                return allow(capability);
            }
            catch
            {
                // A broken gate must not take the effect system down with it.
                return true;
            }
        }
    }
}
