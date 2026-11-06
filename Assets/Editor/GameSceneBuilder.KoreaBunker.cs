using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// GameSceneBuilder의 방공호 바깥(도시 해치) 개편 (맵 개편 세션): 실제 방공호 사진의 느낌 — 언덕에 묻힌 콘크리트 정면, 벽돌 아치 입구 둘(하나는 철문, 하나는 막힘),
// 벗겨진 녹색 페인트와 이끼·흘러내린 때, 마른 풀과 덩굴, 환기관, '민방위 대피소' 명판. 철문·바퀴·통로(HatchShell)·좌표는 그대로 두어 출발·귀환 연출이 기대는 구조를 지킨다
public static partial class GameSceneBuilder
{
    static void KoreaBunkerMaterials()
    {
        var pretendard = KFontFor("Fonts/Pretendard-Bold", KAllChars() + "민방위대피소CIVILDEFENSESHELTER");
        var dohyeon = KFontFor("Fonts/DoHyeon-Regular", KAllChars() + "민방위대피소CIVILDEFENSESHELTER");
        var plaque = PlaquePixels(dohyeon, pretendard);
        BunkerFacePixels(out var fa, out var fn);
        KReleaseFonts();
        M["bunkerFace"] = KLitMat("M_BunkerFace", KSaveTex("bunker_face", fa, 1024, 1024, KTexKind.Color), KSaveTex("bunker_face_normal", fn, 1024, 1024, KTexKind.Normal), Color.white, 0.12f, 0f, 0.8f);
        M["plaque"] = KLitMat("M_ShelterPlaque", KSaveTex("bunker_plaque", plaque, 1024, 256, KTexKind.Color, false), null, Color.white, 0.3f, 0.2f);
        M["grassDry"] = TexMat("M_GrassDry", "dirty_concrete", new Color(0.42f, 0.38f, 0.22f), 0.03f);
        M["vine"] = TexMat("M_Vine", "dirty_concrete", new Color(0.12f, 0.17f, 0.09f), 0.05f);
        if (!M.ContainsKey("earth") || M["earth"] == null) M["earth"] = TexMat("M_Earth", "dirty_concrete", new Color(0.3f, 0.26f, 0.2f), 0.04f);
    }

