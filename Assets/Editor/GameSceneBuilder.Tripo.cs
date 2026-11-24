using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// GameSceneBuilder의 Tripo 소품: Tripo AI로 만든 FBX 모델(Assets/ThirdParty/Tripo/Models/{id})의 재질과 겉모습 바꾸기 도우미.
// 도시 넣기 계획(Docs/맵모델링_미리보기/도시_넣기_계획.md) 0단계. 아직 씬을 만드는 코드 어디서도 부르지 않는다.
// 원칙: 지금 코드로 소품을 똑같이 만든 뒤(도시 난수를 뽑는 횟수가 그대로라 배치가 안 바뀐다) 겉모습만 Tripo 모델로 바꾼다 (TripoSwap).
//       콜라이더는 지우지 않는다 (길 찾기 지도가 콜라이더로 구워진다). 필요한 소품만 부르는 쪽에서 따로 바꾼다.
public static partial class GameSceneBuilder
{
    const string TripoModelDir = "Assets/ThirdParty/Tripo/Models";

    // 사용자가 미리보기 사진(Docs/맵모델링_미리보기)을 보고 통과시킨 값. 각 모델 폴더 SOURCE.md의 '정한 재질 값'과 같다.
    // yaw: 원본 FBX를 코드 소품과 같은 쪽(앞 = +Z)으로 돌리는 각도
    struct TripoSpec
    {
        public Color tint;
        public float metallic, smoothness, yaw;
        public string src; // 모델·텍스처 폴더 id (비면 이 항목의 id). 같은 모델에 다른 재질을 쓸 때 (불탄 차)
        public float roll; // 앞뒤 축(Z)으로 더 돌리는 각도: 모델 자체가 뒤집힌 자세인데 대상(뒤집힌 차 몸체)도 뒤집혀 있을 때 180
        public TripoSpec(Color tint, float metallic, float smoothness, float yaw, string src = null, float roll = 0f) { this.tint = tint; this.metallic = metallic; this.smoothness = smoothness; this.yaw = yaw; this.src = src; this.roll = roll; }
    }

