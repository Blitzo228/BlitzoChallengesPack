using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BlitzoChallengesPack
{
    public static class WindySchoolMode
    {
        public static bool Active = false;
        public static AudioClip? WindClip;
    }

    public static class BootsTracker
    {
        public static readonly HashSet<PlayerManager> PlayersWithBoots = new HashSet<PlayerManager>();
    }

    [HarmonyPatch(typeof(ITM_Boots), "Use")]
    internal static class BootsUsePatch
    {
        [HarmonyPostfix]
        static void Postfix(ITM_Boots __instance, PlayerManager pm, ref bool __result)
        {
            if (!__result) return;
            if (pm == null) return;
            BootsTracker.PlayersWithBoots.Add(pm);
            __instance.StartCoroutine(RemoveAfter(pm));
        }

        static IEnumerator RemoveAfter(PlayerManager pm)
        {
            float t = 15f;
            while (t > 0f)
            {
                t -= Time.deltaTime;
                yield return null;
            }
            BootsTracker.PlayersWithBoots.Remove(pm);
        }
    }

    public class WindySchoolRunner : MonoBehaviour
    {
        public static WindySchoolRunner? Instance;

        private float timer;
        private readonly List<ActivityModifier> moveMods = new List<ActivityModifier>();
        private MovementModifier mod = null!;
        private AudioSource? windSource;
        private System.Random rng = new System.Random();

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("WindySchoolRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<WindySchoolRunner>();
            Instance.Init();
        }

        public static void Stop()
        {
            if (Instance != null && Instance.gameObject != null)
                UnityEngine.Object.Destroy(Instance.gameObject);
            Instance = null;
            BootsTracker.PlayersWithBoots.Clear();
        }

        private void Init()
        {
            mod = new MovementModifier(Vector3.zero, 1f);
            timer = 5f;

            if (WindySchoolMode.WindClip != null)
            {
                windSource = gameObject.AddComponent<AudioSource>();
                windSource.clip = WindySchoolMode.WindClip;
                windSource.loop = true;
                windSource.spatialBlend = 0f;
                windSource.volume = SfxVolume();
                windSource.ignoreListenerPause = true;
                windSource.playOnAwake = false;
                windSource.Play();
            }
        }

        private static float SfxVolume()
        {
            var pfm = Singleton<PlayerFileManager>.Instance;
            if (pfm == null || pfm.volume == null || pfm.volume.Length <= 1) return 1f;
            return Mathf.Clamp01(pfm.volume[1]);
        }

        private void Update()
        {
            if (windSource != null)
            {
                windSource.volume = SfxVolume();
            }

            if (!WindySchoolMode.Active)
            {
                if (windSource != null && windSource.isPlaying) windSource.Pause();
                return;
            }

            var bm = Singleton<BaseGameManager>.Instance;
            if (bm == null)
            {
                if (windSource != null && windSource.isPlaying) windSource.Pause();
                return;
            }

            var cgm = Singleton<CoreGameManager>.Instance;
            bool paused = (cgm != null && cgm.Paused) || Time.timeScale == 0f;

            if (windSource != null)
            {
                if (paused)
                {
                    if (windSource.isPlaying) windSource.Pause();
                }
                else
                {
                    if (!windSource.isPlaying) windSource.UnPause();
                }
            }

            if (paused) return;

            var ec = bm.Ec;
            if (ec == null) return;

            timer -= Time.deltaTime * ec.EnvironmentTimeScale;
            if (timer <= 0f)
            {
                timer = UnityEngine.Random.Range(5f, 7f);
                mod.movementAddend = RandomStrongVector3();
            }

            var all = UnityEngine.Object.FindObjectsOfType<ActivityModifier>();
            for (int i = 0; i < all.Length; i++)
            {
                var comp = all[i];
                if (comp == null || !comp.enabled) continue;

                var npcComp = comp.GetComponent<NPC>();
                if (npcComp != null && npcComp.Character == Character.Chalkles)
                {
                    if (comp.moveMods.Contains(mod)) comp.moveMods.Remove(mod);
                    if (moveMods.Contains(comp)) moveMods.Remove(comp);
                    continue;
                }

                var pm = comp.GetComponent<PlayerManager>();
                if (pm != null && BootsTracker.PlayersWithBoots.Contains(pm))
                {
                    if (comp.moveMods.Contains(mod)) comp.moveMods.Remove(mod);
                    if (moveMods.Contains(comp)) moveMods.Remove(comp);
                    continue;
                }

                if (!moveMods.Contains(comp))
                {
                    moveMods.Add(comp);
                    comp.moveMods.Add(mod);
                }
            }
        }

        private Vector3 RandomStrongVector3()
        {
            float x = (float)rng.NextDouble() * ((rng.Next(0, 2) * 2) - 1) * 20f;
            float z = (float)rng.NextDouble() * ((rng.Next(0, 2) * 2) - 1) * 20f;
            return new Vector3(x, 0f, z);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < moveMods.Count; i++)
            {
                var comp = moveMods[i];
                if (comp != null) comp.moveMods.Remove(mod);
            }
            moveMods.Clear();
        }
    }

    [HarmonyPatch(typeof(EnvironmentController), "BeginPlay")]
    internal static class WindySchoolStartPatch
    {
        [HarmonyPostfix]
        static void Postfix(EnvironmentController __instance)
        {
            if (!WindySchoolMode.Active) return;
            if (__instance == null) return;
            WindySchoolRunner.Ensure();
        }
    }

    [HarmonyPatch(typeof(Structure_StudentSpawner), "SpawnStudents")]
    internal static class WindySchoolNoStudentsPatch
    {
        [HarmonyPrefix]
        static bool Prefix() => !WindySchoolMode.Active;
    }

    [HarmonyPatch(typeof(CoreGameManager), "ReturnToMenu")]
    internal static class WindySchoolResetPatch
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            WindySchoolMode.Active = false;
            WindySchoolRunner.Stop();
        }
    }

    [HarmonyPatch(typeof(PlaceholderWinManager), "Initialize")]
    internal static class WindySchoolWinPatch
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            if (!WindySchoolMode.Active) return;
            WindySchoolMode.Active = false;
            WindySchoolRunner.Stop();
            BlitzoChallengesProgress.Complete("windy");
        }
    }
}