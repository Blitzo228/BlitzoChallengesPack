using HarmonyLib;
using UnityEngine;

namespace BlitzoChallengesPack
{
    public static class BlindChallengeMode
    {
        public static bool Active = false;
    }

    [HarmonyPatch]
    internal static class BlindChallengePatches
    {
        private static bool started = false;

        [HarmonyPatch(typeof(BaseGameManager), "ExitedSpawn")]
        [HarmonyPostfix]
        static void StartBlindness(BaseGameManager __instance)
        {
            if (!BlindChallengeMode.Active) return;
            if (__instance == null) return;
            if (started) return;
            started = true;

            var ec = __instance.Ec;
            if (ec == null) return;

            var fog = new Fog
            {
                color = Color.black,
                startDist = 0f,
                maxDist = 30f,
                strength = 0.75f,
                priority = 1
            };

            try { ec.AddFog(fog); }
            catch { }
        }

        public static void Stop()
        {
            started = false;
        }
    }

    [HarmonyPatch(typeof(CoreGameManager), "ReturnToMenu")]
    internal static class BlindChallengeResetPatch
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            BlindChallengeMode.Active = false;
            BlindChallengePatches.Stop();
        }
    }

    [HarmonyPatch(typeof(PlaceholderWinManager), "Initialize")]
    internal static class BlindChallengeWinPatch
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            if (!BlindChallengeMode.Active) return;
            BlindChallengeMode.Active = false;
            BlindChallengePatches.Stop();
            BlitzoChallengesProgress.Complete("blind");
        }
    }
}
