using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using MTM101BaldAPI;
using MTM101BaldAPI.AssetTools;
using MTM101BaldAPI.Registers;
using ModdedModesAPI.BepInEx;
using ModdedModesAPI.ModesAPI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

namespace BlitzoChallengesPack
{
    [BepInPlugin("blitzo.baldiplus.challengespack", "Blitzo's Challenges Pack", "1.0.0.0")]
    [BepInDependency("pixelguy.pixelmodding.baldiplus.moddedmodesapi")]
    [BepInDependency(MTM101BaldiDevAPI.ModGUID, BepInDependency.DependencyFlags.HardDependency)]
    public class BasePlugin : BaseUnityPlugin
    {
        public static BasePlugin Instance { get; private set; } = null!;
        public static AssetManager asMan = new AssetManager();
        public static AudioClip? SchoolOfTerror;
        public static AudioClip? NullClassic;
        public static Texture2D? NullTexture;
        public static AudioClip? WindyNoise;
        public static AudioClip? AlarmRushMusic;
        public static Sprite? YtpsCounterSprite;

        public static ConfigEntry<int>[] YtpsPointsPerLevel = null!;

        private Harmony harmony = null!;
        private bool isCrashed = false;

        private void Awake()
        {
            Instance = this;
            harmony = new Harmony("blitzo.baldiplus.challengespack");
            harmony.PatchAll();

            InitConfig();
            StartCoroutine(LoadAssets());
            CustomModesHandler.OnMainMenuInitialize += OnMainMenuInit;
        }

        private void Start()
        {
            StartCoroutine(CheckForNullMod());
        }

        private IEnumerator CheckForNullMod()
        {
            yield return null;

            if (Chainloader.PluginInfos.ContainsKey("levs_kittne.baldiplus.null"))
            {
                isCrashed = true;
                ForceCrash();
            }
        }

        private void ForceCrash()
        {
            Debug.LogError("FATAL ERROR: Uhm i dunno why you cant play game normally, but i recommended deleted null style... cuz Null is not can0n");

#if UNITY_STANDALONE_WIN
                        System.Diagnostics.Process.GetCurrentProcess().Kill();
#else
            Application.Quit();
#endif
        }

        private void InitConfig()
        {
            YtpsPointsPerLevel = new ConfigEntry<int>[5];

            YtpsPointsPerLevel[0] = Config.Bind(
                "YtpsHunt", "F1", 100,
                "Points required to win on Floor 1 (F1).");

            YtpsPointsPerLevel[1] = Config.Bind(
                "YtpsHunt", "F2", 300,
                "Points required to win on Floor 2 (F2).");

            YtpsPointsPerLevel[2] = Config.Bind(
                "YtpsHunt", "F3", 500,
                "Points required to win on Floor 3 (F3).");

            YtpsPointsPerLevel[3] = Config.Bind(
                "YtpsHunt", "F4", 400,
                "Points required to win on Floor 4 (F4).");

            YtpsPointsPerLevel[4] = Config.Bind(
                "YtpsHunt", "F5", 700,
                "Points required to win on Floor 5 (F5).");
        }

        private void OnMainMenuInit()
        {
            if (isCrashed) return;
            RegisterChallenges();
        }

        private IEnumerator LoadAssets()
        {
            string baseDir = Path.Combine(Application.streamingAssetsPath,
                "Modded", "blitzo.baldiplus.challengespack");

            string audioDir = Path.Combine(baseDir, "Audio");
            string texDir = Path.Combine(baseDir, "Textures");
            string langDir = Path.Combine(baseDir, "Languages");

            if (!Directory.Exists(audioDir)) Directory.CreateDirectory(audioDir);
            if (!Directory.Exists(texDir)) Directory.CreateDirectory(texDir);
            if (!Directory.Exists(langDir)) Directory.CreateDirectory(langDir);

            yield return LoadAudio(audioDir, "SchoolOfTerror.wav", c => SchoolOfTerror = c);
            yield return LoadAudio(audioDir, "null_classic.wav", c => NullClassic = c);
            yield return LoadAudio(audioDir, "windyNoise.wav", c => WindyNoise = c);
            yield return LoadAudio(audioDir, "AlarmRush.wav", c => AlarmRushMusic = c);
            yield return LoadTexture(texDir, "null.png", t => NullTexture = t);
            yield return LoadSprite(texDir, "HUD_YTPsCounter.png", s => YtpsCounterSprite = s);

            LoadLocalization(langDir);
        }

