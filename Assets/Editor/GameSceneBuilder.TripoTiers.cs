using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Tripo 소품 5종(세단·뒤집힌 세단·소화전·드럼통·버스정류장)의 품질별 판 (2026-11-23, 사용자 "5개 낮은 단계 만들기").
// 매우 높음(Q3) = 11-22에 다시 만든 Tripo FBX 그대로. 높음·중간·낮음(Q2·Q1·Q0) = 그 메시를 텍스처 이음매를 지키며 줄인 것 (TtSimplifier,
// GameSceneBuilder.TripoSimplify.cs). 처음엔 Unity 6 메시 LOD 생성기(MeshLodUtility)를 썼는데 이음매를 넘어 합쳐 텍스처가 찢어졌다.
// 남는 꼭짓점의 UV가 원본 그대로라 텍스처는 Q3 텍스처를 줄여 쓰면 된다 (다시 굽지 않는다).
// 결과 (모델 폴더의 품질별 하위 폴더, 실외기·나무와 같은 이름): Q0_낮음/{id}_q0.asset·_q0_diff.png·_q0.prefab, Q1_중간/…_q1…, Q2_높음/…_q2…,
// Q3_매우높음/{id}_q3.prefab (메시는 같은 폴더의 _q3.fbx를 그대로 쓴다: 2만 삼각형 메시를 .asset으로 또 두면 저장소만 커진다).
// 좌표: 실외기·나무 판과 같이 '앞 = +Z, 위 = +Y' (TripoSpecs의 yaw를 메시에 미리 돌려 넣음). 뒤집힌 차는 모델 자체가 뒤집힌 자세 그대로 (roll은 넣을 때)
public static partial class GameSceneBuilder
{
    // 목표 삼각형 수 {낮음, 중간, 높음}. 이음매·가장자리를 지키느라 더 못 줄이면 그 자리에서 멈춘다 (로그에 실제 수)
    // 기준: 높음 ≈ 매우 높음의 1/2(작은 소품은 1/5), 중간 ≈ 높음의 1/4, 낮음 ≈ 중간의 1/4 — 실외기(124·704·3,954·7,418)·나무처럼 단계마다 확연히 다르게
    static readonly Dictionary<string, int[]> TtTargets = new Dictionary<string, int[]>
    {
        { "wrecked_sedan_01",         new[] { 700, 2700, 10000 } },
        { "wrecked_sedan_flipped_01", new[] { 1000, 2500, 10000 } },
        { "fire_hydrant_01",          new[] { 640, 1280, 5000 } },
        { "steel_drum_01",            new[] { 320, 1280, 5000 } },
        { "bus_stop_01",              new[] { 700, 1400, 7000 } },
        // 트럭 (11-23 다시 만듦, 1만 판 12,970). 도시에 하나뿐이고 수리 목표라 가까이서 오래 보므로 낮음도 넉넉히
        { "pickup_truck_01",          new[] { 1200, 3000, 7000 } },
        // 방공호 가구 6종 (11-23 웹 스튜디오 H3.1, 원본 1.4만~1.7만). 방이 좁아 늘 가까이서 보므로 높음은 원본의 약 1/2
        { "shelter_bed_01",           new[] { 1100, 2200, 8700 } }, // 550에서는 가는 다리가 사라져 침대가 바닥에 붙었다 (11-23 품질 사진)
        { "shelter_cabinet_01",       new[] { 800, 2500, 8000 } },  // 칸 안의 상자·통조림·물병이 잘게 나뉘어 낮음도 넉넉히
        { "shelter_shelf_01",         new[] { 500, 1800, 7000 } },
        { "shelter_table_01",         new[] { 300, 1200, 5000 } },
        { "shelter_chair_01",         new[] { 300, 1200, 5000 } },
        { "shelter_workbench_01",     new[] { 0, 0, 0 } },          // 5만 면 원본(11-23 다시 만듦). 낮음·중간·높음은 GameSceneBuilder.TripoWorkbench.cs가 만든다
        // 방공호 2차 (11-23 웹 스튜디오 4K · 6만 설정, 원본 7.1만·6.6만이 그대로 매우 높음). 사용자 "8만·2K는 매우 높음에서만, 아래는 단계별로 확실히".
        // 높음은 처음 1.6만 → 사용자 "높음은 2.4만까지" (11-23). 중간·낮음은 그대로
        { "shelter_furnace_01",       new[] { 1000, 4000, 24000 } },
        { "shelter_generator_01",     new[] { 1500, 5000, 24000 } }, // 작은 부품(관·볼트·계기함)이 많아 낮음·중간을 넉넉히
        // 도시 남은 소품 1차 (11-24 웹 스튜디오 4K · 6만 설정). 잔해 덩어리는 도시에 650개쯤 깔려 낮음·중간을 아주 가볍게
        { "city_rubble_concrete_01",  new[] { 150, 600, 4000 } },
        { "city_rubble_slab_01",      new[] { 150, 600, 4000 } },
        { "city_rubble_asphalt_01",   new[] { 150, 600, 4000 } },
        { "city_rubble_wall_01",      new[] { 150, 600, 4000 } },
        { "city_rubble_brick_01",     new[] { 500, 1000, 5000 } }, // 200에서는 낮음이 납작한 판으로 무너졌다 (11-24 품질 사진)
        { "city_crate_01",            new[] { 800, 1600, 6000 } },  // 139개, 가게 안에서 가까이 본다. 낮음 200(멈춘 351)은 상자가 비틀려 무너졌다 (11-24 품질 사진)
        { "city_barrier_01",          new[] { 1200, 2000, 6000 } }, // 300 목표(멈춘 756)에서 낮음이 얇은 판자처럼 무너졌다 (11-24 품질 사진)
        { "city_sandbag_01",          new[] { 150, 600, 3000 } },
        // 도시 남은 소품 2차 (11-26 웹 스튜디오 H3.5 프리뷰, 4K · 6만 설정). 상자 꼴(계산대·쓰레기통)은 1차 상자처럼 낮음을 넉넉히
        { "city_mattress_01",         new[] { 300, 1200, 6000 } },
        { "city_counter_01",          new[] { 800, 1600, 6000 } },
        { "city_bin_01",              new[] { 800, 1600, 6000 } },
        { "city_water_tank_01",       new[] { 500, 1500, 8000 } },  // 가로 띠가 살게 높음을 넉넉히
        { "city_slab_01",             new[] { 300, 1000, 5000 } },
        { "city_stump_01",            new[] { 400, 1200, 6000 } },
        { "city_bollard_01",          new[] { 500, 900, 2500 } },   // 192개. 낮음 200(멈춘 312)은 윗부분이 비스듬히 잘렸고, 높음 2000은 이음매 지키기에서 둥근 머리가 깨졌다 → 높음도 새로 굽는다 (11-26 품질 사진)
        // 102동 새 모델 6종 (11-26 웹 스튜디오 H3.5 프리뷰, 원본 5.4만~5.8만). 한두 개씩만 놓이는 큰 소품이라 낮은 단계도 넉넉히
        { "apt_relay_cabinet_01",     new[] { 16000, 16000, 24000 } }, // 보스 보상 상자 하나뿐: 안에 선반·무전기가 있어 새로 구우면 껍데기가 무너지고(3,474), 이음매 지키기 8,000은 몸통이 구겨졌다 → 넉넉히
        { "apt_guard_booth_01",       new[] { 600, 1500, 6000 } },
        { "apt_slide_01",             new[] { 1500, 3000, 8000 } }, // 가는 난간·사다리: 너무 줄이면 무너진다
        { "apt_sofa_01",              new[] { 8000, 10000, 16000 } }, // 새로 구운 1,574는 팔걸이가 접혔고 이음매 지키기 4,500은 쿠션이 울퉁불퉁 구겨졌다 → 넉넉히 (방 하나에 하나뿐)
        { "apt_wardrobe_01",          new[] { 300, 800, 4000 } },
        { "apt_low_table_01",         new[] { 300, 800, 4000 } },
    };

