using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 102동(Apt102) 소품을 Tripo 모델 품질 4단계(LOD)로 (2026-11-26, 사용자 "통과, 102동에 넣기"). 계획: Docs/맵모델링_미리보기/19_102동_소품/계획.md
// 원칙은 도시와 같다: 코드가 난수를 다 뽑아 예전처럼 만든 뒤 겉모습만 바꾼다. 콜라이더·이름·자리는 그대로 (소파·옷장에만 새 콜라이더, 사용자 결정).
// 세 갈래로 들어간다:
//  - 공용 도우미의 훅 (도시와 같은 줄에 kTripoApt를 더했다): 가로등(City.cs LampPost)·가로수(KoreaStreet.cs StreetTree)·실외기(KoreaShops.cs KAcUnit)·
//    세단(Streets.cs WreckedCarV2 → TripoCar. 승합차는 코드 그대로)
//  - 방 안 소품: 방을 짓는 코드가 놓는다 (GameSceneBuilder.TripoAptRooms.cs)
//  - 나머지는 아래 TripoAptPass가 다 지은 뒤 이름으로 훑어 바꾼다
// 102동 생성기는 재질을 읽기만 한다 (지킴 검사가 바뀐 재질 파일을 되돌리고 점검이 떨어진다): 새 모델 6종의 재질은 아래 메뉴로 미리 만들어 둔다
public static partial class GameSceneBuilder
{
    // 102동 씬을 만드는 동안에만 true (BuildApt102Scene을 부르는 쪽이 try/finally로 켜고 끈다). kTripoCity와는 따로다:
    // 도시 전용 훅(차를 모두 세단으로, 드럼통·선반·정류장 등)이 102동에서 함께 켜지지 않게 한다
    static bool kTripoApt;

    // 새 모델 6종 (11-26 웹 스튜디오 H3.5 프리뷰). 여섯 모두 앞이 +Z: 중계기함 열린 앞면, 경비실 문, 미끄럼판 내려오는 쪽, 소파 앉는 쪽, 옷장 문
    static readonly string[] TripoAptNewIds =
    {
        "apt_relay_cabinet_01", "apt_guard_booth_01", "apt_slide_01", "apt_sofa_01", "apt_wardrobe_01", "apt_low_table_01",
    };

    // 102동 생성이 쓸 수 있는 모델·재질 id (TripoSpecs의 키. 색만 다른 재질 _burnt·_blue도 따로 적는다). 생성 전에 품질별 판과 재질이 다 있는지 본다 (TripoAptReady).
    // 방 안 소품(TripoAptRooms.cs)이 쓰는 것도 여기 적는다. 가로수는 지금 둘 다 선 나무지만 자리가 바뀌면 밑동이 나올 수 있어 함께 본다
    static readonly string[] TripoAptIds =
    {
        "wrecked_sedan_01", "wrecked_sedan_01_burnt", "lamp_post_01", "dead_tree_01", "city_stump_01", "ac_unit_01",
        "city_rubble_brick_01", "city_rubble_concrete_01", "city_rubble_slab_01", "city_rubble_wall_01",
        "city_crate_01", "city_slab_01", "city_water_tank_01", "city_water_tank_01_blue",
        "city_mattress_01", "shelter_table_01", "shelter_chair_01",
        "apt_relay_cabinet_01", "apt_guard_booth_01", "apt_slide_01", "apt_sofa_01", "apt_wardrobe_01", "apt_low_table_01",
    };

    // 경비실 모델을 위 축으로 돌려 놓는 각도 (0·90·180·270). 모델은 문과 창이 +Z, 창 하나가 +X이고, 코드 경비실은 창이 +Z(정문 쪽)·+X(진입로 쪽), 문이 -X다.
    // 문을 코드 문 쪽(-X)에 맞추려면 270인데, 모델(가로 1.0 × 깊이 0.897)과 코드(2.8 × 2.4)의 긴 쪽이 엇갈려 30 % 찌그러지고 진입로 쪽이 창 없는 뒷벽이 된다.
    // 그래서 0으로 둔다: 창 둘의 자리가 코드와 같고 문은 정문 쪽 면에 온다 (코드의 문은 얇은 판 하나라 판정·조사 글과 상관없다)
    const float TaBoothYaw = 0f;

