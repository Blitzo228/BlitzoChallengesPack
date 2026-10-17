using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BlitzoChallengesPack
{
    public static class DrReflexChallengeMode
    {
        public static bool Active = false;
    }

    internal static class DrReflexKill
    {
        private static bool killed = false;

        public static void Reset() { killed = false; }

        public static void Kill(PlayerManager? pm)
        {
            if (killed) return;
            if (pm == null) return;

            killed = true;

            var cgm = Singleton<CoreGameManager>.Instance;
            if (cgm == null) return;

            try { cgm.SetLives(0, true); } catch { }
            try { cgm.ReturnToMenu(); } catch { }
        }
    }

    [HarmonyPatch(typeof(LevelGenerator), "Generate")]
    internal static class DrReflexClearTimeoutFieldPatch
    {
        [HarmonyPrefix]
        static void Prefix(LevelGenerator __instance)
        {
            if (!DrReflexChallengeMode.Active) return;
            if (__instance == null) return;
            var ld = __instance.ld;
            if (ld == null) return;
            try { ld.timeOutEvent = null!; } catch { }
        }
    }

    [HarmonyPatch(typeof(EnvironmentController), "AddEvent")]
    internal static class DrReflexNoTimeOutPatch
    {
        [HarmonyPrefix]
        static bool Prefix(RandomEvent randomEvent)
        {
            if (!DrReflexChallengeMode.Active) return true;
            if (randomEvent == null) return true;
            if (randomEvent is TimeOut) return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(MainGameManager), "CreateHappyBaldi")]
    internal static class DrReflexSkipHappyBaldiPatch
    {
        [HarmonyPrefix]
        static bool Prefix(MainGameManager __instance)
        {
            if (!DrReflexChallengeMode.Active) return true;
            if (__instance == null) return true;

            try { __instance.BeginSpoopMode(); } catch { }

            var ec = __instance.Ec;
            if (ec != null)
            {
                try { ec.SpawnNPCs(); } catch { }
                try { ec.StartEventTimers(); } catch { }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(EnvironmentController), "SpawnNPCs")]
    internal static class DrReflexBlockSpawnPatch
    {
        [HarmonyPrefix]
        static bool Prefix(EnvironmentController __instance)
        {
            if (!DrReflexChallengeMode.Active) return true;
            if (__instance == null) return true;

            var cgm = Singleton<CoreGameManager>.Instance;
            var pm = cgm != null ? cgm.GetPlayer(0) : null;
            if (pm == null) return false;

            NPC? drPrefab = null;
            foreach (var n in Resources.FindObjectsOfTypeAll<NPC>())
            {
                if (n == null) continue;
                if (n.Character == Character.DrReflex && !n.gameObject.scene.IsValid())
                {
                    drPrefab = n;
                    break;
                }
            }

            if (drPrefab == null)
                drPrefab = ChallengeUtil.FindNpcPrefab(Character.DrReflex);
            if (drPrefab == null) return false;

            var spawnCell = ChallengeUtil.RandomSpawnCell(__instance, drPrefab, avoidOffice: false);
            if (spawnCell == null) return false;

            spawnCell = ChallengeUtil.FarFromPlayer(__instance, spawnCell, avoidOffice: false);

            __instance.SpawnNPC(drPrefab, spawnCell.position);
            return false;
        }
    }

    [HarmonyPatch(typeof(EnvironmentController), "BeginPlay")]
    internal static class DrReflexChallengeSpawnPatch
    {
        [HarmonyPostfix]
        static void Postfix(EnvironmentController __instance)
        {
            if (!DrReflexChallengeMode.Active) return;
            if (__instance == null) return;

            DrReflexKill.Reset();

            var mgr = Singleton<BaseGameManager>.Instance;
            if (mgr != null) mgr.BeginSpoopMode();

            Singleton<MusicManager>.Instance?.StopMidi();
        }
    }

    [HarmonyPatch(typeof(NPC), "Initialize")]
    internal static class DrReflexBlockOtherNpcInitPatch
    {
        [HarmonyPrefix]
        static bool Prefix(NPC __instance)
        {
            if (!DrReflexChallengeMode.Active) return true;
            if (__instance == null) return true;
            if (__instance.Character == Character.DrReflex) return true;
            if (__instance.gameObject.name.Contains("DrReflex")) return true;

            try
            {
                __instance.gameObject.SetActive(false);
                Object.Destroy(__instance.gameObject);
            }
            catch { }

            return false;
        }
    }

    [HarmonyPatch(typeof(DrReflex), "HotspotClicked")]
    internal static class DrReflexAlwaysWrongPatch
    {
        [HarmonyPrefix]
        static bool Prefix(DrReflex __instance, Direction side)
        {
            if (!DrReflexChallengeMode.Active) return true;
            if (__instance == null) return true;

            var loseMethod = typeof(DrReflex).GetMethod("LoseTest",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (loseMethod != null) { try { loseMethod.Invoke(__instance, null); } catch { } }
            return false;
        }
    }

    [HarmonyPatch(typeof(DrReflex), "EndTest")]
    internal static class DrReflexNoSuccessPatch
    {
        [HarmonyPrefix]
        static void Prefix(DrReflex __instance, ref bool success)
        {
            if (!DrReflexChallengeMode.Active) return;
            success = false;
        }
    }

    [HarmonyPatch(typeof(DrReflex), "Hammer")]
    internal static class DrReflexHammerKillPatch
    {
        [HarmonyPrefix]
        static bool Prefix(DrReflex __instance, Entity entity)
        {
            if (!DrReflexChallengeMode.Active) return true;
            if (__instance == null) return true;
            if (entity == null) return true;

            var pm = entity.GetComponent<PlayerMovement>()?.pm;
            if (pm == null)
            {
                var cgm = Singleton<CoreGameManager>.Instance;
                if (cgm != null)
                {
                    var p0 = cgm.GetPlayer(0);
                    if (p0 != null && p0.plm != null && p0.plm.Entity == entity)
                        pm = p0;
                }
            }

            if (pm != null)
            {
                DrReflexKill.Kill(pm);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    internal static class DrReflexNoMidiPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var m in typeof(MusicManager).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.Name == "PlayMidi") yield return m;
            }
        }

        [HarmonyPrefix]
        static bool Prefix()
        {
            return !DrReflexChallengeMode.Active;
        }
    }

    [HarmonyPatch]
    internal static class DrReflexCoroutineSilencer
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var m in typeof(MonoBehaviour).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.Name != "StartCoroutine") continue;
                var ps = m.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType == typeof(System.Collections.IEnumerator))
                    yield return m;
            }
        }

        [HarmonyPrefix]
        static bool Prefix(MonoBehaviour __instance, System.Collections.IEnumerator routine)
        {
            if (!DrReflexChallengeMode.Active) return true;
            if (__instance == null) return true;
            if (routine == null) return false;
            if (!__instance.gameObject.activeInHierarchy) return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(CoreGameManager), "ReturnToMenu")]
    internal static class DrReflexChallengeResetPatch
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            DrReflexChallengeMode.Active = false;
            DrReflexKill.Reset();
        }
    }

    [HarmonyPatch(typeof(PlaceholderWinManager), "Initialize")]
    internal static class DrReflexChallengeWinPatch
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            if (!DrReflexChallengeMode.Active) return;
            DrReflexChallengeMode.Active = false;
            DrReflexKill.Reset();
            BlitzoChallengesProgress.Complete("drreflex");
        }
    }
}