    // 콘크리트 정면 1024px = 2.5m: 회색 콘크리트 위에 벗겨진 녹색 페인트 조각(가장자리 들림), 이끼, 흘러내린 때, 아래쪽 흙 얼룩. 노멀은 페인트 두께·벗겨진 턱
    static void BunkerFacePixels(out Color32[] albedo, out Color32[] normal)
    {
        const int N = 1024;
        var px = new Color32[N * N];
        var hgt = new float[N * N];
        var concrete = new Color(0.52f, 0.52f, 0.5f);
        var paint = new Color(0.30f, 0.37f, 0.28f);    // 탁한 올리브 (위장무늬처럼 보이지 않게 채도를 낮춤)
        var paintOld = new Color(0.36f, 0.41f, 0.33f);  // 바랜 자리 (두 녹색 차이를 줄여 얼룩무늬로 보이지 않게)
        var moss = new Color(0.22f, 0.32f, 0.14f);
        var dirt = new Color(0.28f, 0.24f, 0.18f);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float grain = GrimeFbm(x, y, N, 32, 4, 6100);
                float peel = GrimeFbm(x, y, N, 5, 4, 6200);
                float peel2 = GrimeFbm(x, y, N, 11, 3, 6300);
                float mossN = GrimeFbm(x, y, N, 4, 3, 6400);
                float streak = GrimeFbm(x, y, N, 24, 2, 6500);
                float edgeN = GrimeFbm(x, y, N, 26, 3, 6600); // 벗겨진 조각 가장자리를 들쭉날쭉하게
                float v = y / (float)N;
                var c = concrete * (0.85f + 0.3f * grain);
                // 페인트: 남은 자리 (peel 낮은 곳), 가장자리 2px는 밝게(들린 턱)
                float peelV = peel + (peel2 - 0.5f) * 0.35f + (edgeN - 0.5f) * 0.14f;
                float paintMask = 1f - Smooth01(0.60f, 0.625f, peelV); // 페인트가 대부분 남고(≈75%), 노이즈가 높은 자리만 벗겨져 콘크리트가 드러난다. 문턱이 좁아 조각 가장자리가 날카롭다
                float h = paintMask;
                if (paintMask > 0.01f)
                {
                    var pc = Color.Lerp(paint, paintOld, (0.2f + 0.6f * v) * Smooth01(0.25f, 0.8f, peel2)); // 위쪽일수록 햇빛에 바랜다
                    c = Color.Lerp(c, pc * (0.85f + 0.3f * grain), paintMask);
                    float lip = Smooth01(0.02f, 0.12f, paintMask) * (1f - Smooth01(0.12f, 0.35f, paintMask)); // 들린 가장자리
                    c = Color.Lerp(c, c * 1.25f + new Color(0.05f, 0.05f, 0.04f), lip * 0.6f);
                }
                // 이끼·때: 아래쪽과 물 흐른 자리
                float mossA = Smooth01(0.55f, 0.85f, mossN) * Mathf.Clamp01(1.3f - v * 1.6f) * 0.7f;
                c = Color.Lerp(c, moss, mossA);
                float streakA = Smooth01(0.6f, 0.9f, streak) * (0.4f + 0.6f * (1f - v)) * 0.35f;
                c = Color.Lerp(c, dirt, streakA);
                c = Color.Lerp(c, dirt, Mathf.Clamp01(0.5f - v * 1.6f) * 0.8f);
                px[y * N + x] = KC(c);
                hgt[y * N + x] = h * 0.7f + grain * 0.15f;
            }
        albedo = px;
        normal = KNormalFromHeight(hgt, N, 2.0f);
    }

    // 명판 1024×256: 어두운 쇠판에 '민방위 대피소 / CIVIL DEFENSE SHELTER'
    static Color32[] PlaquePixels(KFont big, KFont small)
    {
        const int w = 1024, h = 256;
        var px = new Color[w * h];
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                var c = new Color(0.2f, 0.2f, 0.19f) * (0.8f + 0.4f * GrimeFbm(i, j, 1024, 8, 3, 6600));
                c = Color.Lerp(c, new Color(0.4f, 0.22f, 0.1f), Smooth01(0.6f, 0.9f, GrimeFbm(i, j, 1024, 3, 3, 6700)) * 0.5f);
                if (i < 12 || i >= w - 12 || j < 12 || j >= h - 12) c *= 1.3f;
                px[j * w + i] = c;
            }
        KText(px, w, h, big, "민방위 대피소", 128, w * 0.5f, h * 0.62f, new Color(0.9f, 0.85f, 0.7f), 0.5f);
        KText(px, w, h, small, "CIVIL DEFENSE SHELTER", 40, w * 0.5f, h * 0.2f, new Color(0.7f, 0.66f, 0.55f), 0.5f);
        var res = new Color32[w * h];
        for (int k = 0; k < res.Length; k++) { px[k].a = 1f; res[k] = KC(px[k]); }
        return res;
    }

    static int KBunkerStyle = 1; // 0 = 토루(언덕에 묻힌 정면), 1 = 블록하우스(콘크리트 몸통 + 옥상 흙·모래주머니). 정면·아치·명판·덩굴은 공통

    // 해치 둔덕과 정면 (BuildHatch가 HatchShell·문·바퀴·연출 좌표를 만든 뒤 부른다). g = 해치 그룹 (문은 로컬 z 1.53, 바깥 +z)
    static void BuildHatchMound(Transform g)
    {
        var r = KRandom(g.position, 77);
        if (KBunkerStyle == 1) BuildHatchBlockhouse(g, r);
        else
        {
        // 언덕: 둥근 지붕형 토루 — 가로 원기둥(반지름 3.6, z -4.3~1.1, 꼭대기 3.0)이 정면 뒤에 붙고, 뒤는 눌린 반구, 둘레는 낮은 흙 치마로 땅에 녹는다. 메시 콜라이더로 좀비가 못 뚫는다
        System.Action<GameObject> solid = go =>
        {
            go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic);
        };
        solid(Prim(PrimitiveType.Cylinder, "Mound", g, new Vector3(0, -0.6f, -1.6f), new Vector3(7.2f, 2.7f, 7.2f), M["earth"], new Vector3(90f, 0, 0)));
        solid(Prim(PrimitiveType.Sphere, "MoundBack", g, new Vector3(0, -0.6f, -4.3f), new Vector3(7.2f, 7.2f, 3.6f), M["earth"]));
        solid(Prim(PrimitiveType.Sphere, "MoundSkirt", g, new Vector3(0, -0.9f, -1.8f), new Vector3(9.5f, 2.6f, 9.0f), M["earth"]));
        // 마른 풀·흙더미: 토루 등을 따라 납작한 덩어리 (표면 기울기에 맞춰 눕힌다)
        for (int i = 0; i < 30; i++)
        {
            float th = KF(r, -1.15f, 1.15f), z = KF(r, -4.0f, 0.7f);
            var p = new Vector3(Mathf.Sin(th) * 3.6f * 0.98f, -0.6f + Mathf.Cos(th) * 3.6f * 0.98f, z);
            if (p.y < 0.3f) continue;
            Prim(PrimitiveType.Sphere, i % 3 == 0 ? "Soil" : "DryGrass", g, p, new Vector3(KF(r, 0.5f, 1.1f), KF(r, 0.16f, 0.32f), KF(r, 0.4f, 0.9f)), i % 3 == 0 ? M["ashPile"] : M["grassDry"], new Vector3(0, KF(r, -25f, 25f), -th * Mathf.Rad2Deg + KF(r, -8f, 8f)));
        }
        }
        // 정면: 두꺼운 콘크리트 벽 (z 1.15~1.55), 입구 둘 — 철문(가운데, 1.5×2.35)과 막힌 입구(오른쪽, 1.3×2.2)
        var face = Wall(g, "BunkerFace", new Vector3(-3.0f, 0, 1.35f), 0, 6.0f, 3.3f, 0.4f, M["bunkerFace"], new[] { new Op(3.0f, 1.5f, 0f, 2.35f), new Op(5.0f, 1.3f, 0f, 2.2f) });
        foreach (var t in face.GetComponentsInChildren<MeshRenderer>()) { var mf = t.GetComponent<MeshFilter>(); if (mf != null) mf.sharedMesh = KSlabMesh(mf.sharedMesh.bounds.size, 2.5f, Vector2.zero); }
        // 벽돌 아치: 입구 위 모서리를 둥글린다 (반원 고리의 아래 절반만 입구 안쪽에서 보인다)
        Arch(face, "BrickArch", 0.75f, 0.22f, new Vector3(3.0f, 1.6f, 0), 0.44f, M["brick"], 10, 1f);
        Arch(face, "BrickArch", 0.65f, 0.22f, new Vector3(5.0f, 1.55f, 0), 0.44f, M["brick"], 10, 1f);
        foreach (float x in new[] { 2.2f, 3.8f, 4.3f, 5.7f })
            Box("BrickJamb", face, new Vector3(x, 0.8f, 0), new Vector3(0.14f, 1.6f, 0.44f), M["brick"], 1f, false);
        // 막힌 입구: 안쪽 벽돌벽 + 앞의 잔해
        Box("BrickedUp", face, new Vector3(5.0f, 1.1f, -0.1f), new Vector3(1.3f, 2.2f, 0.2f), M["brick"], 1f);
        RubblePile(g, new Vector3(2.0f, 0, 1.9f), 5, 0.7f);
        Rebar(face, new Vector3(4.5f, 1.9f, 0.22f), new Vector3(0.5f, 0.3f, 1f), 0.5f, r);
        // 정면 위 갓돌과 흙이 넘친 가장자리
        Box("Coping", g, new Vector3(0, 3.36f, 1.35f), new Vector3(6.2f, 0.12f, 0.6f), M["dirty"], 2f);
        for (int i = 0; i < 6; i++)
            Prim(PrimitiveType.Sphere, "EdgeSoil", g, new Vector3(KF(r, -2.8f, 2.8f), 3.42f, KF(r, 1.1f, 1.4f)), new Vector3(KF(r, 0.5f, 1.0f), KF(r, 0.12f, 0.22f), KF(r, 0.3f, 0.5f)), M["grassDry"], new Vector3(0, KF(r, 0, 360), 0));
        // 덩굴: 정면 위에서 늘어진 가는 줄기 몇
        var vines = new List<CombineInstance>();
        for (int i = 0; i < 7; i++)
        {
            float x = KF(r, -2.9f, 2.9f);
            if (Mathf.Abs(x) < 1.0f) continue;
            var p = new Vector3(x, 3.3f, 1.57f);
            for (int k = 0, n = 3 + r.Next(4); k < n; k++)
            {
                var q = p + new Vector3(KF(r, -0.15f, 0.15f), -KF(r, 0.25f, 0.5f), KF(r, -0.01f, 0.02f));
                KRod(vines, p, q, 0.007f);
                KSphereAt(vines, q, KF(r, 0.012f, 0.022f)); // 잎 마디 (구슬처럼 보이지 않게 작게)
                p = q;
            }
        }
        CombineInto(g, "Vines", vines, M["vine"]);
        // 명판 (철문 위), 환기관 (언덕 위, 갓 씌움)
        var plaque = Prim(PrimitiveType.Quad, "Plaque", g, new Vector3(0, 2.75f, 1.556f), new Vector3(1.4f, 0.35f, 1f), M["plaque"], new Vector3(0, 180f, 0));
        plaque.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        Box("PlaqueFrame", g, new Vector3(0, 2.75f, 1.552f), new Vector3(1.46f, 0.41f, 0.008f), M["tire"], 1f, false);
        float vy = KBunkerStyle == 1 ? 0.6f : 0f; // 블록하우스는 옥상(3.3)이 토루 등(3.0)보다 높다
        Prim(PrimitiveType.Cylinder, "VentPipe", g, new Vector3(1.6f, 3.0f + vy, -1.6f), new Vector3(0.3f, 0.7f, 0.3f), M["metal"], null, true);
        Prim(PrimitiveType.Cylinder, "VentCap", g, new Vector3(1.6f, 3.72f + vy, -1.6f), new Vector3(0.44f, 0.04f, 0.44f), M["metal"]);
        Prim(PrimitiveType.Cylinder, "VentPipe2", g, new Vector3(-1.9f, 2.6f + vy + 0.3f, -2.3f), new Vector3(0.2f, 0.5f, 0.2f), M["metal"], new Vector3(0, 0, 12f), true);
        // 흙 비탈과 길이 만나는 자리: 흙 얼룩
        Stain("Mud", g, new Vector3(-2.6f, 0.01f, 0.2f), 1.6f, 1.2f, M["ashPile"], 6800);
        Stain("Mud", g, new Vector3(2.9f, 0.01f, -0.4f), 1.4f, 1.1f, M["ashPile"], 6801);
    }

    // 시안 B: 블록하우스 — 통로(HatchShell, x ±1.6·y 2.6·z ±1.5)를 감싸는 두꺼운 콘크리트 몸통(좌·우·위·뒤 네 덩어리), 옥상 슬래브와 흙·마른 풀, 옥상 앞 모래주머니 두 줄, 막힌 입구 앞 모래주머니 반벽
    static void BuildHatchBlockhouse(Transform g, System.Random r)
    {
        var body = M["dirty"];
        Box("BodyL", g, new Vector3(-2.35f, 1.55f, -1.0f), new Vector3(1.3f, 3.1f, 4.3f), body, 2.5f);
        Box("BodyR", g, new Vector3(2.35f, 1.55f, -1.0f), new Vector3(1.3f, 3.1f, 4.3f), body, 2.5f);
        Box("BodyTop", g, new Vector3(0, 2.85f, -1.0f), new Vector3(3.4f, 0.5f, 4.3f), body, 2.5f);
        Box("BodyBack", g, new Vector3(0, 1.3f, -2.4f), new Vector3(3.4f, 2.6f, 1.5f), body, 2.5f);
        Box("RoofSlab", g, new Vector3(0, 3.22f, -1.0f), new Vector3(6.5f, 0.24f, 4.9f), M["cracked"], 2.5f);
        // 옥상 흙 (낮은 타원) + 마른 풀
        // 옥상 흙: 크기·방향이 다른 납작한 타원 넷이 겹친 불규칙한 더미 (원판 하나는 접시처럼 보였다)
        var soilPieces = new[] { new Vector4(0, -1.1f, 5.0f, 3.6f), new Vector4(-1.2f, -0.4f, 3.2f, 2.6f), new Vector4(1.4f, -1.9f, 3.4f, 2.4f), new Vector4(0.3f, 0.2f, 2.6f, 1.8f) };
        for (int i = 0; i < soilPieces.Length; i++)
        {
            var sp = soilPieces[i];
            var soil = Prim(PrimitiveType.Sphere, "RoofSoil", g, new Vector3(sp.x, 3.33f + i * 0.015f, sp.y), new Vector3(sp.z, 0.42f + i * 0.03f, sp.w), M["earth"], new Vector3(0, KF(r, 0, 360), 0), false);
            GameObjectUtility.SetStaticEditorFlags(soil, StaticEditorFlags.BatchingStatic);
        }
        for (int i = 0; i < 12; i++)
        {
            var p = new Vector3(KF(r, -2.2f, 2.2f), 3.56f, KF(r, -2.6f, 0.4f));
            Prim(PrimitiveType.Sphere, i % 3 == 0 ? "Soil" : "DryGrass", g, p, new Vector3(KF(r, 0.4f, 0.9f), KF(r, 0.12f, 0.24f), KF(r, 0.35f, 0.7f)), i % 3 == 0 ? M["ashPile"] : M["grassDry"], new Vector3(0, KF(r, 0, 360), 0), false);
        }
        // 옥상 앞 가장자리 모래주머니 두 줄 (어긋나게, 몇 개 빠짐)
        for (int row = 0; row < 2; row++)
            for (int i = 0; i < 6; i++)
            {
                float bx = -2.3f + i * 0.9f + (row % 2) * 0.45f;
                if (row == 1 && r.NextDouble() < 0.35) continue;
                KSandbag(g, new Vector3(bx, 3.5f + row * 0.3f, 0.75f + KF(r, -0.03f, 0.03f)), KF(r, -6f, 6f));
            }
        // 막힌 입구 앞 모래주머니 반벽 (두 줄 × 두 개)
        for (int row = 0; row < 2; row++)
            for (int i = 0; i < 2; i++)
                KSandbag(g, new Vector3(1.55f + i * 0.9f + (row % 2) * 0.45f, 0.17f + row * 0.3f, 2.05f + KF(r, -0.03f, 0.03f)), KF(r, -5f, 5f));
    }

    // 모래주머니 하나: 눕힌 캡슐 0.85×0.32×0.45 (상자는 멀리서 벽돌처럼 보였다). yaw는 도 단위
    static GameObject KSandbag(Transform parent, Vector3 pos, float yaw)
    {
        return Prim(PrimitiveType.Capsule, "Sandbag", parent, pos, new Vector3(0.32f, 0.425f, 0.45f), M["sandbag"], new Vector3(0, yaw, 90f), true);
    }
}