    // ---------- 생성 전 점검 ----------

    // 102동 생성이 쓰는 모든 모델의 품질별 판 넷(프리팹 + 메시)과 재질 넷(M_TR_{id}_q0~3)이 있는가. 없는 것의 이름을 missing에 담는다.
    // 재질은 불러오지 않고 있는지만 본다 (점검하자고 재질 구십 개를 텍스처째 불러올 일이 아니다)
    static bool TripoAptReady(out string missing)
    {
        var lost = new List<string>();
        foreach (var id in TripoAptIds)
        {
            if (!TripoSpecs.TryGetValue(id, out var spec))
            {
                lost.Add(id + " (TripoSpecs에 없음)");
                continue;
            }
            string src = spec.src ?? id;
            for (int q = 0; q < 4; q++)
            {
                string prefabName = $"{src}_q{q}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TripoModelDir}/{src}/{LpTierDir[q]}/{prefabName}");
                var mf = prefab != null ? prefab.GetComponentInChildren<MeshFilter>() : null;
                if ((mf == null || mf.sharedMesh == null) && !lost.Contains(prefabName)) lost.Add(prefabName);
                string matName = $"M_TR_{id}_q{q}.mat";
                if (AssetDatabase.GetMainAssetTypeAtPath($"{MatDir}/{matName}") != typeof(Material)) lost.Add(matName);
            }
        }
        missing = string.Join(", ", lost);
        return lost.Count == 0;
    }

    // ---------- 새 모델 6종의 재질 ----------

    // 새 모델 6종의 품질별 재질 24개(M_TR_apt_*_q0~3)를 TripoSpecs 값으로 만들거나 고쳐 쓰고, 그 재질 파일만 저장한다 (프로젝트 전체 저장은 하지 않는다).
    // 102동 생성 전에 한 번, 그리고 TripoSpecs의 색·금속성·매끈함을 고친 뒤에 다시 돌린다
    [MenuItem("Tools/핵폭발 생존/Tripo 재질 만들기 (102동 새 모델)")]
    static void TripoAptMakeMaterials()
    {
        const string abort = "[GameSceneBuilder] Tripo 재질 만들기 (102동 새 모델) 중단: ";
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning(abort + "플레이 모드에서는 재질을 만들지 않습니다.");
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Debug.LogWarning(abort + "스크립트 컴파일이나 에셋 들여오기가 끝난 뒤에 다시 실행하세요.");
            return;
        }
        // 텍스처가 없으면 그림 없는 재질이 저장된다. 하나라도 없으면 아무것도 쓰지 않는다
        var lost = new List<string>();
        int fresh = 0;
        foreach (var id in TripoAptNewIds)
            for (int q = 0; q < 4; q++)
            {
                string tex = $"{TripoModelDir}/{id}/{LpTierDir[q]}/{id}_q{q}_diff";
                if (AssetDatabase.GetMainAssetTypeAtPath(tex + ".png") == null && AssetDatabase.GetMainAssetTypeAtPath(tex + ".jpg") == null) lost.Add($"{id}_q{q}_diff");
                if (AssetDatabase.GetMainAssetTypeAtPath($"{MatDir}/M_TR_{id}_q{q}.mat") == null) fresh++;
            }
        if (lost.Count > 0)
        {
            Debug.LogWarning(abort + "품질별 판의 텍스처가 없습니다 (메뉴 'Tripo 품질별 판 만들기 (102동 새 모델…)'을 먼저 하세요): " + string.Join(", ", lost));
            return;
        }
        // 재질 파일이 새로 생기면 URP가 다음 에디터 틱에 프로젝트 전체를 저장한다 (이 메뉴가 부르는 것이 아니다). 그때 메모리에서 틀어진 다른 재질(_EMISSION이 꺼진 간판 등)이
        // 함께 저장되지 않게, 도시 씬만 생성과 같은 정리를 먼저 한다 (파일은 건드리지 않는다. 표시를 지운 목록은 Library/CityGuard/preclean_apt_tripo_mats.txt)
        if (fresh > 0)
        {
            // 새 재질 파일이 생기면 URP가 다음 틱에 프로젝트 전체를 저장한다: 재질이 아닌 저장 안 된 에셋(다른 세션 것일 수 있다)도 함께 저장되므로 도시 생성기처럼 미리 알린다
            var dirtyOthers = KDirtyNonMaterialAssets();
            if (dirtyOthers.Count > 0)
                Debug.LogWarning($"[GameSceneBuilder] Tripo 재질 만들기 (102동 새 모델): 저장되지 않은 다른 에셋·설정 {dirtyOthers.Count}개 (새 재질 파일 때문에 유니티가 전체 저장을 하면 같이 저장될 수 있습니다): " + string.Join(", ", dirtyOthers));
            Debug.Log("[GameSceneBuilder] Tripo 재질 만들기 (102동 새 모델) 전 정리: " + KPrecleanMaterials("apt_tripo_mats"));
        }

        bool keepWas = kKeepMaterials;
        var made = new List<Material>();
        kKeepMaterials = false; // 있는 재질에도 값을 다시 쓴다
        try
        {
            foreach (var id in TripoAptNewIds)
                for (int q = 0; q < 4; q++)
                {
                    var m = TripoTierMat(id, q);
                    made.Add(m);
                    AssetDatabase.SaveAssetIfDirty(m);
                }
        }
        finally
        {
            kKeepMaterials = keepWas;
            // 이 메뉴가 만든 재질을 '이번에 만든 재질' 목록에 남기지 않는다: 남으면 다음 '씬만 생성'이 그 재질을 불러 쓰기에서 빼고 다시 쓴다
            foreach (var m in made)
            {
                kCreated.Remove(m);
                kWritten.Remove(m);
            }
        }
        if (fresh > 0)
            Debug.LogWarning($"[GameSceneBuilder] Tripo 재질 만들기 (102동 새 모델): 재질 파일 {fresh}개를 새로 만들었습니다. URP가 다음 에디터 틱에 프로젝트 전체를 저장합니다 (저장하지 않은 다른 에셋도 함께 저장됩니다)");
        Debug.Log($"[GameSceneBuilder] Tripo 재질 만들기 (102동 새 모델): 모델 {TripoAptNewIds.Length}종의 재질 {made.Count}개를 TripoSpecs 값으로 쓰고 그 파일만 저장했다 (새로 만든 것 {fresh}개)");
    }

    // ---------- 다 지은 뒤 이름으로 훑어 바꾸기 ----------

    // 마당·탑·방을 다 지은 뒤, 내비메시를 굽기 직전에 한 번 부른다 (BuildApt102Scene). 난수를 쓰지 않고 콜라이더·이름·자리를 건드리지 않는다:
    // 지도는 콜라이더로 굽으므로(PhysicsColliders) 길 찾기·아이템 자리·점검이 그대로다. 도시 1·2차(TripoCity2.cs)와 같은 규칙·같은 함수를 쓴다.
    //  - 잔해 더미(Rubble)의 덩어리 Chunk: 마당 열두 곳, 4층 층계참, 옥상 더미 위. 예전 재질과 납작함으로 벽돌·철근 콘크리트·판 조각·칠한 벽을 고른다
    //  - 6층 홀의 나무 상자 Crate, 잘린 면(CutFace)의 바닥판 토막 BrokenSlab·매달린 벽 HangingWall
    //  - 꼭대기 옥탑 옆 물탱크 PenthouseTank (합친 메시, 콜라이더 없음), 옥상 싸움터의 쓰러진 물탱크 Tank (파란 상자 → 파란 물탱크를 눕힌다)
    //  - 경비실 GuardBooth, 놀이터 미끄럼틀 Slide, 옥상 통신 중계기함 RelayCabinet의 몸통
    // 그대로 두는 것: 옥상 잔해 상자 RoofHeap·혀 Slab·경사로 R1(발밑·길막이 콜라이더와 겉이 맞아야 한다), 바리케이드, 모닥불 통, 조리대, 난간 Seg와 그 위 조각,
    //   그네, 탑의 채움·바닥·벽, 승합차
    static string TripoAptPass(Transform geo)
    {
        if (!kTripoApt) return "꺼짐";
        var cnt = new Dictionary<string, int>();
        System.Action<string> add = k => { cnt.TryGetValue(k, out int n); cnt[k] = n + 1; };

        // 묶음째 바꾸는 셋 (씬에 하나씩)
        foreach (var t in geo.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || t.parent == null) continue;
            if (t.name == "GuardBooth") { if (TaGuardBooth(t)) add("apt_guard_booth_01"); }
            else if (t.name == "Slide" && t.parent.name == "Playground") { if (TaSlide(t)) add("apt_slide_01"); }
            else if (t.name == "RelayCabinet") { if (TaRelayCabinet(t)) add("apt_relay_cabinet_01"); }
        }

        // 렌더러 하나씩
        foreach (var r in geo.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r == null || !r.enabled) continue;
            var t = r.transform; var mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || t.parent == null) continue;
            bool isBox = mf.sharedMesh.vertexCount == 24 && (t.lossyScale - Vector3.one).sqrMagnitude < 1e-4f; // Box(): 크기가 메시에 들어간 상자
            Vector3 size = mf.sharedMesh.bounds.size;
            string n = t.name, pn = t.parent.name;
            if (isBox && n == "Chunk" && pn == "Rubble")
            {
                string id = TcRubbleId(t, r.sharedMaterial, size);
                if (id != null && TcBoxSwap(t, r, id, size) != null) add(id);
            }
            else if (isBox && n == "Crate")
            {
                if (TcBoxSwap(t, r, "city_crate_01", size) != null) add("city_crate_01");
            }
            else if (isBox && (n == "BrokenSlab" || n == "HangingWall") && pn == "CutFace")
            {
                int pieces = TcSlabSwap(t, r, size);
                if (pieces > 0)
                {
                    add("city_slab_01(" + n + ")"); if (pieces > 1) add("city_slab_01(이어 놓은 장)");
                }
            }
            else if (!isBox && n == "PenthouseTank")
            {
                // 도시의 아파트 옥상 물탱크와 같은 것 (AptTwFacade가 싸움터의 Tank와 헷갈리지 않게 이름만 바꿨다). 예전 재질이 줄무늬면 노랑, 파랑이면 파랑
                bool blue = r.sharedMaterial == M["paintBlue"];
                if (TcTankSwap(t, r, blue) != null) add(blue ? "city_water_tank_01(옥탑, 파랑)" : "city_water_tank_01(옥탑)");
            }
            else if (isBox && n == "Tank" && pn == "Roof" && t.GetComponent<BoxCollider>() != null)
            {
                if (TaArenaTank(t, r, size)) add("city_water_tank_01(싸움터, 파랑, 눕힘)");
            }
        }
        var parts = new List<string>();
        foreach (var kv in cnt) parts.Add(kv.Key + " " + kv.Value);
        return string.Join(", ", parts);
    }

    // geo 아래 Tripo 모델 받침(Tripo_{id})을 모델별로 센다: 훅(실외기·세단·가로등·가로수)과 방 안 소품까지 한 줄로 확인한다
    static string TripoAptTally(Transform geo)
    {
        var cnt = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
        int total = 0;
        foreach (var t in geo.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Tripo_", System.StringComparison.Ordinal)) continue;
            string id = t.name.Substring(6);
            cnt.TryGetValue(id, out int n); cnt[id] = n + 1;
            total++;
        }
        var parts = new List<string>();
        foreach (var kv in cnt) parts.Add(kv.Key + " " + kv.Value);
        return $"모두 {total}개: " + string.Join(", ", parts);
    }

    // 품질 4단계 모델을 frame 기준 상자 box에 축마다 맞춰 놓는다 (TcBoxFit). yaw가 90·270이면 모델의 가로·깊이가 상자의 깊이·가로에 놓이므로 맞출 크기를 바꿔 넘긴다
    static Transform TaBoxFitYaw(Transform frame, string id, Bounds box, float yaw)
    {
        bool quarter = Mathf.Abs(Mathf.Repeat(yaw, 180f) - 90f) < 1f;
        var size = quarter ? new Vector3(box.size.z, box.size.y, box.size.x) : box.size;
        return TcBoxFit(frame, id, null, new Bounds(box.center, size), yaw, Vector3.one);
    }

    // 경비실: 몸통·지붕·창 둘·문 판 다섯 겉모습을 경비실 모델 하나로 (예전 전체 크기 2.8 × 2.76 × 2.4에 축마다 맞춘다).
    // 받침은 몸통(Body: 상자 콜라이더 + 조사 글)의 자식으로 둔다: 조사할 때 윤곽선이 그 아래 메시에 붙고, 타격 효과도 콜라이더 아래 렌더러를 본다
    static bool TaGuardBooth(Transform booth)
    {
        var body = booth.Find("Body");
        if (body == null) return false;
        var old = new List<Renderer>();
        foreach (Transform c in booth)
            if (c.name == "Body" || c.name == "Roof" || c.name == "Window" || c.name == "Door") old.AddRange(c.GetComponents<MeshRenderer>());
        if (old.Count == 0) return false;
        var ob = TripoLocalBounds(body, old);
        var holder = TaBoxFitYaw(body, "apt_guard_booth_01", ob, TaBoothYaw);
        if (holder == null) return false;
        // 모델은 처마가 앞쪽만 짧아, 전체 상자에 맞추면 문·창 쪽 벽이 몸통 콜라이더 앞면보다 0.2 m 안쪽에 선다 (11-26 검토).
        // 깊이를 1.1배 늘리고 0.09 m 앞으로 밀어 앞벽·뒷벽이 콜라이더 면(±1.0)에 닿게 한다 (콜라이더는 그대로)
        var boothModel = holder.Find("Model");
        if (boothModel != null && Mathf.Abs(TaBoothYaw) < 1f)
        {
            var bs = boothModel.localScale; bs.z *= 1.1f; boothModel.localScale = bs;
            boothModel.localPosition += new Vector3(0f, 0f, 0.09f);
            var blg = boothModel.GetComponent<LODGroup>(); if (blg != null) blg.RecalculateBounds();
        }
        TcStrip(old);
        return true;
    }

    // 미끄럼틀: 발판·다리 넷·미끄럼판·사다리를 미끄럼틀 모델 하나로. 축마다 맞추면 가는 난간이 찌그러지므로 길이 하나로만 맞춘다 (한 배율).
    // 모델은 사다리가 -Z, 미끄럼판이 +Z로 내려가 코드와 같은 방향이다. 바닥은 땅에, 가운데는 예전 전체 상자의 가운데에. 콜라이더(발판·미끄럼판·사다리)는 그대로
    static bool TaSlide(Transform slide)
    {
        var old = new List<Renderer>(slide.GetComponentsInChildren<Renderer>());
        return TripoSwapRenderers(slide, "apt_slide_01", old, TripoFit.Length, TripoAlign.Bottom, 0f, null, Vector3.zero) != null;
    }

    // 옥상 통신 중계기함: 몸통(Body)과 윗판(Cap)만 모델로 바꾼다 (방공호 창고와 같은 방식). 문짝 묶음 Door(Leaf·Handle·Plate)는 LootCache가 돌려 여는 코드 물체 그대로,
    // 몸통의 콜라이더·LootCache, 안테나 불빛(StrobeBulb)도 그대로다. 모델의 열린 앞면이 +Z = 문짝 쪽.
    // 맞추는 상자는 몸통 상자(0.9 × 1.5 × 0.5)를 윗판 윗면(1.54)까지 올린 것: 윗판의 처마(앞 4 cm)까지 넣으면 모델 앞면이 문짝과 같은 면에 겹친다.
    // 받침은 몸통의 자식 (윤곽선·타격 효과가 몸통 아래 렌더러를 본다)
    static bool TaRelayCabinet(Transform cab)
    {
        var body = cab.Find("Body"); var cap = cab.Find("Cap");
        var br = body != null ? body.GetComponent<MeshRenderer>() : null;
        if (br == null) return false;
        var cr = cap != null ? cap.GetComponent<MeshRenderer>() : null;
        var ob = TripoLocalBounds(body, new Renderer[] { br });
        if (cr != null)
        {
            var cb = TripoLocalBounds(body, new Renderer[] { cr });
            ob.SetMinMax(ob.min, new Vector3(ob.max.x, Mathf.Max(ob.max.y, cb.max.y), ob.max.z));
        }
        // 깊이 1.13배: 상자에 꼭 맞추면 몸통의 열린 앞면이 문짝보다 4 cm 뒤에 서서 옆에서 틈이 보인다 (받침돌·지붕 앞면이 몸통보다 나와 있는 모델, 11-26 검토)
        if (TcBoxFit(body, "apt_relay_cabinet_01", null, ob, 0f, new Vector3(1f, 1f, 1.13f)) == null) return false;
        TcStrip(new List<Renderer> { br, cr });
        return true;
    }

    // 옥상 싸움터의 쓰러진 물탱크: 파란 상자(1.5 × 1.6 × 2.4, 상자 콜라이더가 양옆 길 폭을 정한다)의 겉만 파란 물탱크 모델을 눕힌 것으로. 콜라이더는 그대로.
    // 탱크 축(모델 Y)을 상자의 긴 수평 쪽에 놓고 길이는 상자 길이에 맞춘다. 몸통은 둥글게 (지름 = 상자의 폭·높이 가운데 작은 쪽) 바닥에 얹는다:
    // 상자를 꽉 채우면 눌린 타원이 된다. 모델 밑의 배수 꼭지(+Z)는 아래로 돌려 바닥에 묻는다 (옆으로 두면 콜라이더 밖 좁은 길로 삐져나온다)
    static bool TaArenaTank(Transform t, MeshRenderer r, Vector3 size)
    {
        var fit = new GameObject("TankFit").transform;
        fit.SetParent(t, false);
        fit.localRotation = Quaternion.LookRotation(Vector3.down, size.z >= size.x ? Vector3.forward : Vector3.right);
        var lb = TripoLocalBounds(fit, new Renderer[] { r }); // 받침 기준 예전 상자: x 폭, y 길이(탱크 축), z 높이 (+z가 아래)
        float d = Mathf.Min(lb.size.x, lb.size.z);
        var box = new Bounds(new Vector3(lb.center.x, lb.center.y, lb.max.z - d * 0.5f), new Vector3(d, lb.size.y, d));
        var holder = TcBoxFit(fit, "city_water_tank_01", "city_water_tank_01_blue", box, 0f, Vector3.one);
        if (holder == null)
        {
            Object.DestroyImmediate(fit.gameObject);
            return false;
        }
        TcTankRound(holder, new Bounds(Vector3.zero, box.size)); // 몸통을 둥글게 하고 등(-z = 위)을 맞춘 상자의 윗면에: 몸통 밑이 바닥에 닿고 꼭지만 바닥 밑으로 나간다
        TcStrip(new List<Renderer> { r });
        return true;
    }
}
