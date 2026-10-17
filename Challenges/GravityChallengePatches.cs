using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BlitzoChallengesPack
{
    public static class GravityChallengeMode
    {
        public static bool Active = false;
    }

    [HarmonyPatch(typeof(PlayerEntity), "Flip")]
    internal static class PlayerFlipNoSlowPatch
    {
        [HarmonyPrefix]
        static bool Prefix(PlayerEntity __instance)
        {
            if (!GravityChallengeMode.Active) return true;

            var flippedField = typeof(Entity).GetField("flipped",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (flippedField == null) return true;

            bool flipped = (bool)flippedField.GetValue(__instance);
            flipped = !flipped;
            flippedField.SetValue(__instance, flipped);

            __instance.SetBaseRotation(flipped ? 180f : 0f);
            __instance.SetSpriteRotation(flipped ? 180f : 0f);

            var spinningField = typeof(PlayerEntity).GetField("playerSpinning",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (spinningField != null) spinningField.SetValue(__instance, false);

            var rotationLeftField = typeof(PlayerEntity).GetField("rotationLeft",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (rotationLeftField != null) rotationLeftField.SetValue(__instance, 0f);

            var playerRotationField = typeof(PlayerEntity).GetField("playerRotation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (playerRotationField != null) playerRotationField.SetValue(__instance, flipped ? 180f : 0f);

            return false;
        }
    }

    [HarmonyPatch(typeof(Entity), "Flip")]
    internal static class EntityFlipNoSlowPatch
    {
        [HarmonyPrefix]
        static bool Prefix(Entity __instance)
        {
            if (!GravityChallengeMode.Active) return true;
            if (__instance is PlayerEntity) return true;

            var flippedField = typeof(Entity).GetField("flipped",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (flippedField == null) return true;

            bool flipped = (bool)flippedField.GetValue(__instance);
            flippedField.SetValue(__instance, !flipped);

            if (!flipped)
            {
                __instance.SetSpriteRotation(180f);
                __instance.SetBaseRotation(180f);
            }
            else
            {
                __instance.SetSpriteRotation(0f);
                __instance.SetBaseRotation(0f);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(GravityFlipper), "OnTriggerEnter")]
    internal static class GravityFlipperNoPausePatch
    {
        [HarmonyPrefix]
        static bool Prefix(GravityFlipper __instance, Collider other)
        {
            if (!GravityChallengeMode.Active) return true;
            if (other == null || !other.isTrigger) return false;

            var evField = typeof(GravityFlipper).GetField("gravityEvent",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (evField == null) return false;
            var ev = evField.GetValue(__instance) as GravityEvent;
            if (ev == null) return false;

            if (other.CompareTag("Player"))
            {
                ev.FlipPlayer();
                ev.DestroyFlipper(__instance);
                return false;
            }

            if (other.CompareTag("NPC"))
            {
                var npc = other.GetComponent<NPC>();
                if (npc != null) ev.FlipNPC(npc);
                ev.DestroyFlipper(__instance);
                return false;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(GravityEvent), "End")]
    internal static class GravityEventNeverEndsPatch
    {
        [HarmonyPrefix]
        static bool Prefix() => !GravityChallengeMode.Active;
    }

    [HarmonyPatch(typeof(GravityEvent), "Begin")]
    internal static class GravityEventStartNoDelayPatch
    {
        [HarmonyPostfix]
        static void Postfix(GravityEvent __instance)
        {
            if (!GravityChallengeMode.Active) return;
            var f = typeof(GravityEvent).GetField("initialSpawnDelay",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null) f.SetValue(__instance, 0.01f);
        }
    }

    [HarmonyPatch(typeof(EnvironmentController), "StartEventTimers")]
    internal static class GravityChallengeEventPatch
    {
        [HarmonyPostfix]
        static void Postfix(EnvironmentController __instance)
        {
            if (!GravityChallengeMode.Active) return;
            if (__instance == null) return;

            try
            {
                var eventsField = typeof(EnvironmentController).GetField("events",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var eventTimesField = typeof(EnvironmentController).GetField("eventTimes",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (eventsField == null || eventTimesField == null) return;

                var events = eventsField.GetValue(__instance) as List<RandomEvent>;
                var eventTimes = eventTimesField.GetValue(__instance) as List<float>;
                if (events == null || eventTimes == null) return;

                if (events.Find(e => e is GravityEvent) != null) return;

                GravityEvent? prefab = null;
                var all = Resources.FindObjectsOfTypeAll<GravityEvent>();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] == null) continue;
                    if (!all[i].gameObject.scene.IsValid())
                    {
                        prefab = all[i];
                        break;
                    }
                }
                if (prefab == null) return;

                var gv = UnityEngine.Object.Instantiate(prefab);
                gv.Initialize(__instance, new System.Random());
                events.Add(gv);
                eventTimes.Add(0.01f);
                gv.Begin();
            }
            catch { }
        }
    }
}