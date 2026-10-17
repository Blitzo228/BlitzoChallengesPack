using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BlitzoChallengesPack
{
    public static class BalderChaseMode
    {
        public static bool Active = false;
        public static bool Started = false;
        public static float BaseSpeed = 18f;
        public static float SpeedPerNotebook = 0.4f;
        public static float KillRadius = 1.4f;
    }

    public class BalderChaseRunner : MonoBehaviour
    {
        public static BalderChaseRunner? Instance;

        private Balder_Entity? balder;
        private bool npcsDespawned;
        private static FieldInfo? navigatorField;

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("BalderChaseRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<BalderChaseRunner>();
        }

        public static void Stop()
        {
            if (Instance != null && Instance.gameObject != null)
                UnityEngine.Object.Destroy(Instance.gameObject);
            Instance = null;
        }

        public Balder_Entity? GetBalder() => balder;

        public static void SpawnBalder(EnvironmentController ec)
        {
            if (ec == null) return;
            if (Instance == null) Ensure();
            if (Instance == null) return;
            if (Instance.balder != null) return;

            Balder_Entity? prefab = null;
            var all = Resources.FindObjectsOfTypeAll<Balder_Entity>();
            for (int i = 0; i < all.Length; i++)
            {
                var b = all[i];
                if (b == null) continue;
                if (!b.gameObject.scene.IsValid())
                {
                    prefab = b;
                    break;
                }
            }
            if (prefab == null) return;

            Cell? spawnCell = ChallengeUtil.RandomSpawnCell(ec, prefab, avoidOffice: false);
            if (spawnCell == null) return;
            spawnCell = ChallengeUtil.FarFromPlayer(ec, spawnCell, avoidOffice: false);

            try
            {
                ec.SpawnNPC(prefab, spawnCell.position);
            }
            catch { return; }

            var npcs = ec.Npcs;
            for (int i = npcs.Count - 1; i >= 0; i--)
            {
                var npc = npcs[i];
                if (npc == null) continue;
                if (npc is Balder_Entity be)
                {
                    Instance.balder = be;
                    break;
                }
            }

            if (Instance.balder == null) return;

            TrySetupBalder(Instance.balder);
        }

        private static void TrySetupBalder(Balder_Entity b)
        {
            if (b == null) return;

            if (navigatorField == null)
            {
                navigatorField = typeof(NPC).GetField("navigator",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            var nav = navigatorField?.GetValue(b) as Navigator;
            if (nav != null)
            {
                nav.maxSpeed = BalderChaseMode.BaseSpeed;
                nav.SetSpeed(BalderChaseMode.BaseSpeed);
            }

            var spriteField = typeof(NPC).GetField("spriteRenderer",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (spriteField != null)
            {
                var sr = spriteField.GetValue(b) as SpriteRenderer;
                if (sr != null) sr.color = new Color(1f, 0.3f, 0.3f, 1f);
            }
        }

        private void Update()
        {
            if (!BalderChaseMode.Active) return;
            if (!BalderChaseMode.Started) return;

            var cgmCheck = Singleton<CoreGameManager>.Instance;
            if (cgmCheck != null && cgmCheck.Paused) return;
            if (Time.timeScale == 0f) return;

            var bm = Singleton<BaseGameManager>.Instance;
            if (bm == null) return;
            var ec = bm.Ec;
            if (ec == null) return;
            if (bm.InPitstop()) return;

            if (!npcsDespawned)
            {
                var happy = UnityEngine.Object.FindObjectOfType<HappyBaldi>();
                if (happy == null)
                {
                    DespawnAllOtherNpcs(ec);
                    npcsDespawned = true;
                }
            }

            if (balder == null || balder.gameObject == null) return;

            var cgm = Singleton<CoreGameManager>.Instance;
            if (cgm == null) return;
            var player = cgm.GetPlayer(0);
            if (player == null) return;

            Vector3 target = player.transform.position;

            if (navigatorField == null)
            {
                navigatorField = typeof(NPC).GetField("navigator",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            var nav = navigatorField?.GetValue(balder) as Navigator;
            if (nav != null)
            {
                int notebooks = 0;
                if (bm != null) notebooks = bm.FoundNotebooks;

                float speed = BalderChaseMode.BaseSpeed + notebooks * BalderChaseMode.SpeedPerNotebook;
                nav.maxSpeed = speed;
                nav.SetSpeed(speed);

                if (!nav.HasDestination)
                {
                    balder.TargetPosition(target);
                }
                else
                {
                    var destField = typeof(Navigator).GetField("destination",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (destField != null)
                    {
                        var dest = (Vector3)destField.GetValue(nav);
                        if ((dest - target).sqrMagnitude > 4f)
                            balder.TargetPosition(target);
                    }
                    else
                    {
                        balder.TargetPosition(target);
                    }
                }
            }

            float dist = Vector3.Distance(balder.transform.position, target);
            if (dist <= BalderChaseMode.KillRadius)
            {
                try { cgm.SetLives(0, true); } catch { }
                try { cgm.ReturnToMenu(); } catch { }
            }
        }

        private void DespawnAllOtherNpcs(EnvironmentController ec)
        {
            var npcs = ec.Npcs;
            if (npcs == null) return;

            var toRemove = new List<NPC>();

            for (int i = 0; i < npcs.Count; i++)
            {
                var npc = npcs[i];
                if (npc == null) continue;
                if (npc == balder) continue;
                if (npc is Balder_Entity) continue;
                toRemove.Add(npc);
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                var npc = toRemove[i];
                if (npc == null) continue;

                try
                {
                    if (npc.Character == Character.Baldi)
                    {
                        var baldi = npc.GetComponent<Baldi>();
                        if (baldi != null) { try { baldi.Despawn(); } catch { } }
                    }
                }
                catch { }

                npcs.Remove(npc);

                try { UnityEngine.Object.Destroy(npc.gameObject); } catch { }
            }
        }

        public void Reset()
        {
            balder = null;
            npcsDespawned = false;
        }
    }

    [HarmonyPatch(typeof(BaseGameManager), "Initialize")]
    internal static class BalderChaseResetPerLevel
    {
        [HarmonyPostfix]
        static void Postfix(BaseGameManager __instance)
        {
            if (!BalderChaseMode.Active) return;
            if (__instance == null) return;

            BalderChaseMode.Started = false;

            if (BalderChaseRunner.Instance != null)
                BalderChaseRunner.Instance.Reset();
        }
    }

    [HarmonyPatch(typeof(BaseGameManager), "BeginSpoopMode")]
    internal static class BalderChaseBeginSpoopPatch
    {
        [HarmonyPostfix]
        static void Postfix(BaseGameManager __instance)
        {
            if (!BalderChaseMode.Active) return;
            if (__instance == null) return;

            var ec = __instance.Ec;
            if (ec == null) return;

            BalderChaseMode.Started = true;

            BalderChaseRunner.Ensure();
            if (BalderChaseRunner.Instance != null)
                BalderChaseRunner.Instance.Reset();

            BalderChaseRunner.SpawnBalder(ec);
        }
    }

    [HarmonyPatch(typeof(EnvironmentController), "SpawnNPCs")]
    internal static class BalderChaseSpawnNpcPatch
    {
        [HarmonyPrefix]
        static bool Prefix(EnvironmentController __instance)
        {
            if (!BalderChaseMode.Active) return true;
            if (!BalderChaseMode.Started) return true;
            if (__instance == null) return true;

            if (__instance.npcsToSpawn != null)
            {
                for (int i = __instance.npcsToSpawn.Count - 1; i >= 0; i--)
                {
                    var npc = __instance.npcsToSpawn[i];
                    if (npc == null) continue;
                    if (npc is Balder_Entity) continue;
                    if (npc.Character == Character.Baldi) continue;
                    __instance.npcsToSpawn.RemoveAt(i);
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Structure_StudentSpawner), "SpawnStudents")]
    internal static class BalderChaseNoStudentsPatch
    {
        [HarmonyPrefix]
        static bool Prefix() => !BalderChaseMode.Active || !BalderChaseMode.Started;
    }

    [HarmonyPatch(typeof(Balder_Entity), "Crumble")]
    internal static class BalderChaseNoCrumblePatch
    {
        [HarmonyPrefix]
        static bool Prefix(Balder_Entity __instance)
        {
            if (!BalderChaseMode.Active) return true;
            if (__instance == null) return true;
            if (BalderChaseRunner.Instance == null) return true;

            var mine = BalderChaseRunner.Instance.GetBalder();
            if (mine != null && __instance == mine) return false;
            if (mine == null && __instance.gameObject.name.Contains("Balder")) return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(NPC), "Despawn")]
    internal static class BalderChaseNoDespawnPatch
    {
        [HarmonyPrefix]
        static bool Prefix(NPC __instance)
        {
            if (!BalderChaseMode.Active) return true;
            if (__instance == null) return true;
            if (!(__instance is Balder_Entity)) return true;
            if (BalderChaseRunner.Instance == null) return true;

            var mine = BalderChaseRunner.Instance.GetBalder();
            if (mine != null && __instance == mine) return false;
            if (mine == null && __instance.gameObject.name.Contains("Balder")) return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(Balder_Crumbled), "Update")]
    internal static class BalderChaseNoCrumbledDespawnPatch
    {
        [HarmonyPrefix]
        static bool Prefix(Balder_Crumbled __instance)
        {
            if (!BalderChaseMode.Active) return true;
            if (__instance == null || __instance.balder == null) return true;
            if (BalderChaseRunner.Instance == null) return true;

            var mine = BalderChaseRunner.Instance.GetBalder();
            if (mine != null && __instance.balder == mine) return false;
            if (mine == null && __instance.balder.gameObject.name.Contains("Balder")) return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(EnvironmentController), "StartEventTimers")]
    internal static class BalderChaseNoEventsPatch
    {
        [HarmonyPrefix]
        static bool Prefix(EnvironmentController __instance)
        {
            if (!BalderChaseMode.Active) return true;
            if (!BalderChaseMode.Started) return true;
            if (__instance == null) return true;

            var eventsField = AccessTools.Field(typeof(EnvironmentController), "events");
            var timesField = AccessTools.Field(typeof(EnvironmentController), "eventTimes");
            var events = eventsField?.GetValue(__instance) as List<RandomEvent>;
            var times = timesField?.GetValue(__instance) as List<float>;
            if (events != null) events.Clear();
            if (times != null) times.Clear();
            return false;
        }
    }

    [HarmonyPatch(typeof(CoreGameManager), "ReturnToMenu")]
    internal static class BalderChaseResetPatch
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            BalderChaseMode.Active = false;
            BalderChaseMode.Started = false;
            BalderChaseRunner.Stop();
        }
    }

    [HarmonyPatch(typeof(PlaceholderWinManager), "Initialize")]
    internal static class BalderChaseWinPatch
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            if (!BalderChaseMode.Active) return;
            BalderChaseMode.Active = false;
            BalderChaseMode.Started = false;
            BalderChaseRunner.Stop();
            BlitzoChallengesProgress.Complete("balder");
        }
    }
}
