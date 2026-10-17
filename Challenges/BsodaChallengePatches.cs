using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlitzoChallengesPack
{
    public static class BsodaChallengeMode
    {
        public static bool Active = false;
        public static bool Started = false;
        public static float StartDistance = 30f;
    }

    public static class BsodaChallengeState
    {
        public static float Elapsed = 0f;
    }

    internal static class BsodaInfiniteHelper
    {
        private static FieldInfo? usesField;
        private static FieldInfo? maxUsesField;
        private static bool searched;

        public static void ForceInfinite(ItemObject? item)
        {
            if (item == null) return;
            if (item.itemType != Items.Bsoda && item.itemType != Items.DietBsoda) return;

            if (!searched)
            {
                searched = true;
                usesField = AccessTools.Field(typeof(ItemObject), "uses");
                maxUsesField = AccessTools.Field(typeof(ItemObject), "maxUses");
            }

            try
            {
                if (usesField != null) usesField.SetValue(item, int.MaxValue);
            }
            catch { }
            try
            {
                if (maxUsesField != null) maxUsesField.SetValue(item, int.MaxValue);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(ItemManager), "AddItem", new Type[] { typeof(ItemObject) })]
    internal static class BsodaInfiniteAddItemPatch
    {
        [HarmonyPrefix]
        static void Prefix(ItemObject item) => BsodaInfiniteHelper.ForceInfinite(item);
    }

    [HarmonyPatch(typeof(ItemManager), "AddItem", new Type[] { typeof(ItemObject), typeof(Pickup) })]
    internal static class BsodaInfiniteAddItemPickupPatch
    {
        [HarmonyPrefix]
        static void Prefix(ItemObject item) => BsodaInfiniteHelper.ForceInfinite(item);
    }

    [HarmonyPatch(typeof(ItemManager), "SetItem")]
    internal static class BsodaInfiniteSetItemPatch
    {
        [HarmonyPrefix]
        static void Prefix(ItemObject item) => BsodaInfiniteHelper.ForceInfinite(item);
    }

    [HarmonyPatch(typeof(Pickup), "AssignItem")]
    internal static class BsodaInfinitePickupPatch
    {
        [HarmonyPrefix]
        static void Prefix(ItemObject item) => BsodaInfiniteHelper.ForceInfinite(item);
    }

    public class BsodaChallengeAudio : MonoBehaviour
    {
        public static BsodaChallengeAudio? Instance;
        private AudioSource? src;

        public static void Start()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("BsodaChallengeMusic");
            Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<BsodaChallengeAudio>();
            Instance.Init();
        }

        private void Init()
        {
            src = gameObject.AddComponent<AudioSource>();
            src.clip = BasePlugin.SchoolOfTerror;
            src.loop = true;
            src.spatialBlend = 0f;
            src.ignoreListenerPause = false;
            src.playOnAwake = false;
            src.volume = MusicVolume();
            if (src.clip != null) src.Play();
        }

        private void Update()
        {
            if (src == null) return;
            src.volume = MusicVolume();
        }

        private static float MusicVolume()
        {
            var pfm = Singleton<PlayerFileManager>.Instance;
            if (pfm == null || pfm.volume == null || pfm.volume.Length <= 2) return 1f;
            return Mathf.Clamp01(pfm.volume[2]);
        }

        public static void Stop()
        {
            if (Instance != null && Instance.gameObject != null)
                Object.Destroy(Instance.gameObject);
            Instance = null;
        }

        public static float Pulse()
        {
            if (Instance == null || Instance.src == null) return 0f;
            if (Instance.src.clip == null) return 0f;
            float len = Instance.src.clip.length;
            if (len <= 0f) return 0f;
            float t = Instance.src.time % len;
            return Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3f));
        }
    }

    internal static class BsodaChallengeRedLight
    {
        private static Cell[]? cells;
        private static Color[]? originals;
        private static int frameCounter;
        private const int UpdateEveryFrames = 2;
        private const float CycleSeconds = 3f;

        private static bool skyCaptured = false;
        private static Color originalSky;

        public static void Capture(EnvironmentController ec)
        {
            if (ec == null) return;
            var arr = ec.cells;
            if (arr == null) return;

            var list = new List<Cell>();
            int w = arr.GetLength(0);
            int h = arr.GetLength(1);
            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < h; j++)
                {
                    var c = arr[i, j];
                    if (c != null && c.lightStrength > 0) list.Add(c);
                }
            }

            if (list.Count == 0) return;

            cells = list.ToArray();
            originals = new Color[cells.Length];
            for (int i = 0; i < cells.Length; i++)
                originals[i] = cells[i].lightColor;

            if (!skyCaptured)
            {
                originalSky = Shader.GetGlobalColor("_SkyboxColor");
                skyCaptured = true;
            }
            Shader.SetGlobalColor("_SkyboxColor", Color.red);
        }

        public static void Tick(EnvironmentController ec)
        {
            if (ec == null || cells == null || originals == null) return;

            frameCounter++;
            if (frameCounter % UpdateEveryFrames != 0) return;

            float t = Time.time / CycleSeconds;
            float k = (Mathf.Sin(t * Mathf.PI * 2f) * 0.5f + 0.5f);

            for (int i = 0; i < cells.Length; i++)
            {
                var c = cells[i];
                if (c == null) continue;
                c.lightColor = Color.Lerp(originals[i], Color.red, k);
                try { ec.RegenerateLight(c); } catch { }
            }
        }

        public static void RestoreSky()
        {
            if (!skyCaptured) return;
            Shader.SetGlobalColor("_SkyboxColor", originalSky);
            skyCaptured = false;
        }

        public static void Clear()
        {
            cells = null;
            originals = null;
            frameCounter = 0;
        }
    }

    [HarmonyPatch(typeof(BaseGameManager), "Update")]
    internal static class BsodaChallengeTriggerPatch
    {
        private static HappyBaldi? happyBaldi;
        private static PlayerManager? playerManager;
        private static float dist;

        public static void ResetCache()
        {
            happyBaldi = null;
            playerManager = null;
            dist = 0f;
        }

        [HarmonyPostfix]
        static void Postfix(BaseGameManager __instance)
        {
            if (!BsodaChallengeMode.Active) return;
            if (__instance == null) return;

            if (__instance.InPitstop())
            {
                if (BsodaChallengeAudio.Instance != null) BsodaChallengeAudio.Stop();
                return;
            }

            if (BsodaChallengeMode.Started) return;

            if (happyBaldi == null)
                happyBaldi = Object.FindObjectOfType<HappyBaldi>();

            if (playerManager == null)
                playerManager = Singleton<CoreGameManager>.Instance?.GetPlayer(0);

            if (happyBaldi == null || playerManager == null) return;

            dist = Vector3.Distance(happyBaldi.transform.position, playerManager.transform.position);
            if (dist <= BsodaChallengeMode.StartDistance) return;

            BsodaChallengeMode.Started = true;
            BsodaChallengeState.Elapsed = 0f;

            Singleton<MusicManager>.Instance?.StopMidi();
            __instance.BeginSpoopMode();
            __instance.Ec.SpawnNPCs();
            __instance.Ec.StartEventTimers();

            var baldi = __instance.Ec.GetBaldi();
            if (baldi != null)
            {
                baldi.transform.position = happyBaldi.transform.position;
                baldi.TargetPosition(playerManager.transform.position);
            }

            Object.Destroy(happyBaldi.gameObject);
            happyBaldi = null;

            var ec = __instance.Ec;
            try { ec.SetAllLights(true); } catch { }
            try { ec.InitializeLighting(); } catch { }
            try { BsodaChallengeRedLight.Capture(ec); } catch { }

            BsodaChallengeAudio.Start();
        }
    }

    [HarmonyPatch(typeof(BaseGameManager), "Initialize")]
    internal static class BsodaChallengeResetPerLevel
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            if (!BsodaChallengeMode.Active) return;

            BsodaChallengeMode.Started = false;
            BsodaChallengeState.Elapsed = 0f;

            BsodaChallengeRedLight.Clear();
            BsodaChallengeRedLight.RestoreSky();
            BsodaChallengeAudio.Stop();
            BsodaChallengeTriggerPatch.ResetCache();
        }
    }

    [HarmonyPatch(typeof(EnvironmentController), "Update")]
    internal static class BsodaChallengeUpdatePatch
    {
        private static ItemObject? cachedBsoda;
        private static ItemObject? cachedDietBsoda;
        private static Vector3 lastTarget = Vector3.zero;
        private const float SpeedMultiplier = 1.45f;
        private const float PerNotebook = 1.2f;

        [HarmonyPostfix]
        static void Postfix(EnvironmentController __instance)
        {
            if (!BsodaChallengeMode.Active || __instance == null) return;

            var bm = Singleton<BaseGameManager>.Instance;
            if (bm != null && bm.InPitstop()) return;

            var cgm = Singleton<CoreGameManager>.Instance;
            if (cgm == null) return;
            var pm = cgm.GetPlayer(0);
            if (pm == null) return;

            GiveItems(pm);

            if (!BsodaChallengeMode.Started) return;

            BsodaChallengeState.Elapsed += Time.deltaTime;
            BsodaChallengeRedLight.Tick(__instance);

            var baldi = __instance.GetBaldi();
            if (baldi == null || baldi.Navigator == null) return;

            int notebooks = 0;
            if (bm != null) notebooks = bm.FoundNotebooks;

            baldi.baseSpeed = (3.5f + notebooks * PerNotebook) * SpeedMultiplier;
            baldi.GetAngry(Time.deltaTime * 0.05f);

            Vector3 ppos = pm.transform.position;
            bool retarget = !baldi.Navigator.HasDestination;
            if ((lastTarget - ppos).sqrMagnitude > 9f) retarget = true;

            if (retarget)
            {
                lastTarget = ppos;
                baldi.TargetPosition(ppos);
            }
        }

        private static void GiveItems(PlayerManager pm)
        {
            if (pm.itm == null) return;

            if (cachedBsoda == null || cachedDietBsoda == null)
            {
                var all = Resources.FindObjectsOfTypeAll<ItemObject>();
                for (int i = 0; i < all.Length; i++)
                {
                    var it = all[i];
                    if (it == null) continue;
                    if (cachedBsoda == null && it.itemType == Items.Bsoda) cachedBsoda = it;
                    else if (cachedDietBsoda == null && it.itemType == Items.DietBsoda) cachedDietBsoda = it;
                    if (cachedBsoda != null && cachedDietBsoda != null) break;
                }
            }

            var items = pm.itm.items;
            if (items == null) return;

            bool hasBsoda = false;
            bool hasDiet = false;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                if (items[i].itemType == Items.Bsoda)
                {
                    hasBsoda = true;
                    BsodaInfiniteHelper.ForceInfinite(items[i]);
                }
                else if (items[i].itemType == Items.DietBsoda)
                {
                    hasDiet = true;
                    BsodaInfiniteHelper.ForceInfinite(items[i]);
                }
            }

            bool changed = false;
            if (!hasBsoda && cachedBsoda != null) changed |= TryAdd(items, cachedBsoda);
            if (!hasDiet && cachedDietBsoda != null) changed |= TryAdd(items, cachedDietBsoda);

            if (changed) pm.itm.UpdateItems();
        }

        private static bool TryAdd(ItemObject[] items, ItemObject obj)
        {
            BsodaInfiniteHelper.ForceInfinite(obj);
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null || items[i].itemType == Items.None)
                {
                    items[i] = obj;
                    return true;
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(GameCamera), "LateUpdate")]
    internal static class BsodaChallengeCameraPatch
    {
        private static float baseFov = 60f;
        private static bool captured = false;

        public static void ResetCapture()
        {
            captured = false;
            baseFov = 60f;
        }

        [HarmonyPostfix]
        static void Postfix(GameCamera __instance)
        {
            if (!BsodaChallengeMode.Active || !BsodaChallengeMode.Started || __instance == null) return;

            var bm = Singleton<BaseGameManager>.Instance;
            if (bm != null && bm.InPitstop()) return;

            if (!captured && __instance.camCom != null)
            {
                baseFov = __instance.camCom.fieldOfView;
                captured = true;
            }

            float pulse = BsodaChallengeAudio.Pulse();
            float targetFov = baseFov * (1f + pulse * 0.18f);

            if (__instance.camCom != null)
                __instance.camCom.fieldOfView = Mathf.Lerp(__instance.camCom.fieldOfView, targetFov, Time.unscaledDeltaTime * 8f);

            if (__instance.billboardCam != null)
                __instance.billboardCam.fieldOfView = __instance.camCom != null ? __instance.camCom.fieldOfView : targetFov;
        }
    }

    [HarmonyPatch]
    internal static class BsodaChallengeNoMidiPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var m in typeof(MusicManager).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.Name == "PlayMidi") yield return m;
            }
        }

        static bool Prefix()
        {
            if (!BsodaChallengeMode.Active) return true;
            if (!BsodaChallengeMode.Started) return true;
            var bm = Singleton<BaseGameManager>.Instance;
            if (bm != null && bm.InPitstop()) return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(CoreGameManager), "ReturnToMenu")]
    internal static class BsodaChallengeResetPatch
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            BsodaChallengeMode.Active = false;
            BsodaChallengeMode.Started = false;
            BsodaChallengeState.Elapsed = 0f;
            BsodaChallengeRedLight.Clear();
            BsodaChallengeRedLight.RestoreSky();
            BsodaChallengeAudio.Stop();
            BsodaChallengeCameraPatch.ResetCapture();
            BsodaChallengeTriggerPatch.ResetCache();
        }
    }

    [HarmonyPatch(typeof(PlaceholderWinManager), "Initialize")]
    internal static class BlitzoChallengeEndingPatch
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            if (!BsodaChallengeMode.Active) return;
            if (!BsodaChallengeMode.Started) return;

            BsodaChallengeRedLight.Clear();
            BsodaChallengeRedLight.RestoreSky();
            BsodaChallengeAudio.Stop();
            BsodaChallengeCameraPatch.ResetCapture();

            BsodaChallengeMode.Active = false;
            BsodaChallengeMode.Started = false;
            BsodaChallengeState.Elapsed = 0f;

            BlitzoChallengesProgress.Complete("bsoda");
        }
    }

    public static class BlitzoChallengesProgress
    {
        private const string PrefKey = "blitzo.challenges.done";
        private static readonly string[] All = { "bsoda" };
        private static readonly HashSet<string> done = Load();

        public static bool AllDone => AllDoneCheck();
        public static bool IsDone(string id) => done.Contains(id);

        public static void Complete(string id)
        {
            if (!done.Add(id)) return;

            string s = "";
            foreach (var x in done)
            {
                if (s.Length > 0) s += ",";
                s += x;
            }
            PlayerPrefs.SetString(PrefKey, s);
            PlayerPrefs.Save();
        }

        private static bool AllDoneCheck()
        {
            for (int i = 0; i < All.Length; i++)
                if (!done.Contains(All[i])) return false;
            return true;
        }

        private static HashSet<string> Load()
        {
            var s = new HashSet<string>();
            var raw = PlayerPrefs.GetString(PrefKey, "");
            if (string.IsNullOrEmpty(raw)) return s;

            var parts = raw.Split(new char[] { ',' });
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                if (p != null && p.Length > 0) s.Add(p);
            }
            return s;
        }
    }
}