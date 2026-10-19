using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BlitzoChallengesPack
{
    public enum ChallengeMode
    {
        None,
        Speedy,
        Stealthy,
        Grapple
    }

    public static class ChallengeConfig
    {
        public static int SpeedyNotebooks = 25;
        public static int SpeedyClasses = 25;

        public static int StealthyNotebooks = 7;
        public static int StealthyClasses = 7;

        public static int GrappleNotebooks = 7;
        public static int GrappleClasses = 7;

        public static bool ScaleMapToClassCount = true;
        public static float MaxMapScale = 1.6f;
        public static float MinNpcSpawnDistance = 80f;

        public static Dictionary<Character, int> SpeedyNpcs = new Dictionary<Character, int>
        {
            { Character.Baldi, 1 }
        };

        public static Dictionary<Character, int> StealthyNpcs = new Dictionary<Character, int>
        {
            { Character.Baldi, 1 },
            { Character.Principal, 6 }
        };

        public static Dictionary<Character, int> GrappleNpcs = new Dictionary<Character, int>
        {
            { Character.Baldi, 1 },
            { Character.Bully, 1 },
            { Character.Pomp, 1 },
            { Character.Sweep, 1 },
            { Character.Chalkles, 1 },
            { Character.Cumulo, 1 },
            { Character.DrReflex, 1 }
        };

        internal static int Classes(ChallengeMode mode)
        {
            switch (mode)
            {
                case ChallengeMode.Speedy: return SpeedyClasses;
                case ChallengeMode.Stealthy: return StealthyClasses;
                case ChallengeMode.Grapple: return GrappleClasses;
                default: return 0;
            }
        }

        internal static int Notebooks(ChallengeMode mode)
        {
            switch (mode)
            {
                case ChallengeMode.Speedy: return SpeedyNotebooks;
                case ChallengeMode.Stealthy: return StealthyNotebooks;
                case ChallengeMode.Grapple: return GrappleNotebooks;
                default: return 0;
            }
        }

        internal static Dictionary<Character, int>? NpcPlan(ChallengeMode mode)
        {
            switch (mode)
            {
                case ChallengeMode.Speedy: return SpeedyNpcs;
                case ChallengeMode.Stealthy: return StealthyNpcs;
                case ChallengeMode.Grapple: return GrappleNpcs;
                default: return null;
            }
        }
    }

    public class ChallengeRunner : MonoBehaviour
    {
        public static ChallengeRunner? Instance;

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("ChallengeRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<ChallengeRunner>();
        }

        public void Run(IEnumerator r)
        {
            StartCoroutine(r);
        }
    }

    internal static class ChallengeUtil
    {
        static readonly Dictionary<Character, NPC> KnownPrefabs = new Dictionary<Character, NPC>();

        static ChallengeMode FromName(string name)
        {
            if (name.IndexOf("Speedy", StringComparison.OrdinalIgnoreCase) >= 0) return ChallengeMode.Speedy;
            if (name.IndexOf("Stealthy", StringComparison.OrdinalIgnoreCase) >= 0) return ChallengeMode.Stealthy;
            if (name.IndexOf("Grapple", StringComparison.OrdinalIgnoreCase) >= 0) return ChallengeMode.Grapple;
            return ChallengeMode.None;
        }

        public static ChallengeMode ModeOf(BaseGameManager? manager)
        {
            if (manager == null) return ChallengeMode.None;
            return FromName(manager.GetType().Name);
        }

        public static ChallengeMode ModeOfScene(SceneObject? scene)
        {
            if (scene == null) return ChallengeMode.None;

            var mode = ModeOf(scene.manager);
            if (mode != ChallengeMode.None) return mode;

            if (scene.name == null) return ChallengeMode.None;
            if (scene.name.IndexOf("Challenge", StringComparison.OrdinalIgnoreCase) < 0) return ChallengeMode.None;
            return FromName(scene.name);
        }

        public static ChallengeMode Current()
        {
            var mode = ModeOf(Singleton<BaseGameManager>.Instance);
            if (mode != ChallengeMode.None) return mode;
            var core = Singleton<CoreGameManager>.Instance;
            if (core == null) return ChallengeMode.None;
            return ModeOfScene(core.sceneObject);
        }

        static void Register(NPC? npc)
        {
            if (npc == null) return;
            if (!KnownPrefabs.ContainsKey(npc.Character)) KnownPrefabs[npc.Character] = npc;
        }

        static void Register(NPC[]? npcs)
        {
            if (npcs == null) return;
            for (int i = 0; i < npcs.Length; i++) Register(npcs[i]);
        }

        public static void RegisterPrefabs(SceneObject? scene)
        {
            if (scene == null) return;

            Register(scene.forcedNpcs);

            if (scene.potentialNPCs != null)
            {
                for (int i = 0; i < scene.potentialNPCs.Count; i++)
                {
                    var weighted = scene.potentialNPCs[i];
                    if (weighted == null) continue;
                    Register(weighted.selection);
                }
            }

            if (scene.levelObject != null) Register(scene.levelObject.forcedNpcs);

            if (scene.randomizedLevelObject != null)
            {
                for (int i = 0; i < scene.randomizedLevelObject.Length; i++)
                {
                    var entry = scene.randomizedLevelObject[i];
                    if (entry != null && entry.selection != null) Register(entry.selection.forcedNpcs);
                }
            }
        }

        public static NPC? FindNpcPrefab(Character want)
        {
            NPC? known;
            if (KnownPrefabs.TryGetValue(want, out known) && known != null) return known;

            var all = Resources.FindObjectsOfTypeAll<NPC>();
            for (int i = 0; i < all.Length; i++)
            {
                var npc = all[i];
                if (npc == null) continue;
                if (npc.Character == want && !npc.gameObject.scene.IsValid()) return npc;
            }
            return null;
        }

        public static void CleanYtp(EnvironmentController ec)
        {
            if (ec == null || ec.items == null) return;
            for (int i = ec.items.Count - 1; i >= 0; i--)
            {
                var pickup = ec.items[i];
                if (pickup == null || pickup.item == null) continue;
                string itemName = pickup.item.name ?? "";
                if (itemName.IndexOf("YTP", StringComparison.OrdinalIgnoreCase) < 0) continue;
                try { pickup.Hide(true); } catch { }
                UnityEngine.Object.Destroy(pickup.gameObject);
                ec.items.RemoveAt(i);
            }
        }

        public static void RemoveEmptyPickups(EnvironmentController ec)
        {
            if (ec == null || ec.items == null) return;
            for (int i = ec.items.Count - 1; i >= 0; i--)
            {
                var pickup = ec.items[i];
                if (pickup == null)
                {
                    ec.items.RemoveAt(i);
                    continue;
                }
                if (pickup.item != null && pickup.item.itemType != Items.None) continue;
                try { pickup.Hide(true); } catch { }
                UnityEngine.Object.Destroy(pickup.gameObject);
                ec.items.RemoveAt(i);
            }
        }

        public static RoomController? FindOfficeRoom(EnvironmentController ec)
        {
            if (ec == null) return null;
            if (ec.offices != null)
            {
                for (int i = 0; i < ec.offices.Count; i++)
                {
                    if (ec.offices[i] != null) return ec.offices[i];
                }
            }
            if (ec.rooms == null) return null;
            for (int i = 0; i < ec.rooms.Count; i++)
            {
                var room = ec.rooms[i];
                if (room != null && room.category == RoomCategory.Office) return room;
            }
            return null;
        }

        public static Cell? FindOfficeCell(EnvironmentController ec)
        {
            var office = FindOfficeRoom(ec);
            if (office == null) return null;
            var tiles = office.AllTilesNoGarbage(true, false);
            if (tiles == null || tiles.Count == 0) tiles = office.AllTilesNoGarbage(true, true);
            if (tiles == null || tiles.Count == 0) return null;
            return tiles[tiles.Count / 2];
        }

        public static bool UsableCell(Cell? cell)
        {
            if (cell == null || cell.room == null) return false;
            if (cell.room.type == RoomType.Null) return false;
            return cell.room.functions != null;
        }

        public static Cell? RandomSpawnCell(EnvironmentController ec, NPC npc, bool avoidOffice)
        {
            Cell? fallback = null;
            for (int i = 0; i < 60; i++)
            {
                var cell = ec.RandomCell(false, false, true);
                if (!UsableCell(cell)) continue;
                if (avoidOffice && cell.room.category == RoomCategory.Office) continue;

                fallback = cell;
                if (npc.spawnableRooms == null || npc.spawnableRooms.Count == 0 || npc.spawnableRooms.Contains(cell.room.category)) return cell;
            }
            return fallback;
        }

        public static Cell FarFromPlayer(EnvironmentController ec, Cell current, bool avoidOffice)
        {
            var core = Singleton<CoreGameManager>.Instance;
            if (core == null) return current;
            var player = core.GetPlayer(0);
            if (player == null) return current;

            Vector3 playerPosition = player.transform.position;
            float minDistance = ChallengeConfig.MinNpcSpawnDistance;
            bool currentAllowed = UsableCell(current) && !(avoidOffice && current.room != null && current.room.category == RoomCategory.Office);
            float currentDistance = Vector3.Distance(current.FloorWorldPosition, playerPosition);
            if (currentAllowed && currentDistance >= minDistance) return current;

            Cell best = current;
            float bestDistance = currentAllowed ? currentDistance : -1f;
            for (int i = 0; i < 80; i++)
            {
                var cell = ec.RandomCell(false, false, true);
                if (!UsableCell(cell)) continue;
                if (avoidOffice && cell.room.category == RoomCategory.Office) continue;

                float distance = Vector3.Distance(cell.FloorWorldPosition, playerPosition);
                if (distance >= minDistance) return cell;
                if (distance > bestDistance)
                {
                    best = cell;
                    bestDistance = distance;
                }
            }
            return best;
        }

        public static void LockOfficeDoors(EnvironmentController ec)
        {
            var office = FindOfficeRoom(ec);
            if (office == null || office.doors == null) return;
            for (int i = 0; i < office.doors.Count; i++)
            {
                var door = office.doors[i];
                if (door == null) continue;
                door.makesNoise = false;
                door.lockBlocks = true;
                door.closeBlocks = true;
                try { door.Shut(); } catch { }
                try { door.Lock(true); } catch { }
            }
        }

        public static void ReconcileNotebooks(BaseGameManager manager, ChallengeMode mode)
        {
            var ec = manager.Ec;
            if (ec == null || ec.notebooks == null) return;

            int actual = ec.notebooks.Count;
            if (actual <= 0) return;

            int target = Mathf.Min(ChallengeConfig.Notebooks(mode), actual);
            if (target <= 0 || ec.notebookTotal == target) return;

            ec.notebookTotal = target;
            manager.CollectNotebooks(0);
        }
    }

    internal static class ChallengeRoster
    {
        public static void Apply(EnvironmentController ec, ChallengeMode mode)
        {
            var plan = ChallengeConfig.NpcPlan(mode);
            if (plan == null || ec.npcsToSpawn == null) return;

            var originalNpcs = ec.npcsToSpawn;
            var originalTiles = ec.npcSpawnTile;
            var prefabs = new Dictionary<Character, NPC>();
            var counts = new Dictionary<Character, int>();
            var npcs = new List<NPC>();
            var tiles = new List<Cell?>();

            for (int i = 0; i < originalNpcs.Count; i++)
            {
                var npc = originalNpcs[i];
                if (npc == null) continue;
                if (!prefabs.ContainsKey(npc.Character)) prefabs[npc.Character] = npc;
                if (!plan.ContainsKey(npc.Character)) continue;

                npcs.Add(npc);
                tiles.Add(originalTiles != null && i < originalTiles.Length ? originalTiles[i] : null);
                int have;
                counts.TryGetValue(npc.Character, out have);
                counts[npc.Character] = have + 1;
            }

            foreach (var entry in plan)
            {
                int have;
                counts.TryGetValue(entry.Key, out have);
                if (have >= entry.Value) continue;

                NPC? prefab;
                if (!prefabs.TryGetValue(entry.Key, out prefab)) prefab = ChallengeUtil.FindNpcPrefab(entry.Key);
                if (prefab == null) continue;

                for (int k = have; k < entry.Value; k++)
                {
                    npcs.Add(prefab);
                    tiles.Add(null);
                }
            }

            bool stealthy = mode == ChallengeMode.Stealthy;
            Cell? officeCell = stealthy ? ChallengeUtil.FindOfficeCell(ec) : null;
            if (!ChallengeUtil.UsableCell(officeCell)) officeCell = null;

            var finalNpcs = new List<NPC>();
            var finalTiles = new List<Cell>();
            for (int i = 0; i < npcs.Count; i++)
            {
                Cell? tile = tiles[i];
                bool isPinned = false;
                if (stealthy && npcs[i].Character == Character.Baldi && officeCell != null)
                {
                    tile = officeCell;
                    isPinned = true;
                }
                else if (!ChallengeUtil.UsableCell(tile))
                {
                    tile = ChallengeUtil.RandomSpawnCell(ec, npcs[i], stealthy);
                }
                if (tile == null || !ChallengeUtil.UsableCell(tile)) continue;

                if (!isPinned) tile = ChallengeUtil.FarFromPlayer(ec, tile, stealthy);

                finalNpcs.Add(npcs[i]);
                finalTiles.Add(tile);
            }

            ec.npcsToSpawn.Clear();
            ec.npcsToSpawn.AddRange(finalNpcs);
            ec.npcSpawnTile = finalTiles.ToArray();
        }
    }

    internal static class ChallengeLevelEditor
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        static readonly List<RoomAsset> KnownNoActivity = new List<RoomAsset>();
        static List<RoomAsset>? noActivityRooms;

        public static void RegisterNoActivityFrom(LevelObject? level)
        {
            if (level == null || level.roomGroup == null) return;

            for (int i = 0; i < level.roomGroup.Length; i++)
            {
                var group = level.roomGroup[i];
                if (group == null || group.potentialRooms == null) continue;

                for (int j = 0; j < group.potentialRooms.Length; j++)
                {
                    var entry = group.potentialRooms[j];
                    if (entry == null || entry.selection == null) continue;
                    if (IsNoActivity(entry.selection) && !KnownNoActivity.Contains(entry.selection))
                    {
                        KnownNoActivity.Add(entry.selection);
                        noActivityRooms = null;
                    }
                }
            }
        }

        public static void ApplyToLevel(LevelObject level, int classes)
        {
            if (level == null || level.roomGroup == null) return;

            var replacements = FindNoActivityRooms();

            int originalTotal = 0;
            for (int i = 0; i < level.roomGroup.Length; i++)
            {
                var group = level.roomGroup[i];
                if (group == null || group.potentialRooms == null) continue;
                if (!ContainsClass(group.potentialRooms)) continue;

                originalTotal += group.maxRooms;
                if (replacements.Count > 0) group.potentialRooms = SwapClasses(group.potentialRooms, replacements);
                group.minRooms = classes;
                group.maxRooms = classes;
            }

            ScaleMap(level, originalTotal, classes);
        }

        public static void ApplyToParameters(LevelGenerationParameters ld, int classes)
        {
            if (ld == null || ld.roomGroup == null) return;

            var replacements = FindNoActivityRooms();
            for (int i = 0; i < ld.roomGroup.Count; i++)
            {
                var group = ld.roomGroup[i];
                if (group == null || group.potentialRooms == null) continue;
                if (!ContainsClass(group.potentialRooms)) continue;

                var copy = CopyGroup(group);
                if (replacements.Count > 0) copy.potentialRooms = SwapClasses(copy.potentialRooms, replacements);
                copy.minRooms = classes;
                copy.maxRooms = classes;
                ld.roomGroup[i] = copy;
            }
        }

        public static NPC[] FilterNpcs(NPC[]? source, Dictionary<Character, int> plan)
        {
            var result = new List<NPC>();
            if (source != null)
            {
                for (int i = 0; i < source.Length; i++)
                {
                    if (source[i] != null && plan.ContainsKey(source[i].Character)) result.Add(source[i]);
                }
            }
            return result.ToArray();
        }

        static RoomGroup CopyGroup(RoomGroup source)
        {
            var copy = new RoomGroup();
            copy.name = source.name;
            copy.potentialRooms = source.potentialRooms;
            copy.wallTexture = source.wallTexture;
            copy.floorTexture = source.floorTexture;
            copy.ceilingTexture = source.ceilingTexture;
            copy.light = source.light;
            copy.stickToHallChance = source.stickToHallChance;
            copy.centerWeightMultiplier = source.centerWeightMultiplier;
            copy.dijkstraWeightValueMultiplier = source.dijkstraWeightValueMultiplier;
            copy.dijkstraWeightPower = source.dijkstraWeightPower;
            copy.minRooms = source.minRooms;
            copy.maxRooms = source.maxRooms;
            copy.overridePositionWeighting = source.overridePositionWeighting;
            return copy;
        }

        static List<RoomAsset> FindNoActivityRooms()
        {
            if (noActivityRooms != null && noActivityRooms.Count > 0) return noActivityRooms;

            var found = new List<RoomAsset>();
            for (int i = 0; i < KnownNoActivity.Count; i++)
            {
                if (KnownNoActivity[i] != null) found.Add(KnownNoActivity[i]);
            }

            var all = Resources.FindObjectsOfTypeAll<RoomAsset>();
            for (int i = 0; i < all.Length; i++)
            {
                var room = all[i];
                if (room == null || found.Contains(room)) continue;
                if (IsNoActivity(room)) found.Add(room);
            }

            found.RemoveAll(room => room.cells == null || room.cells.Count == 0);
            found.Sort((a, b) => string.CompareOrdinal(((UnityEngine.Object)a).name, ((UnityEngine.Object)b).name));
            noActivityRooms = found;
            return found;
        }

        static bool IsNoActivity(RoomAsset room)
        {
            if (HasNoActivityName(((UnityEngine.Object)room).name) || HasNoActivityName(room.name)) return true;
            return room.category == RoomCategory.Class && HasNoActivityPrefab(room);
        }

        static bool HasNoActivityName(string? name)
        {
            return !string.IsNullOrEmpty(name) && name.IndexOf("Class_NoActivity", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool HasNoActivityPrefab(RoomAsset room)
        {
            object? activity = room.activity;
            if (activity == null) return false;
            var field = activity.GetType().GetField("prefab", Flags);
            if (field == null) return false;
            return field.GetValue(activity) is NoActivity;
        }

        static bool IsClass(WeightedRoomAsset? entry)
        {
            return entry != null && entry.selection != null && entry.selection.category == RoomCategory.Class;
        }

        static bool ContainsClass(WeightedRoomAsset[] entries)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (IsClass(entries[i])) return true;
            }
            return false;
        }

        static WeightedRoomAsset[] SwapClasses(WeightedRoomAsset[] source, List<RoomAsset> replacements)
        {
            var result = new List<WeightedRoomAsset>();
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != null && !IsClass(source[i])) result.Add(source[i]);
            }
            for (int i = 0; i < replacements.Count; i++)
            {
                result.Add(new WeightedRoomAsset { selection = replacements[i], weight = 100 });
            }
            return result.ToArray();
        }

        static void ScaleMap(LevelObject level, int originalClasses, int wantedClasses)
        {
            if (!ChallengeConfig.ScaleMapToClassCount) return;
            if (originalClasses <= 0 || wantedClasses <= originalClasses) return;

            float factor = (float)wantedClasses / originalClasses;
            float linear = Mathf.Clamp(Mathf.Sqrt(factor), 1f, ChallengeConfig.MaxMapScale);

            level.minSize = ScaleSize(level.minSize, linear);
            level.maxSize = ScaleSize(level.maxSize, linear);

            float plotFactor = linear * linear;
            level.minPlots = Mathf.CeilToInt(level.minPlots * plotFactor);
            level.maxPlots = Mathf.CeilToInt(level.maxPlots * plotFactor);
        }

        static IntVector2 ScaleSize(IntVector2 size, float factor)
        {
            size.x = Mathf.RoundToInt(size.x * factor);
            size.z = Mathf.RoundToInt(size.z * factor);
            return size;
        }
    }

    internal static class ChallengeScenePreparer
    {
        static readonly HashSet<SceneObject> Prepared = new HashSet<SceneObject>();
        static SceneObject? mainScene;

        public static void Prepare(SceneObject? scene)
        {
            if (scene == null || Prepared.Contains(scene)) return;

            var mode = ChallengeUtil.ModeOfScene(scene);
            if (mode == ChallengeMode.None) return;

            var main = FindMainScene();
            if (main == null) return;

            var clones = new Dictionary<LevelObject, LevelObject>();
            var randomized = new List<WeightedLevelObject>();

            if (main.randomizedLevelObject != null)
            {
                for (int i = 0; i < main.randomizedLevelObject.Length; i++)
                {
                    var entry = main.randomizedLevelObject[i];
                    if (entry == null) continue;

                    if (entry.selection == null)
                    {
                        randomized.Add(entry);
                        continue;
                    }

                    randomized.Add(new WeightedLevelObject
                    {
                        selection = CloneLevel(entry.selection, clones),
                        weight = entry.weight
                    });
                }
            }

            LevelObject? mainLevel = null;
            if (main.levelObject != null)
            {
                mainLevel = CloneLevel(main.levelObject, clones);
            }
            else
            {
                foreach (var clone in clones.Values)
                {
                    mainLevel = clone;
                    break;
                }
            }
            if (mainLevel == null) return;

            ChallengeUtil.RegisterPrefabs(main);
            ChallengeUtil.RegisterPrefabs(scene);

            foreach (var pair in clones) ChallengeLevelEditor.RegisterNoActivityFrom(pair.Key);

            var plan = ChallengeConfig.NpcPlan(mode);
            int classes = ChallengeConfig.Classes(mode);
            foreach (var clone in clones.Values)
            {
                if (classes > 0) ChallengeLevelEditor.ApplyToLevel(clone, classes);
                if (plan != null) clone.forcedNpcs = ChallengeLevelEditor.FilterNpcs(clone.forcedNpcs, plan);
            }

            if (plan != null) EnsureForcedNpcs(scene, plan);

            scene.randomizedLevelObject = randomized.ToArray();
            scene.levelObject = mainLevel;
            scene.levelAsset = null;
            scene.levelContainer = null;
            scene.additionalNPCs = 0;
            scene.baldiPrefab = main.baldiPrefab;

            Prepared.Add(scene);
        }

        static void EnsureForcedNpcs(SceneObject scene, Dictionary<Character, int> plan)
        {
            var list = new List<NPC>();
            if (scene.forcedNpcs != null)
            {
                for (int i = 0; i < scene.forcedNpcs.Length; i++)
                {
                    if (scene.forcedNpcs[i] != null) list.Add(scene.forcedNpcs[i]);
                }
            }

            foreach (var entry in plan)
            {
                if (entry.Value <= 0) continue;
                if (entry.Key == Character.Baldi) continue;

                bool present = false;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Character == entry.Key)
                    {
                        present = true;
                        break;
                    }
                }
                if (present) continue;

                var prefab = ChallengeUtil.FindNpcPrefab(entry.Key);
                if (prefab != null) list.Add(prefab);
            }

            scene.forcedNpcs = list.ToArray();
        }

        static LevelObject CloneLevel(LevelObject source, Dictionary<LevelObject, LevelObject> clones)
        {
            LevelObject? existing;
            if (clones.TryGetValue(source, out existing)) return existing;

            var clone = UnityEngine.Object.Instantiate(source);
            clone.name = source.name + "_ChallengeClone";
            clones[source] = clone;
            return clone;
        }

        static SceneObject? FindMainScene()
        {
            if (mainScene != null) return mainScene;
            var all = Resources.FindObjectsOfTypeAll<SceneObject>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "MainLevel_3")
                {
                    mainScene = all[i];
                    return mainScene;
                }
            }
            return null;
        }
    }

    internal static class ChallengePatchTargets
    {
        public static IEnumerable<MethodBase> Overrides(string methodName)
        {
            var result = new List<MethodBase>();

            var baseMethod = AccessTools.DeclaredMethod(typeof(BaseGameManager), methodName);
            if (baseMethod != null) result.Add(baseMethod);

            Type[] types;
            try
            {
                types = typeof(BaseGameManager).Assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types;
            }

            for (int i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (type == null || !type.IsSubclassOf(typeof(BaseGameManager))) continue;

                MethodBase? method = null;
                try { method = AccessTools.DeclaredMethod(type, methodName); } catch { }
                if (method != null && !method.IsAbstract) result.Add(method);
            }

            return result;
        }
    }

    [HarmonyPatch(typeof(GameLoader), "LoadLevel")]
    internal static class ChallengeLoadLevelPatch
    {
        [HarmonyPrefix]
        static void Prefix(SceneObject sceneObject)
        {
            ChallengeScenePreparer.Prepare(sceneObject);
        }
    }

    [HarmonyPatch]
    internal static class ChallengeGameSceneLoadPatch
    {
        static List<MethodBase> Targets()
        {
            var result = new List<MethodBase>();
            var methods = AccessTools.GetDeclaredMethods(typeof(AdditiveSceneManager));
            for (int i = 0; i < methods.Count; i++)
            {
                var method = methods[i];
                if (method.Name != "LoadScene") continue;
                var parameters = method.GetParameters();
                if (parameters.Length > 0 && parameters[0].ParameterType == typeof(string)) result.Add(method);
            }
            return result;
        }

        [HarmonyPrepare]
        static bool Prepare()
        {
            return Targets().Count > 0;
        }

        static IEnumerable<MethodBase> TargetMethods()
        {
            return Targets();
        }

        [HarmonyPrefix]
        static void Prefix(object[] __args)
        {
            if (__args == null || __args.Length == 0) return;
            if (!(__args[0] is string sceneName) || sceneName != "Game") return;

            var core = Singleton<CoreGameManager>.Instance;
            if (core == null) return;
            ChallengeScenePreparer.Prepare(core.sceneObject);
        }
    }

    [HarmonyPatch]
    internal static class ChallengeGenerationDataPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            return ChallengePatchTargets.Overrides("PrepareLevelGenerationData");
        }

        [HarmonyPostfix]
        static void Postfix(BaseGameManager __instance)
        {
            var mode = ChallengeUtil.ModeOf(__instance);
            if (mode == ChallengeMode.None) return;

            var ld = __instance.levelObject;
            if (ld == null) return;

            int classes = ChallengeConfig.Classes(mode);
            if (classes > 0) ChallengeLevelEditor.ApplyToParameters(ld, classes);

            var plan = ChallengeConfig.NpcPlan(mode);
            if (plan != null) ld.forcedNpcs = ChallengeLevelEditor.FilterNpcs(ld.forcedNpcs, plan);
        }
    }

    [HarmonyPatch(typeof(Structure_StudentSpawner), "SpawnStudents")]
    internal static class ChallengeNoStudentsPatch
    {
        [HarmonyPrefix]
        static bool Prefix()
        {
            return ChallengeUtil.Current() == ChallengeMode.None;
        }
    }

    [HarmonyPatch(typeof(EnvironmentController), "SpawnNPCs")]
    internal static class ChallengeRosterPatch
    {
        [HarmonyPrefix]
        static void Prefix(EnvironmentController __instance)
        {
            var mode = ChallengeUtil.Current();
            if (mode == ChallengeMode.None) return;

            ChallengeRoster.Apply(__instance, mode);
        }
    }

    [HarmonyPatch(typeof(EnvironmentController), "SpawnNPC")]
    internal static class ChallengeSpawnGuardPatch
    {
        [HarmonyFinalizer]
        static Exception? Finalizer(Exception? __exception, EnvironmentController __instance, NPC npc, IntVector2 position, ref NPC __result)
        {
            if (__exception == null) return null;
            if (ChallengeUtil.Current() == ChallengeMode.None) return __exception;

            var npcs = __instance.Npcs;
            if (npcs.Count > 0 && npcs[npcs.Count - 1] != null && npc != null && npcs[npcs.Count - 1].Character == npc.Character)
            {
                var broken = npcs[npcs.Count - 1];
                npcs.RemoveAt(npcs.Count - 1);
                UnityEngine.Object.Destroy(broken.gameObject);
            }

            __result = null!;
            return null;
        }
    }

    [HarmonyPatch(typeof(EnvironmentController), "MakeNoise", new Type[] { typeof(GameObject), typeof(Vector3), typeof(int), typeof(bool) })]
    internal static class ChallengeSafeNoisePatch
    {
        static readonly FieldInfo? SilentField = AccessTools.Field(typeof(EnvironmentController), "silent");
        static readonly FieldInfo? LastNoiseField = AccessTools.Field(typeof(EnvironmentController), "lastNoisePosition");

        [HarmonyPrefix]
        static bool Prefix(EnvironmentController __instance, GameObject source, Vector3 position, int value, bool hasSilentChance)
        {
            if (SilentField == null || LastNoiseField == null) return true;
            if (ChallengeUtil.Current() == ChallengeMode.None) return true;

            Vector3 last = (Vector3)LastNoiseField.GetValue(__instance);
            bool silent = (bool)SilentField.GetValue(__instance);

            var stickers = Singleton<StickerManager>.Instance;
            float silenceChance = stickers != null ? (float)stickers.StickerValue(Sticker.Silence) * 0.125f : 0f;
            if (hasSilentChance && last != position && UnityEngine.Random.value < silenceChance) return false;
            if (silent) return false;

            var cell = __instance.CellFromPosition(IntVector2.GetGridPosition(position));
            if (cell != null && cell.Silent) return false;

            LastNoiseField.SetValue(__instance, position);

            var npcs = __instance.Npcs;
            for (int i = 0; i < npcs.Count; i++)
            {
                var npc = npcs[i];
                if (npc == null) continue;
                try
                {
                    npc.Hear(source, position, value);
                    var machine = npc.behaviorStateMachine;
                    if (machine != null && machine.CurrentState != null) machine.CurrentState.Hear(source, position, value);
                }
                catch { }
            }

            var manager = Singleton<BaseGameManager>.Instance;
            if (manager != null) manager.NoiseMade(__instance, position, value);
            return false;
        }
    }

    [HarmonyPatch]
    internal static class ChallengeInitializePatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            return ChallengePatchTargets.Overrides("Initialize");
        }

        [HarmonyPostfix]
        static void Postfix(BaseGameManager __instance)
        {
            var mode = ChallengeUtil.ModeOf(__instance);
            if (mode == ChallengeMode.None) return;

            var ec = __instance.Ec;
            if (ec == null) return;

            ChallengeUtil.CleanYtp(ec);
            ChallengeUtil.RemoveEmptyPickups(ec);
        }
    }

    [HarmonyPatch]
    internal static class ChallengeExitedSpawnPatch
    {
        static BaseGameManager? scheduled;

        static IEnumerable<MethodBase> TargetMethods()
        {
            return ChallengePatchTargets.Overrides("ExitedSpawn");
        }

        [HarmonyPrefix]
        static void Prefix(BaseGameManager __instance)
        {
            var mode = ChallengeUtil.ModeOf(__instance);
            if (mode == ChallengeMode.None || __instance.Ec == null) return;

            ChallengeRoster.Apply(__instance.Ec, mode);
        }

        [HarmonyPostfix]
        static void Postfix(BaseGameManager __instance)
        {
            var mode = ChallengeUtil.ModeOf(__instance);
            if (mode == ChallengeMode.None) return;
            if (ReferenceEquals(scheduled, __instance)) return;

            scheduled = __instance;
            __instance.StartCoroutine(PostRoutine(__instance, mode));
        }

        static IEnumerator PostRoutine(BaseGameManager manager, ChallengeMode mode)
        {
            yield return new WaitForSeconds(1f);

            if (manager == null || manager.Ec == null) yield break;

            ChallengeUtil.CleanYtp(manager.Ec);
            ChallengeUtil.RemoveEmptyPickups(manager.Ec);
            if (mode == ChallengeMode.Stealthy) ChallengeUtil.LockOfficeDoors(manager.Ec);
            ChallengeUtil.ReconcileNotebooks(manager, mode);
        }
    }
}