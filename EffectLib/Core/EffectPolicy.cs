using System;

namespace EffectLib
{
    public static class EffectCapability
    {
        public const string Fly = "fly";

        public const string Climb = "climb";

        public const string Fall = "fall";

        public const string Refresh = "refresh";

        public const string RetainOnDisconnect = "retainOnDisconnect";

        public const string Resize = "resize";
    }

    public static class EffectPolicy
    {
        private static Func<string, bool> gate;

        public static void SetGate(Func<string, bool> allow) => gate = allow;

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
                return true;
            }
        }
    }
}