        private static void LoadLocalization(string dir)
        {
            if (!Directory.Exists(dir)) return;

            string file = Path.Combine(dir, "Localization.json");
            if (!File.Exists(file)) return;

            try
            {
                AssetLoader.LocalizationFromFile(file, Language.English);
            }
            catch { }
        }

        private IEnumerator LoadAudio(string dir, string fileName, Action<AudioClip> assign)
        {
            string path = Path.Combine(dir, fileName);
            if (!File.Exists(path)) yield break;

            string uri = new Uri(path).AbsoluteUri;
            using UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.WAV);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;

            AudioClip? clip = null;
            try { clip = DownloadHandlerAudioClip.GetContent(req); } catch { }
            if (clip != null && clip.samples > 0) assign(clip);
        }

        private IEnumerator LoadTexture(string dir, string fileName, Action<Texture2D> assign)
        {
            string path = Path.Combine(dir, fileName);
            if (!File.Exists(path)) yield break;

            string uri = new Uri(path).AbsoluteUri;
            using UnityWebRequest req = UnityWebRequestTexture.GetTexture(uri);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;

            Texture2D? tex = null;
            try { tex = DownloadHandlerTexture.GetContent(req); } catch { }
            if (tex != null) assign(tex);
        }

        private IEnumerator LoadSprite(string dir, string fileName, Action<Sprite> assign)
        {
            string path = Path.Combine(dir, fileName);
            if (!File.Exists(path)) yield break;

            string uri = new Uri(path).AbsoluteUri;
            using UnityWebRequest req = UnityWebRequestTexture.GetTexture(uri);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;

            Texture2D? tex = null;
            try { tex = DownloadHandlerTexture.GetContent(req); } catch { }
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                assign(sprite);
            }
        }

        private static SceneObject? FindScene(string name)
        {
            var all = Resources.FindObjectsOfTypeAll<SceneObject>();
            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i];
                if (s == null || s.name == null) continue;
                if (s.name == name) return s;
            }
            return null;
        }

        private void RegisterChallenges()
        {
            var screen = ModeObject.CreateModeObjectOverExistingScreen(SelectionScreen.ChallengesScreen);
            SceneObject? scene = FindScene("MainLevel_1");
            if (scene == null) return;

            AddChallengeButton(screen, scene, "BsodaChallengeButton", "Blitzo_BsodaChallenge", "Blitzo_BsodaChallengeDesc", LaunchBsoda);
            AddChallengeButton(screen, scene, "GravityChallengeButton", "Blitzo_GravityChallenge", "Blitzo_GravityChallengeDesc", LaunchGravity);
            AddChallengeButton(screen, scene, "StudentShuffleButton", "Blitzo_StudentShuffleChallenge", "Blitzo_StudentShuffleChallengeDesc", LaunchStudentShuffle);
            AddChallengeButton(screen, scene, "DrReflexChallengeButton", "Blitzo_DrReflexChallenge", "Blitzo_DrReflexChallengeDesc", LaunchDrReflex);
            AddChallengeButton(screen, scene, "BeansChallengeButton", "Blitzo_BeansChallenge", "Blitzo_BeansChallengeDesc", LaunchBeans);
            AddChallengeButton(screen, scene, "BlindChallengeButton", "Blitzo_BlindChallenge", "Blitzo_BlindChallengeDesc", LaunchBlind);
            AddChallengeButton(screen, scene, "CorruptedActivitiesButton", "Blitzo_CorruptedActivitiesChallenge", "Blitzo_CorruptedActivitiesChallengeDesc", LaunchCorruptedActivities);
            AddChallengeButton(screen, scene, "NanaPeelChaosButton", "Blitzo_NanaPeelChaosChallenge", "Blitzo_NanaPeelChaosChallengeDesc", LaunchNanaPeelChaos);
            AddChallengeButton(screen, scene, "WindySchoolButton", "Blitzo_WindySchoolChallenge", "Blitzo_WindySchoolChallengeDesc", LaunchWindySchool);
            AddChallengeButton(screen, scene, "AlarmRushButton", "Blitzo_AlarmRushChallenge", "Blitzo_AlarmRushChallengeDesc", LaunchAlarmRush);
            AddChallengeButton(screen, scene, "BalderChaseButton", "Blitzo_BalderChaseChallenge", "Blitzo_BalderChaseChallengeDesc", LaunchBalderChase);
            AddChallengeButton(screen, scene, "YtpsHuntButton", "Blitzo_YtpsHuntChallenge", "Blitzo_YtpsHuntChallengeDesc", LaunchYtpsHunt);
        }

        private static void AddChallengeButton(ModeObject screen, SceneObject scene, string id, string titleKey, string descriptionKey, Action<SceneObject, ModeObject> launch)
        {
            var button = screen.StandardButtonBuilder.CreateBlankButton(id);
            button.AddTextVisual(titleKey, out TextMeshProUGUI _);
            screen.StandardButtonBuilder.AddDescriptionText(button, descriptionKey);
            button.OnPress.AddListener(() => launch(scene, screen));
        }

        private static void ResetAllModes()
        {
            BsodaChallengeMode.Active = false;
            BsodaChallengeMode.Started = false;
            BsodaChallengeState.Elapsed = 0f;
            BsodaChallengeAudio.Stop();
            GravityChallengeMode.Active = false;
            StudentShuffleChallengeMode.Active = false;
            DrReflexChallengeMode.Active = false;
            BeansChallengeMode.Active = false;
            BlindChallengeMode.Active = false;
            CorruptedActivitiesMode.Active = false;
            NanaPeelChaosMode.Active = false;
            NanaPeelChaosRunner.Stop();
            WindySchoolMode.Active = false;
            WindySchoolRunner.Stop();
            AlarmRushMode.Active = false;
            AlarmRushAudio.Stop();
            AlarmRushNPC.Stop();
            BalderChaseMode.Active = false;
            BalderChaseRunner.Stop();
            YtpsHuntMode.Active = false;
            YtpsHuntMode.CurrentGoal = 0;
            YtpsHuntMode.GoalReached = false;
            YtpsHuntRunner.Stop();
        }

        private static void LaunchBsoda(SceneObject scene, ModeObject screen)
        {
            ResetAllModes();
            BsodaChallengeMode.Active = true;
            BsodaChallengeMode.Started = false;
            BsodaChallengeState.Elapsed = 0f;
            LoadChallenge(scene, screen);
        }

        private static void LaunchGravity(SceneObject scene, ModeObject screen)
        {
            ResetAllModes();
            GravityChallengeMode.Active = true;
            LoadChallenge(scene, screen);
        }

        private static void LaunchStudentShuffle(SceneObject scene, ModeObject screen)
        {
            ResetAllModes();
            StudentShuffleChallengeMode.Active = true;
            LoadChallenge(scene, screen);
        }

        private static void LaunchDrReflex(SceneObject scene, ModeObject screen)
        {
            ResetAllModes();
            DrReflexChallengeMode.Active = true;
            LoadChallenge(scene, screen);
        }

        private static void LaunchBeans(SceneObject scene, ModeObject screen)
        {
            ResetAllModes();
            BeansChallengeMode.Active = true;
            LoadChallenge(scene, screen);
        }

        private static void LaunchBlind(SceneObject scene, ModeObject screen)
        {
            ResetAllModes();
            BlindChallengeMode.Active = true;
            LoadChallenge(scene, screen);
        }

        private static void LaunchCorruptedActivities(SceneObject scene, ModeObject screen)
        {
            ResetAllModes();
            CorruptedActivitiesMode.Active = true;
            LoadChallenge(scene, screen);
        }

        private static void LaunchNanaPeelChaos(SceneObject scene, ModeObject screen)
        {
            ResetAllModes();
            NanaPeelChaosMode.Active = true;
            LoadChallenge(scene, screen);
        }

        private static void LaunchWindySchool(SceneObject scene, ModeObject screen)
        {
            ResetAllModes();
            WindySchoolMode.Active = true;
            WindySchoolMode.WindClip = WindyNoise;
            LoadChallenge(scene, screen);
        }

        private static void LaunchAlarmRush(SceneObject scene, ModeObject screen)
        {
            ResetAllModes();
            AlarmRushMode.Active = true;
            AlarmRushMode.AlarmMusic = AlarmRushMusic;
            LoadChallenge(scene, screen);
        }

        private static void LaunchBalderChase(SceneObject scene, ModeObject screen)
        {
            ResetAllModes();
            BalderChaseMode.Active = true;
            LoadChallenge(scene, screen);
        }

        private static void LaunchYtpsHunt(SceneObject scene, ModeObject screen)
        {
            ResetAllModes();
            YtpsHuntMode.Active = true;
            YtpsHuntMode.YtpsCounterSprite = YtpsCounterSprite;
            YtpsHuntRunner.Ensure();
            LoadChallenge(scene, screen);
        }

        private static void LoadChallenge(SceneObject scene, ModeObject screen)
        {
            var cgm = Singleton<CoreGameManager>.Instance;
            if (cgm != null) cgm.SetRandomSeed();

            var rsType = AccessTools.TypeByName("ModdedModesAPI.BepInEx.ResourceStorage");
            if (rsType == null) return;

            var loader = AccessTools.Field(rsType, "loaderInstance")?.GetValue(null) as GameLoader;
            var elv = AccessTools.Field(rsType, "elvScreen")?.GetValue(null) as ElevatorScreen;
            if (loader == null || elv == null) return;

            screen.ScreenTransform.gameObject.SetActive(false);

            CallInitialize(loader);
            TryCall(loader, "AssignElevatorScreen", elv);
            loader.LoadLevel(scene);
            elv.gameObject.SetActive(true);
            TryCall(loader, "SetMode", (int)Mode.Main);
            TryCall(loader, "SetSave", false);
        }

        private static void CallInitialize(GameLoader loader)
        {
            var type = loader.GetType();
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var m in methods)
            {
                if (m.Name != "Initialize") continue;
                var ps = m.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType == typeof(int))
                {
                    m.Invoke(loader, new object[] { 2 });
                    return;
                }
                if (ps.Length == 2 && ps[0].ParameterType == typeof(int) && ps[1].ParameterType == typeof(int))
                {
                    m.Invoke(loader, new object[] { 2, (int)Mode.Main });
                    return;
                }
            }
        }

        private static void TryCall(object target, string name, params object[] args)
        {
            if (target == null) return;
            var t = target.GetType();
            var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var m in methods)
            {
                if (m.Name != name) continue;
                var ps = m.GetParameters();
                if (ps.Length != args.Length) continue;
                bool ok = true;
                for (int i = 0; i < ps.Length; i++)
                {
                    if (args[i] != null && !ps[i].ParameterType.IsInstanceOfType(args[i]))
                    {
                        ok = false;
                        break;
                    }
                }
                if (!ok) continue;
                m.Invoke(target, args);
                return;
            }
        }
    }
}