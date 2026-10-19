using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BlitzoChallengesPack
{
    public static class AlarmRushMode
    {
        public static bool Active = false;
        public static int NotebooksToStart = 2;
        public static AudioClip? AlarmMusic;
    }

    public class AlarmRushAudio : MonoBehaviour
    {
        public static AlarmRushAudio? Instance;
        private AudioSource? src;

        public static void Start()
        {
            if (Instance != null) return;
            if (AlarmRushMode.AlarmMusic == null) return;

            var go = new GameObject("AlarmRushMusic");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<AlarmRushAudio>();
            Instance.src = go.AddComponent<AudioSource>();
            Instance.src.clip = AlarmRushMode.AlarmMusic;
            Instance.src.loop = true;
            Instance.src.spatialBlend = 0f;
            Instance.src.ignoreListenerPause = false;
            Instance.src.playOnAwake = false;
            Instance.src.volume = MusicVolume();
            Instance.src.Play();
        }

        private void Update()
        {
            if (src == null) return;
            src.volume = MusicVolume();
            if (!src.isPlaying) src.Play();
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
                UnityEngine.Object.Destroy(Instance.gameObject);
            Instance = null;
        }
    }

    public class AlarmRushNPC : MonoBehaviour
    {
        public static AlarmRushNPC? Instance;

        private SpriteRenderer? spriteRenderer;
        private float speed = 17f;
        private Vector3 floatBase;
        private float floatPhase;
        private const float FloatHeight = 1.0f;
        private const float FloatSpeed = 1.5f;

        public static void Spawn()
        {
            if (Instance != null) return;

            var cgm = Singleton<CoreGameManager>.Instance;
            if (cgm == null) return;
            var player = cgm.GetPlayer(0);
            if (player == null) return;

            Sprite? alarmSprite = null;
            var allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
            for (int i = 0; i < allSprites.Length; i++)
            {
                var s = allSprites[i];
                if (s == null) continue;
                if (s.name == "AlarmClockDrop") { alarmSprite = s; break; }
            }

            if (alarmSprite == null)
            {
                var pickups = UnityEngine.Object.FindObjectsOfType<Pickup>();
                for (int i = 0; i < pickups.Length; i++)
                {
                    var p = pickups[i];
                    if (p == null || p.item == null) continue;
                    if (p.item.name == null) continue;
                    if (p.item.name.IndexOf("Alarm", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var sr = p.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null && sr.sprite != null) { alarmSprite = sr.sprite; break; }
                }
            }

            if (alarmSprite == null) return;

            int npcLayer = 0;
            int npcSortingLayer = 0;
            int npcSortingOrder = 0;
            Material? npcMaterial = null;

            var anyNpc = UnityEngine.Object.FindObjectOfType<NPC>();
            if (anyNpc != null && anyNpc.spriteRenderer != null && anyNpc.spriteRenderer.Length > 0)
            {
                var sr = anyNpc.spriteRenderer[0];
                if (sr != null)
                {
                    npcLayer = anyNpc.gameObject.layer;
                    npcSortingLayer = sr.sortingLayerID;
                    npcSortingOrder = sr.sortingOrder;
                    npcMaterial = sr.sharedMaterial;
                }
            }

            var go = new GameObject("AlarmRushNPC");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.layer = npcLayer;

            var visual = new GameObject("Visual");
            visual.layer = npcLayer;
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = Vector3.zero;

            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = alarmSprite;
            renderer.sortingLayerID = npcSortingLayer;
            renderer.sortingOrder = npcSortingOrder + 100;
            renderer.color = Color.white;

            var overlayMaterial = CreateOverlayMaterial(alarmSprite);
            if (overlayMaterial != null)
            {
                renderer.sharedMaterial = overlayMaterial;
            }
            else if (npcMaterial != null)
            {
                renderer.sharedMaterial = npcMaterial;
            }
            else if (shader != null)
            {
                var mat = new Material(shader);
                mat.mainTexture = alarmSprite.texture;
                mat.color = Color.white;
                renderer.sharedMaterial = mat;
            }

            var scale = 0.9f;
            visual.transform.localScale = new Vector3(scale, scale, 1f);

            Vector3 spawn = player.transform.position + new Vector3(20f, 0f, 20f);

            var bm = Singleton<BaseGameManager>.Instance;
            if (bm != null && bm.Ec != null)
            {
                var cell = bm.Ec.RandomCell(false, false, true);
                if (cell != null) spawn = cell.CenterWorldPosition;
            }
            spawn.y = 5f;
            go.transform.position = spawn;

            var npc = go.AddComponent<AlarmRushNPC>();
            npc.spriteRenderer = renderer;
            npc.floatBase = visual.transform.localPosition;
            npc.floatPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            go.AddComponent<AlarmRushAfterimage>();

            Instance = npc;
        }

        private static Material? CreateOverlayMaterial(Sprite sprite)
        {
            var overlayShader = Shader.Find("UI/Default");
            if (overlayShader == null) return null;

            var mat = new Material(overlayShader);
            mat.mainTexture = sprite.texture;
            mat.color = Color.white;
            mat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            mat.renderQueue = 4000;
            return mat;
        }

        public static void Stop()
        {
            if (Instance == null) return;
            var go = Instance.gameObject;
            if (go != null) UnityEngine.Object.Destroy(go);
            Instance = null;
        }

        private void LateUpdate()
        {
            if (!AlarmRushMode.Active) return;
            if (spriteRenderer == null) return;

            if (Camera.main != null)
            {
                var dir = spriteRenderer.transform.position - Camera.main.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                    spriteRenderer.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }

        private void Update()
        {
            if (!AlarmRushMode.Active) return;

            if (spriteRenderer != null)
            {
                var t = spriteRenderer.transform;
                var localPos = floatBase;
                localPos.y += Mathf.Sin(Time.time * FloatSpeed + floatPhase) * FloatHeight;
                t.localPosition = localPos;
            }

            if (Time.timeScale <= 0f) return;

            var cgm = Singleton<CoreGameManager>.Instance;
            if (cgm == null) return;
            var player = cgm.GetPlayer(0);
            if (player == null) return;

            Vector3 target = player.transform.position;
            target.y = transform.position.y;

            Vector3 worldPos = transform.position;
            Vector3 dir = target - worldPos;
            dir.y = 0f;
            float dist = dir.magnitude;
            if (dist > 0.1f)
            {
                dir /= dist;
                float step = speed * Time.deltaTime;
                if (step > dist) step = dist;
                worldPos += dir * step;
            }
            transform.position = worldPos;

            if (dist < 1.5f)
            {
                try { cgm.SetLives(0, true); } catch { }
                try { cgm.ReturnToMenu(); } catch { }
            }
        }
    }

    public class AlarmRushAfterimage : MonoBehaviour
    {
        private SpriteRenderer? sourceRenderer;
        private float timer;
        private const float Interval = 0.08f;

        private void Start()
        {
            sourceRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void Update()
        {
            if (!AlarmRushMode.Active) return;
            if (sourceRenderer == null) return;
            if (sourceRenderer.sprite == null) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = Interval;

            var trail = new GameObject("AlarmRushTrail");
            trail.transform.position = sourceRenderer.transform.position;
            trail.transform.rotation = sourceRenderer.transform.rotation;
            trail.transform.localScale = sourceRenderer.transform.lossyScale;

            var sr = trail.AddComponent<SpriteRenderer>();
            sr.sprite = sourceRenderer.sprite;
            sr.sortingLayerID = sourceRenderer.sortingLayerID;
            sr.sortingOrder = sourceRenderer.sortingOrder - 1;
            sr.sharedMaterial = sourceRenderer.sharedMaterial;
            sr.color = sourceRenderer.color;

            trail.AddComponent<AlarmRushAfterimageFader>();
        }
    }

    public class AlarmRushAfterimageFader : MonoBehaviour
    {
        private SpriteRenderer? sr;
        private float alpha = 1f;
        private const float FadeSpeed = 0.5f;

        private void Start()
        {
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) alpha = sr.color.a;
        }

        private void Update()
        {
            alpha -= FadeSpeed * Time.deltaTime;

            if (alpha <= 0f)
            {
                UnityEngine.Object.Destroy(gameObject);
                return;
            }

            if (sr != null)
            {
                var c = sr.color;
                c.a = alpha;
                sr.color = c;
            }
        }
    }

    [HarmonyPatch(typeof(BaseGameManager), "CollectNotebooks")]
    internal static class AlarmRushStartPatch
    {
        [HarmonyPostfix]
        static void Postfix(BaseGameManager __instance)
        {
            if (!AlarmRushMode.Active) return;
            if (__instance == null) return;
            if (__instance is TutorialGameManager) return;

            if (__instance.FoundNotebooks < AlarmRushMode.NotebooksToStart) return;

            if (AlarmRushAudio.Instance == null)
                AlarmRushAudio.Start();

            if (AlarmRushNPC.Instance == null)
            {
                Singleton<MusicManager>.Instance?.StopMidi();
                AlarmRushNPC.Spawn();
            }
        }
    }

    [HarmonyPatch(typeof(EnvironmentController), "SpawnNPCs")]
    internal static class AlarmRushNoNpcsPatch
    {
        [HarmonyPrefix]
        static bool Prefix(EnvironmentController __instance)
        {
            if (!AlarmRushMode.Active) return true;
            if (__instance == null) return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Structure_StudentSpawner), "SpawnStudents")]
    internal static class AlarmRushNoStudentsPatch
    {
        [HarmonyPrefix]
        static bool Prefix() => !AlarmRushMode.Active;
    }

    [HarmonyPatch(typeof(HappyBaldi), "SpawnWait")]
    internal static class AlarmRushSkipHappyBaldiPatch
    {
        [HarmonyPrefix]
        static bool Prefix(HappyBaldi __instance, ref IEnumerator __result)
        {
            if (!AlarmRushMode.Active) return true;
            if (__instance == null) return true;

            __result = Skip(__instance);
            return false;
        }

        static IEnumerator Skip(HappyBaldi hb)
        {
            yield return null;
            if (hb != null) UnityEngine.Object.Destroy(hb.gameObject);
        }
    }

    [HarmonyPatch(typeof(EnvironmentController), "AddEvent")]
    internal static class AlarmRushNoEventsPatch
    {
        [HarmonyPrefix]
        static bool Prefix()
        {
            if (!AlarmRushMode.Active) return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(EnvironmentController), "StartEventTimers")]
    internal static class AlarmRushNoTimersPatch
    {
        [HarmonyPrefix]
        static bool Prefix(EnvironmentController __instance)
        {
            if (!AlarmRushMode.Active) return true;
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

    [HarmonyPatch(typeof(BaseGameManager), "Update")]
    internal static class AlarmRushPitstopPatch
    {
        private static bool wasInPitstop;

        public static void Reset()
        {
            wasInPitstop = false;
        }

        [HarmonyPostfix]
        static void Postfix(BaseGameManager __instance)
        {
            if (!AlarmRushMode.Active) return;
            if (__instance == null) return;

            bool inPitstop = __instance.InPitstop();

            if (inPitstop)
            {
                if (!wasInPitstop)
                {
                    wasInPitstop = true;
                    AlarmRushAudio.Stop();
                    AlarmRushNPC.Stop();
                }
                return;
            }

            if (wasInPitstop)
            {
                wasInPitstop = false;
            }
        }
    }

    [HarmonyPatch(typeof(BaseGameManager), "Initialize")]
    internal static class AlarmRushResetPerLevel
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            if (!AlarmRushMode.Active) return;

            AlarmRushAudio.Stop();
            AlarmRushNPC.Stop();
            AlarmRushPitstopPatch.Reset();
        }
    }

    [HarmonyPatch(typeof(CoreGameManager), "ReturnToMenu")]
    internal static class AlarmRushResetPatch
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            AlarmRushMode.Active = false;
            AlarmRushAudio.Stop();
            AlarmRushNPC.Stop();
            AlarmRushPitstopPatch.Reset();
        }
    }

    [HarmonyPatch(typeof(PlaceholderWinManager), "Initialize")]
    internal static class AlarmRushWinPatch
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            if (!AlarmRushMode.Active) return;
            AlarmRushMode.Active = false;
            AlarmRushAudio.Stop();
            AlarmRushNPC.Stop();
            AlarmRushPitstopPatch.Reset();
            BlitzoChallengesProgress.Complete("alarmrush");
        }
    }

    [HarmonyPatch(typeof(Elevator), "ButtonPressed")]
    internal static class AlarmRushElevatorButtonPatch
    {
        [HarmonyPrefix]
        static void Prefix()
        {
            if (!AlarmRushMode.Active) return;
            AlarmRushAudio.Stop();
            AlarmRushNPC.Stop();
        }
    }

    [HarmonyPatch(typeof(Elevator), "OpenForExitStateUpdate")]
    internal static class AlarmRushElevatorEnterPatch
    {
        [HarmonyPostfix]
        static void Postfix(Elevator __instance)
        {
            if (!AlarmRushMode.Active) return;
            if (__instance == null || __instance.ColliderGroup == null) return;

            if (__instance.ColliderGroup.HasPlayer)
            {
                AlarmRushAudio.Stop();
                AlarmRushNPC.Stop();
            }
        }
    }
}