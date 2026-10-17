using System.Collections.Generic;
using HarmonyLib;

namespace BlitzoChallengesPack
{
    public static class BeansChallengeMode
    {
        public static bool Active = false;
    }

    [HarmonyPatch]
    internal static class BeansChallengePatches
    {
        public const int BeansCount = 99;

        [HarmonyPatch(typeof(LevelGenerator), "Generate")]
        [HarmonyPrefix]
        static void Prefix(LevelGenerator __instance)
        {
            if (!BeansChallengeMode.Active) return;
            if (__instance == null) return;

            var ld = __instance.ld;
            if (ld == null) return;

            if (__instance.scene == null) return;
            var beans = ChallengeUtil.FindNpcPrefab(Character.Beans);
            if (beans == null) return;

            var list = new List<NPC>();
            if (ld.forcedNpcs != null)
            {
                for (int i = 0; i < ld.forcedNpcs.Length; i++)
                {
                    if (ld.forcedNpcs[i] != null && ld.forcedNpcs[i].Character != Character.Beans)
                        list.Add(ld.forcedNpcs[i]);
                }
            }

            for (int i = 0; i < BeansCount; i++) list.Add(beans);
            ld.forcedNpcs = list.ToArray();
        }

        [HarmonyPatch(typeof(EnvironmentController), "SpawnNPCs")]
        [HarmonyPostfix]
        static void Postfix(EnvironmentController __instance)
        {
            if (!BeansChallengeMode.Active) return;
            if (__instance == null) return;

            var beans = ChallengeUtil.FindNpcPrefab(Character.Beans);
            if (beans == null) return;

            int existing = 0;
            var npcs = __instance.Npcs;
            for (int i = 0; i < npcs.Count; i++)
            {
                if (npcs[i] != null && npcs[i].Character == Character.Beans) existing++;
            }

            int missing = BeansCount - existing;
            if (missing <= 0) return;

            for (int i = 0; i < missing; i++)
            {
                var tile = ChallengeUtil.RandomSpawnCell(__instance, beans, false);
                if (tile == null) continue;
                tile = ChallengeUtil.FarFromPlayer(__instance, tile, false);
                try { __instance.SpawnNPC(beans, tile.position); }
                catch { }
            }
        }
    }

    [HarmonyPatch(typeof(CoreGameManager), "ReturnToMenu")]
    internal static class BeansChallengeResetPatch
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            BeansChallengeMode.Active = false;
        }
    }

    [HarmonyPatch(typeof(PlaceholderWinManager), "Initialize")]
    internal static class BeansChallengeWinPatch
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            if (!BeansChallengeMode.Active) return;
            BeansChallengeMode.Active = false;
            BlitzoChallengesProgress.Complete("beans");
        }
    }
}