    static readonly Dictionary<string, TripoSpec> TripoSpecs = new Dictionary<string, TripoSpec>
    {
        { "wrecked_sedan_01", new TripoSpec(new Color(0.4f, 0.4f, 0.4f), 0.3f, 0.3f, 90f) },     // 11-22 통과 (01_부서진차, 어둡게 0.4)
        { "steel_drum_01",    new TripoSpec(new Color(0.6f, 0.6f, 0.6f), 0.3f, 0.3f, 0f) },      // 11-22 통과 (02, 어둡게)
        { "ac_unit_01",       new TripoSpec(new Color(0.45f, 0.45f, 0.45f), 0.2f, 0.25f, 90f) }, // 11-22 통과 (02, 어둡게). 팬이 +Z, 뒷면을 벽에
        { "fire_hydrant_01",  new TripoSpec(new Color(0.55f, 0.17f, 0.14f), 0.3f, 0.3f, 90f) },  // 11-22 통과 (02, 빨강2). 토출구가 좌우(X)
        { "bus_stop_01",      new TripoSpec(new Color(0.35f, 0.35f, 0.35f), 0.4f, 0.35f, 180f) },// 11-22 통과 (03, 어둡게). 벤치 쪽이 +Z
        { "dead_tree_01",     new TripoSpec(new Color(0.3f, 0.3f, 0.3f), 0f, 0.1f, 0f) },        // 11-22 통과 0.5 → 11-23 도시에서 가까이 손전등을 받으면 밝은 회색이라 0.3 (사용자 "둘 다 손보기")
        // 불탄 차(도시 차의 약 18 %): 같은 차 모델을 그을린 듯 아주 어둡게. 11-22 2단계에서 처음 넣음 (사진으로 확인 전)
        { "wrecked_sedan_01_burnt", new TripoSpec(new Color(0.13f, 0.12f, 0.11f), 0.1f, 0.12f, 90f, "wrecked_sedan_01") },
        // 뒤집힌 차 전용 (11-22, 바닥 부품이 보이는 판. 모델이 원래 뒤집힌 자세라 뒤집힌 몸체 안에서 한 번 더 뒤집는다). 재질 값은 wrecked_sedan_01과 같게
        { "wrecked_sedan_flipped_01", new TripoSpec(new Color(0.4f, 0.4f, 0.4f), 0.3f, 0.3f, 0f, null, 180f) },
        { "wrecked_sedan_flipped_01_burnt", new TripoSpec(new Color(0.13f, 0.12f, 0.11f), 0.1f, 0.12f, 0f, "wrecked_sedan_flipped_01", 180f) },
        // 11-23 품질 4단계와 함께 더함 (재질 값은 도시 사진으로 확인 전). 가로등은 직접 짠 판이 이미 앞 = +Z, 트럭은 원본 FBX를 90° 돌린다
        { "lamp_post_01",     new TripoSpec(new Color(0.55f, 0.55f, 0.55f), 0.35f, 0.3f, 0f) },
        { "pickup_truck_01",  new TripoSpec(new Color(0.5f, 0.5f, 0.5f), 0.3f, 0.3f, 90f) },
        // 방공호 가구 6종 (11-23 웹 스튜디오, GameSceneBuilder.TripoShelter.cs). yaw는 품질별 판을 만들 때 쓰는 값(TtYaw)과 같게 (판은 이미 코드 가구와 같은 축)
        { "shelter_bed_01",       new TripoSpec(new Color(0.8f, 0.8f, 0.8f), 0.3f, 0.25f, 180f) },
        { "shelter_cabinet_01",   new TripoSpec(new Color(0.8f, 0.8f, 0.8f), 0.3f, 0.25f, 0f) },
        { "shelter_shelf_01",     new TripoSpec(new Color(0.8f, 0.8f, 0.8f), 0.3f, 0.25f, 0f) },
        { "shelter_table_01",     new TripoSpec(new Color(0.8f, 0.8f, 0.8f), 0f, 0.15f, 0f) },
        { "shelter_chair_01",     new TripoSpec(new Color(0.8f, 0.8f, 0.8f), 0f, 0.15f, 0f) },
        { "shelter_workbench_01", new TripoSpec(new Color(0.8f, 0.8f, 0.8f), 0.4f, 0.3f, 180f) },
        // 방공호 2차 (11-23, 화구·계기함 쪽이 코드와 같게 180°). 재질 값은 사진으로 확인 전
        { "shelter_furnace_01",   new TripoSpec(new Color(0.8f, 0.8f, 0.8f), 0.1f, 0.2f, 180f) },
        { "shelter_generator_01", new TripoSpec(new Color(0.8f, 0.8f, 0.8f), 0.4f, 0.3f, 180f) },
        // 도시 남은 소품 1차 (11-24 웹 스튜디오, GameSceneBuilder.TripoCity2.cs). 색은 예전 코드 재질의 밝기와 손전등 사진으로 어둡게 (11-24, 재질 파일에 직접 넣었다)
        { "city_rubble_concrete_01", new TripoSpec(new Color(0.6f, 0.6f, 0.6f), 0f, 0.15f, 0f) },
        { "city_rubble_slab_01",     new TripoSpec(new Color(0.7f, 0.7f, 0.7f), 0f, 0.15f, 0f) },
        { "city_rubble_wall_01",     new TripoSpec(new Color(0.55f, 0.55f, 0.55f), 0f, 0.15f, 0f) },
        { "city_rubble_brick_01",    new TripoSpec(new Color(0.4f, 0.4f, 0.4f), 0f, 0.15f, 0f) },
        { "city_rubble_asphalt_01",  new TripoSpec(new Color(0.45f, 0.45f, 0.45f), 0f, 0.15f, 0f) },
        { "city_crate_01",           new TripoSpec(new Color(0.55f, 0.55f, 0.55f), 0f, 0.15f, 0f) },
        { "city_barrier_01",         new TripoSpec(new Color(0.5f, 0.5f, 0.5f), 0f, 0.15f, 90f) }, // 원본은 긴 쪽이 Z: 코드 방호벽(긴 쪽 X)에 맞춰 90°
        { "city_sandbag_01",         new TripoSpec(new Color(0.4f, 0.4f, 0.4f), 0f, 0.1f, 0f) },
    };

    // 도시 씬을 만드는 동안만 켠다: 같은 함수(소화전·정류장·불 드럼통 등)를 메인 메뉴·컷씬도 쓰지만, 사용자가 정할 때까지 그 씬들은 예전 모습 그대로 둔다
    static bool kTripoCity;

    // 도시 씬 만들기 (게임 씬 전체 생성·도시 씬만 생성 둘 다 이것을 부른다). 본체는 GameSceneBuilder.City.cs의 BuildCitySceneCore
    static void BuildCityScene(GameObject playerPrefab, GameObject zombiePrefab, GameObject dogPrefab)
    {
        kTripoCity = true;
        try { BuildCitySceneCore(playerPrefab, zombiePrefab, dogPrefab); }
        finally { kTripoCity = false; }
    }

    // 원기둥으로 만든 드럼통(이름 Drum·Barrel 등, 늘인 원기둥 + 캡슐 콜라이더)을 도시에서는 Tripo 드럼통으로. 누운 통은 누운 채로 맞춘다
    static GameObject TripoDrum(GameObject prim)
    {
        if (kTripoCity && prim != null) TripoSwap(prim.transform, "steel_drum_01", null, TripoFit.Height, TripoAlign.Bottom);
        return prim;
    }