    // 품질별 텍스처 크기 {낮음, 중간, 높음}. 없으면 256·512·512. 2차 두 모델은 매우 높음이 2048이라 1024·512·256으로 한 단계씩
    static readonly Dictionary<string, int[]> TtTexSizes = new Dictionary<string, int[]>
    {
        { "shelter_furnace_01", new[] { 256, 512, 1024 } },
        { "shelter_generator_01", new[] { 256, 512, 1024 } },
    };
    // 방공호 2차 (화로·발전기): 메뉴가 따로다 (1차 가구 메뉴는 이 둘을 건드리지 않는다)
    static readonly HashSet<string> TtShelter2 = new HashSet<string> { "shelter_furnace_01", "shelter_generator_01" };
    // 도시 남은 소품 2차 (11-26): 메뉴가 따로다
    static readonly HashSet<string> TtCity2 = new HashSet<string>
    {
        "city_mattress_01", "city_counter_01", "city_bin_01", "city_water_tank_01", "city_slab_01", "city_stump_01", "city_bollard_01",
    };
    // 102동 새 모델 (11-26): 메뉴가 따로다
    static readonly HashSet<string> TtApt1 = new HashSet<string>
    {
        "apt_relay_cabinet_01", "apt_guard_booth_01", "apt_slide_01", "apt_sofa_01", "apt_wardrobe_01", "apt_low_table_01",
    };
    // 도시 남은 소품 1차 (11-24): 메뉴가 따로다
    static readonly HashSet<string> TtCity1 = new HashSet<string>
    {
        "city_rubble_concrete_01", "city_rubble_slab_01", "city_rubble_asphalt_01", "city_rubble_wall_01", "city_rubble_brick_01",
        "city_crate_01", "city_barrier_01", "city_sandbag_01",
    };

