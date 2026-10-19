using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlitzoChallengesPack
{
    public static class YtpsHuntMode
    {
        public static bool Active = false;
        public static int CurrentGoal = 0;
        public static bool GoalReached = false;
        public static Sprite? YtpsCounterSprite;

        public static float BaldiAngerAtFull = 5.1f;
        public static float BaldiAngerCurve = 1f;

        public static readonly int[] PointsPerLevel = new int[] { 100, 300, 500, 400, 700 };

        public static int GetPointsForLevel(int level)
        {
            int idx = Mathf.Clamp(level, 0, PointsPerLevel.Length - 1);
            return Mathf.Max(1, PointsPerLevel[idx]);
        }

        public static float GetTargetAnger(int curPoints)
        {
            if (CurrentGoal <= 0) return 0.1f;

            float ratio = (float)curPoints / CurrentGoal;
            if (ratio < 0f) ratio = 0f;
            if (ratio > 1f) ratio = 1f;

            float curved = Mathf.Pow(ratio, BaldiAngerCurve);
            return Mathf.Lerp(0.1f, BaldiAngerAtFull, curved);
        }
    }

    public class YtpsHuntRunner : MonoBehaviour
    {
        public static YtpsHuntRunner? Instance;

        private HudManager? cachedHud;
        private TMP_Text? notebookText;
        private Image? notebookIconImage;
        private bool searchedText;
        private bool searchedIcon;

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("YtpsHuntRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<YtpsHuntRunner>();
        }

        public static void Stop()
        {
            if (Instance != null && Instance.gameObject != null)
                UnityEngine.Object.Destroy(Instance.gameObject);
            Instance = null;
        }

        private void Update()
        {
            if (!YtpsHuntMode.Active) return;

            var cgm = Singleton<CoreGameManager>.Instance;
            if (cgm == null) return;
            var hud = cgm.GetHud(0);
            if (hud == null) return;

            var bm = Singleton<BaseGameManager>.Instance;
            bool inPitstop = bm != null && bm.InPitstop();

            if (inPitstop)
            {
                if (notebookText != null && notebookText.gameObject.activeSelf)
                    notebookText.gameObject.SetActive(false);
                return;
            }

            if (cachedHud != hud)
            {
                cachedHud = hud;
                notebookText = null;
                notebookIconImage = null;
                searchedText = false;
                searchedIcon = false;
            }

            if (!searchedText)
            {
                searchedText = true;
                var all = hud.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    var t = all[i];
                    if (t == null) continue;
                    if (t.name == "Notebook Text")
                    {
                        notebookText = t.GetComponent<TMP_Text>();
                        if (notebookText == null) notebookText = t.GetComponentInChildren<TMP_Text>(true);
                        break;
                    }
                }
            }

            if (!searchedIcon)
            {
                searchedIcon = true;
                var all = hud.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    var t = all[i];
                    if (t == null) continue;
                    if (t.name == "NotebookIcon")
                    {
                        notebookIconImage = t.GetComponent<Image>();
                        if (notebookIconImage == null) notebookIconImage = t.GetComponentInChildren<Image>(true);
                        if (notebookIconImage != null)
                        {
                            notebookIconImage.gameObject.SetActive(true);
                            notebookIconImage.enabled = true;
                            notebookIconImage.color = Color.white;
                        }

                        var anim = t.GetComponentInChildren<Animator>(true);
                        if (anim != null && anim.enabled)
                        {
                            anim.enabled = false;
                            anim.Rebind();
                        }
                        break;
                    }
                }
            }

            if (notebookText != null && YtpsHuntMode.CurrentGoal > 0)
            {
                if (!notebookText.gameObject.activeSelf) notebookText.gameObject.SetActive(true);
                if (!notebookText.enabled) notebookText.enabled = true;
                if (notebookText.color != Color.black) notebookText.color = Color.black;
                int cur = cgm.GetPointsThisLevel(0);
                if (cur > YtpsHuntMode.CurrentGoal) cur = YtpsHuntMode.CurrentGoal;
                string nt = cur + "/" + YtpsHuntMode.CurrentGoal;
                if (notebookText.text != nt) notebookText.text = nt;
            }

            if (notebookIconImage != null && YtpsHuntMode.YtpsCounterSprite != null)
            {
                if (notebookIconImage.sprite != YtpsHuntMode.YtpsCounterSprite)
                    notebookIconImage.sprite = YtpsHuntMode.YtpsCounterSprite;
            }

            if (YtpsHuntMode.GoalReached) return;
            if (YtpsHuntMode.CurrentGoal <= 0) return;

            if (bm == null) return;
            var ec = bm.Ec;
            if (ec == null) return;

            int pointsNow = cgm.GetPointsThisLevel(0);
            if (pointsNow < YtpsHuntMode.CurrentGoal) return;

            YtpsHuntMode.GoalReached = true;

            try
            {
                var method = AccessTools.Method(typeof(BaseGameManager), "AllNotebooks");
                if (method != null) method.Invoke(bm, null);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(BaseGameManager), "Initialize")]
    internal static class YtpsHuntInitPatch
    {
        [HarmonyPostfix]
        static void Postfix(BaseGameManager __instance)
        {
            if (!YtpsHuntMode.Active) return;
            if (__instance == null) return;

            var ec = __instance.Ec;
            if (ec == null) return;

            YtpsHuntMode.GoalReached = false;

            int level = 0;
            var cgm = Singleton<CoreGameManager>.Instance;
            if (cgm != null && cgm.sceneObject != null) level = cgm.sceneObject.levelNo;

            YtpsHuntMode.CurrentGoal = YtpsHuntMode.GetPointsForLevel(level);

            var list = ec.notebooks;
            if (list != null)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var nb = list[i];
                    if (nb == null) continue;
                    try { UnityEngine.Object.Destroy(nb.gameObject); } catch { }
                    list.RemoveAt(i);
                }
            }

            ec.notebookTotal = 0;

            if (YtpsHuntRunner.Instance != null)
            {
                var textField = AccessTools.Field(typeof(YtpsHuntRunner), "notebookText");
                if (textField != null) textField.SetValue(YtpsHuntRunner.Instance, null);
                var iconField = AccessTools.Field(typeof(YtpsHuntRunner), "notebookIconImage");
                if (iconField != null) iconField.SetValue(YtpsHuntRunner.Instance, null);
                var stField = AccessTools.Field(typeof(YtpsHuntRunner), "searchedText");
                if (stField != null) stField.SetValue(YtpsHuntRunner.Instance, false);
                var siField = AccessTools.Field(typeof(YtpsHuntRunner), "searchedIcon");
                if (siField != null) siField.SetValue(YtpsHuntRunner.Instance, false);
            }
        }
    }

    [HarmonyPatch(typeof(HudManager), "UpdateNotebookText")]
    internal static class YtpsHuntBlockNotebookTextPatch
    {
        [HarmonyPrefix]
        static bool Prefix(int textVal)
        {
            if (!YtpsHuntMode.Active) return true;
            return textVal != 0;
        }
    }

    [HarmonyPatch(typeof(CoreGameManager), "AddPoints", new Type[] { typeof(int), typeof(int), typeof(bool) })]
    internal static class YtpsHuntPointsPatch
    {
        [HarmonyPostfix]
        static void Postfix(CoreGameManager __instance, int points, int player)
        {
            if (!YtpsHuntMode.Active) return;
            if (__instance == null) return;
            if (player != 0) return;
            if (YtpsHuntMode.CurrentGoal <= 0) return;

            int cur = __instance.GetPointsThisLevel(0);
            if (cur > YtpsHuntMode.CurrentGoal) cur = YtpsHuntMode.CurrentGoal;

            var hud = __instance.GetHud(0);
            if (hud == null) return;

            var all = hud.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                if (all[i].name != "Notebook Text") continue;

                var t = all[i].GetComponent<TMP_Text>();
                if (t == null) t = all[i].GetComponentInChildren<TMP_Text>(true);
                if (t != null)
                {
                    t.gameObject.SetActive(true);
                    t.enabled = true;
                    t.color = Color.black;
                    t.text = cur + "/" + YtpsHuntMode.CurrentGoal;
                }
                break;
            }
        }
    }

    [HarmonyPatch(typeof(Notebook), "Start")]
    internal static class YtpsHuntNoNotebooksPatch
    {
        [HarmonyPrefix]
        static bool Prefix(Notebook __instance)
        {
            if (!YtpsHuntMode.Active) return true;
            if (__instance == null) return true;
            try { UnityEngine.Object.Destroy(__instance.gameObject); } catch { }
            return false;
        }
    }

    [HarmonyPatch(typeof(Activity), "Initialize")]
    internal static class YtpsHuntBonusActivitiesPatch
    {
        private static readonly HashSet<Activity> processed = new HashSet<Activity>();

        [HarmonyPostfix]
        static void Postfix(Activity __instance)
        {
            if (!YtpsHuntMode.Active) return;
            if (__instance == null) return;
            if (processed.Contains(__instance)) return;
            processed.Add(__instance);
            try
            {
                if (!__instance.InBonusMode)
                    __instance.SetBonusMode(true);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(Activity), "ReInit")]
    internal static class YtpsHuntBonusActivitiesReInitPatch
    {
        [HarmonyPrefix]
        static bool Prefix(Activity __instance)
        {
            if (!YtpsHuntMode.Active) return true;
            if (__instance == null) return true;
            try
            {
                if (!__instance.InBonusMode)
                    __instance.SetBonusMode(true);
            }
            catch { }
            return true;
        }
    }

    [HarmonyPatch(typeof(Activity), "SpawnNotebook")]
    internal static class YtpsHuntNoActivityNotebookPatch
    {
        [HarmonyPrefix]
        static bool Prefix(Activity __instance)
        {
            if (!YtpsHuntMode.Active) return true;
            if (__instance == null) return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Baldi), "GetAngry")]
    internal static class YtpsHuntNoBaldiAngerPatch
    {
        [HarmonyPrefix]
        static bool Prefix()
        {
            if (!YtpsHuntMode.Active) return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(EnvironmentController), "Update")]
    internal static class YtpsHuntBaldiAngerPatch
    {
        private static FieldInfo? angerField;
        private static bool searched;

        [HarmonyPostfix]
        static void Postfix(EnvironmentController __instance)
        {
            if (!YtpsHuntMode.Active) return;
            if (__instance == null) return;
            if (YtpsHuntMode.CurrentGoal <= 0) return;

            var bm = Singleton<BaseGameManager>.Instance;
            if (bm != null && bm.InPitstop()) return;

            var cgm = Singleton<CoreGameManager>.Instance;
            if (cgm == null) return;

            var baldi = __instance.GetBaldi();
            if (baldi == null) return;

            if (!searched)
            {
                searched = true;
                angerField = AccessTools.Field(typeof(Baldi), "anger");
            }
            if (angerField == null) return;

            int cur = cgm.GetPointsThisLevel(0);
            if (cur > YtpsHuntMode.CurrentGoal) cur = YtpsHuntMode.CurrentGoal;

            float target = YtpsHuntMode.GetTargetAnger(cur);

            try { angerField.SetValue(baldi, target); }
            catch { }
        }
    }

    [HarmonyPatch(typeof(CoreGameManager), "ReturnToMenu")]
    internal static class YtpsHuntResetPatch
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            YtpsHuntMode.Active = false;
            YtpsHuntMode.CurrentGoal = 0;
            YtpsHuntMode.GoalReached = false;
            YtpsHuntRunner.Stop();
        }
    }

    [HarmonyPatch(typeof(PlaceholderWinManager), "Initialize")]
    internal static class YtpsHuntWinPatch
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            if (!YtpsHuntMode.Active) return;
            YtpsHuntMode.Active = false;
            YtpsHuntMode.CurrentGoal = 0;
            YtpsHuntMode.GoalReached = false;
            YtpsHuntRunner.Stop();
            BlitzoChallengesProgress.Complete("ytpshunt");
        }
    }
}