    // 모델 크기를 예전 소품의 어느 치수에 맞출지 (예전 겉모습의 대상 기준 축: x 오른쪽, y 위, z 앞)
    enum TripoFit { Height, Width, Length, Box }
    // 맞춘 뒤 어디를 붙일지: Bottom = 바닥 높이와 가운데(x, z), Back = 뒷면(-z 쪽 면)과 가운데(x, y). 벽에 붙는 실외기는 Back.
    // Top = 윗면과 가운데(x, z): 뒤집힌 차처럼 대상이 거꾸로 놓여 대상 기준 '위'가 땅에 닿는 것
    // None = 옮기지 않는다 (가로등처럼 품질별 판을 코드 소품과 같은 치수·원점으로 짠 것)
    // BottomTrunk = 바닥 높이를 맞추고, 모델 밑동(가장 낮은 꼭짓점들의 가운데)을 대상 원점 위에 (나무: 예전 상자 가운데는 가지 쪽으로 쏠려 줄기가 격자·콜라이더에서 최대 1 m 비껴 섰다, 11-23 검수)
    // BackBottom = Back처럼 뒷면을 맞추고 바닥도 맞춘다 (실외기: 다리 끝이 예전 몸통 바닥 = 브래킷 팔 위에 앉게)
    enum TripoAlign { Bottom, Back, Top, None, BottomTrunk, BackBottom }

    // 부서진 차의 겉모습을 Tripo 차로 (WreckedCarV2 끝에서 부른다). 대상은 차 몸체(Body): 뒤집힌 차는 몸체째 뒤집혀 있으므로 지붕(윗면)을 맞춘다.
    // 바닥의 그을음 자국(불탄 차)은 몸체 밖이라 남는다. 콜라이더는 여기서는 그대로 두고, 길 찾기 지도를 굽기 직전에 TripoCarColliders가 새 모양으로 바꾼다
    // (그 전에는 길가 잔해·아이템 자리가 콜라이더를 보고 자리를 고르므로, 바꾸면 배치가 달라진다)
    static void TripoCar(Transform body, bool flipped, bool burnt)
    {
        if (!kTripoCity || body == null) return;
        // 뒤집힌 차는 바닥 부품이 보이는 전용 모델 (원본 FBX 길이 0.996 → 3.49배 = 똑바로 선 차와 같은 길이 3.48 m)
        string id = flipped ? "wrecked_sedan_flipped_01" : "wrecked_sedan_01";
        var h = TripoSwap(body, id, null, TripoFit.Length, flipped ? TripoAlign.Top : TripoAlign.Bottom, flipped ? 3.49f : 3.5f, burnt ? id + "_burnt" : null);
        if (h == null || h.transform.childCount == 0) return;
        // 가운데는 몸체 원점(차 가운데)에 맞춘다: 열린 문·받침목·거울이 예전 겉모습 상자를 한쪽으로 늘려 놓아 상자 가운데가 차 가운데가 아니다
        var model = h.transform.GetChild(0);
        var mb = TripoLocalBounds(h.transform, model.GetComponentsInChildren<MeshRenderer>());
        model.localPosition -= new Vector3(mb.center.x, 0f, mb.center.z);
    }

    // 길 찾기 지도를 굽기 직전: Tripo 차를 든 차마다 예전 부품 콜라이더(트리거가 아닌 것)를 지우고, 모델에 맞춘 상자 콜라이더 하나를 받침에 단다.
    // 똑바로 선 차에는 바닥 부품과 기름 자국도 여기서 붙인다 (11-22 사용자 승인 "선 차 부품도 넣고 생성". 배치가 다 끝난 뒤라 자리에 영향이 없다)
    // 예전 콜라이더는 코드 세단(길이 3.95~4.7 m)에 맞춰져 있어 Tripo 차(3.48 m)보다 0.5 m쯤 길다 (보이지 않는 벽이 된다)
    static int TripoCarColliders(Transform root)
    {
        int n = 0;
        foreach (var holder in root.GetComponentsInChildren<Transform>(true))
        {
            if (!holder.name.StartsWith("Tripo_wrecked_sedan_")) continue;
            var car = holder.parent != null ? holder.parent.parent : null; // 받침 → Body → WreckedCar
            if (car == null || !car.name.StartsWith("WreckedCar")) continue;
            foreach (var c in car.GetComponentsInChildren<Collider>(true))
                if (!c.isTrigger) Object.DestroyImmediate(c);
            var rs = holder.GetComponentsInChildren<MeshRenderer>();
            if (rs.Length == 0) continue;
            var b = TripoLocalBounds(holder, rs);
            var box = holder.gameObject.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size;
            // 똑바로 선 차: 바닥 부품과 바닥 기름 자국 (뒤집힌 차는 전용 모델에 부품이 이미 있다). 콜라이더를 만든 뒤에 붙여 상자 크기에 섞이지 않게 한다
            if (holder.name == "Tripo_wrecked_sedan_01")
                TripoCarUnderside(car, holder, rs[0].sharedMaterial != null && rs[0].sharedMaterial.name.Contains("burnt"));
            n++;
        }
        return n;
    }