    // TripoSpecs(재질 값이 정해진 모델)에 아직 없는 모델의 돌림 각도 (앞 = +Z).
    // 방공호 가구는 '코드 가구와 같은 축'으로 맞춘다: 침대는 머리판을 +Z(코드 침대 머리 쪽), 제작대는 공구판을 +Z(코드 제작대가 북쪽 벽에 대는 쪽)
    static readonly Dictionary<string, float> TtYaw = new Dictionary<string, float>
    {
        { "pickup_truck_01", 90f },
        { "shelter_bed_01", 180f }, { "shelter_cabinet_01", 0f }, { "shelter_shelf_01", 0f },
        { "shelter_table_01", 0f }, { "shelter_chair_01", 0f }, { "shelter_workbench_01", 180f },
        { "shelter_furnace_01", 180f }, { "shelter_generator_01", 180f }, // 화구·계기함이 -Z/+Z 쪽으로 코드와 같게 (11-23 앞모습 사진)
    };

    // 웹 스튜디오에서 FBX(Blender 설정)로 받은 모델 (11-23 방공호 가구). 맨 위 물체에 X -90°, 메시 자식에 X 270°가 들어 있어
    // 둘을 다 넣은 월드 자세가 바로 선 자세다 (맨 위 회전을 빼고 재면 누워 보인다). 아래 매우 높음 프리팹은 자식 회전까지 넣는다
    static readonly HashSet<string> TtStandUp = new HashSet<string>
    {
        "shelter_bed_01", "shelter_cabinet_01", "shelter_shelf_01", "shelter_table_01", "shelter_chair_01", "shelter_workbench_01",
        "shelter_furnace_01", "shelter_generator_01",
        "city_rubble_concrete_01", "city_rubble_slab_01", "city_rubble_asphalt_01", "city_rubble_wall_01", "city_rubble_brick_01",
        "city_crate_01", "city_barrier_01", "city_sandbag_01",
        "city_mattress_01", "city_counter_01", "city_bin_01", "city_water_tank_01", "city_slab_01", "city_stump_01", "city_bollard_01",
        "apt_relay_cabinet_01", "apt_guard_booth_01", "apt_slide_01", "apt_sofa_01", "apt_wardrobe_01", "apt_low_table_01",
    };

    // 모델마다 줄이는 방법 고르기 (11-23 방공호 가구 품질 사진에서):
    // TtSeamOnly = 이음매를 지키며 줄이다 멈추면 그 수 그대로 쓴다 (새로 굽기는 제작대 다리를 지우고 앞을 막힌 상자로 무너뜨렸다: 구멍 뚫린 공구판이 면을 붙잡아 다른 곳이 먼저 무너짐)
    // TtForceRebake = 이 판은 늘 모양만 줄이고 새로 굽는다 (선반 중간: 이음매 지키기로 목표에 닿아도 판자가 구겨졌다)
    // TtKeepParts = 낮음 판에서 작은 조각을 먼저 빼지 않는다 (침대 다리)
    static readonly HashSet<string> TtSeamOnly = new HashSet<string> { "shelter_workbench_01" };
    // 매우 높음 프리팹만 만들고 낮음·중간·높음은 다른 곳에서 만드는 모델
    static readonly HashSet<string> TtHighOnly = new HashSet<string> { "shelter_workbench_01" };
    // TtFreeze = 이 자리(앞 = +Z 좌표, 원본 크기)의 점은 줄이지 않는다.
    // 제작대: 낮음·중간에서 다리가 한쪽으로 쏠려 뭉개졌다 (11-23 사용자)
    static readonly Dictionary<string, System.Func<Vector3, bool>> TtFreeze = new Dictionary<string, System.Func<Vector3, bool>>
    {
    };
    static readonly Dictionary<string, int[]> TtForceRebake = new Dictionary<string, int[]> { { "shelter_shelf_01", new[] { 1 } }, { "city_bollard_01", new[] { 2 } },
        // 11-26 H3.5 모델: 이음매를 지키며 6천으로 줄인 높음 판이 중간(새로 구운 것)보다 찌그러져 보였다 (계산대 모서리·쓰레기통 옆면·물탱크 띠) → 높음도 새로 굽는다
        { "city_counter_01", new[] { 2 } }, { "city_bin_01", new[] { 2 } }, { "city_water_tank_01", new[] { 2 } },
        // 102동 새 모델도 같다: 경비실 지붕 모서리·옷장 옆면·밥상 상판이 이음매 지키기 높음에서 구겨졌다
        { "apt_guard_booth_01", new[] { 2 } }, { "apt_wardrobe_01", new[] { 2 } }, { "apt_low_table_01", new[] { 2 } } };
    static readonly HashSet<string> TtKeepParts = new HashSet<string> { "shelter_bed_01" };

