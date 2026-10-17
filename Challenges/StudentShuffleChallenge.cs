using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BlitzoChallengesPack
{
    public static class StudentShuffleChallengeMode
    {
        public static bool Active = false;
    }

    [HarmonyPatch]
    internal static class StudentShuffleChallengePatch
    {
        private static StudentEvent? _eventPrefab;
        private static FieldInfo? eventsField;
        private static FieldInfo? eventTimesField;
        private static FieldInfo? activeField;
        private static FieldInfo? remainingTimeField;
        private static FieldInfo? eventSpawnerRoomGroupField;
        private static FieldInfo? ecField;

        static StudentShuffleChallengePatch()
        {
            var envType = typeof(EnvironmentController);
            eventsField = envType.GetField("events", BindingFlags.Instance | BindingFlags.NonPublic);
            eventTimesField = envType.GetField("eventTimes", BindingFlags.Instance | BindingFlags.NonPublic);

            var randomEventType = typeof(RandomEvent);
            activeField = randomEventType.GetField("active", BindingFlags.Instance | BindingFlags.NonPublic);
            remainingTimeField = randomEventType.GetField("remainingTime", BindingFlags.Instance | BindingFlags.NonPublic);
            ecField = randomEventType.GetField("ec",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            eventSpawnerRoomGroupField = typeof(LevelGenerator).GetField("eventSpawnerRoomGroup",
                BindingFlags.Instance | BindingFlags.NonPublic);
        }

        [HarmonyPatch(typeof(LevelGenerator), "StartGenerate")]
        [HarmonyPrefix]
        private static void ForceEventSpawnerRoom(LevelGenerator __instance)
        {
            if (!StudentShuffleChallengeMode.Active) return;
            if (eventSpawnerRoomGroupField == null) return;
            if (__instance == null) return;

            var spawnerGroup = eventSpawnerRoomGroupField.GetValue(__instance) as RoomGroup;
            if (spawnerGroup == null) return;

            var ld = __instance.ld;
            if (ld == null) return;
            if (ld.roomGroup == null) return;

            if (!ld.roomGroup.Contains(spawnerGroup))
                ld.roomGroup.Insert(0, spawnerGroup);
        }

        [HarmonyPatch(typeof(EnvironmentController), "StartEventTimers")]
        [HarmonyPostfix]
        private static void StartEventTimersPostfix(EnvironmentController __instance)
        {
            if (!StudentShuffleChallengeMode.Active) return;
            if (__instance == null) return;
            if (eventsField == null || eventTimesField == null) return;

            try
            {
                if (__instance.eventSpawnRooms == null) return;

                if (__instance.eventSpawnRooms.Count == 0)
                {
                    if (__instance.rooms != null && __instance.rooms.Count > 0)
                        __instance.eventSpawnRooms.Add(__instance.rooms[0]);
                    else if (__instance.mainHall != null)
                        __instance.eventSpawnRooms.Add(__instance.mainHall);
                    else
                        return;
                }

                var events = eventsField.GetValue(__instance) as List<RandomEvent>;
                var eventTimes = eventTimesField.GetValue(__instance) as List<float>;
                if (events == null || eventTimes == null) return;

                var existing = events.OfType<StudentEvent>().FirstOrDefault();
                if (existing != null)
                {
                    if (activeField != null && !(bool)activeField.GetValue(existing))
                    {
                        activeField.SetValue(existing, true);
                        if (remainingTimeField != null) remainingTimeField.SetValue(existing, float.MaxValue);
                        existing.Begin();
                    }
                    return;
                }

                if (_eventPrefab == null)
                    _eventPrefab = Resources.FindObjectsOfTypeAll<StudentEvent>().FirstOrDefault();
                var prefab = _eventPrefab;
                if (prefab == null) return;

                var newEvent = Object.Instantiate(prefab);
                if (newEvent == null) return;

                newEvent.Initialize(__instance, new System.Random());

                if (activeField != null) activeField.SetValue(newEvent, true);
                if (remainingTimeField != null) remainingTimeField.SetValue(newEvent, float.MaxValue);

                newEvent.Begin();

                events.Add(newEvent);
                eventTimes.Add(float.MaxValue);
            }
            catch { }
        }

        [HarmonyPatch(typeof(StudentEvent), "End")]
        [HarmonyPrefix]
        private static bool PreventEnd()
        {
            return !StudentShuffleChallengeMode.Active;
        }

        [HarmonyPatch(typeof(Door), "VirtualTriggerHit")]
        [HarmonyPrefix]
        private static bool DoorVirtualTriggerHit(Door __instance)
        {
            if (!StudentShuffleChallengeMode.Active) return true;
            if (__instance is SwingDoor swingDoor && swingDoor.audMan == null)
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(StudentEvent), "Begin")]
    internal static class StudentShuffleBeginGuard
    {
        [HarmonyPrefix]
        static bool Prefix(StudentEvent __instance)
        {
            if (!StudentShuffleChallengeMode.Active) return true;
            if (__instance == null) return false;

            var ecField = typeof(RandomEvent).GetField("ec",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (ecField == null) return false;

            var ec = ecField.GetValue(__instance) as EnvironmentController;
            if (ec == null) return false;
            if (ec.eventSpawnRooms == null || ec.eventSpawnRooms.Count == 0) return false;

            for (int i = 0; i < ec.eventSpawnRooms.Count; i++)
            {
                if (ec.eventSpawnRooms[i] == null) continue;
                return true;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(CoreGameManager), "ReturnToMenu")]
    internal static class StudentShuffleResetPatch
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            StudentShuffleChallengeMode.Active = false;
        }
    }
}
