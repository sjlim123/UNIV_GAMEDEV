using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// GameSceneBuilder의 삼일아파트 102동 씬 (Apt102): 메뉴, 재질 불러오기, 지킴 검사, 씬 조립, 마당, 하늘·빛, 내비메시, 저장.
// 탑은 GameSceneBuilder.AptTower.cs / .AptTowerRooms.cs, 점검 메뉴는 .Apt102Probe.cs, 소품의 Tripo 모델은 .TripoApt.cs / .TripoAptRooms.cs에 있다.
// 이 생성기는 재질·텍스처·글꼴을 만들거나 고치지 않는다: 도시 생성기가 디스크에 저장해 둔 재질을 불러 쓰기만 하고,
// 저장하는 것은 자기 씬·메시 묶음·내비메시·후처리 프로파일뿐이다 (SaveAssets를 부르면 메모리에서 틀어진 재질까지 함께 저장된다)
public static partial class GameSceneBuilder
{
    // 생성 뒤 지킴 검사에서 달라진 파일 수 (-1 = 이 에디터 세션에서 아직 생성하지 않았다)
    public static int Apt102GuardDiffs = -1;

    // 소품을 Tripo 모델로 바꿔 만든다 (GameSceneBuilder.TripoApt.cs). false면 예전 코드 소품 그대로 만든다: 전후 비교용 기준 씬을 뽑을 때
    // 스크립트를 고치지 않고 이 값만 끄고 생성한다 (스크립트를 다시 읽으면 true로 돌아온다)
    public static bool Apt102Tripo = true;

    const string Apt102ScenePath = SceneDir + "/Apt102.unity";
    const string Apt102MeshPath = GenDir + "/Apt102Meshes.asset";
    const string Apt102NavPath = GenDir + "/Apt102NavMesh.asset";
    const bool Apt102BakeOcclusion = true; // 가려진 층은 그리지 않게 오클루전 컬링까지 굽는다

    // CityDirector에 넣는 이 씬의 값 (도시는 GameBalance를 따르고, 이 건물만 따로 정한다)
    const float Apt102NormalWeight = 60f, Apt102LeatherWeight = 30f, Apt102RangedWeight = 10f; // 임시 수치
    const float Apt102HeavyWeight = 0f;  // 임시 수치: 천장 2.6m 아래로는 큰 놈이 돌아다니지 않는다 (옥상의 한 마리는 자리를 정해 세운다)
    const float Apt102UpstairsFill = 0f; // 임시 수치: 지킴이는 모두 정해 둔 자리에 선다. 비율로는 세우지 않는다
    const float Apt102SafeRadius = 18f;  // 임시 수치
    const int Apt102StartZombies = 2, Apt102MaxZombies = 5, Apt102DogCount = 0; // 임시 수치: 들개는 정해 둔 두 자리뿐이다

    // ================= 메뉴 =================

