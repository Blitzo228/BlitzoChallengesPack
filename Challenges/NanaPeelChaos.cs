using HarmonyLib;
using UnityEngine;

namespace BlitzoChallengesPack
{
    public static class NanaPeelChaosMode
    {
        public static bool Active = false;
    }

    public class NanaPeelChaosRunner : MonoBehaviour
    {
        public static NanaPeelChaosRunner? Instance;

        private float timer;
        private int wave;
        private ITM_NanaPeel? peelPrefab;

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("NanaPeelChaosRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<NanaPeelChaosRunner>();
        }

        public static void Stop()
        {
            if (Instance != null && Instance.gameObject != null)
                UnityEngine.Object.Destroy(Instance.gameObject);
            Instance = null;
        }

        private void Update()
        {
            if (!NanaPeelChaosMode.Active) return;

            var bm = Singleton<BaseGameManager>.Instance;
            if (bm == null) return;
            var ec = bm.Ec;
            if (ec == null) return;

            if (bm.InPitstop()) return;

            if (peelPrefab == null)
            {
                var all = Resources.FindObjectsOfTypeAll<ITM_NanaPeel>();
                for (int i = 0; i < all.Length; i++)
                {
                    var p = all[i];
                    if (p == null) continue;
                    if (!p.gameObject.scene.IsValid())
                    {
                        peelPrefab = p;
                        break;
                    }
                }
                if (peelPrefab == null) return;
            }

            timer -= Time.deltaTime * ec.EnvironmentTimeScale;
            if (timer > 0f) return;
            timer = 15f;
            wave++;

            SpawnWave(ec, wave);
        }

        private void SpawnWave(EnvironmentController ec, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Cell? cell = ec.RandomCell(false, false, true);
                if (cell == null || cell.Null) continue;

                try
                {
                    var peel = UnityEngine.Object.Instantiate(peelPrefab);
                    if (peel == null) continue;
                    peel.Spawn(ec, cell.FloorWorldPosition, Vector3.forward, 0f);
                }
                catch { }
            }
        }

        public void Reset()
        {
            timer = 15f;
            wave = 0;
        }
    }

    [HarmonyPatch(typeof(EnvironmentController), "BeginPlay")]
    internal static class NanaPeelChaosStartPatch
    {
        [HarmonyPostfix]
        static void Postfix(EnvironmentController __instance)
        {
            if (!NanaPeelChaosMode.Active) return;
            if (__instance == null) return;
            NanaPeelChaosRunner.Ensure();
            if (NanaPeelChaosRunner.Instance != null)
                NanaPeelChaosRunner.Instance.Reset();
        }
    }

    [HarmonyPatch(typeof(Structure_StudentSpawner), "SpawnStudents")]
    internal static class NanaPeelChaosNoStudentsPatch
    {
        [HarmonyPrefix]
        static bool Prefix() => !NanaPeelChaosMode.Active;
    }

    [HarmonyPatch(typeof(CoreGameManager), "ReturnToMenu")]
    internal static class NanaPeelChaosResetPatch
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            NanaPeelChaosMode.Active = false;
            NanaPeelChaosRunner.Stop();
        }
    }

    [HarmonyPatch(typeof(PlaceholderWinManager), "Initialize")]
    internal static class NanaPeelChaosWinPatch
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            if (!NanaPeelChaosMode.Active) return;
            NanaPeelChaosMode.Active = false;
            NanaPeelChaosRunner.Stop();
            BlitzoChallengesProgress.Complete("nana");
        }
    }
}