    [MenuItem("Tools/핵폭발 생존/Tripo 품질별 판 만들기 (방공호 가구 6종)")]
    static void TtBuildShelter()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[GameSceneBuilder] 플레이 중에는 하지 않는다"); return; }
        var log = new List<string>();
        foreach (var id in TtStandUp) if (!TtShelter2.Contains(id) && !TtCity1.Contains(id) && !TtCity2.Contains(id) && !TtApt1.Contains(id)) log.Add(TtMake(id, TtTargets[id]));
        log.Add(WbMakeHigh());
        log.Add(WbMakeLow());
        Debug.Log("[GameSceneBuilder] Tripo 품질별 판 (방공호 가구): " + string.Join(" / ", log));
    }

    [MenuItem("Tools/핵폭발 생존/Tripo 품질별 판 만들기 (방공호 2차: 화로·발전기)")]
    static void TtBuildShelter2()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[GameSceneBuilder] 플레이 중에는 하지 않는다"); return; }
        var log = new List<string>();
        foreach (var id in TtShelter2) log.Add(TtMake(id, TtTargets[id]));
        Debug.Log("[GameSceneBuilder] Tripo 품질별 판 (방공호 2차): " + string.Join(" / ", log));
    }

    [MenuItem("Tools/핵폭발 생존/Tripo 품질별 판 만들기 (도시 남은 소품 1차: 잔해 5·상자·방호벽·모래주머니)")]
    static void TtBuildCity1()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[GameSceneBuilder] 플레이 중에는 하지 않는다"); return; }
        var log = new List<string>();
        foreach (var id in TtCity1) log.Add(TtMake(id, TtTargets[id]));
        Debug.Log("[GameSceneBuilder] Tripo 품질별 판 (도시 남은 소품 1차): " + string.Join(" / ", log));
    }

    [MenuItem("Tools/핵폭발 생존/Tripo 품질별 판 만들기 (도시 남은 소품 2차: 매트리스·계산대·쓰레기통·물탱크·콘크리트 판·밑동·볼라드)")]
    static void TtBuildCity2()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[GameSceneBuilder] 플레이 중에는 하지 않는다"); return; }
        var log = new List<string>();
        foreach (var id in TtCity2) log.Add(TtMake(id, TtTargets[id]));
        Debug.Log("[GameSceneBuilder] Tripo 품질별 판 (도시 남은 소품 2차): " + string.Join(" / ", log));
    }

    [MenuItem("Tools/핵폭발 생존/Tripo 품질별 판 만들기 (102동 새 모델: 중계함·경비실·미끄럼틀·소파·옷장·낮은 밥상)")]
    static void TtBuildApt1()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[GameSceneBuilder] 플레이 중에는 하지 않는다"); return; }
        var log = new List<string>();
        foreach (var id in TtApt1) log.Add(TtMake(id, TtTargets[id]));
        Debug.Log("[GameSceneBuilder] Tripo 품질별 판 (102동 새 모델): " + string.Join(" / ", log));
    }

    [MenuItem("Tools/핵폭발 생존/Tripo 품질별 판 만들기 (차·뒤집힌 차·소화전·드럼통·정류장·트럭)")]
    static void TtBuildAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[GameSceneBuilder] 플레이 중에는 하지 않는다"); return; }
        var log = new List<string>();
        foreach (var kv in TtTargets) if (!TtStandUp.Contains(kv.Key)) log.Add(TtMake(kv.Key, kv.Value)); // 방공호 가구는 따로 (TtBuildShelter)
        Debug.Log("[GameSceneBuilder] Tripo 품질별 판: " + string.Join(" / ", log));
    }

    static string TtMake(string id, int[] targets)
    {
        string dir = $"{TripoModelDir}/{id}";
        string q3Dir = $"{dir}/{LpTierDir[3]}";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{q3Dir}/{id}_q3.fbx");
        if (model == null) return id + ": Q3 FBX 없음";
        var mf = model.GetComponentInChildren<MeshFilter>();
        var src = mf.sharedMesh;
        float yaw = TripoSpecs.TryGetValue(id, out var spec) ? spec.yaw : TtYaw[id];
        var rootRot = Quaternion.Euler(0, yaw, 0) * model.transform.localRotation;
        var mm = Matrix4x4.Rotate(rootRot) * model.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        var nm = mm.inverse.transpose;

        // 원본 꼭짓점을 '앞 = +Z' 좌표로
        var pos = src.vertices; var nrm = src.normals; var uv = src.uv; var tri0 = src.triangles;
        for (int i = 0; i < pos.Length; i++) { pos[i] = mm.MultiplyPoint3x4(pos[i]); nrm[i] = nm.MultiplyVector(nrm[i]).normalized; }
        var srcTex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        srcTex.LoadImage(File.ReadAllBytes($"{q3Dir}/{id}_q3_diff.jpg"));
        srcTex.wrapMode = TextureWrapMode.Clamp; // 굽기에서 아틀라스 가장자리 색이 반대쪽과 섞이지 않게

        var srcB = new Bounds(pos[0], Vector3.zero); foreach (var v in pos) srcB.Encapsulate(v);
        var bakeSrc = new LpSource { p = pos, uv = uv, tri = tri0, tex = srcTex, b = srcB };

        var parts = new List<string>();
        for (int q = 2; q >= (TtHighOnly.Contains(id) ? 3 : 0); q--)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int size = TtTexSizes.TryGetValue(id, out var ts) ? ts[q] : q == 0 ? 256 : 512; // 높음·중간 512, 낮음 256 (실외기·나무와 같다)
            List<Vector3> p2, n2; List<Vector2> u2; int[] t2; Color32[] px = null; string how;

            // 1) 텍스처 이음매를 지키며 줄이기 (원본 UV·텍스처 그대로). 목표의 115 % 안에 들면 이것을 쓴다
            TtFreeze.TryGetValue(id, out var freeze);
            var simp = new TtSimplifier(pos, uv, tri0, false, freeze);
            simp.Run(targets[q]);
            bool forceRebake = TtForceRebake.TryGetValue(id, out var fr) && System.Array.IndexOf(fr, q) >= 0;
            bool seamOnly = TtSeamOnly.Contains(id);
            if (!forceRebake && (simp.alive <= targets[q] * 1.15f || seamOnly))
            {
                how = simp.alive <= targets[q] * 1.15f ? "UV 그대로" : "UV 그대로 (이음매 지키기로 " + simp.alive + "에서 멈춘 수 그대로)";
                var idx = simp.Triangles();
                var remap = new Dictionary<int, int>();
                p2 = new List<Vector3>(); n2 = new List<Vector3>(); u2 = new List<Vector2>();
                t2 = new int[idx.Length];
                for (int i = 0; i < idx.Length; i++)
                {
                    if (!remap.TryGetValue(idx[i], out int w))
                    {
                        w = p2.Count; remap[idx[i]] = w;
                        // 법선은 줄인 면들로 다시 낸다 (원본 꼭짓점 법선의 작은 울퉁불퉁함이 길어진 삼각형 위로 번져 드럼통 옆에 얼룩이 생겼다 — 검수 11-23)
                        p2.Add(pos[idx[i]]); n2.Add(simp.Normal(idx[i], nrm[idx[i]])); u2.Add(uv[idx[i]]);
                    }
                    t2[i] = w;
                }
            }
            else
            {
                // 2) 이음매가 너무 잘게 나뉘어 더 못 줄이면: 모양만 줄이고(같은 자리 꼭짓점을 하나로), 새로 펼쳐 원본 텍스처를 구워 옮긴다
                how = "새로 구움 (이음매 지키기로는 " + simp.alive + "에서 멈춤)";
                var m = TtRebakeMesh(pos, tri0, targets[q], q == 0 && !TtKeepParts.Contains(id) ? 0.05f : 0f, out int dropped, freeze);
                var uvb = LpPackUv(m, size, q == 0 ? 2 : 3);
                int outside = 0; foreach (var w in uvb) if (w.x < 0f || w.y < 0f || w.x > 1f || w.y > 1f) outside++;
                if (outside > 0) Debug.LogWarning($"[GameSceneBuilder] {id} q{q}: 텍스처 한 장에 조각이 다 들어가지 않음 (꼭짓점 {outside}개가 밖)");
                px = LpBake(m, uvb, bakeSrc, size, 0.06f);
                p2 = m.pos; n2 = m.nrm; u2 = new List<Vector2>(uvb); t2 = m.tri.ToArray();
                how += ", 조각 " + m.charts + (dropped > 0 ? ", 작은 덩어리 면 " + dropped + "개 뺌" : "");
            }
            long ms = sw.ElapsedMilliseconds;

            string sub = $"{dir}/{LpTierDir[q]}";
            if (!AssetDatabase.IsValidFolder(sub)) AssetDatabase.CreateFolder(dir, LpTierDir[q]);
            string suf = "_q" + q;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{sub}/{id}{suf}.asset");
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.Clear();
            mesh.name = id + suf;
            mesh.indexFormat = p2.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(p2); mesh.SetNormals(n2); mesh.SetUVs(0, u2); mesh.SetTriangles(t2, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            if (isNew) AssetDatabase.CreateAsset(mesh, $"{sub}/{id}{suf}.asset");
            else { EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh); }

            // 텍스처: UV 그대로면 Q3 원본(2048)의 밉 단계(상자 평균), 새로 구웠으면 구운 것
            Texture2D tex;
            if (px == null)
            {
                int mip = Mathf.RoundToInt(Mathf.Log(srcTex.width / (float)size, 2f));
                int mw = Mathf.Max(1, srcTex.width >> mip), mh = Mathf.Max(1, srcTex.height >> mip);
                tex = new Texture2D(mw, mh, TextureFormat.RGBA32, false);
                tex.SetPixels32(srcTex.GetPixels32(mip));
            }
            else { tex = new Texture2D(size, size, TextureFormat.RGBA32, false); tex.SetPixels32(px); }
            tex.Apply();
            string texPath = $"{sub}/{id}{suf}_diff.png";
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (ti != null && (ti.maxTextureSize != size || !ti.sRGBTexture || !ti.mipmapEnabled || ti.textureCompression != TextureImporterCompression.Compressed || ti.alphaSource != TextureImporterAlphaSource.None || ti.mipmapLimitGroupName != ""))
            {
                ti.maxTextureSize = size; ti.sRGBTexture = true; ti.mipmapEnabled = true; ti.textureCompression = TextureImporterCompression.Compressed;
                ti.alphaSource = TextureImporterAlphaSource.None; ti.mipmapLimitGroupName = "";
                ti.SaveAndReimport();
            }

            var go = new GameObject(id + suf);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
            PrefabUtility.SaveAsPrefabAsset(go, $"{sub}/{id}{suf}.prefab");
            Object.DestroyImmediate(go);
            parts.Insert(0, $"q{q} {t2.Length / 3}(목표 {targets[q]}, 꼭짓점 {p2.Count}, 텍스처 {size}, {how}, {ms} ms)");
        }
        Object.DestroyImmediate(srcTex);
        // 색만 다른 판 텍스처({id}_q{n}_diff_blue 등, TripoSpec.tex)는 여기서 다시 만들지 않는다: 새로 구우면 UV가 바뀌어 예전 색 판이 어긋나므로 알린다 (11-26 검토)
        if (Directory.GetFiles($"{TripoModelDir}/{id}", id + "_q*_diff_*.*", SearchOption.AllDirectories).Length > 0)
            Debug.LogWarning($"[GameSceneBuilder] {id}: 색만 다른 텍스처(_diff_*)가 있다. 품질별 판을 다시 만들었으니 그것도 다시 만든다 (Docs/맵모델링_미리보기/18_도시_남은소품_2차/_도구/blue_textures.py)");

        // 매우 높음: FBX 메시를 그대로, 같은 좌표가 되게 자식을 돌려 둔 프리팹
        {
            var go = new GameObject(id + "_q3");
            var child = new GameObject("M");
            child.transform.SetParent(go.transform, false);
            // 메시 자식의 회전(Blender 설정 FBX는 X 270°)까지 넣는다. 예전 모델은 자식 회전이 없어 rootRot 그대로와 같다
            child.transform.localRotation = rootRot * Quaternion.Inverse(model.transform.rotation) * mf.transform.rotation;
            child.AddComponent<MeshFilter>().sharedMesh = src;
            child.AddComponent<MeshRenderer>();
            PrefabUtility.SaveAsPrefabAsset(go, $"{q3Dir}/{id}_q3.prefab");
            Object.DestroyImmediate(go);
        }
        parts.Add($"q3 {src.GetIndexCount(0) / 3}(FBX)");
        return id + ": " + string.Join(", ", parts);
    }

    // 모양만 줄이고(같은 자리 꼭짓점을 한 점으로 묶어 이음매 없이 QEM) 새 조각으로 나눈 메시. 조각 = 이웃 면을 따라 번지되
    // 조각 평균 법선과 45° 안, 바로 옆 면과 35° 안인 면까지 (평면에 비춰 펼친다). 법선은 그 점의 면 중 이 조각 쪽 법선과 60° 안인 면의 평균
    // minPart > 0: 전체 대각선의 그 비율보다 작은 따로 떨어진 조각(볼트·너트 등)을 먼저 뺀다 (낮음 판: 작은 조각이 면을 붙잡아 큰 모양이 먼저 무너졌다)
    static LpMesh TtRebakeMesh(Vector3[] wpos, int[] wtri, int target, float minPart, out int dropped, System.Func<Vector3, bool> freeze = null)
    {
        var map = new Dictionary<Vector3Int, int>(); var pl = new List<Vector3>(); var P = new int[wpos.Length];
        for (int w = 0; w < wpos.Length; w++)
        {
            var key = Vector3Int.RoundToInt(wpos[w] * 100000f);
            if (!map.TryGetValue(key, out int p)) { p = pl.Count; pl.Add(wpos[w]); map[key] = p; }
            P[w] = p;
        }
        var ptri = new int[wtri.Length]; for (int i = 0; i < wtri.Length; i++) ptri[i] = P[wtri[i]];
        var pts = pl.ToArray();
        dropped = 0;
        if (minPart > 0f)
        {
            // 이어진 덩어리 (점 기준 합집합 찾기)
            var par = new int[pts.Length]; for (int i = 0; i < par.Length; i++) par[i] = i;
            System.Func<int, int> Find = null;
            Find = x => { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; };
            for (int t = 0; t < ptri.Length; t += 3) { int a = Find(ptri[t]), b = Find(ptri[t + 1]), c = Find(ptri[t + 2]); par[b] = a; par[Find(c)] = a; }
            var lo = new Dictionary<int, Vector3>(); var hi = new Dictionary<int, Vector3>();
            var all = new Bounds(pts[0], Vector3.zero);
            for (int i = 0; i < pts.Length; i++)
            {
                int r = Find(i); all.Encapsulate(pts[i]);
                if (!lo.ContainsKey(r)) { lo[r] = pts[i]; hi[r] = pts[i]; } else { lo[r] = Vector3.Min(lo[r], pts[i]); hi[r] = Vector3.Max(hi[r], pts[i]); }
            }
            float lim = all.size.magnitude * minPart;
            var keep = new List<int>();
            for (int t = 0; t < ptri.Length; t += 3)
            {
                int r = Find(ptri[t]);
                if ((hi[r] - lo[r]).magnitude < lim) { dropped++; continue; }
                keep.Add(ptri[t]); keep.Add(ptri[t + 1]); keep.Add(ptri[t + 2]);
            }
            ptri = keep.ToArray();
        }
        var simp = new TtSimplifier(pts, new Vector2[pts.Length], ptri, true, freeze);
        simp.Run(target);
        for (int i = 0; i < pts.Length; i++) pts[i] = simp.Pos(i);
        var tri = simp.Triangles(); // 점 번호 (쐐기 = 점)
        tri = TtDropTinyShells(tri, pts.Length, 4); // 줄이다 남은 면 4개 이하 조각(볼트 찌꺼기)은 뺀다
        int T = tri.Length / 3;
        var fn = new Vector3[T]; var fa = new float[T];
        var pf = new Dictionary<int, List<int>>();
        var ef = new Dictionary<long, List<int>>();
        for (int t = 0; t < T; t++)
        {
            Vector3 a = pts[tri[3 * t]], b = pts[tri[3 * t + 1]], c = pts[tri[3 * t + 2]];
            var n = Vector3.Cross(b - a, c - a); fa[t] = n.magnitude; fn[t] = fa[t] > 1e-12f ? n / fa[t] : Vector3.up;
            for (int k = 0; k < 3; k++)
            {
                int p0 = tri[3 * t + k], p1 = tri[3 * t + (k + 1) % 3];
                if (!pf.TryGetValue(p0, out var l)) pf[p0] = l = new List<int>();
                l.Add(t);
                long key = p0 < p1 ? ((long)p0 << 32) | (uint)p1 : ((long)p1 << 32) | (uint)p0;
                if (!ef.TryGetValue(key, out var e)) ef[key] = e = new List<int>(2);
                e.Add(t);
            }
        }
        // 조각 나누기 (넓은 면부터 씨앗)
        var chartOf = new int[T]; for (int t = 0; t < T; t++) chartOf[t] = -1;
        var order = new List<int>(); for (int t = 0; t < T; t++) order.Add(t);
        order.Sort((x, y) => fa[y].CompareTo(fa[x]));
        var m = new LpMesh();
        var chartN = new List<Vector3>();
        // 조각 평균과 60° 안, 바로 옆 면과 50° 안 (처음 45°·35°는 면 1~2개 조각이 절반이라 텍스처 대부분이 여백이었다 — 검수 11-23)
        float cos45 = Mathf.Cos(60f * Mathf.Deg2Rad), cos35 = Mathf.Cos(50f * Mathf.Deg2Rad);
        foreach (int seed in order)
        {
            if (chartOf[seed] >= 0) continue;
            int c = m.NewChart(); chartOf[seed] = c;
            var sum = fn[seed] * fa[seed]; var avg = fn[seed];
            var queue = new Queue<int>(); queue.Enqueue(seed);
            while (queue.Count > 0)
            {
                int f = queue.Dequeue();
                for (int k = 0; k < 3; k++)
                {
                    int p0 = tri[3 * f + k], p1 = tri[3 * f + (k + 1) % 3];
                    long key = p0 < p1 ? ((long)p0 << 32) | (uint)p1 : ((long)p1 << 32) | (uint)p0;
                    foreach (int g in ef[key])
                    {
                        if (chartOf[g] >= 0 || g == f) continue;
                        if (Vector3.Dot(fn[g], avg) < cos45 || Vector3.Dot(fn[g], fn[f]) < cos35) continue;
                        chartOf[g] = c; sum += fn[g] * fa[g]; avg = sum.normalized; queue.Enqueue(g);
                    }
                }
            }
            chartN.Add(avg);
        }
        // 면 5개보다 작은 조각은 이웃 조각 중 법선이 가장 비슷한 것(75° 안)에 붙인다
        {
            var cnt = new int[m.charts]; foreach (int c in chartOf) cnt[c]++;
            var csum = new Vector3[m.charts]; for (int t = 0; t < T; t++) csum[chartOf[t]] += fn[t] * fa[t];
            float cos75 = Mathf.Cos(75f * Mathf.Deg2Rad);
            for (int pass = 0; pass < 3; pass++)
                for (int t = 0; t < T; t++)
                {
                    int c = chartOf[t]; if (cnt[c] >= 5) continue;
                    int best = -1; float bd = cos75;
                    for (int k = 0; k < 3; k++)
                    {
                        int p0 = tri[3 * t + k], p1 = tri[3 * t + (k + 1) % 3];
                        long key = p0 < p1 ? ((long)p0 << 32) | (uint)p1 : ((long)p1 << 32) | (uint)p0;
                        foreach (int g in ef[key])
                        {
                            int cg = chartOf[g]; if (cg == c || cnt[cg] < cnt[c]) continue;
                            float d = Vector3.Dot(fn[t], csum[cg].normalized);
                            if (d > bd) { bd = d; best = cg; }
                        }
                    }
                    if (best < 0) continue;
                    cnt[c]--; csum[c] -= fn[t] * fa[t]; chartOf[t] = best; cnt[best]++; csum[best] += fn[t] * fa[t];
                }
            // 빈 조각 번호를 당겨 채운다
            var renum = new int[m.charts]; for (int c = 0; c < renum.Length; c++) renum[c] = -1;
            int nc = 0; var newN = new List<Vector3>();
            for (int t = 0; t < T; t++) { int c = chartOf[t]; if (renum[c] < 0) { renum[c] = nc++; newN.Add(csum[c].sqrMagnitude > 1e-20f ? csum[c].normalized : chartN[c]); } chartOf[t] = renum[c]; }
            m.charts = nc; chartN = newN;
        }
        // 조각마다 평면 축 (조각 평균 법선에 수직)
        var ax = new Vector3[m.charts]; var ay = new Vector3[m.charts];
        for (int c = 0; c < m.charts; c++)
        {
            var n = chartN[c];
            var x = Vector3.Cross(Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right, n).normalized;
            ax[c] = x; ay[c] = Vector3.Cross(n, x);
        }
        // 꼭짓점 = (점, 조각)
        var wk = new Dictionary<long, int>();
        for (int t = 0; t < T; t++)
        {
            int c = chartOf[t];
            for (int k = 0; k < 3; k++)
            {
                int p = tri[3 * t + k];
                long key = ((long)p << 20) | (uint)c;
                if (!wk.TryGetValue(key, out int vi))
                {
                    // 이 조각 쪽 기준 법선: 이 점에서 이 조각에 든 면들의 평균
                    Vector3 refN = Vector3.zero;
                    foreach (int g in pf[p]) if (chartOf[g] == c) refN += fn[g] * fa[g];
                    refN = refN.sqrMagnitude > 1e-20f ? refN.normalized : chartN[c];
                    Vector3 s = Vector3.zero;
                    foreach (int g in pf[p])
                    {
                        if (Vector3.Dot(fn[g], refN) < 0.5f) continue;
                        int kk = tri[3 * g] == p ? 0 : tri[3 * g + 1] == p ? 1 : 2;
                        Vector3 a = pts[tri[3 * g + kk]], b = pts[tri[3 * g + (kk + 1) % 3]], cc = pts[tri[3 * g + (kk + 2) % 3]];
                        s += fn[g] * Vector3.Angle(b - a, cc - a);
                    }
                    var nrmv = s.sqrMagnitude > 1e-12f ? s.normalized : refN;
                    var pt = pts[p];
                    vi = m.V(pt, nrmv, new Vector2(Vector3.Dot(pt, ax[c]), Vector3.Dot(pt, ay[c])), c);
                    wk[key] = vi;
                }
                m.tri.Add(vi);
            }
        }
        return m;
    }

    // 면 수가 limit 이하인 따로 떨어진 덩어리(점으로 이어진 것)를 뺀 삼각형 (점 번호)
    static int[] TtDropTinyShells(int[] tri, int np, int limit)
    {
        var par = new int[np]; for (int i = 0; i < np; i++) par[i] = i;
        System.Func<int, int> Find = x => { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; };
        for (int t = 0; t < tri.Length; t += 3) { int a = Find(tri[t]); par[Find(tri[t + 1])] = a; par[Find(tri[t + 2])] = Find(a); }
        var cnt = new Dictionary<int, int>();
        for (int t = 0; t < tri.Length; t += 3) { int r = Find(tri[t]); cnt.TryGetValue(r, out int n); cnt[r] = n + 1; }
        var keep = new List<int>(tri.Length);
        for (int t = 0; t < tri.Length; t += 3) if (cnt[Find(tri[t])] > limit) { keep.Add(tri[t]); keep.Add(tri[t + 1]); keep.Add(tri[t + 2]); }
        return keep.ToArray();
    }
}