    [MenuItem("Tools/핵폭발 생존/삼일아파트 102동 씬만 생성")]
    static void BuildApt102OnlyMenu()
    {
        // 묻는 창은 띄우지 않는다 (에디터를 여러 세션이 같이 쓴다). 만들 수 없으면 까닭을 한 줄 남기고 그만둔다
        const string abort = "[GameSceneBuilder] Apt102 생성 중단: ";
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning(abort + "플레이 모드에서는 씬을 만들지 않습니다.");
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Debug.LogWarning(abort + "스크립트 컴파일이나 에셋 들여오기가 끝난 뒤에 다시 실행하세요.");
            return;
        }
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            // 새 씬을 열면 프리팹 편집 화면에서 나가게 된다. 저장하지 않은 프리팹 변경이 있으면 유니티가 저장할지 묻는 창을 띄운다
            Debug.LogWarning(abort + "프리팹 편집 화면이 열려 있습니다. 닫은 뒤 다시 실행하세요.");
            return;
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            // 새 씬을 열면 저장하지 않은 변경이 말없이 사라진다
            var open = SceneManager.GetSceneAt(i);
            if (!open.isDirty) continue;
            Debug.LogWarning(abort + $"열려 있는 씬 '{(string.IsNullOrEmpty(open.name) ? "이름 없음" : open.name)}'에 저장하지 않은 변경이 있습니다. 저장하거나 되돌린 뒤 다시 실행하세요.");
            return;
        }
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Player.prefab");
        var zombie = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Zombie.prefab");
        var dog = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Dog.prefab");
        if (player == null || zombie == null || dog == null)
        {
            Debug.LogWarning(abort + "프리팹(Player·Zombie·Dog)이 없습니다. 게임 씬 전체 생성을 먼저 하세요.");
            return;
        }
        // Tripo 모델의 품질별 판과 재질이 미리 다 있어야 한다: 이 생성기는 재질을 만들지 않는다 (없는 채로 돌리면 생성 중에 재질 파일이 새로 생겨 지킴 검사에 걸린다)
        if (Apt102Tripo && !TripoAptReady(out string tripoMissing))
        {
            Debug.LogWarning(abort + "Tripo 모델의 품질별 판이나 재질 파일이 없습니다 (새 모델의 재질은 메뉴 'Tripo 재질 만들기 (102동 새 모델)'로 먼저 만드세요): " + tripoMissing);
            return;
        }
        // 이 생성기가 M에 넣은 재질은 끝나면(거절·실패 때도) 도로 뺀다. 다른 생성기는 M에 든 키를 보고 갈라진다:
        // 같은 에디터 세션에서 도시를 다시 생성하면 "gold"가 있다고 RuinMaterials를 건너뛰고, "doorRust"가 있다고 해치 문짝 재질을 바꾼다
        var kept = new Dictionary<string, Material>(M);
        if (!Apt102LoadMaterials(out string missing))
        {
            Apt102RestoreM(kept);
            Debug.LogWarning(abort + "재질 파일이 없습니다 (도시 씬을 먼저 생성하세요): " + missing);
            return;
        }
        Apt102Guard guard;
        try
        {
            guard = Apt102GuardSnapshot();
        }
        catch (System.Exception e)
        {
            Apt102RestoreM(kept);
            Debug.LogException(e);
            Debug.LogError(abort + "지킬 파일의 사본을 뜨지 못했습니다. " + e.Message);
            return;
        }

        System.Exception error = null;
        apt102MeshesReplaced = apt102SceneSaved = false;
        // Tripo 소품을 넣는 동안(kTripoApt) 재질은 불러 쓰기만 한다 (kKeepMaterials: TripoTierMat이 있는 재질에 값을 쓰지 않는다).
        // '이번에 만든 재질' 목록은 먼저 비운다: 같은 에디터 세션의 앞선 생성이 남긴 재질은 불러 쓰기에서 빠져 다시 쓰인다
        bool keepWas = kKeepMaterials, tripoWas = kTripoApt;
        HashSet<Material> createdWas = null, writtenWas = null;
        if (Apt102Tripo)
        {
            createdWas = new HashSet<Material>(kCreated); writtenWas = new HashSet<Material>(kWritten); // 도시 씬만 생성의 2초 뒤 재검사가 이 목록을 쓴다: 끝나면 돌려놓는다
            kCreated.Clear();
            kWritten.Clear();
            kKeepMaterials = true;
            kTripoApt = true;
        }
        try
        {
            BuildApt102Scene(player, zombie, dog);
            AddApt102ToBuildList();
        }
        catch (System.Exception e)
        {
            error = e;
        }
        finally
        {
            kTripoApt = tripoWas; // 같은 도우미(가로등·가로수·실외기·차)를 메뉴·컷씬 생성기도 쓴다: 멈췄을 때도 반드시 끈다
            kKeepMaterials = keepWas;
            if (createdWas != null) { kCreated.Clear(); kCreated.UnionWith(createdWas); kWritten.Clear(); kWritten.UnionWith(writtenWas); }
            Apt102RestoreM(kept);
            Apt102GuardDiffs = Apt102GuardVerify(guard); // 도중에 멈춰도 재질·도시가 그대로인지는 본다
            SessionState.SetInt(Apt102GuardKey, Apt102GuardDiffs);
        }
        if (error != null)
        {
            Debug.LogException(error);
            // 만들다 만 씬은 버린다: 저장하지 않은 씬이 열린 채로 남으면 다음 실행도 막히고, 다른 씬을 열 때 저장 여부를 묻는 창이 뜬다
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            string left = " (만들다 만 씬은 버렸습니다)";
            try
            {
                left = Apt102LeftAfterFailure();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            Debug.LogError(abort + error.GetType().Name + ": " + error.Message + left);
            return;
        }
        Debug.Log("[GameSceneBuilder] Apt102 씬만 다시 생성 완료");
    }

    // 생성이 어디까지 갔는가 (도중에 멈췄을 때 디스크에 무엇이 남았는지 가린다)
    static bool apt102MeshesReplaced, apt102SceneSaved;

    // 도중에 멈춘 뒤 디스크에 남은 것 (중단 줄 끝에 붙인다).
    // 메시 묶음은 생성을 시작하면서 지우고 새로 만들어 GUID가 바뀐다. 그 뒤 씬을 저장하기 전에 멈추면 디스크에 남은 지난번 Apt102.unity는
    // 없어진 묶음을 가리켜 메시가 모두 빈다 (계단 쐐기 판정도 빈다). 그런 씬이 출발 창과 빌드에 들어가지 않게 빌드 목록에서 꺼 둔다
    static string Apt102LeftAfterFailure()
    {
        if (apt102SceneSaved) return " (씬을 저장한 뒤에 멈췄습니다. 디스크의 Apt102.unity는 이번에 만든 것이지만 오클루전 굽기나 빌드 목록 넣기가 빠졌을 수 있습니다)";
        if (!apt102MeshesReplaced) return " (만들다 만 씬은 버렸습니다. 디스크의 씬과 메시 묶음은 그대로입니다)";
        if (!File.Exists(Apt102ProjectRoot + "/" + Apt102ScenePath)) return " (만들다 만 씬은 버렸습니다)";
        return " (만들다 만 씬은 버렸습니다. 디스크의 Apt102.unity는 지난 생성본이라 새로 만든 메시 묶음과 맞지 않습니다. "
            + (Apt102DisableInBuildList() ? "다시 생성할 때까지 빌드 목록에서 꺼 둡니다)" : "다시 생성해야 합니다)");
    }

    // 시험용: 날짜와 상관없이 102동을 바로 고를 수 있게 한다 (Destinations.DebugUnlockAll). 저장되지 않는다
    const string Apt102DebugMenu = "Tools/핵폭발 생존/테스트/102동 바로 열기";
    const string Apt102DebugKey = "GameSceneBuilder.Apt102.DebugUnlockAll";
    const string Apt102GuardKey = "GameSceneBuilder.Apt102.GuardDiffs";

    [MenuItem(Apt102DebugMenu)]
    static void Apt102DebugUnlockMenu()
    {
        Destinations.DebugUnlockAll = !Destinations.DebugUnlockAll;
        SessionState.SetBool(Apt102DebugKey, Destinations.DebugUnlockAll);
        Debug.Log("[GameSceneBuilder] 102동 바로 열기: " + (Destinations.DebugUnlockAll ? "켬" : "끔") + " (에디터를 다시 컴파일하면 꺼집니다)");
    }

    [MenuItem(Apt102DebugMenu, true)]
    static bool Apt102DebugUnlockMenuValidate()
    {
        Menu.SetChecked(Apt102DebugMenu, Destinations.DebugUnlockAll);
        return true;
    }

    // 스크립트 영역이 다시 올라오면(컴파일, 플레이 시작) 정적 값이 지워진다. 에디터를 끌 때까지만 남는 SessionState에서 되살린다:
    // 지킴 검사 결과는 늘 되살리고 (점검이 "이 에디터 세션의 마지막 결과"를 읽는다),
    // 바로 열기는 플레이를 시작하느라 다시 올라온 때만 다시 켠다 (편집 모드에서 켜 둔 것이 그 플레이까지 이어지게. 컴파일이라면 꺼진 채로 둔다)
    [InitializeOnLoadMethod]
    static void Apt102RestoreAfterReload()
    {
        Apt102GuardDiffs = SessionState.GetInt(Apt102GuardKey, -1);
        if (!SessionState.GetBool(Apt102DebugKey, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Destinations.DebugUnlockAll = true;
            // 켠 세션이 아닌 다른 세션이 같은 에디터에서 플레이할 수도 있다. 출발 창이 평소와 다른 까닭을 플레이마다 남긴다
            Debug.LogWarning("[GameSceneBuilder] 102동 바로 열기: 켜진 채로 플레이를 시작합니다 (" + Apt102DebugMenu + " 메뉴로 끕니다)");
        }
        else SessionState.EraseBool(Apt102DebugKey);
    }

    // ================= 재질 (불러오기만) =================

    // M의 키와 Assets/Materials/Game의 파일 이름. 탑(AptTower·AptTowerRooms)도 여기 적힌 키만 읽는다
    static readonly string[] Apt102Materials =
    {
        "wall", "M_ConcreteWall", "cracked", "M_CrackedWall", "dirty", "M_DirtyConcrete", "asphalt", "M_Asphalt",
        "metal", "M_RustyMetal", "steel", "M_Steel", "brick", "M_Brick", "floor", "M_ConcreteFloor",
        "wood", "M_WoodPlanks", "glass", "M_DarkWindow", "line", "M_RoadLine", "tire", "M_Tire",
        "paintRed", "M_PaintRed", "paintBlue", "M_PaintBlue", "cloth", "M_Cloth", "blanket", "M_Blanket",
        "bulb", "M_Bulb", "redLamp", "M_RedLamp", "fire", "M_FireParticle", "ash", "M_AshParticle",
        "wetAsphalt", "M_WetAsphalt", "wetConcrete", "M_WetConcrete", "puddle", "M_Puddle", "crossPaint", "M_CrossPaint",
        "blastStripe", "M_BlastDoorStripe", "rebar", "M_Rebar", "soot", "M_Soot", "scorch", "M_Scorch",
        "ashPile", "M_AshPile", "bloodOld", "M_BloodOld", "tunnel", "M_TunnelConcrete", "gold", "M_GoldPaint",
        "paver", "M_StreetPaver", "granite", "M_StreetGranite", "grate", "M_StreetGrate",
        "paintW", "M_RoadPaintWhite", "paintY", "M_RoadPaintYellow", "fenceGreen", "M_StreetFence",
        "steelGrey", "M_SteelGrey", "bark", "M_Bark", "treeGrate", "M_TreeGrate", "lampHead", "M_LampHead",
        "shopGlass", "M_ShopGlass", "whitePaint", "M_WhitePaint", "frameDark", "M_FrameDark", "acUnit", "M_AcUnit",
        "aptWall", "M_AptWall", "aptBand", "M_AptBand", "aptRail", "M_AptRail", "aptGate", "M_AptGate",
        "aptNum0", "M_AptStripe_101", "aptNum1", "M_AptStripe_102", "aptNum2", "M_AptStripe_103", "aptPlate1", "M_AptPlate_102",
        "notice", "M_Notice", "banner0", "M_Banner_0", "earth", "M_Earth", "sandbag", "M_Sandbag", "doorRust", "M_DarkRustDoor",
    };

    // 재질을 디스크에서 불러 M에 넣는다. 만들거나 고치지 않는다. 하나라도 없으면 false (missing = 없는 파일 이름들)
    static bool Apt102LoadMaterials(out string missing)
    {
        var lost = new List<string>();
        for (int i = 0; i < Apt102Materials.Length; i += 2)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + Apt102Materials[i + 1] + ".mat");
            if (m != null) M[Apt102Materials[i]] = m;
            else lost.Add(Apt102Materials[i + 1]);
        }
        if (lost.Count == 0) M["parkPaint"] = M["paintW"]; // 주차선은 차선과 같은 흰 페인트
        missing = string.Join(", ", lost);
        return lost.Count == 0;
    }

    // M을 생성 전의 내용(kept)으로 되돌린다: Apt102LoadMaterials가 새로 넣은 키만 빠진다
    static void Apt102RestoreM(Dictionary<string, Material> kept)
    {
        M.Clear();
        foreach (var kv in kept) M[kv.Key] = kv.Value;
    }

    // ================= 지킴 검사 =================

    // 이 생성기가 건드리면 안 되는 파일을 생성 전후로 견준다. 재질·밸런스·도시 프로파일은 사본을 떠 두고(Library/Apt102Guard)
    // 달라지면 되돌린다. 다른 씬·메시·내비메시·프리팹·설정은 지문(SHA-1)만 견주어 알린다
    sealed class Apt102Guard
    {
        public readonly Dictionary<string, string> hash = new Dictionary<string, string>(); // 프로젝트 기준 경로 → 지문 ("" = 없던 파일)
        public readonly HashSet<string> copied = new HashSet<string>();                      // 사본이 있는 것
        public readonly HashSet<string> dirtyBefore = new HashSet<string>();                 // 사본이 있는 것 가운데 생성 전부터 메모리에서 바뀌어 있던 에셋
    }

    const string Apt102GuardDir = "Library/Apt102Guard";

    static string Apt102ProjectRoot => Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');

    // 지문만 견줄 파일들 (프로젝트 기준 경로): 도시 씬·메시·내비메시와 프리팹 셋, 거기에 프로젝트 설정과 이 생성기 것(이름이 Apt102로 시작)이 아닌 씬·생성 에셋·설정 에셋 전부.
    // 메모리에서 바뀐 에셋이 한꺼번에 저장되는 일이 생기면 재질만이 아니라 다른 씬의 프로파일·URP 설정·프로젝트 설정도 같이 바뀌는데, 목록에 없으면 아무 줄도 남지 않는다
    static List<string> Apt102GuardHashOnly()
    {
        string root = Apt102ProjectRoot;
        var list = new List<string>
        {
            SceneDir + "/City.unity", GenDir + "/CityMeshes.asset", GenDir + "/CityNavMesh.asset",
            PrefabDir + "/Player.prefab", PrefabDir + "/Zombie.prefab", PrefabDir + "/Dog.prefab",
            "ProjectSettings/ProjectSettings.asset",
        };
        foreach (string dir in new[] { SceneDir, GenDir, SettingsDir })
        {
            if (!Directory.Exists(root + "/" + dir)) continue;
            string ext = dir == SceneDir ? ".unity" : ".asset";
            foreach (string f in Directory.GetFiles(root + "/" + dir, "*" + ext, SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileName(f);
                if (!name.EndsWith(ext, System.StringComparison.OrdinalIgnoreCase) || name.StartsWith("Apt102", System.StringComparison.Ordinal)) continue;
                if (!list.Contains(dir + "/" + name)) list.Add(dir + "/" + name);
            }
        }
        return list;
    }

    // 사본을 떠 둘 파일들 (프로젝트 기준 경로): 모든 재질, 밸런스, 도시 프로파일과 그 .meta
    static List<string> Apt102GuardFiles()
    {
        string root = Apt102ProjectRoot;
        var list = new List<string>();
        foreach (string dir in new[] { "Assets/Materials", EffectDir })
        {
            if (!Directory.Exists(root + "/" + dir)) continue;
            var depth = dir == EffectDir ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;
            foreach (string f in Directory.GetFiles(root + "/" + dir, "*.mat", depth))
                if (f.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase)) list.Add(f.Replace('\\', '/').Substring(root.Length + 1));
        }
        list.Add("Assets/Resources/GameBalance.asset");
        list.Add(SettingsDir + "/CityProfile.asset");
        for (int i = 0, n = list.Count; i < n; i++) list.Add(list[i] + ".meta");
        list.Sort(string.CompareOrdinal);
        return list;
    }

    static string Apt102Sha1(string path)
    {
        if (!File.Exists(path)) return "";
        using (var sha = System.Security.Cryptography.SHA1.Create())
        using (var stream = File.OpenRead(path))
            return System.BitConverter.ToString(sha.ComputeHash(stream));
    }

    static Apt102Guard Apt102GuardSnapshot()
    {
        string root = Apt102ProjectRoot;
        var g = new Apt102Guard();
        foreach (string rel in Apt102GuardFiles())
        {
            string src = root + "/" + rel, copy = root + "/" + Apt102GuardDir + "/" + rel;
            g.hash[rel] = Apt102Sha1(src);
            if (!File.Exists(src)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(copy));
            File.Copy(src, copy, true);
            g.copied.Add(rel);
            if (Apt102Unsaved(rel)) g.dirtyBefore.Add(rel);
        }
        foreach (string rel in Apt102GuardHashOnly())
            if (!g.hash.ContainsKey(rel)) g.hash[rel] = Apt102Sha1(root + "/" + rel); // 도시 프로파일은 위에서 사본까지 떴다
        if (g.copied.Count < 20) throw new IOException("지킬 재질 파일을 찾지 못했습니다: " + root + "/Assets/Materials"); // 못 찾은 채로 지나가면 검사가 늘 통과해 버린다
        return g;
    }

    // 메모리에 올라와 있는 에셋에 저장하지 않은 변경이 있는가 (.meta는 늘 false).
    // 올라와 있지 않은 에셋은 불러오지 않는다: 불러온 적이 없으면 바뀌어 있을 수 없고, 물어보자고 재질 이백 개를 텍스처째 불러올 일이 아니다
    static bool Apt102Unsaved(string rel)
    {
        if (rel.EndsWith(".meta", System.StringComparison.Ordinal) || !AssetDatabase.IsMainAssetAtPathLoaded(rel)) return false;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(rel))
            if (o != null && EditorUtility.IsDirty(o)) return true;
        return false;
    }

    // 되돌린 파일이 누구 손에 바뀌었는지 짐작할 실마리 (되돌렸다는 줄 끝에 붙인다). 되돌리기는 누가 바꿨든 한다:
    // 다른 세션이 디스크에서 고친 것이었다면 그쪽이 .changed 사본에서 다시 가져가야 하므로, 그런 경우인지 가릴 수 있게 적어 둔다
    static string Apt102GuardHint(Apt102Guard g, string asset)
    {
        try
        {
            if (Apt102Unsaved(asset)) return ". 메모리의 에셋은 저장되지 않은 채입니다: 유니티가 아니라 밖에서(다른 세션·git) 디스크의 파일을 고친 것으로 보입니다";
            if (g.dirtyBefore.Contains(asset)) return ". 생성 전부터 메모리에서 바뀌어 있던 에셋을 유니티가 저장한 것으로 보입니다";
            return ". 생성 전에는 메모리에서 바뀌어 있지 않던 에셋입니다: 이 생성기가 고쳐서 저장했거나 밖에서(다른 세션·git) 디스크의 파일을 고친 것입니다";
        }
        catch (System.Exception)
        {
            return ""; // 실마리를 못 얻어도 되돌리기는 해야 한다
        }
    }

    // 달라진 파일 수를 돌려준다. 사본이 있는 파일은 되돌리고(바뀐 쪽은 사본 옆에 .changed로 남긴다) 나머지는 알리기만 한다
    static int Apt102GuardVerify(Apt102Guard g)
    {
        string root = Apt102ProjectRoot;
        int diffs = 0;
        var reimport = new List<string>();
        foreach (var kv in g.hash)
        {
            string rel = kv.Key, path = root + "/" + rel;
            try
            {
                if (Apt102Sha1(path) == kv.Value) continue;
                diffs++;
                if (!g.copied.Contains(rel))
                {
                    Debug.LogError("[GameSceneBuilder] Apt102 지킴 검사: 생성 중에 바뀌면 안 되는 파일이 바뀌었습니다 (되돌리지 않음): " + rel);
                    continue;
                }
                string copy = root + "/" + Apt102GuardDir + "/" + rel;
                string asset = rel.EndsWith(".meta", System.StringComparison.Ordinal) ? rel.Substring(0, rel.Length - 5) : rel;
                string hint = Apt102GuardHint(g, asset); // 되돌린 파일을 다시 들여오면 메모리의 흔적이 지워진다. 그 전에 본다
                if (File.Exists(path)) File.Copy(path, copy + ".changed", true);
                File.Copy(copy, path, true);
                if (!reimport.Contains(asset)) reimport.Add(asset);
                Debug.LogError("[GameSceneBuilder] Apt102 지킴 검사: 생성 중에 바뀐 파일을 되돌렸습니다: " + rel + " (바뀐 내용은 " + Apt102GuardDir + "/" + rel + ".changed" + hint + ")");
            }
            catch (System.Exception e)
            {
                diffs++;
                Debug.LogError("[GameSceneBuilder] Apt102 지킴 검사: 확인하거나 되돌리지 못했습니다: " + rel + " (" + e.Message + ")");
            }
        }
        try
        {
            foreach (string asset in reimport) AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceUpdate);
            // 새로 생긴 재질도 어긋난 것이다 (이 생성기는 재질을 만들지 않는다)
            var dirtied = new List<string>();
            foreach (string rel in Apt102GuardFiles())
            {
                if (!g.hash.ContainsKey(rel))
                {
                    if (!File.Exists(root + "/" + rel)) continue; // 목록에는 .meta가 있든 없든 들어 있다
                    diffs++;
                    Debug.LogError("[GameSceneBuilder] Apt102 지킴 검사: 생성 중에 새 파일이 생겼습니다: " + rel);
                    continue;
                }
                // 디스크는 그대로라도 메모리의 에셋이 바뀌어 있으면 다음에 누가 프로젝트를 저장할 때 같이 저장된다
                if (!g.dirtyBefore.Contains(rel) && Apt102Unsaved(rel)) dirtied.Add(Path.GetFileNameWithoutExtension(rel));
            }
            if (dirtied.Count > 0)
                Debug.LogWarning($"[GameSceneBuilder] Apt102 지킴 검사: 디스크 파일은 그대로지만 생성 중에 메모리에서 바뀐 에셋 {dirtied.Count}개: {string.Join(", ", dirtied)} (프로젝트를 저장하기 전에 확인하세요)");
        }
        catch (System.Exception e)
        {
            // 검사가 스스로 넘어져도 메뉴의 완료·중단 줄은 남아야 한다. 끝내지 못한 것은 달라진 것으로 센다
            diffs++;
            Debug.LogError("[GameSceneBuilder] Apt102 지킴 검사: 검사를 끝내지 못했습니다 (" + e.Message + ")");
        }
        if (diffs > 0) Debug.LogError($"[GameSceneBuilder] Apt102 지킴 검사: 달라진 파일 {diffs}개 (파일 {g.hash.Count}개 확인)");
        else Debug.Log($"[GameSceneBuilder] Apt102 지킴 검사: 파일 {g.hash.Count}개 그대로");
        return diffs;
    }

    // ================= 빌드 목록·후처리 프로파일 =================

    // 빌드 씬 목록에 Apt102를 City 바로 뒤에 넣는다 (씬 파일이 있을 때만. 이미 있으면 그대로, 꺼져 있으면 켠다)
    static void AddApt102ToBuildList()
    {
        if (!File.Exists(Apt102ProjectRoot + "/" + Apt102ScenePath)) return;
        var list = Apt102BuildListWith(EditorBuildSettings.scenes);
        if (list == null) return;
        EditorBuildSettings.scenes = list;
        Destinations.ForgetBuildCache(); // 출발 창이 "빌드 목록에 없다"고 기억해 둔 것을 지운다
    }

    // (순수 계산) Apt102가 켜진 채로 든 목록을 새로 만들어 돌려준다: 없으면 City 바로 뒤에 넣고, 꺼져 있으면 켠다. 고칠 것이 없으면 null.
    // 받은 목록과 그 항목은 건드리지 않는다. 점검(B13)이 목록의 사본으로 넣는 길·켜는 길을 두 번씩 지나가 본다
    static EditorBuildSettingsScene[] Apt102BuildListWith(EditorBuildSettingsScene[] scenes)
    {
        var list = new List<EditorBuildSettingsScene>(scenes);
        int at = list.FindIndex(s => s.path == Apt102ScenePath);
        if (at >= 0)
        {
            if (list[at].enabled) return null;
            list[at] = new EditorBuildSettingsScene(Apt102ScenePath, true);
        }
        else
        {
            int city = list.FindIndex(s => s.path.EndsWith("/City.unity", System.StringComparison.Ordinal));
            list.Insert(city < 0 ? list.Count : city + 1, new EditorBuildSettingsScene(Apt102ScenePath, true));
        }
        return list.ToArray();
    }

    // 빌드 씬 목록에서 Apt102를 꺼 둔다 (지우지는 않는다: 다음에 끝까지 생성되면 AddApt102ToBuildList가 다시 켠다). 끈 것이 있으면 true
    static bool Apt102DisableInBuildList()
    {
        var list = EditorBuildSettings.scenes;
        bool changed = false;
        foreach (var s in list)
        {
            if (s.path != Apt102ScenePath || !s.enabled) continue;
            s.enabled = false;
            changed = true;
        }
        if (!changed) return false;
        EditorBuildSettings.scenes = list;
        Destinations.ForgetBuildCache();
        return true;
    }

    // 후처리 프로파일: 있으면 그 에셋을 그대로 두고 값만 고쳐 쓴다 (다시 만들면 GUID가 바뀌어 씬의 참조가 끊긴다). 없을 때만 새로 만든다
    static VolumeProfile KProfileKeep(string name, float exposure, float saturation, Color filter, float grain, float contrast)
    {
        var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{SettingsDir}/{name}.asset");
        if (p == null) p = MakeProfile(name, exposure, saturation, filter, grain, contrast);
        else
        {
            if (p.TryGet<ColorAdjustments>(out var ca))
            {
                ca.postExposure.Override(exposure);
                ca.contrast.Override(contrast);
                ca.saturation.Override(saturation);
                ca.colorFilter.Override(filter);
                EditorUtility.SetDirty(ca);
            }
            if (p.TryGet<FilmGrain>(out var fg))
            {
                fg.intensity.Override(grain);
                EditorUtility.SetDirty(fg);
            }
            EditorUtility.SetDirty(p);
        }
        AssetDatabase.SaveAssetIfDirty(p);
        return p;
    }

    // ================= 씬 =================

    static void BuildApt102Scene(GameObject playerPrefab, GameObject zombiePrefab, GameObject dogPrefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        apt102MeshesReplaced = true; // 여기서부터 씬을 저장할 때까지, 디스크에 남은 지난번 씬은 없어진 메시 묶음을 가리킨다
        BeginMeshes(Apt102MeshPath);
        rng = new System.Random(102); // RubblePile·LampPost가 쓰는 공용 난수 (도시 생성기가 남긴 값에 기대지 않는다)
        var r = KRandom(Vector3.zero, 102);

        var root = new GameObject("Apt102").transform;
        var geo = Group("Geometry", root, Vector3.zero);       // 콜라이더·NavCut·상호작용 물체는 모두 이 아래 (내비메시는 이 아래만 굽는다)
        var points = Group("SpawnPoints", root, Vector3.zero);
        var dyn = Group("Dynamic", root, Vector3.zero);        // 위험 구역·추락 감시·재 입자

        var signBoard = BuildApt102Site(geo, points, dyn, r);
        BuildAptTower(geo, points, dyn, Vector3.zero, 0f, r);
        // 옥상 무대 위로 떨어지는 재. 탑 기준 좌표로 잡아 탑을 옮겨도 따라간다
        var tower = geo.Find("Tower");
        var roofAsh = new Vector3(10.6f, 25.5f, 0f);
        int dynCount = dyn.childCount;
        AptTAsh(dyn, tower != null ? tower.TransformPoint(roofAsh) : roofAsh, new Vector3(4.5f, 1f, 10.5f), 20f, 250);
        // 재 입자는 바닥에 막히지 않는다. 9초를 살면 12m를 떨어져 옥상 바닥(3m 아래)을 지나 804·704·604호 안까지 내린다.
        // 옥상 것만 바닥 조금 위에서 사라지게 수명을 줄인다 (중력 0.03배로 4초에 2.4m)
        var roofAshFall = dyn.childCount > dynCount ? dyn.GetChild(dyn.childCount - 1).GetComponent<ParticleSystem>() : null;
        if (roofAshFall != null)
        {
            var ashMain = roofAshFall.main;
            ashMain.startLifetime = 4f;
        }

        // 하늘·안개·달빛. 도시보다 어둡다 (안은 불빛과 손전등에 기댄다). 달은 북동쪽 위: 걸어 들어가며 보는 발코니 면이 달빛을 받는다
        var moon = new GameObject("Moonlight").AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.55f, 0.62f, 0.75f);
        moon.intensity = 0.9f;
        moon.shadows = LightShadows.Soft;
        moon.shadowStrength = 0.7f;
        moon.transform.rotation = Quaternion.Euler(38, 200, 0);
        RenderSettings.sun = moon;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.15f, 0.16f, 0.18f);
        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.03f;
        RenderSettings.fogColor = new Color(0.13f, 0.14f, 0.15f);
        RenderSettings.reflectionIntensity = 0.2f;
        AddVolume(KProfileKeep("Apt102Profile", 1.4f, -35f, new Color(0.92f, 1f, 0.95f), 0.4f, 10f));

        // 출발 자리: 정문 왼쪽 바깥. 재는 고정 입자로 뿌린다 (플레이어를 따라다니는 AddAshFall은 건물 안에서도 내린다)
        PlacePlayer(playerPrefab, new Vector3(-14f, 0.05f, 30f), 135f, RenderSettings.fogColor);

        var director = new GameObject("CityDirector").AddComponent<CityDirector>();
        director.zombiePrefab = zombiePrefab;
        director.dogPrefab = dogPrefab;
        director.normalWeight = Apt102NormalWeight;
        director.leatherWeight = Apt102LeatherWeight;
        director.rangedWeight = Apt102RangedWeight;
        director.heavyWeight = Apt102HeavyWeight;
        director.upstairsFill = Apt102UpstairsFill;
        director.weatherMode = CityDirector.WeatherMode.None; // 실내 위주라 날씨를 만들지 않는다
        director.safeRadius = Apt102SafeRadius;
        director.startZombies = Apt102StartZombies;
        director.maxZombies = Apt102MaxZombies;
        director.dogCount = Apt102DogCount;
        director.exitPoint = signBoard;

        var fall = new GameObject("FallWatch");
        fall.transform.SetParent(dyn, false);
        fall.AddComponent<FallWatch>();

        // Tripo 소품 (kTripoApt): 마당·탑·방을 다 지은 뒤, 내비메시를 굽기 직전에 이름으로 훑어 겉모습만 바꾼다 (난수를 쓰지 않는다, GameSceneBuilder.TripoApt.cs).
        // 여기서 콜라이더가 바뀌는 것은 세단 둘뿐이다: 도시처럼 예전 부품 콜라이더를 지우고 모델에 맞춘 상자 하나를 단다 (승합차는 코드 그대로라 콜라이더도 그대로)
        if (kTripoApt)
        {
            Debug.Log($"[GameSceneBuilder] Apt102: Tripo 소품 (잔해·나무 상자·콘크리트 판·물탱크·경비실·미끄럼틀·중계기함): {TripoAptPass(geo)} (굽기 직전, 겉모습만)");
            Debug.Log($"[GameSceneBuilder] Apt102: Tripo 차 {TripoCarColliders(geo)}대의 콜라이더를 모델에 맞춤 (굽기 직전. 승합차는 코드 그대로)");
            Debug.Log("[GameSceneBuilder] Apt102: Tripo 모델 받침 — " + TripoAptTally(geo));
        }

        // 내비메시 굽기. 문짝이 없는 씬이라 굽는 동안 꺼 둘 것이 없다 (레이어 2를 빼는 것은 도시와 같게 둔다)
        var lids = Apt102SiteNavCuts(geo.Find("Site"));
        Physics.SyncTransforms();
        var surface = geo.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~(1 << 2);
        surface.BuildNavMesh();
        if (!Apt102YardWalkable())
        {
            // 마당 뚜껑은 바닥에서 0.8m 떠 있어 바닥의 메시에는 닿지 않아야 한다. 그런데도 바닥이 지워졌다면 뚜껑을 빼고 다시 굽는다
            Object.DestroyImmediate(lids.gameObject);
            surface.BuildNavMesh();
            if (Apt102YardWalkable()) Debug.LogWarning("[GameSceneBuilder] Apt102: 마당 뚜껑 볼륨(Site/Lids)이 바닥의 내비메시까지 지워서, 뚜껑 없이 다시 구웠습니다. 큰 잔해 윗면에 외딴 메시 조각이 남을 수 있습니다.");
            else Debug.LogError("[GameSceneBuilder] Apt102: 마당 바닥(출발 자리·정문 안·경사로 발치)에 내비메시가 없습니다. 뚜껑 볼륨을 빼고 구워도 같습니다.");
        }
        AssetDatabase.DeleteAsset(Apt102NavPath);
        AssetDatabase.CreateAsset(surface.navMeshData, Apt102NavPath);

        UnbatchInteractables();
        Debug.Log($"[GameSceneBuilder] Apt102: 같은 면에 겹친 상자 {FixCoplanarFaces()}곳을 고침");

        // 저장: 메시 묶음과 씬만. SaveAssets는 부르지 않는다
        EditorUtility.SetDirty(meshContainer);
        AssetDatabase.SaveAssetIfDirty(meshContainer);
        long meshBytes = new FileInfo(Apt102ProjectRoot + "/" + Apt102MeshPath).Length;
        if (meshBytes < 64 * 1024) // 메시가 빠진 채 저장되면 묶음 파일은 몇 KB뿐이고, 씬을 다시 열 때 메시가 비어 있다
            Debug.LogError($"[GameSceneBuilder] Apt102: 메시 묶음이 디스크에 저장되지 않은 것 같습니다 ({Apt102MeshPath}, {meshBytes}바이트). 씬을 다시 열어 메시가 비지 않았는지 확인하세요.");
        if (!EditorSceneManager.SaveScene(scene, Apt102ScenePath)) throw new IOException("씬을 저장하지 못했습니다: " + Apt102ScenePath);
        apt102SceneSaved = true;
        if (Apt102BakeOcclusion)
        {
            if (!StaticOcclusionCulling.Compute()) Debug.LogWarning("[GameSceneBuilder] Apt102: 오클루전 컬링을 굽지 못했습니다 (씬은 저장됐습니다).");
            EditorSceneManager.SaveScene(scene, Apt102ScenePath);
        }
    }

    // ================= 마당 =================

    // 48×48m 마당 (x -22~26, z -12~36). 탑은 가운데(원점), 발코니 면이 +z. 돌아갈 곳인 표지판의 판(Board)을 돌려준다
    static Transform BuildApt102Site(Transform geo, Transform points, Transform dyn, System.Random r)
    {
        var site = Group("Site", geo, Vector3.zero);
        foreach (float gx in new[] { -16f, -4f, 8f, 20f })
            foreach (float gz in new[] { -6f, 6f, 18f, 30f })
                Box("Ground", site, new Vector3(gx, -0.1f, gz), new Vector3(12f, 0.2f, 12f), M["asphalt"], 5f);

        // 공용 난수(rng)를 쓰는 잔해 더미·가로등을 먼저 다 짓고, 그 뒤에 탑 쪽 도우미(AptTFlight·AptTFire·AptTAsh)를 부른다:
        // 도우미 안이 바뀌어도 마당의 잔해 배치는 그대로다
        Apt102Boundary(site);
        var board = Apt102ExitSign(site, r);
        var yard = Apt102Yard(site, r);
        Apt102WayIn(site, r);
        AptTFire(yard, new Vector3(19.0f, 1.0f, 9.0f), 12f, 12f, false);                  // 동쪽 잔해밭의 탄 차는 아직 타고 있다
        AptTAsh(dyn, new Vector3(2f, 4.5f, 21f), new Vector3(44f, 1f, 26f), 150f, 1500); // 마당 위로 떨어지는 재

        // 돌아다니는 좀비가 나오는 자리: 탑을 한 바퀴 도는 길 위
        var roam = new[] { new Vector3(-18.5f, 0, 2f), new Vector3(-16f, 0, -9f), new Vector3(4f, 0, -9.5f), new Vector3(21.5f, 0, -7f), new Vector3(22.5f, 0, 16f), new Vector3(-18f, 0, 20f) };
        for (int i = 0; i < roam.Length; i++) Spawn<ZombieSpawnPoint>(points, roam[i]).gameObject.name = "Roam" + (i + 1);
        // 바깥 아이템 자리 (돌만 나온다)
        var stones = new[] { new Vector3(-8f, 0.1f, 9f), new Vector3(3f, 0.1f, 15f), new Vector3(17f, 0.1f, 15f), new Vector3(19f, 0.1f, -3f), new Vector3(-17f, 0.1f, -6f) };
        for (int i = 0; i < stones.Length; i++)
        {
            var stone = Spawn<ItemSpawnPoint>(points, stones[i]);
            stone.outdoor = true;
            stone.gameObject.name = "ItemOut_" + (i + 1);
        }
        // 들개 두 마리: 놀이터 모래밭, 세워 둔 차 뒤. 그네 자리(-10.5, 14)는 그네 판정 상자 안이라 내비메시가 없어 모래밭 남쪽으로 뺐다
        Apt102DogPost(points, "D1", new Vector3(-10.5f, 0, 11.4f), 90f);
        Apt102DogPost(points, "D2", new Vector3(11.0f, 0, 18.6f), 200f);
        return board;
    }

    static void Apt102DogPost(Transform points, string name, Vector3 pos, float yaw)
    {
        var p = Spawn<ZombieSpawnPoint>(points, pos);
        p.gameObject.name = name;
        p.transform.rotation = Quaternion.Euler(0, yaw, 0);
        p.upstairs = true; // 지킴이 자리 (돌아다니는 좀비는 여기서 나오지 않는다)
        p.guaranteed = true;
        p.kind = SpawnKind.Dog;
    }

    // 둘레: 사방을 잔해 벽(높이 10)으로 막고 바로 안쪽에 보이지 않는 벽을 세운다.
    // 서쪽 벽의 틈(z 29~32) 뒤로 어두운 골목이 보인다. 돌아가는 길이지만 들어갈 수는 없다 (틈 앞 표지판에서 돌아간다)
    static void Apt102Boundary(Transform site)
    {
        var g = Group("Boundary", site, Vector3.zero);
        var rubble = M["cracked"];
        AptTBox("WallOfRubble", g, -25f, 29f, 0f, 10f, 36f, 39f, rubble, 4f);
        AptTBox("WallOfRubble", g, -25f, 29f, 0f, 10f, -15f, -12f, rubble, 4f);
        AptTBox("WallOfRubble", g, 26f, 29f, 0f, 10f, -12f, 36f, rubble, 4f);
        AptTBox("WallOfRubble", g, -25f, -22f, 0f, 10f, -12f, 29f, rubble, 4f);
        AptTBox("WallOfRubble", g, -25f, -22f, 0f, 10f, 32f, 36f, rubble, 4f);
        // 골목: 바닥, 양옆 벽, 막다른 끝은 검게
        AptTBox("Ground", g, -31f, -22f, -0.2f, 0f, 29f, 32f, M["asphalt"], 5f);
        AptTBox("WallOfRubble", g, -31f, -25f, 0f, 10f, 28f, 29f, rubble, 4f);
        AptTBox("WallOfRubble", g, -31f, -25f, 0f, 10f, 32f, 33f, rubble, 4f);
        AptTBox("AlleyEnd", g, -31.5f, -31f, 0f, 10f, 28f, 33f, M["tire"], 4f);
        // 보이지 않는 벽 (두께 0.5, 높이 30): 잔해 벽에 닿기 전에 막는다
        ColliderBox("Invisible", g, new Vector3(-21.7f, 15f, 12f), new Vector3(0.5f, 30f, 48f), Vector3.zero);
        ColliderBox("Invisible", g, new Vector3(25.7f, 15f, 12f), new Vector3(0.5f, 30f, 48f), Vector3.zero);
        ColliderBox("Invisible", g, new Vector3(2f, 15f, -11.7f), new Vector3(48f, 30f, 0.5f), Vector3.zero);
        ColliderBox("Invisible", g, new Vector3(2f, 15f, 35.7f), new Vector3(48f, 30f, 0.5f), Vector3.zero);
        // 벽 앞 잔해 더미 (공용 난수를 쓴다. 순서를 바꾸면 더미 모양이 모두 달라진다)
        foreach (var at in new[] { new Vector3(-19.5f, 0, -9f), new Vector3(-19f, 0, 12f), new Vector3(8f, 0, -10f), new Vector3(23.5f, 0, -9.5f), new Vector3(23.5f, 0, 24f), new Vector3(-10f, 0, 33.5f), new Vector3(12f, 0, 33.5f) })
            RubblePile(g, at, 6, 2f);
    }

    // 돌아가는 곳: 골목 틈 옆의 대피소 표지판 (동쪽을 본다). 도시 해치와 같은 붉은 등이 골목 어귀에서 깜빡인다
    static Transform Apt102ExitSign(Transform site, System.Random r)
    {
        var sign = Group("ShelterSign", site, new Vector3(-21.0f, 0, 28.2f), 90f); // 로컬 +z = 월드 +x
        foreach (float sx in new[] { -0.85f, 0.85f })
            Box("Post", sign, new Vector3(sx, 1.1f, 0), new Vector3(0.08f, 2.2f, 0.08f), M["steelGrey"], 1f);
        var board = Box("Board", sign, new Vector3(0, 1.6f, 0), new Vector3(1.8f, 0.6f, 0.06f), M["tire"], 1f);
        board.GetComponent<BoxCollider>().size = new Vector3(1.9f, 2.0f, 0.4f); // 판보다 큰 판정 (기둥 사이 어디를 봐도 잡힌다)
        board.AddComponent<ExpeditionExit>();
        // 쿼드는 -z를 보므로 앞면(+z)에는 180도 돌려 붙인다
        var face = Prim(PrimitiveType.Quad, "Face", sign, new Vector3(0, 1.6f, 0.032f), new Vector3(1.7f, 0.32f, 1f), M["banner0"], new Vector3(0, 180f, 0));
        face.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

        Box("Lamp", site, new Vector3(-21.9f, 3.0f, 30.5f), new Vector3(0.22f, 0.12f, 0.1f), M["redLamp"], 1f, false);
        var beacon = PointLight("BeaconLight", site, new Vector3(-21.2f, 2.8f, 30.5f), new Color(1f, 0.12f, 0.08f), 8f, 10f);
        Flicker(beacon, 0.1f, 3f, false);
        KBanner(site, new Vector3(-21.9f, 3.3f, 29.0f), new Vector3(-21.9f, 3.3f, 32.0f), Vector3.right, 0, r); // '민방위 대피소 → 300m'
        return board.transform;
    }

    // 마당: 울타리(z 24)와 정문, 경비실, 놀이터, 세워 둔 차, 주차선·과속방지턱, 가로등·가로수.
    // 정문·경비실·놀이터는 도시의 단지 마당(BuildAptYard)과 같은 모양을 이 자리 치수로 다시 짓는다
    static Transform Apt102Yard(Transform site, System.Random r)
    {
        var yard = Group("Yard", site, Vector3.zero);
        // 울타리: 정문 자리(x -5.2~5.2)만 비운다
        var fence = new List<CombineInstance>();
        for (int x = -21; x <= 25; x += 2)
        {
            if (Mathf.Abs(x) <= 5.2f) continue;
            KFenceSegment(fence, new Vector3(x, 0, 24f), Quaternion.identity);
            ColliderBox("YardFence", yard, new Vector3(x, 0.5f, 24f), new Vector3(2.0f, 1.0f, 0.1f), Vector3.zero);
        }
        CombineInto(yard, "YardFence", fence, M["fenceGreen"]);

        // 정문: 돌기둥 둘 + 가로 보 + '삼일아파트' 표지 (바깥인 +z를 본다) + 차단기
        var gate = Group("Gate", yard, new Vector3(0, 0, 24f));
        Box("Pillar", gate, new Vector3(-4.4f, 1.3f, 0), new Vector3(0.8f, 2.6f, 0.8f), M["granite"], 1f);
        Box("Pillar", gate, new Vector3(4.4f, 1.3f, 0), new Vector3(0.8f, 2.6f, 0.8f), M["granite"], 1f);
        Box("Beam", gate, new Vector3(0, 2.9f, 0), new Vector3(9.6f, 0.6f, 0.5f), M["granite"], 1f, false);
        var gateSign = Prim(PrimitiveType.Quad, "GateSign", gate, new Vector3(0, 2.9f, 0.26f), new Vector3(8.0f, 0.5f, 1f), M["aptGate"], new Vector3(0, 180f, 0));
        gateSign.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        var bar = new List<CombineInstance>();
        KRod(bar, new Vector3(3.4f, 1.0f, 0.5f), new Vector3(-0.6f, 1.3f, 0.5f), 0.04f);
        KPart(bar, new Vector3(0.3f, 1.0f, 0.3f), 1f, new Vector3(3.4f, 0.5f, 0.5f), Quaternion.identity);
        CombineInto(gate, "Barrier", bar, M["blastStripe"]);
        // 서쪽 기둥 바깥 면의 게시판 (기둥 면에서 5mm 띄운다)
        var noticeBoard = Box("NoticeBoard", gate, new Vector3(-4.4f, 1.5f, 0.43f), new Vector3(0.9f, 0.7f, 0.05f), M["wood"], 1f);
        var notice = noticeBoard.AddComponent<InspectNote>();
        notice.prompt = "게시판 - 읽어 본다";
        notice.sound = InspectNote.NoteSound.Paper;
        notice.lines = new[] { "'102동 출입 금지. 붕괴 위험. 주민은 민방위 대피소로.' 삼일아파트 관리사무소" };
        KNotice(gate, new Vector3(-4.4f, 1.5f, 0.461f), Vector3.forward, r);

        // 경비실: 정문 서쪽 안
        var booth = Group("GuardBooth", yard, new Vector3(-6.6f, 0, 21.6f));
        var boothBody = Box("Body", booth, new Vector3(0, 1.3f, 0), new Vector3(2.4f, 2.6f, 2.0f), M["aptWall"], 2f);
        Box("Roof", booth, new Vector3(0, 2.68f, 0), new Vector3(2.8f, 0.16f, 2.4f), M["dirty"], 2f, false);
        Box("Window", booth, new Vector3(0, 1.5f, 1.01f), new Vector3(1.6f, 0.9f, 0.02f), M["glass"], 1f, false);
        Box("Window", booth, new Vector3(1.21f, 1.5f, 0), new Vector3(0.02f, 0.9f, 1.2f), M["glass"], 1f, false);
        Box("Door", booth, new Vector3(-1.21f, 1.0f, 0.2f), new Vector3(0.02f, 2.0f, 0.8f), M["metal"], 1f, false);
        var boothNote = boothBody.AddComponent<InspectNote>();
        boothNote.prompt = "경비실 - 살펴본다";
        boothNote.sound = InspectNote.NoteSound.Paper;
        boothNote.inOrder = true;
        boothNote.lines = new[] { "근무 일지가 펼쳐져 있다. '102동 옥상 중계기 점검 - 기사 방문 예정'.", "열쇠 걸이는 비어 있다. 현관 열쇠는 없다." };

        // 놀이터: 모래 바닥, 그네, 미끄럼틀
        var play = Group("Playground", yard, new Vector3(-10.5f, 0, 14f));
        Box("Sand", play, new Vector3(0, 0.03f, 0), new Vector3(7f, 0.06f, 6f), M["ashPile"], 2f, false);
        var sw = new List<CombineInstance>();
        foreach (float sx in new[] { -1.5f, 1.5f })
        {
            KRod(sw, new Vector3(sx, 0, -1f), new Vector3(sx * 0.7f, 2.4f, -1f), 0.04f);
            KRod(sw, new Vector3(sx, 0, 1f), new Vector3(sx * 0.7f, 2.4f, 1f), 0.04f);
        }
        KRod(sw, new Vector3(-1.05f, 2.4f, -1f), new Vector3(1.05f, 2.4f, -1f), 0.04f);
        KRod(sw, new Vector3(-1.05f, 2.4f, 1f), new Vector3(1.05f, 2.4f, 1f), 0.04f);
        foreach (float sx in new[] { -0.5f, 0.5f })
        {
            KRod(sw, new Vector3(sx, 2.4f, -1f), new Vector3(sx, 0.6f, -1.3f), 0.01f);
            KRod(sw, new Vector3(sx, 2.4f, 1f), new Vector3(sx, 0.6f, 0.7f), 0.01f);
        }
        KPart(sw, new Vector3(0.45f, 0.04f, 0.2f), 1f, new Vector3(-0.5f, 0.6f, -1.3f), Quaternion.identity);
        KPart(sw, new Vector3(0.45f, 0.04f, 0.2f), 1f, new Vector3(0.5f, 0.6f, 0.7f), Quaternion.identity);
        CombineInto(play, "Swing", sw, M["aptRail"]);
        ColliderBox("SwingCollider", play, new Vector3(0, 1.2f, 0), new Vector3(3.2f, 2.4f, 2.4f), Vector3.zero);
        var slide = Group("Slide", play, new Vector3(2.5f, 0, 1.5f), KF(r, 0, 360));
        Box("Platform", slide, new Vector3(0, 1.4f, 0), new Vector3(1.0f, 0.08f, 1.0f), M["paintBlue"], 1f);
        foreach (float px in new[] { -0.45f, 0.45f })
            foreach (float pz in new[] { -0.45f, 0.45f })
                Box("Leg", slide, new Vector3(px, 0.7f, pz), new Vector3(0.06f, 1.4f, 0.06f), M["aptRail"], 1f, false);
        // 미끄럼판은 발판 끝(높이 1.5)에서 땅으로 내려간다. 도시 마당의 것은 -33도라 거꾸로(바깥 끝이 들려) 서 있다. 여기서는 바로 세운다
        Box("Chute", slide, new Vector3(0, 0.8f, 1.5f), new Vector3(0.8f, 0.05f, 2.6f), M["paintRed"], 1f, true, new Vector3(33f, 0, 0));
        Box("Ladder", slide, new Vector3(0, 0.75f, -0.7f), new Vector3(0.5f, 1.4f, 0.05f), M["aptRail"], 1f, true, new Vector3(20f, 0, 0));

        // 주차 칸을 가로질러 버려진 차 둘(하나는 탔다)과 동쪽 잔해밭의 탄 차. 차마다 자리에서 뽑은 난수를 쓴다:
        // 앞의 난수 쓰임이 바뀌어도 차종이 그대로다 (세단과 승합차. 승합차는 길이 5m라 둘이 겹치지 않게 그만큼 띄웠다)
        var carA = new Vector3(8.0f, 0, 20.8f);
        var carB = new Vector3(13.2f, 0, 20.4f);
        var carC = new Vector3(19.0f, 0, 9.0f);
        WreckedCarV2(yard, carA, 95f, false, false, KRandom(carA, 66)); // 파란 세단, 앞문이 열려 있다
        WreckedCarV2(yard, carB, 84f, false, true, KRandom(carB, 62));  // 탄 승합차
        WreckedCarV2(yard, carC, 20f, false, true, KRandom(carC, 64));  // 탄 세단 (불꽃은 BuildApt102Site가 붙인다)
        foreach (float x in new[] { 6.5f, 9.5f, 12.5f, 15.5f })
            Box("ParkingLine", yard, new Vector3(x, 0.011f, 21f), new Vector3(0.12f, 0.006f, 5f), M["parkPaint"], 1.5f, false);
        // 과속방지턱 (노랑·검정): 정문에서 현관 쪽으로 가는 길
        foreach (float z in new[] { 12f, 19f })
            Box("Bump", yard, new Vector3(0, 0.04f, z), new Vector3(3.2f, 0.08f, 0.6f), M["blastStripe"], 1f);

        // 정문 밖: 켜진 가로등 하나 (공용 난수 한 번: 방향), 불탄 가로수 둘
        LampPost(yard, new Vector3(5.6f, 0, 25.4f), true);
        StreetTree(yard, new Vector3(-8f, 0, 25.2f));
        StreetTree(yard, new Vector3(14f, 0, 25.2f));
        return yard;
    }

    // 들어가는 길: 탑 동쪽 끝에 쓰러진 바닥판(R1)을 밟고 올라 혀처럼 내민 판을 지나 2층 벽의 터진 곳으로 들어간다.
    // 통로(x 12.9~16.5, z 1.8~11.9)는 비워 두고 그 둘레를 잔해·그을음·연기로 채운다
    static void Apt102WayIn(Transform site, System.Random r)
    {
        RubblePile(site, new Vector3(17.4f, 0, 3.6f), 7, 1.0f);
        RubblePile(site, new Vector3(14.6f, 0, 0.8f), 6, 0.9f);
        RubblePile(site, new Vector3(20f, 0, 4f), 9, 2.2f);
        RubblePile(site, new Vector3(22.5f, 0, -3f), 7, 1.8f);
        RubblePile(site, new Vector3(19f, 0, -8f), 6, 1.6f);
        Stain("Scorch", site, new Vector3(16f, 0.012f, 2f), 5f, 4f, M["scorch"], 4704);
        Smoke(site, new Vector3(19.5f, 0.3f, 3.5f));

        AptTBox("Slab", site, 13.0f, 16.2f, 0f, 2.8f, 2.0f, 5.2f, M["dirty"]);
        AptTFlight(site, "R1", new Vector3(14.7f, 0, 11.6f), 90f, 6.4f, 2.8f, 3.0f, 0, "Slab", M["dirty"]); // -z로 오른다: 꼭대기 (14.7, 2.8, 5.2)
        // 경사로 꼭대기 양옆과 혀 옆면에서 삐져나온 철근 (길을 가리지 않게 바깥으로 뻗는다)
        Rebar(site, new Vector3(13.25f, 2.72f, 5.35f), new Vector3(-0.8f, 0.45f, 0.3f), 0.9f, r);
        Rebar(site, new Vector3(16.15f, 2.74f, 5.3f), new Vector3(0.8f, 0.45f, 0.2f), 1.0f, r);
        Rebar(site, new Vector3(16.18f, 2.2f, 4.2f), new Vector3(1f, 0.1f, -0.2f), 0.8f, r);
    }

    // 내비메시에서 빼는 영역 가운데 마당 몫 (탑 안의 것은 BuildAptTower가 만든다). 뚜껑들을 담은 그룹을 돌려준다
    static Transform Apt102SiteNavCuts(Transform site)
    {
        // 보이지 않는 벽 바깥: 벽 위와 골목에 메시가 남으면 안쪽 좀비가 그리로 가려고 벽에 붙어 서성인다
        AptTNavCut(site, new Vector3(2f, 10f, 45.7f), new Vector3(80f, 40f, 20f));
        AptTNavCut(site, new Vector3(2f, 10f, -21.7f), new Vector3(80f, 40f, 20f));
        AptTNavCut(site, new Vector3(-31.7f, 10f, 12f), new Vector3(20f, 40f, 80f));
        AptTNavCut(site, new Vector3(35.7f, 10f, 12f), new Vector3(20f, 40f, 80f));
        AptTNavCut(site, new Vector3(-6.6f, 3.2f, 21.6f), new Vector3(3.2f, 1.2f, 2.8f)); // 경비실 지붕
        // 아무도 오르지 못하지만 넓어서 메시 조각이 남는 윗면 둘: 그네 판정 상자(높이 2.4)와 승합차 짐칸 지붕(2.3). 물체 바로 위만 덮는다
        AptTNavCut(site, new Vector3(-10.5f, 2.9f, 14f), new Vector3(3.6f, 1.4f, 2.8f));
        AptTNavCut(site, new Vector3(12.16f, 2.7f, 20.29f), new Vector3(3.5f, 1.4f, 2.3f));
        // 키 큰 상자 셋의 속. 굽기는 상자를 속이 빈 껍데기로 본다 (탑의 AptTwSolidCuts와 같은 까닭): 상자 속 땅에도 아무도 못 가는 메시 조각이 생겨,
        // 가까운 적이 그 속을 배회 목적지로 잡고 상자 벽에 붙어 서성인다. 도시의 굽기에도 같은 그네 상자 속에 2.3㎡ 조각이 남아 있다.
        // 상자 면에서 0.2 안쪽으로, 땅 밑 0.3부터 윗면 아래까지만 덮는다. 혀의 윗면 2.8은 들어가는 길이라 닿으면 안 된다:
        // 0.5 아래에서 끝낸다 (탑의 채움 속 영역도 그 위의 걷는 바닥보다 0.4~0.5 아래에서 끝난다)
        AptTNavCut(site, new Vector3(14.6f, 1.0f, 3.6f), new Vector3(2.8f, 2.6f, 2.8f));    // 혀 상자 속: x 13.2..16.0, y -0.3..2.3, z 2.2..5.0
        AptTNavCut(site, new Vector3(-10.5f, 0.95f, 14f), new Vector3(2.8f, 2.5f, 2.0f));   // 그네 판정 상자 속: x -11.9..-9.1, y -0.3..2.2, z 13.0..15.0
        AptTNavCut(site, new Vector3(-6.6f, 1.0f, 21.6f), new Vector3(2.0f, 2.6f, 1.6f));   // 경비실 속: x -7.6..-5.6, y -0.3..2.3, z 20.8..22.4 (1㎡ 안팎이라 대개 굽기가 지우지만 타일 경계에 걸리면 남는다)
        // 마당의 뚜껑: 땅에서 발로 오를 수 없는 높이(0.8m 위)의 윗면은 걷는 곳이 아니다. 큰 잔해·차·현관 캐노피 위에
        // 아무도 가지 못하는 메시 조각이 남지 않게 한다 (잔해는 난수라 하나씩 덮을 수 없다). 탑 자리와 경사로 통로(x 12.9~16.6, z -5.75~11.9)는 덮지 않는다
        var lids = Group("Lids", site, Vector3.zero);
        Apt102Lid(lids, -21.7f, 25.7f, 11.9f, 35.7f);     // 북쪽 마당
        Apt102Lid(lids, -21.7f, 12.9f, 5.75f, 11.9f);     // 탑 앞
        Apt102Lid(lids, 16.6f, 25.7f, -11.7f, 11.9f);     // 동쪽 잔해밭
        Apt102Lid(lids, -21.7f, -13.25f, -11.7f, 5.75f);  // 서쪽
        Apt102Lid(lids, -13.25f, 16.6f, -11.7f, -5.75f);  // 탑 뒤
        return lids;
    }

    static void Apt102Lid(Transform lids, float x0, float x1, float z0, float z1)
    {
        const float y0 = 0.8f, y1 = 9f; // 에이전트가 오르는 턱 0.75m보다 조금 위부터, 둘레 벽(10m) 아래까지
        AptTNavCut(lids, new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, (z0 + z1) / 2f), new Vector3(x1 - x0, y1 - y0, z1 - z0));
    }

    // 굽고 난 뒤 마당 바닥에 내비메시가 있는가 (출발 자리, 정문 안, 경사로 발치)
    static bool Apt102YardWalkable()
    {
        foreach (var p in new[] { new Vector3(-14f, 0, 30f), new Vector3(0, 0, 21f), new Vector3(14.7f, 0, 12.4f) })
            if (!NavMesh.SamplePosition(p, out var hit, 0.6f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - p.y) > 0.3f) return false;
        return true;
    }
}
