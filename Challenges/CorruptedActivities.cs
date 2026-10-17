using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace BlitzoChallengesPack
{
    public static class CorruptedActivitiesMode
    {
        public static bool Active = false;
    }

    internal static class CorruptedActivityTargets
    {
        public static IEnumerable<MethodBase> Overrides(string methodName)
        {
            var result = new List<MethodBase>();
            Type[] types;
            try { types = typeof(Activity).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types; }

            for (int i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (type == null || !type.IsSubclassOf(typeof(Activity))) continue;
                MethodBase? method = null;
                try { method = AccessTools.DeclaredMethod(type, methodName, new Type[0]); } catch { }
                if (method != null && !method.IsAbstract) result.Add(method);
            }
            return result;
        }
    }

    [HarmonyPatch]
    internal static class CorruptedActivitiesReInitPatch
    {
        static IEnumerable<MethodBase> TargetMethods() => CorruptedActivityTargets.Overrides("ReInit");

        [HarmonyPostfix]
        static void Postfix(Activity __instance)
        {
            if (!CorruptedActivitiesMode.Active || __instance == null) return;
            try { __instance.Corrupt(true); } catch { }
        }
    }

    public class BalloonBusterFlicker : MonoBehaviour
    {
        public static readonly Dictionary<BalloonBuster, BalloonBusterFlicker> Instances =
            new Dictionary<BalloonBuster, BalloonBusterFlicker>();

        private BalloonBuster buster = null!;
        private TMP_Text? solutionTmp;
        private System.Random rng = new System.Random();

        private static FieldInfo? solutionField;
        private static FieldInfo? solutionTmpField;
        private static FieldInfo? poweredField;

        public static void Attach(BalloonBuster buster)
        {
            if (buster == null || Instances.ContainsKey(buster)) return;
            var go = new GameObject("BalloonBusterFlicker");
            go.transform.SetParent(buster.transform, false);
            var comp = go.AddComponent<BalloonBusterFlicker>();
            comp.buster = buster;
            comp.Init();
            Instances[buster] = comp;
        }

        private void Init()
        {
            var t = typeof(BalloonBuster);
            solutionField = t.GetField("solution", BindingFlags.Instance | BindingFlags.NonPublic);
            solutionTmpField = t.GetField("solutionTmp", BindingFlags.Instance | BindingFlags.NonPublic);
            poweredField = typeof(Activity).GetField("powered", BindingFlags.Instance | BindingFlags.NonPublic);
            if (solutionTmpField != null) solutionTmp = solutionTmpField.GetValue(buster) as TMP_Text;
        }

        private void Update()
        {
            if (!CorruptedActivitiesMode.Active || buster == null) return;
            if (solutionField == null || solutionTmp == null) return;
            if (poweredField != null && !(bool)poweredField.GetValue(buster)) return;

            int newSolution = rng.Next(0, 10);
            solutionField.SetValue(buster, newSolution);
            solutionTmp.text = newSolution.ToString();
        }
    }

    [HarmonyPatch(typeof(BalloonBuster), "ReInit")]
    internal static class BalloonBusterReInitPatch
    {
        [HarmonyPostfix]
        static void Postfix(BalloonBuster __instance)
        {
            if (!CorruptedActivitiesMode.Active) return;
            BalloonBusterFlicker.Attach(__instance);
        }
    }

    [HarmonyPatch(typeof(BalloonBuster), "CheckCount")]
    internal static class BalloonBusterInstantFailPatch
    {
        [HarmonyPrefix]
        static bool Prefix(BalloonBuster __instance)
        {
            if (!CorruptedActivitiesMode.Active || __instance == null) return true;
            try { __instance.SubmitAnswer(); } catch { }
            return false;
        }
    }

    [HarmonyPatch]
    internal static class ActivityAlwaysWrongPatch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Activity), "Completed", new Type[] { typeof(int), typeof(bool) });
        }

        [HarmonyPrefix]
        static void Prefix(Activity __instance, ref bool correct)
        {
            if (!CorruptedActivitiesMode.Active || __instance == null) return;
            if (__instance as BalloonBuster == null) return;
            correct = false;
        }
    }

    [HarmonyPatch(typeof(MatchActivityBalloon), "ReInit")]
    internal static class MatchBalloonRedSpritePatch
    {
        private static FieldInfo? spriteRendererField;
        private static FieldInfo? redField;
        private static FieldInfo? revealedSpriteField;

        static bool Prepare()
        {
            var t = typeof(MatchActivityBalloon);
            spriteRendererField = t.GetField("spriteRenderer", BindingFlags.Instance | BindingFlags.NonPublic);
            redField = t.GetField("redQuestionMarkSprite", BindingFlags.Instance | BindingFlags.NonPublic);
            revealedSpriteField = t.GetField("revealedSprite", BindingFlags.Instance | BindingFlags.NonPublic);
            return spriteRendererField != null && redField != null;
        }

        [HarmonyPostfix]
        static void Postfix(MatchActivityBalloon __instance)
        {
            if (!CorruptedActivitiesMode.Active || __instance == null) return;
            if (spriteRendererField == null || redField == null) return;

            var renderer = spriteRendererField.GetValue(__instance) as SpriteRenderer;
            if (renderer == null) return;

            var red = redField.GetValue(__instance) as Sprite;
            if (red != null) renderer.sprite = red;
            if (revealedSpriteField != null) revealedSpriteField.SetValue(__instance, red);
        }
    }

    [HarmonyPatch(typeof(MatchActivityBalloon), "RevealAnimation")]
    internal static class MatchBalloonRedRevealPatch
    {
        private static FieldInfo? redField;
        private static FieldInfo? revealedSpriteField;

        static bool Prepare()
        {
            var t = typeof(MatchActivityBalloon);
            redField = t.GetField("redQuestionMarkSprite", BindingFlags.Instance | BindingFlags.NonPublic);
            revealedSpriteField = t.GetField("revealedSprite", BindingFlags.Instance | BindingFlags.NonPublic);
            return redField != null;
        }

        [HarmonyPrefix]
        static void Prefix(MatchActivityBalloon __instance)
        {
            if (!CorruptedActivitiesMode.Active || __instance == null) return;
            if (redField == null) return;

            var red = redField.GetValue(__instance) as Sprite;
            if (red != null && revealedSpriteField != null)
                revealedSpriteField.SetValue(__instance, red);
        }
    }

    [HarmonyPatch(typeof(MatchActivity), "BalloonRevealed")]
    internal static class MatchActivityAlwaysFailPatch
    {
        private static FieldInfo? revealedListField;

        static bool Prepare()
        {
            revealedListField = typeof(MatchActivity).GetField("revealedBalloons",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return revealedListField != null;
        }

        [HarmonyPrefix]
        static bool Prefix(MatchActivity __instance, MatchActivityBalloon revealedBalloon)
        {
            if (!CorruptedActivitiesMode.Active || __instance == null) return true;
            if (revealedBalloon == null || revealedListField == null) return true;

            var list = revealedListField.GetValue(__instance) as List<MatchActivityBalloon>;
            if (list == null) return true;

            list.Add(revealedBalloon);

            if (list.Count > 1)
            {
                list.Clear();
                try { __instance.Fail(); } catch { }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(MatchActivity), "CompletionCheck")]
    internal static class MatchActivityNoCorrectPatch
    {
        [HarmonyPrefix]
        static bool Prefix(MatchActivity __instance)
        {
            if (!CorruptedActivitiesMode.Active || __instance == null) return true;

            var completedField = typeof(Activity).GetField("completed",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (completedField != null && (bool)completedField.GetValue(__instance)) return false;

            try { __instance.Fail(); } catch { }
            return false;
        }
    }

    [HarmonyPatch(typeof(MatchActivity), "Completed", new Type[] { typeof(int), typeof(bool) })]
    internal static class MatchActivityCompletedForceWrongPatch
    {
        [HarmonyPrefix]
        static void Prefix(MatchActivity __instance, ref bool correct)
        {
            if (!CorruptedActivitiesMode.Active || __instance == null) return;
            correct = false;
        }
    }

    [HarmonyPatch(typeof(PlaceholderWinManager), "BeginPlay")]
    internal static class NullEndingBeginPlayPatch
    {
        [HarmonyPrefix]
        static bool Prefix(PlaceholderWinManager __instance)
        {
            if (!CorruptedActivitiesMode.Active) return true;
            if (__instance == null) return true;

            __instance.StartCoroutine(NullEndingRoutine(__instance));
            return false;
        }

        static IEnumerator NullEndingRoutine(PlaceholderWinManager manager)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;

            var cgm = Singleton<CoreGameManager>.Instance;
            if (cgm == null) yield break;

            var dancingField = typeof(PlaceholderWinManager).GetField("dancingBaldi",
                BindingFlags.Instance | BindingFlags.NonPublic);
            BaldiDance? dancing = null;
            if (dancingField != null)
                dancing = dancingField.GetValue(manager) as BaldiDance;

            if (dancing != null)
            {
                dancing.gameObject.SetActive(true);
                var spriteField = typeof(BaldiDance).GetField("baldiSprite",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (spriteField != null)
                {
                    var renderer = spriteField.GetValue(dancing) as SpriteRenderer;
                    if (renderer != null && BasePlugin.NullTexture != null)
                    {
                        var tex = BasePlugin.NullTexture;
                        tex.filterMode = FilterMode.Point;
                        renderer.sprite = Sprite.Create(tex,
                            new Rect(0, 0, tex.width, tex.height),
                            new Vector2(0.5f, 0.5f), 100f);
                        renderer.transform.localScale = renderer.transform.localScale * 1.8f;
                    }
                }

                var glitchField = typeof(BaldiDance).GetField("glitching",
                    BindingFlags.Instance | BindingFlags.Public);
                if (glitchField != null) glitchField.SetValue(dancing, false);

                var pos = dancing.transform.position;
                pos.y = Mathf.Max(pos.y, 1.83f);
                dancing.transform.position = pos;
            }

            Singleton<MusicManager>.Instance?.StopMidi();

            AudioSource? audioSource = null;
            if (BasePlugin.NullClassic != null)
            {
                audioSource = manager.gameObject.AddComponent<AudioSource>();
                audioSource.clip = BasePlugin.NullClassic;
                audioSource.spatialBlend = 0f;
                audioSource.volume = 1f;
                audioSource.playOnAwake = false;
                audioSource.Play();
            }

            if (audioSource != null && audioSource.clip != null)
            {
                while (audioSource.isPlaying) yield return null;
            }
            else
            {
                yield return new WaitForSeconds(3f);
            }

            if (audioSource != null) UnityEngine.Object.Destroy(audioSource);

            CorruptedActivitiesMode.Active = false;
            BlitzoChallengesProgress.Complete("corrupted");
            cgm.ReturnToMenu();
        }
    }
}