    // ---------- 차 바닥 부품과 바닥 기름 자국 (11-22 사용자 의견: 차 바닥에 부품 묘사가 없고, 타이어가 붕 뜬 느낌) ----------
    // Tripo 차는 바퀴가 땅에 닿아 있지만(측정 틈 0 cm) 바닥판이 땅에서 0.31~0.5 m 위에 있고 그 아래가 텅 비어 뒤가 훤히 보이며,
    // 뒤집힌 차는 바닥이 민판이다. 그래서 바닥판 아래에 프레임·차축·구동축·엔진 밑·배기관·연료통·판스프링을 붙이고(뒤집힌 차는 이것이 위로 보인다),
    // 똑바로 선 차 밑 땅에는 어두운 기름 자국을 깔아 바퀴가 땅에 앉아 보이게 한다.
    // 치수는 Tripo 차 몸체 기준(배율 3.5): 폭 ±0.89, 길이 ±1.74, 바퀴 x ±0.67·z +1.07/-1.17, 바닥판 높이 약 0.31 (11-22 측정).
    // 모든 차가 같은 메시 두 개(녹슨 강철 프레임, 녹슨 배기)를 함께 쓴다. 난수를 쓰지 않는다

    static Material TripoM(string key, string file)
    {
        Material m;
        if (M.TryGetValue(key, out m) && m != null) return m;
        return AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + file + ".mat");
    }

    // 상자 조각 (내장 큐브를 늘린다: 메시 묶음에 상자 메시를 따로 넣지 않아 미리보기에서도 쓸 수 있다)
    static void TBox(List<CombineInstance> parts, Vector3 size, Vector3 pos)
        => parts.Add(new CombineInstance { mesh = KBuiltin(PrimitiveType.Cube), transform = Matrix4x4.TRS(pos, Quaternion.identity, size) });

    // rust = false: 프레임·바닥판·차축·엔진 밑·연료통·판스프링, true: 배기관·소음기
    static Mesh TripoUnderMesh(bool rust)
    {
        string key = rust ? "tripo_car_under_rust" : "tripo_car_under";
        Mesh cached;
        if (meshCache != null && meshCache.TryGetValue(key, out cached)) return cached;
        var parts = new List<CombineInstance>();
        var along = Quaternion.Euler(90, 0, 0); // 원기둥 축을 앞뒤(z)로
        if (!rust)
        {
            TBox(parts, new Vector3(1.1f, 0.025f, 2.7f), new Vector3(0, 0.295f, -0.05f)); // 바닥판 (빈 곳을 막는다)
            foreach (float s in new[] { -1f, 1f })
            {
                TBox(parts, new Vector3(0.07f, 0.09f, 3.0f), new Vector3(s * 0.36f, 0.245f, -0.05f)); // 프레임
                TBox(parts, new Vector3(0.06f, 0.05f, 0.9f), new Vector3(s * 0.55f, 0.21f, -1.17f)); // 뒤 판스프링
                KRod(parts, new Vector3(s * 0.22f, 0.24f, 0.95f), new Vector3(s * 0.6f, 0.27f, 1.07f), 0.025f);            // 앞 서스펜션 팔
            }
            TBox(parts, new Vector3(0.9f, 0.08f, 0.08f), new Vector3(0, 0.24f, 0.72f));   // 앞 가로 부재
            TBox(parts, new Vector3(0.9f, 0.08f, 0.08f), new Vector3(0, 0.25f, -0.55f));  // 가운데 가로 부재
            KRod(parts, new Vector3(-0.6f, 0.27f, 1.07f), new Vector3(0.6f, 0.27f, 1.07f), 0.035f);                // 앞 차축
            KRod(parts, new Vector3(-0.6f, 0.27f, -1.17f), new Vector3(0.6f, 0.27f, -1.17f), 0.04f);               // 뒤 차축
            KCylAt(parts, new Vector3(0, 0.27f, -1.17f), 0.11f, 0.18f, along);                                      // 디퍼렌셜
            KRod(parts, new Vector3(0, 0.22f, 0.45f), new Vector3(0, 0.25f, -1.05f), 0.03f);                        // 구동축
            TBox(parts, new Vector3(0.42f, 0.12f, 0.5f), new Vector3(0, 0.24f, 0.98f));    // 엔진 밑(오일 팬)
            TBox(parts, new Vector3(0.25f, 0.13f, 0.42f), new Vector3(0, 0.235f, 0.52f));  // 변속기
            TBox(parts, new Vector3(0.7f, 0.14f, 0.26f), new Vector3(0, 0.3f, -1.47f));    // 연료통
        }
        else
        {
            KRod(parts, new Vector3(0.26f, 0.2f, 0.75f), new Vector3(0.26f, 0.2f, -1.05f), 0.028f);                 // 배기관
            KCylAt(parts, new Vector3(0.26f, 0.2f, -1.3f), 0.09f, 0.5f, along);                                     // 소음기
            KRod(parts, new Vector3(0.26f, 0.2f, -1.55f), new Vector3(0.32f, 0.22f, -1.8f), 0.028f);                // 꼬리관
        }
        var mesh = new Mesh { name = key };
        mesh.CombineMeshes(parts.ToArray(), true, true);
        mesh.RecalculateBounds();
        if (meshContainer != null && meshCache != null)
        {
            AssetDatabase.AddObjectToAsset(mesh, meshContainer);
            meshCache[key] = mesh;
        }
        return mesh;
    }

    // 차 하나에 바닥 부품과 기름 자국을 붙인다. car = WreckedCar, holder = 그 차의 Tripo 받침 (몸체를 따라 뒤집혀 있다). 콜라이더는 만들지 않는다
    static void TripoCarUnderside(Transform car, Transform holder, bool burnt)
    {
        foreach (var rust in new[] { false, true })
        {
            var go = new GameObject(rust ? "UnderRust" : "Under");
            go.transform.SetParent(holder, false);
            go.AddComponent<MeshFilter>().sharedMesh = TripoUnderMesh(rust);
            go.AddComponent<MeshRenderer>().sharedMaterial = burnt ? TripoM("soot", "M_Soot") : rust ? TripoM("metal", "M_RustyMetal") : TripoM("steelGrey", "M_SteelGrey"); // 처음엔 M_Tire(거의 검정)였는데 어두운 차 바닥과 구분되지 않았다
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }
        if (burnt) return; // 불탄 차는 이미 그을음 자국이 있다
        // 바닥 기름 자국: 차 밑에 길쭉한 어두운 얼룩 (차와 같은 방향). 씨앗은 자리에서 (난수를 쓰지 않는다)
        int seed = 950 + Mathf.Abs(Mathf.RoundToInt(car.position.x * 13f + car.position.z * 7f)) % 40;
        Mesh blob;
        if (meshCache != null && meshContainer != null) blob = BlobMesh(seed);
        else blob = Gore.Blob(seed); // 미리보기 (생성 밖): 묶음에 넣지 않는다
        var st = new GameObject("OilStain");
        st.transform.SetParent(car, false);
        st.transform.localPosition = new Vector3(0, 0.012f, -0.05f);
        st.transform.localRotation = Quaternion.identity;
        st.transform.localScale = new Vector3(0.95f, 1f, 1.75f);
        st.AddComponent<MeshFilter>().sharedMesh = blob;
        var sr = st.AddComponent<MeshRenderer>();
        sr.sharedMaterial = TripoM("scorch", "M_Scorch");
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(st, StaticEditorFlags.BatchingStatic);
    }

    // 모델마다 한 벌: URP Lit, 색 텍스처 + 통과한 기본색·금속성·매끈함. '도시 씬만 생성'에서는 있는 재질을 그대로 쓴다 (PolyMat과 같다)
    static Material TripoMat(string id)
    {
        var m = LoadOrCreate("M_TR_" + id, Shader.Find("Universal Render Pipeline/Lit"));
        if (KKeep(m)) return m;
        var spec = TripoSpecs[id];
        string src = spec.src ?? id;
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TripoModelDir}/{src}/{src}_diff.jpg");
        m.SetTexture("_BaseMap", albedo);
        m.SetTexture("_MainTex", albedo);
        m.SetColor("_BaseColor", spec.tint);
        m.SetColor("_Color", spec.tint);
        m.SetFloat("_Smoothness", spec.smoothness);
        m.SetFloat("_Metallic", spec.metallic);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- 품질별 판 LOD 묶음 (11-23, 사용자 "9종 도시에 넣기") ----------
    // LOD0 = 매우 높음(q3) · LOD1 = 높음(q2) · LOD2 = 중간(q1) · LOD3 = 낮음(q0). 세부 묘사 품질이 가까이서 쓸 수 있는 가장 좋은 판을 정한다
    // (플레이 중 QualityLod가 묶음마다 LOD 칸을 고쳐 쓴다). 판마다 자기 텍스처의 재질 M_TR_{matId}_q{n}.
    // 메시는 품질별 폴더의 .asset (q3는 원본 FBX 메시를 같은 좌표로 돌린 자식) — 씬에 메시를 새로 넣지 않는다
    static readonly float[] TripoLodHeights = { 0.25f, 0.1f, 0.035f, 0.004f }; // 화면 높이 비율: 이보다 작아지면 다음 판 (마지막은 사라짐)

    static GameObject TripoTierModel(Transform holder, string id, string matId, float roll)
    {
        string src = TripoSpecs[id].src ?? id;
        string dir = $"{TripoModelDir}/{src}";
        var meshes = new Mesh[4]; var rot = new Quaternion[4];
        for (int q = 0; q < 4; q++)
        {
            rot[q] = Quaternion.identity;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{dir}/{LpTierDir[q]}/{src}_q{q}.prefab");
            var mf = prefab != null ? prefab.GetComponentInChildren<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null)
            {
                Debug.LogWarning($"[GameSceneBuilder] Tripo 품질별 판이 없음: {dir}/{LpTierDir[q]}/{src}_q{q}.prefab — 원본 FBX 하나로 대신한다");
                return null;
            }
            meshes[q] = mf.sharedMesh;
            if (mf.transform != prefab.transform) rot[q] = mf.transform.localRotation; // q3 프리팹: FBX 메시를 돌린 자식
        }
        var model = new GameObject("Model");
        model.transform.SetParent(holder, false);
        model.transform.localRotation = Quaternion.Euler(0, 0, roll);
        var lods = new LOD[4];
        for (int l = 0; l < 4; l++)
        {
            int q = 3 - l;
            var c = new GameObject($"LOD{l}_q{q}");
            c.transform.SetParent(model.transform, false);
            c.transform.localRotation = rot[q];
            c.AddComponent<MeshFilter>().sharedMesh = meshes[q];
            var r = c.AddComponent<MeshRenderer>();
            r.sharedMaterial = TripoTierMat(matId, q);
            lods[l] = new LOD(TripoLodHeights[l], new Renderer[] { r });
        }
        var lg = model.AddComponent<LODGroup>();
        lg.fadeMode = LODFadeMode.None;
        lg.SetLODs(lods);
        lg.RecalculateBounds();
        // 품질별 판 고르기 (플레이 중 세부 묘사 품질에 맞춰 LOD 칸을 고쳐 쓴다, Scripts/City/QualityLod.cs). 씬에는 판 4개 그대로 저장
        var ql = model.AddComponent<QualityLod>();
        ql.tiers = new Renderer[4];
        for (int l = 0; l < 4; l++) ql.tiers[3 - l] = lods[l].renderers[0];
        ql.heights = (float[])TripoLodHeights.Clone();
        return model;
    }

    // 판마다 재질: 그 판의 텍스처 + 통과한 기본색·금속성·매끈함 (TripoSpecs[matId]). '도시 씬만 생성'에서는 있는 재질을 그대로 쓴다
    static Material TripoTierMat(string matId, int q)
    {
        var m = LoadOrCreate($"M_TR_{matId}_q{q}", Shader.Find("Universal Render Pipeline/Lit"));
        if (KKeep(m)) return m;
        var spec = TripoSpecs[matId];
        string src = spec.src ?? matId;
        string d = $"{TripoModelDir}/{src}/{LpTierDir[q]}/{src}_q{q}_diff";
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(d + ".png") ?? AssetDatabase.LoadAssetAtPath<Texture2D>(d + ".jpg");
        m.SetTexture("_BaseMap", albedo);
        m.SetTexture("_MainTex", albedo);
        m.SetColor("_BaseColor", spec.tint);
        m.SetColor("_Color", spec.tint);
        m.SetFloat("_Smoothness", spec.smoothness);
        m.SetFloat("_Metallic", spec.metallic);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    // 들여오기 설정: 색 텍스처는 소품 규칙대로 512 (Docs/텍스처_규칙.md, '낮음'은 전체 설정으로 반), 모델은 재질·애니메이션을 가져오지 않는다.
    // 이미 그 값이면 아무것도 다시 들여오지 않는다
    static void TripoConfigureImport(string id)
    {
        var ti = AssetImporter.GetAtPath($"{TripoModelDir}/{id}/{id}_diff.jpg") as TextureImporter;
        if (ti != null && (ti.maxTextureSize != 512 || !ti.sRGBTexture || !ti.mipmapEnabled || ti.textureCompression != TextureImporterCompression.Compressed))
        {
            ti.maxTextureSize = 512; ti.sRGBTexture = true; ti.mipmapEnabled = true; ti.textureCompression = TextureImporterCompression.Compressed;
            ti.SaveAndReimport();
        }
        var mi = AssetImporter.GetAtPath($"{TripoModelDir}/{id}/{id}.fbx") as ModelImporter;
        if (mi != null && (mi.materialImportMode != ModelImporterMaterialImportMode.None || mi.importAnimation || mi.importCameras || mi.importLights || mi.animationType != ModelImporterAnimationType.None))
        {
            mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.importAnimation = false; mi.importCameras = false; mi.importLights = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.SaveAndReimport();
        }
    }

    [MenuItem("Tools/핵폭발 생존/Tripo 소품 재질·들여오기 설정 맞추기")]
    static void TripoPrepareAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[GameSceneBuilder] 플레이 중에는 하지 않는다"); return; }
        foreach (var id in TripoSpecs.Keys)
        {
            string src = TripoSpecs[id].src ?? id;
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"{TripoModelDir}/{src}/{src}.fbx") == null) continue; // 품질별 판만 있는 모델 (방공호 가구): 판마다 재질은 TripoTierMat
            TripoConfigureImport(id);
            TripoMat(id);
        }
        Debug.Log("[GameSceneBuilder] Tripo 소품 " + TripoSpecs.Count + "개: 들여오기 설정과 재질(M_TR_*)을 맞췄다");
    }

    // 렌더러 메시의 가장 낮은 꼭짓점들(바닥에서 높이의 2 % 안)의 가운데를 target 기준 좌표로 (나무 밑동 자리)
    static Vector3 TripoBaseCenter(Transform target, IEnumerable<Renderer> renderers)
    {
        var pts = new List<Vector3>(); float lo = float.MaxValue, hi = float.MinValue;
        foreach (var r in renderers)
        {
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            foreach (var v in mf.sharedMesh.vertices) { var p = target.InverseTransformPoint(r.transform.TransformPoint(v)); pts.Add(p); lo = Mathf.Min(lo, p.y); hi = Mathf.Max(hi, p.y); }
        }
        if (pts.Count == 0) return Vector3.zero;
        float lim = lo + (hi - lo) * 0.02f; Vector3 sum = Vector3.zero; int n = 0;
        foreach (var p in pts) if (p.y <= lim) { sum += p; n++; }
        return n > 0 ? sum / n : Vector3.zero;
    }

    // target 아래 렌더러들의 메시 모서리를 target 기준 좌표로 모은 상자 (월드 축 상자보다 정확하다: 돌아간 소품도 크기가 그대로 나온다)
    static Bounds TripoLocalBounds(Transform target, IEnumerable<Renderer> renderers)
    {
        bool any = false;
        var b = new Bounds();
        foreach (var r in renderers)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds;
            for (int k = 0; k < 8; k++)
            {
                var c = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                var p = target.InverseTransformPoint(r.transform.TransformPoint(c));
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }
        return b;
    }

    // 소품의 겉모습을 Tripo 모델로 바꾼다. 이미 다 만들어진 소품(target)에 쓴다: 난수는 하나도 뽑지 않는다.
    // parts: 바꿀 겉모습 (target 기준 자식 경로). 비면 target 아래 렌더러 전부. "!a,b"면 a, b 아래를 뺀 전부.
    // 바뀌는 렌더러는 MeshRenderer·MeshFilter만 지운다: 같은 물체에 붙은 콜라이더는 그대로 남아 충돌·길 찾기 지도가 예전과 같다.
    // 처음부터 꺼져 있던 렌더러(고치기 전 트럭 부품 등)는 건드리지 않는다. 돌려주는 것은 모델을 든 받침 "Tripo_{id}"
    // fit: 예전 겉모습의 어느 치수에 맞출지. fixedScale > 0이면 그 배율을 그대로 쓴다 (원본 FBX는 긴 변이 약 1 m)
    static GameObject TripoSwap(Transform target, string id, string[] parts, TripoFit fit, TripoAlign align, float fixedScale = 0f, string matId = null)
    {
        // 바꿀 렌더러 모으기
        var old = new List<Renderer>();
        if (parts == null || parts.Length == 0) old.AddRange(target.GetComponentsInChildren<Renderer>());
        else if (parts.Length == 1 && parts[0].StartsWith("!"))
        {
            var keep = new List<Transform>();
            foreach (var kn in parts[0].Substring(1).Split(',')) { var k = target.Find(kn.Trim()); if (k != null) keep.Add(k); }
            foreach (var r in target.GetComponentsInChildren<Renderer>())
            {
                bool kept = false;
                foreach (var k in keep) if (r.transform.IsChildOf(k)) kept = true;
                if (!kept) old.Add(r);
            }
        }
        else foreach (var path in parts) { var c = target.Find(path); if (c != null) old.AddRange(c.GetComponentsInChildren<Renderer>()); }
        return TripoSwapRenderers(target, id, old, fit, align, fixedScale, matId, Vector3.zero);
    }

    // TripoSwap의 본체: 바꿀 렌더러를 목록으로 받는다 (이름이 같은 부품이 여럿인 방공호 선반 판자·탁자 다리용, 11-23).
    // fit Box = 축마다 따로 예전 겉모습 크기에 맞춘다 (비율이 다른 가구). axisScale의 0보다 큰 성분은 그 축 배율을 그대로 쓴다 (제작대 높이: 상판 높이로 맞춤)
    static GameObject TripoSwapRenderers(Transform target, string id, List<Renderer> old, TripoFit fit, TripoAlign align, float fixedScale, string matId, Vector3 axisScale)
    {
        var spec = TripoSpecs[id];
        old.RemoveAll(r => r == null || !r.enabled || !(r is MeshRenderer));
        if (old.Count == 0)
        {
            Debug.LogWarning("[GameSceneBuilder] TripoSwap: 바꿀 겉모습이 없음 (" + target.name + ", " + id + ")");
            return null;
        }
        // 받침 물체: target과 같은 자리·방향, 크기 1. target이 늘인 원기둥(드럼통 등)처럼 크기가 1이 아니면 target 옆(같은 부모)에 둔다
        var holder = new GameObject("Tripo_" + id).transform;
        bool unitScale = (target.lossyScale - Vector3.one).sqrMagnitude < 1e-4f;
        holder.SetParent(unitScale || target.parent == null ? target : target.parent, false);
        holder.SetPositionAndRotation(target.position, target.rotation);
        var ob = TripoLocalBounds(holder, old);

        // 모델 놓기: 받침 아래, 앞이 target의 +Z를 보게.
        // 품질별 판(Q0~Q3)이 있으면 네 판을 든 LOD 묶음 (11-23), 없으면 예전처럼 원본 FBX 하나
        GameObject go = TripoTierModel(holder, id, matId ?? id, spec.roll);
        MeshRenderer[] rs; // 크기·자리를 잴 렌더러 (LOD 묶음이면 가장 좋은 판만)
        bool tiered = go != null;
        var model = tiered ? null : AssetDatabase.LoadAssetAtPath<GameObject>($"{TripoModelDir}/{id}/{id}.fbx");
        if (tiered) rs = go.transform.Find("LOD0_q3").GetComponentsInChildren<MeshRenderer>();
        else if (model == null)
        {
            Debug.LogWarning("[GameSceneBuilder] Tripo 모델이 없음: " + id);
            Object.DestroyImmediate(holder.gameObject);
            return null;
        }
        else
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(model, holder);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0, 0, spec.roll) * Quaternion.Euler(0, spec.yaw, 0) * model.transform.localRotation;
            go.transform.localScale = Vector3.one;
            var mat = TripoMat(matId ?? id);
            rs = go.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in rs)
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        // 크기 맞추기 (한 배율로 줄이고 늘인다: 모양이 찌그러지지 않게)
        var nb = TripoLocalBounds(holder, rs);
        float want = fit == TripoFit.Height ? ob.size.y : fit == TripoFit.Width ? ob.size.x : ob.size.z;
        float have = fit == TripoFit.Height ? nb.size.y : fit == TripoFit.Width ? nb.size.x : nb.size.z;
        float scale = fixedScale > 0f ? fixedScale : have > 1e-4f ? want / have : 1f; // fixedScale: 사진으로 통과한 배율을 그대로 (차는 3.5: 예전 세단보다 짧은 옛 소형차라 길이로 맞추면 너무 높아진다)
        var sv = Vector3.one * scale;
        if (fit == TripoFit.Box)
            for (int a = 0; a < 3; a++) sv[a] = nb.size[a] > 1e-4f ? ob.size[a] / nb.size[a] : 1f;
        for (int a = 0; a < 3; a++) if (axisScale[a] > 0f) sv[a] = axisScale[a];
        go.transform.localScale = sv;
        nb = TripoLocalBounds(holder, rs);

        // 자리 맞추기 (받침 기준 좌표. go는 받침의 자식이라 그대로 더한다)
        Vector3 shift = ob.center - nb.center;
        if (align == TripoAlign.Bottom) shift.y = ob.min.y - nb.min.y;
        else if (align == TripoAlign.Top) shift.y = ob.max.y - nb.max.y;
        else if (align == TripoAlign.None) shift = Vector3.zero;
        else if (align == TripoAlign.BottomTrunk)
        {
            var root = TripoBaseCenter(holder, rs);
            shift = new Vector3(-root.x, ob.min.y - nb.min.y, -root.z);
        }
        else if (align == TripoAlign.BackBottom) { shift.z = ob.min.z - nb.min.z; shift.y = ob.min.y - nb.min.y; }
        else shift.z = ob.min.z - nb.min.z;
        go.transform.localPosition += shift;
        // LOD 묶음 크기는 축마다 늘인 뒤에 다시 잰다 (TripoTierModel이 배율 1일 때 잰 값이면 늘인 소품의 매우 높음 판이 1.5~2.5배 멀리까지 남는다, 11-24 검토)
        if (tiered) { var lg = go.GetComponent<LODGroup>(); if (lg != null) lg.RecalculateBounds(); }

        // 예전 겉모습 지우기 (콜라이더는 남긴다)
        foreach (var r in old)
        {
            var mf = r.GetComponent<MeshFilter>();
            Object.DestroyImmediate(r);
            if (mf != null) Object.DestroyImmediate(mf);
        }

        // LOD 묶음은 정적 묶기(static batching)에서 뺀다: 묶으면 소품마다 네 판의 꼭짓점이 묶음 버퍼에 복사돼 메모리가 크게 는다 (실외기 467개)
        foreach (var t in holder.GetComponentsInChildren<Transform>())
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, tiered ? StaticEditorFlags.OccludeeStatic : StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        return holder.gameObject;
    }
}
