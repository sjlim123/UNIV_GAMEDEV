using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// GameSceneBuilder의 폐허 보강 부분: 폭격 맞은 건물(2층이 터져 드러나고 휜 철근이 돌출), 건물 벽의 금·때,
// 해치 주변의 황폐함(물웅덩이·젖은 아스팔트·잔해·탄 차), 벽 재질의 금 디테일 맵, 도시 씬만 다시 만드는 메뉴
public static partial class GameSceneBuilder
{
    const string CityGenDir = "Assets/Generated/City";

    // ---------- 재질 ----------

    static void RuinMaterials()
    {
        if (M.ContainsKey("gold") && M["gold"] != null) return;
        // 황금 문: 매끈한 금빛 (녹슨 쇠의 요철만 빌린다)
        M["gold"] = TexMat("M_GoldPaint", null, new Color(0.95f, 0.74f, 0.24f), 0.72f, 0.9f);
        M["gold"].SetTexture("_BaseMap", null); // 예전에 넣었던 녹 그림은 뺀다
        M["gold"].SetTexture("_MainTex", null);
        var goldNormal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/rusty_metal_02_nor_gl_1k.jpg");
        if (goldNormal != null)
        {
            M["gold"].SetTexture("_BumpMap", goldNormal);
            M["gold"].SetFloat("_BumpScale", 0.5f);
            M["gold"].EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(M["gold"]);
        }
        M["rebar"] = TexMat("M_Rebar", "rusty_metal_02", new Color(0.48f, 0.26f, 0.12f), 0.2f, 0.3f);
        M["soot"] = TexMat("M_Soot", "dirty_concrete", new Color(0.11f, 0.1f, 0.095f), 0.06f);
        M["scorch"] = TexMat("M_Scorch", "dirty_concrete", new Color(0.07f, 0.065f, 0.06f), 0.05f);
        M["ashPile"] = TexMat("M_AshPile", "dirty_concrete", new Color(0.36f, 0.35f, 0.33f), 0.04f);
        M["bloodOld"] = TexMat("M_BloodOld", null, new Color(0.2f, 0.025f, 0.02f), 0.4f);
        M["tunnel"] = TexMat("M_TunnelConcrete", "dirty_concrete", new Color(0.17f, 0.17f, 0.165f), 0.05f); // 해치 통로 안감 (그을린 콘크리트)
        // 도시 씬만 지을 때는 방공호 빌더가 만드는 경고 줄무늬 재질도 필요하다 (터널 입구)
        if (!M.ContainsKey("blastStripe") || M["blastStripe"] == null)
            M["blastStripe"] = TexMat("M_BlastDoorStripe", "rusty_metal_02", new Color(0.72f, 0.55f, 0.1f), 0.3f, 0.3f);
        WallCrackDetail();
    }

    // 벽 재질(콘크리트·벽돌·금 간 벽·때 묻은 콘크리트)에 금·구멍 디테일 맵을 얹는다. 한 장이 벽 약 7.5m를 덮어 금이 크고 드문드문 간다
    static void WallCrackDetail()
    {
        CrackTextures(out var albedo, out var normal);
        foreach (var key in new[] { "wall", "brick", "cracked", "dirty" })
        {
            if (!M.TryGetValue(key, out var m) || m == null) continue;
            m.SetTexture("_DetailAlbedoMap", albedo);
            m.SetTexture("_DetailNormalMap", normal);
            m.SetFloat("_DetailAlbedoMapScale", 1f);
            m.SetFloat("_DetailNormalMapScale", key == "brick" ? 0.7f : 1f);
            m.SetTextureScale("_DetailAlbedoMap", new Vector2(0.33f, 0.33f));
            m.EnableKeyword("_DETAIL_MULX2");
            m.DisableKeyword("_DETAIL_SCALED");
            EditorUtility.SetDirty(m);
        }
    }

    // 금 그림: 가지 치며 뻗는 가는 균열 + 잔 구멍. 0.5 회색이 중립(곱하기 2 = 그대로), 금 안쪽은 어둡다. 이어 붙여도 이음새 없음
    static void CrackTextures(out Texture2D albedo, out Texture2D normal)
    {
        const int N = 1024;
        var h = new float[N * N];
        var rnd = new System.Random(2027);
        void Stamp(float x, float y, float w)
        {
            int cx = Mathf.RoundToInt(x), cy = Mathf.RoundToInt(y);
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    float d = Mathf.Sqrt((cx + dx - x) * (cx + dx - x) + (cy + dy - y) * (cy + dy - y));
                    float cov = Mathf.Clamp01(w * 0.5f - d + 0.5f);
                    if (cov <= 0) continue;
                    int px = ((cx + dx) % N + N) % N, py = ((cy + dy) % N + N) % N;
                    int k = py * N + px;
                    h[k] = Mathf.Max(h[k], cov);
                }
        }
        void Walk(float x, float y, float ang, int len, float w, int depth)
        {
            for (int i = 0; i < len; i++)
            {
                ang += ((float)rnd.NextDouble() - 0.5f) * 0.5f;
                x += Mathf.Cos(ang);
                y += Mathf.Sin(ang);
                float t = i / (float)len;
                Stamp(x, y, w * (1f - 0.55f * t));
                if (depth > 0 && rnd.NextDouble() < 0.012)
                    Walk(x, y, ang + ((float)rnd.NextDouble() < 0.5f ? 0.8f : -0.8f), 40 + rnd.Next(160), w * 0.6f, depth - 1);
            }
        }
        for (int c = 0; c < 16; c++)
            Walk(rnd.Next(N), rnd.Next(N), (float)rnd.NextDouble() * Mathf.PI * 2f, 260 + rnd.Next(620), 1.6f + (float)rnd.NextDouble() * 1.6f, 2);

        var px = new Color32[N * N];
        var nrm = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int k = y * N + x;
                float pores = GrimeNoise(x, y, N, 128, 909);
                float pore = Smooth01(0.82f, 0.95f, pores) * 0.25f;
                float v = 0.5f - 0.3f * h[k] - pore + (GrimeFbm(x, y, N, 8, 3, 303) - 0.5f) * 0.05f;
                byte b = (byte)(Mathf.Clamp01(v) * 255);
                px[k] = new Color32(b, b, b, 255);
            }
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float l = h[y * N + (x + N - 1) % N], r = h[y * N + (x + 1) % N];
                float d = h[((y + N - 1) % N) * N + x], u = h[((y + 1) % N) * N + x];
                // 금은 홈: 높이가 낮다 (-h)
                var n = new Vector3((r - l) * 2.2f, (u - d) * 2.2f, 1f).normalized;
                nrm[y * N + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255), (byte)((n.y * 0.5f + 0.5f) * 255), (byte)((n.z * 0.5f + 0.5f) * 255), 255);
            }
        albedo = SaveCityTexture("wall_cracks_detail", px, N, false);
        normal = SaveCityTexture("wall_cracks_normal", nrm, N, true);
    }

    static Texture2D SaveCityTexture(string name, Color32[] px, int n, bool normalMap)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(CityGenDir)) AssetDatabase.CreateFolder("Assets/Generated", "City");
        string path = $"{CityGenDir}/{name}.png";
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(path) is TextureImporter imp)
        {
            imp.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !normalMap;
            imp.alphaSource = TextureImporterAlphaSource.None;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 4;
            imp.maxTextureSize = 1024;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------- 도우미 ----------

    // 가장자리가 들쭉날쭉한 납작한 얼룩 메시 (반지름 1, xz 평면). 물웅덩이·그을음·핏자국·때에 쓴다
    static Mesh BlobMesh(int seed)
    {
        string key = "blob_" + seed;
        if (meshCache.TryGetValue(key, out var cached)) return cached;
        var m = Gore.Blob(seed);
        var copy = new Mesh { name = key };
        copy.vertices = m.vertices;
        copy.normals = m.normals;
        copy.uv = m.uv;
        copy.triangles = m.triangles;
        copy.RecalculateBounds();
        AssetDatabase.AddObjectToAsset(copy, meshContainer);
        meshCache[key] = copy;
        return copy;
    }

    // 납작한 얼룩 하나 (yaw 임의). 바닥에 놓을 때 y를 조금 띄운다
    static GameObject Stain(string name, Transform parent, Vector3 pos, float rx, float rz, Material mat, int seed, Vector3? euler = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler ?? new Vector3(0, seed * 37 % 360, 0));
        go.transform.localScale = new Vector3(rx, 1, rz);
        go.AddComponent<MeshFilter>().sharedMesh = BlobMesh(seed);
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    // 휜 철근: from에서 dir 쪽으로 뻗다가 마디마다 꺾인다 (녹슨 쇠, 콜라이더 없음)
    static void Rebar(Transform parent, Vector3 from, Vector3 dir, float length, System.Random r, float radius = 0.012f)
    {
        float F(float a, float b) => a + (float)r.NextDouble() * (b - a);
        int segs = 2 + r.Next(2);
        var p = from;
        var d = dir.normalized;
        float segLen = length / segs;
        for (int i = 0; i < segs; i++)
        {
            var q = p + d * segLen;
            var go = Prim(PrimitiveType.Cylinder, "Rebar", parent, (p + q) / 2, new Vector3(radius * 2, segLen / 2 + radius, radius * 2), M["rebar"]);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d);
            // 다음 마디는 아래·옆으로 휜다
            d = (d + new Vector3(F(-0.5f, 0.5f), F(-0.9f, -0.2f), F(-0.5f, 0.5f))).normalized;
            p = q;
        }
    }

    // 높이가 들쭉날쭉한 부서진 벽: start에서 로컬 X(yaw) 방향으로 length. fullUntil(0~1)까지는 온전하고 그 뒤로 무너져 내려 끝에서 minH
    static void RaggedWall(Transform parent, string name, Vector3 start, float yaw, float length, float fullUntil, float fullH, float minH, float thick, Material mat, System.Random r, bool rebar = true)
    {
        float F(float a, float b) => a + (float)r.NextDouble() * (b - a);
        var w = Group(name, parent, start, yaw);
        float s = 0;
        while (s < length - 0.05f)
        {
            float seg = Mathf.Min(F(0.45f, 1.1f), length - s);
            float u = (s + seg / 2) / length;
            float hgt;
            if (u < fullUntil) hgt = fullH;
            else
            {
                float k = (u - fullUntil) / Mathf.Max(0.05f, 1 - fullUntil);
                hgt = Mathf.Lerp(fullH, minH, Mathf.Pow(k, 0.7f)) + F(-0.35f, 0.35f);
                hgt = Mathf.Clamp(hgt, minH * 0.6f, fullH);
            }
            Box("Seg", w, new Vector3(s + seg / 2, hgt / 2, 0), new Vector3(seg, hgt, thick), mat, 2.5f);
            // 무너진 윗면의 덩어리와 삐져나온 철근
            if (hgt < fullH - 0.2f)
            {
                if (r.NextDouble() < 0.6) Box("Chunk", w, new Vector3(s + seg / 2 + F(-0.2f, 0.2f), hgt + 0.08f, F(-0.08f, 0.08f)), new Vector3(F(0.2f, 0.45f), F(0.12f, 0.2f), thick + F(0f, 0.1f)), mat, 1.5f, false, new Vector3(F(-12, 12), F(-10, 10), F(-15, 15)));
                if (rebar && r.NextDouble() < 0.55) Rebar(w, new Vector3(s + seg / 2 + F(-0.2f, 0.2f), hgt - 0.05f, F(-0.05f, 0.05f)), new Vector3(F(-0.4f, 0.4f), 1f, F(-0.5f, 0.5f)), F(0.3f, 0.7f), r);
            }
            s += seg;
        }
    }

    // ---------- 폭격 맞은 건물 ----------

    // 이 구역이 폭격 맞은 건물로 바뀌는가: 예전 도시(26.0.0.10)에서 무너진 건물(Ruin)이던 세 곳만 바꾼다.
    // 들어갈 수 있던 건물 22채는 하나도 건드리지 않는다 (잡상인·출발 지점·트럭 정비소와도 겹치지 않는다)
    static bool IsBombedLot(Vector3 c, int ix, int iz) =>
        (c.x == -46f && c.z == 46f && ix == 1 && iz == 1) || (c.x == 46f && c.z == -46f && ix == 1 && iz == 0) || (c.x == 46f && c.z == 46f && ix == 1 && iz == 1);

    // 미사일에 맞아 바깥 모퉁이의 2층이 터져 나간 건물: 1층은 온전해 문으로 들어가고 계단으로 2층에 오르면 하늘이 뚫려 있다.
    // 부서진 단면에는 휜 철근이 녹슨 채 삐져나오고, 지붕 한 귀퉁이는 비스듬히 무너져 인도까지 내려와 있다. 둘레에는 콘크리트 잔해·그을음
    static void BombedBuilding(Transform block, Transform points, int ix, int iz)
    {
        var cityRng = rng;
        rng = new System.Random(7700 + ix * 3 + iz * 5 + Mathf.RoundToInt(block.localPosition.x) * 7 + Mathf.RoundToInt(block.localPosition.z) * 13);
        var r = rng;
        float F(float a, float b) => a + (float)r.NextDouble() * (b - a);

        LotRect(ix, iz, out float x0, out float z0, out float w, out float d);
        w = Mathf.Max(w, 11f);
        d = Mathf.Max(d, 11f);
        x0 = ix == 0 ? -KLotHalf : KLotHalf - w;
        z0 = iz == 0 ? -KLotHalf : KLotHalf - d;
        var g = Group("Bombed", block, new Vector3(0, 0.15f, 0));
        const float h = 3.6f, t = 0.3f;
        var mat = Pick(M["wall"], M["cracked"], M["brick"]);

        // 바깥쪽 두 면: 0=남 1=북 2=서 3=동. 문은 바깥 면 하나, 창문은 다른 바깥 면
        int xSide = ix == 0 ? 2 : 3, zSide = iz == 0 ? 0 : 1;
        int doorSide = Chance(0.5) ? xSide : zSide;
        int winSide = doorSide == xSide ? zSide : xSide;
        float len(int side) => side < 2 ? w : d;
        var ops = new Op[4][];
        for (int s = 0; s < 4; s++) ops[s] = Solid;
        // 문은 터진 모퉁이에서 먼 쪽에 (문 위가 무너지지 않게)
        float doorC = doorSide == xSide ? (iz == 0 ? len(doorSide) - 2.5f : 2.5f) : (ix == 0 ? len(doorSide) - 2.5f : 2.5f);
        ops[doorSide] = new[] { DoorOp(doorC) };
        ops[winSide] = new[] { WinOp(len(winSide) * 0.3f), WinOp(len(winSide) * 0.65f) };
        var walls = RoomWalls(g, x0, z0, w, d, h, t, mat, ops[0], ops[1], ops[2], ops[3]);
        Box("Floor", g, new Vector3(x0 + w / 2, 0.02f, z0 + d / 2), new Vector3(w - t, 0.04f, d - t), M["floor"], 2.5f);
        AddDoor(walls[doorSide], doorC + ShiftFor(doorSide, t), 1.4f, 2.3f, Pick(M["wood"], M["metal"]));
        // 폭발에 떨어진 가로 간판 (자리에서 뽑은 난수)
        var signRng = KRandom(block.localPosition + new Vector3(x0, 0, z0), 21);
        KSignBand(g, doorSide, x0 - t / 2, z0 - t / 2, w + t, d + t, h - 0.55f, h + 0.4f, signRng.Next(KTraderShop), 2, false, signRng);

        // 계단은 문 맞은편 벽을 따라 (PlanUpstairs가 난수를 쓴다)
        var plan = PlanUpstairs(x0, z0, w, d, t, doorSide ^ 1);
        Vector3 c0 = plan.P(plan.open, 0, 0), c1 = plan.P(plan.Top, StairWidth, 0);
        SlabWithHole(g, x0 - t / 2, x0 + w + t / 2, z0 - t / 2, z0 + d + t / 2,
            Mathf.Min(c0.x, c1.x), Mathf.Max(c0.x, c1.x), Mathf.Min(c0.z, c1.z), Mathf.Max(c0.z, c1.z), h + 0.15f, 0.3f, M["dirty"]);
        BuildStairs(g, plan);

        // 1층 안: 선반·상자·잔해·아이템 자리
        Vector3 center = new Vector3(x0 + w / 2, 0, z0 + d / 2);
        var shelf = BuildShelf(g, plan.P(plan.len - 1.2f, plan.depth - 0.3f, 0), plan.yaw + 180f, true);
        Spawn<ItemSpawnPoint>(points, shelf.position + shelf.rotation * new Vector3(-0.4f, 0.65f, 0));
        Spawn<ItemSpawnPoint>(points, shelf.position + shelf.rotation * new Vector3(0.4f, 1.15f, 0));
        for (int i = 0; i < 2; i++)
        {
            var p = plan.P(F(1f, plan.len - 1f), F(StairWidth + 1.2f, plan.depth - 1f), 0);
            Box("Crate", g, p + Vector3.up * 0.3f, new Vector3(0.7f, 0.6f, 0.6f), M["wood"], 1f, true, new Vector3(0, F(0, 90), 0));
            if (i == 0) Spawn<ItemSpawnPoint>(points, g.TransformPoint(p) + Vector3.up * 0.65f);
        }
        for (int i = 0; i < 5; i++)
            Box("Debris", g, plan.P(F(0.5f, plan.len - 0.5f), F(StairWidth + 0.6f, plan.depth - 0.5f), 0.05f), new Vector3(F(0.15f, 0.6f), 0.07f, F(0.15f, 0.5f)), Pick(M["cracked"], M["dirty"], M["wood"]), 1f, false, new Vector3(F(-10, 10), F(0, 180), 0));
        // 천장에서 떨어진 콘크리트 판 하나 (1층 안, 계단 길은 피해서)
        Box("FallenSlab", g, plan.P(F(2f, plan.len - 2f), plan.depth - 1.6f, 0.45f), new Vector3(F(1.6f, 2.4f), 0.2f, F(1f, 1.6f)), M["dirty"], 2.5f, true, new Vector3(F(14, 26), F(0, 360), 0));
        if (Chance(0.5)) Spawn<ZombieSpawnPoint>(points, g.TransformPoint(center));

        // 2층: 터진 모퉁이 = 바깥 모퉁이 (xSide, zSide). 모퉁이에서 안쪽으로 cx·cz만큼이 무너진 영역
        float cx = w * F(0.42f, 0.5f), cz = d * F(0.42f, 0.5f);
        var g2 = Group("Floor2", g, new Vector3(0, Floor2Y, 0));
        float ix0 = x0 - t / 2, ix1 = x0 + w + t / 2, iz0 = z0 - t / 2, iz1 = z0 + d + t / 2;
        bool west = xSide == 2, south = zSide == 0;
        // 바깥 x벽 (서 또는 동): z를 따라. 터진 쪽 끝(남/북)으로 갈수록 무너진다
        {
            // 벽은 start에서 로컬 X로 뻗는다: 남쪽이 터졌으면 북쪽 끝에서 시작해 남쪽으로 (yaw 90: +x → -z). 중심선은 1층 벽과 같다
            float length = d - t;
            Vector3 start = new Vector3(west ? x0 : x0 + w, 0, south ? z0 + d - t / 2 : z0 + t / 2);
            RaggedWall(g2, "WallOuterX", start, south ? 90f : -90f, length, 1f - cz / length, Floor2H, 0.5f, t, mat, r);
        }
        // 바깥 z벽 (남 또는 북): x를 따라, 터진 쪽 끝(서/동)으로 갈수록 무너진다
        {
            float length = w + t;
            Vector3 start = new Vector3(west ? x0 + w + t / 2 : x0 - t / 2, 0, south ? z0 : z0 + d);
            RaggedWall(g2, "WallOuterZ", start, west ? 180f : 0f, length, 1f - cx / length, Floor2H, 0.5f, t, mat, r);
        }
        // 안쪽 두 벽: 온전하고 창문이 난다
        {
            int innerX = west ? 3 : 2, innerZ = south ? 1 : 0;
            var wx = innerX == 2 ? Wall(g2, "WallW", new Vector3(x0, 0, z0 + t / 2), -90, d - t, Floor2H, t, mat, new[] { new Op((d - t) * 0.5f, 1.2f, 0.95f, 2.45f) })
                                 : Wall(g2, "WallE", new Vector3(x0 + w, 0, z0 + t / 2), -90, d - t, Floor2H, t, mat, new[] { new Op((d - t) * 0.5f, 1.2f, 0.95f, 2.45f) });
            var wz = innerZ == 0 ? Wall(g2, "WallS", new Vector3(x0 - t / 2, 0, z0), 0, w + t, Floor2H, t, mat, new[] { new Op((w + t) * 0.35f, 1.2f, 0.95f, 2.45f), new Op((w + t) * 0.7f, 1.2f, 0.95f, 2.45f) })
                                 : Wall(g2, "WallN", new Vector3(x0 - t / 2, 0, z0 + d), 0, w + t, Floor2H, t, mat, new[] { new Op((w + t) * 0.35f, 1.2f, 0.95f, 2.45f), new Op((w + t) * 0.7f, 1.2f, 0.95f, 2.45f) });
        }
        // 지붕: 무너진 모퉁이를 뺀 ㄱ자 (두 판). 부서진 가장자리에 덩어리·철근
        float roofY = Floor2Y + Floor2H + 0.15f;
        float bx0 = west ? ix0 : ix1 - cx, bx1 = west ? ix0 + cx : ix1; // 무너진 x 범위
        float bz0 = south ? iz0 : iz1 - cz, bz1 = south ? iz0 + cz : iz1; // 무너진 z 범위
        // 판 A: 전체 폭, 무너진 z 범위를 뺀 깊이
        float az0 = south ? bz1 : iz0, az1 = south ? iz1 : bz0;
        Box("RoofA", g, new Vector3((ix0 + ix1) / 2, roofY, (az0 + az1) / 2), new Vector3(ix1 - ix0, 0.3f, az1 - az0), M["dirty"], 3f);
        // 판 B: 무너진 z 범위 안에서 무너진 x 범위를 뺀 폭
        float bxA = west ? bx1 : ix0, bxB = west ? ix1 : bx0;
        Box("RoofB", g, new Vector3((bxA + bxB) / 2, roofY, (bz0 + bz1) / 2), new Vector3(bxB - bxA, 0.3f, bz1 - bz0), M["dirty"], 3f);
        // 지붕 단면의 철근과 덩어리: 판 A의 터진 쪽 모서리(z = south ? bz1 : bz0, x는 bx 범위)와 판 B의 x 모서리
        for (int i = 0; i < 5; i++)
        {
            float ex = Mathf.Lerp(bx0, bx1, (i + 0.5f) / 5f) + F(-0.3f, 0.3f);
            float ez = south ? bz1 : bz0;
            Rebar(g, new Vector3(ex, roofY + F(-0.1f, 0.05f), ez), new Vector3(F(-0.3f, 0.3f), F(-0.2f, 0.3f), south ? -1f : 1f), F(0.35f, 0.8f), r);
            if (i % 2 == 0) Box("RoofChunk", g, new Vector3(ex, roofY - 0.05f, ez + (south ? -0.25f : 0.25f)), new Vector3(F(0.3f, 0.6f), 0.25f, F(0.3f, 0.5f)), M["dirty"], 1.5f, false, new Vector3(F(-20, 20), F(-15, 15), F(-25, 25)));
        }
        for (int i = 0; i < 4; i++)
        {
            float ez = Mathf.Lerp(bz0, bz1, (i + 0.5f) / 4f) + F(-0.3f, 0.3f);
            float ex = west ? bx1 : bx0;
            Rebar(g, new Vector3(ex, roofY + F(-0.1f, 0.05f), ez), new Vector3(west ? -1f : 1f, F(-0.2f, 0.3f), F(-0.3f, 0.3f)), F(0.35f, 0.8f), r);
        }
        // 2층 바닥판 터진 모퉁이 가장자리에서도 철근이 아래로 늘어진다
        for (int i = 0; i < 4; i++)
        {
            bool onX = i % 2 == 0;
            var at = onX ? new Vector3(Mathf.Lerp(bx0, bx1, F(0.1f, 0.9f)), Floor2Y + 0.05f, south ? iz0 : iz1) : new Vector3(west ? ix0 : ix1, Floor2Y + 0.05f, Mathf.Lerp(bz0, bz1, F(0.1f, 0.9f)));
            var dir = onX ? new Vector3(F(-0.3f, 0.3f), -0.3f, south ? -1f : 1f) : new Vector3(west ? -1f : 1f, -0.3f, F(-0.3f, 0.3f));
            Rebar(g, at, dir, F(0.3f, 0.6f), r);
        }

        // 그 위층 겉모습 (ㄱ자 위에만, 모퉁이는 터져 나갔다)
        int floors = 1 + r.Next(3);
        float upperH = (floors - 1) * 3.2f;
        if (floors > 1)
        {
            Box("UpperA", g, new Vector3((ix0 + ix1) / 2, roofY + 0.15f + upperH / 2, (az0 + az1) / 2), new Vector3(ix1 - ix0, upperH, az1 - az0), mat, 3f);
            Box("UpperB", g, new Vector3((bxA + bxB) / 2, roofY + 0.15f + upperH / 2, (bz0 + bz1) / 2), new Vector3(bxB - bxA, upperH, bz1 - bz0), mat, 3f);
            // 위층 단면도 들쭉날쭉: 터진 모퉁이 가장자리에 덩어리
            for (int i = 0; i < 6; i++)
            {
                bool onX = i % 2 == 0;
                var at = onX ? new Vector3(Mathf.Lerp(bx0, bx1, F(0.05f, 0.95f)), roofY + 0.15f + F(0.2f, upperH - 0.2f), south ? bz1 : bz0)
                             : new Vector3(west ? bx1 : bx0, roofY + 0.15f + F(0.2f, upperH - 0.2f), Mathf.Lerp(bz0, bz1, F(0.05f, 0.95f)));
                Box("Jag", g, at, new Vector3(F(0.4f, 0.9f), F(0.4f, 0.9f), F(0.4f, 0.9f)), mat, 1.5f, false, new Vector3(F(-15, 15), F(-15, 15), F(-15, 15)));
            }
        }
        BombedFacadeWindows(g, ix0, iz0, ix1, iz1, roofY + 0.15f, floors - 1, bx0, bx1, bz0, bz1, west, south);

        // 무너져 내린 지붕 귀퉁이: 2층 가장자리에서 바깥 인도까지 비스듬히 기댄 큰 판 (걸어 올라갈 수 있다)
        {
            float slabW = Mathf.Min(cx, cz) * 0.9f, slabL = 6.2f;
            float topY = Floor2Y - 0.1f;
            var cornerX = west ? ix0 : ix1;
            var cornerZ = south ? iz0 : iz1;
            // 모퉁이 바깥쪽 대각선 방향으로 내려온다
            var outDir = new Vector3(west ? -1f : 1f, 0, south ? -1f : 1f).normalized;
            float angle = Mathf.Asin(Mathf.Clamp01(topY / slabL)) * Mathf.Rad2Deg;
            var top = new Vector3(cornerX, topY, cornerZ) - outDir * 0.3f;
            var mid = top + outDir * (slabL / 2 * Mathf.Cos(angle * Mathf.Deg2Rad)) + Vector3.down * (topY / 2);
            var yaw = Mathf.Atan2(outDir.x, outDir.z) * Mathf.Rad2Deg;
            var slab = Group("FallenRoof", g, mid, yaw);
            slab.localRotation *= Quaternion.Euler(angle, 0, F(-2f, 2f)); // 로컬 +z(앞)가 내리막
            Box("Slab", slab, Vector3.zero, new Vector3(slabW, 0.26f, slabL), M["dirty"], 3f);
            for (int i = 0; i < 3; i++)
                Rebar(slab, new Vector3(F(-slabW / 2 + 0.2f, slabW / 2 - 0.2f), 0.1f, -slabL / 2 + F(0f, 0.3f)), new Vector3(F(-0.5f, 0.5f), 0.6f, -1f), F(0.3f, 0.6f), r);
        }

        // 2층 안: 매트리스·책상·잔해, 아이템·좀비 자리 (온전한 쪽에)
        {
            float safeX = west ? Mathf.Lerp(bx1 + 1.2f, ix1 - 1.2f, 0.5f) : Mathf.Lerp(ix0 + 1.2f, bx0 - 1.2f, 0.5f);
            float safeZ = south ? Mathf.Lerp(bz1 + 1.2f, iz1 - 1.2f, 0.5f) : Mathf.Lerp(iz0 + 1.2f, bz0 - 1.2f, 0.5f);
            var bed = Group("Mattress", g2, new Vector3(safeX + F(-1f, 1f), 0, safeZ + F(-1f, 1f)), F(0, 360));
            Box("Pad", bed, new Vector3(0, 0.11f, 0), new Vector3(0.9f, 0.22f, 1.9f), M["blanket"], 1f);
            Spawn<ItemSpawnPoint>(points, bed.position + Vector3.up * 0.3f);
            for (int i = 0; i < 3; i++)
                Spawn<ItemSpawnPoint>(points, g2.TransformPoint(new Vector3(Mathf.Lerp(west ? bx1 : ix0, west ? ix1 : bx0, F(0.15f, 0.85f)), 0.1f, Mathf.Lerp(south ? bz1 : iz0, south ? iz1 : bz0, F(0.15f, 0.85f)))));
            for (int i = 0; i < 8; i++)
                Box("Debris", g2, new Vector3(Mathf.Lerp(ix0 + 0.5f, ix1 - 0.5f, F(0, 1)), 0.04f, Mathf.Lerp(iz0 + 0.5f, iz1 - 0.5f, F(0, 1))), new Vector3(F(0.15f, 0.7f), 0.08f, F(0.15f, 0.5f)), Pick(M["cracked"], M["dirty"], M["brick"]), 1f, false, new Vector3(F(-10, 10), F(0, 180), 0));
            SpawnUpstairsPoint(points, g2.TransformPoint(new Vector3(safeX, 0, safeZ)));
            // 바깥 모퉁이 가까이 큰 덩어리들 (터진 자리의 잔해)
            RubblePile(g2, new Vector3((bx0 + bx1) / 2, 0, (bz0 + bz1) / 2), 7, 1.4f);
        }

        // 바깥: 모퉁이 둘레의 콘크리트 잔해·그을음·휜 철근 덩어리, 인도 위 큰 조각들
        {
            var cornerOut = new Vector3(west ? ix0 - 1.5f : ix1 + 1.5f, 0, south ? iz0 - 1.5f : iz1 + 1.5f);
            RubblePile(g, cornerOut + new Vector3(F(-1f, 1f), 0, F(-1f, 1f)), 12, 1.9f);
            RubblePile(g, cornerOut + new Vector3(west ? -2.2f : 2.2f, 0, F(-2f, 2f)), 7, 1.3f);
            RubblePile(g, cornerOut + new Vector3(F(-2f, 2f), 0, south ? -2.2f : 2.2f), 7, 1.3f);
            for (int i = 0; i < 3; i++)
            {
                var p = cornerOut + new Vector3(F(-2.5f, 2.5f), 0, F(-2.5f, 2.5f));
                Rebar(g, p + Vector3.up * 0.1f, new Vector3(F(-1f, 1f), F(0.3f, 1f), F(-1f, 1f)), F(0.4f, 0.9f), r);
            }
            // 그을음: 터진 면 아래 벽과 인도
            Stain("Scorch", g, cornerOut + Vector3.up * 0.012f, F(2.5f, 3.5f), F(2f, 3f), M["scorch"], 41 + r.Next(50));
            float sootY = Floor2Y - 0.8f;
            Stain("Soot", g, new Vector3(west ? ix0 - 0.012f : ix1 + 0.012f, sootY, (bz0 + bz1) / 2), 1.4f, 2.2f, M["soot"], 91 + r.Next(50), new Vector3(0, 0, west ? 90f : -90f));
            Stain("Soot", g, new Vector3((bx0 + bx1) / 2, sootY, south ? iz0 - 0.012f : iz1 + 0.012f), 2.2f, 1.4f, M["soot"], 141 + r.Next(50), new Vector3(south ? -90f : 90f, 0, 0));
            for (int i = 0; i < 3; i++) OutdoorItemPoint(points, g.TransformPoint(cornerOut + new Vector3(F(-3f, 3f), 0.4f, F(-3f, 3f))));
            // 잔해 사이에서 가늘게 피어오르는 연기
            Smoke(g, cornerOut + new Vector3(F(-0.8f, 0.8f), 0.3f, F(-0.8f, 0.8f)));
        }
        rng = cityRng;
    }

    // 잔해에서 가늘게 오르는 회색 연기 (느리고 옅다)
    static void Smoke(Transform parent, Vector3 pos)
    {
        var psGo = new GameObject("Smoke");
        psGo.transform.SetParent(parent, false);
        psGo.transform.localPosition = pos;
        psGo.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        var ps = psGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startColor = new Color(0.3f, 0.3f, 0.3f, 0.35f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        var emission = ps.emission;
        emission.rateOverTime = 6;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 10;
        shape.radius = 0.3f;
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        vel.y = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(new Color(0.35f, 0.35f, 0.35f), 0), new GradientColorKey(new Color(0.25f, 0.25f, 0.26f), 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.35f, 0.2f), new GradientAlphaKey(0, 1) });
        col.color = grad;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2.2f));
        psGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = M["ash"];
    }

    // ---------- 건물 벽의 금·때 ----------

    // 바깥벽에 비스듬히 내려가는 금 몇 줄(검은 얇은 띠로 그린다)과 바닥 가까이의 때·그을음 얼룩.
    // (X0, Z0, W, D)는 바깥면 사각형, height는 벽 꼭대기. 난수는 자리에서 뽑아 도시 배치는 바뀌지 않는다
    static void FacadeWeathering(Transform g, float X0, float Z0, float W, float D, float height, Vector3 worldHint)
    {
        var r = new System.Random(5100 + Mathf.RoundToInt(worldHint.x * 3f) * 31 + Mathf.RoundToInt(worldHint.z * 3f) * 1009);
        float F(float a, float b) => a + (float)r.NextDouble() * (b - a);
        var cracks = new List<CombineInstance>();
        int n = 1 + r.Next(3);
        for (int c = 0; c < n; c++)
        {
            int side = r.Next(4);
            float along = F(1f, (side < 2 ? W : D) - 1f), y = height * F(0.55f, 0.95f);
            float drift = F(-0.25f, 0.25f);
            int segs = 5 + r.Next(5);
            for (int i = 0; i < segs; i++)
            {
                float segLen = F(0.25f, 0.6f);
                drift = Mathf.Clamp(drift + F(-0.35f, 0.35f), -0.8f, 0.8f);
                float dy = -segLen / Mathf.Sqrt(1 + drift * drift), da = drift * -dy;
                Vector3 p0 = FacePoint(side, along, y, X0, Z0, W, D), p1 = FacePoint(side, along + da, y + dy, X0, Z0, W, D);
                var mid = (p0 + p1) / 2;
                var dir = p1 - p0;
                var outN = side == 0 ? Vector3.back : side == 1 ? Vector3.forward : side == 2 ? Vector3.left : Vector3.right;
                var rot = Quaternion.LookRotation(outN, dir.normalized);
                float wdt = F(0.012f, 0.028f);
                cracks.Add(new CombineInstance { mesh = BoxMesh(new Vector3(wdt, dir.magnitude + wdt, 0.004f), 1f), transform = Matrix4x4.TRS(mid + outN * 0.004f, rot, Vector3.one) });
                along += da;
                y += dy;
                if (y < 0.3f) break;
            }
        }
        CombineInto(g, "Cracks", cracks, M["tire"]);
        // 바닥 가까이 때 얼룩 (벽면에 눕힌 납작한 얼룩)
        int stains = 2 + r.Next(3);
        for (int i = 0; i < stains; i++)
        {
            int side = r.Next(4);
            float along = F(1.5f, (side < 2 ? W : D) - 1.5f);
            float sy = F(0.25f, 0.6f), sx = F(0.9f, 2.2f);
            var p = FacePoint(side, along, sy * 0.9f, X0, Z0, W, D);
            var euler = side == 0 ? new Vector3(-90, 0, 0) : side == 1 ? new Vector3(90, 0, 0) : side == 2 ? new Vector3(0, 0, 90) : new Vector3(0, 0, -90);
            var outN = side == 0 ? Vector3.back : side == 1 ? Vector3.forward : side == 2 ? Vector3.left : Vector3.right;
            Stain("Grime", g, p + outN * 0.006f, side < 2 ? sx : sy, side < 2 ? sy : sx, r.NextDouble() < 0.3 ? M["scorch"] : M["soot"], 200 + r.Next(300), euler);
        }
    }

    // 바깥면 side(0 남 1 북 2 서 3 동)의 한 점 (along: 면을 따라, y: 높이)
    static Vector3 FacePoint(int side, float along, float y, float X0, float Z0, float W, float D) =>
        side == 0 ? new Vector3(X0 + along, y, Z0) : side == 1 ? new Vector3(X0 + along, y, Z0 + D) : side == 2 ? new Vector3(X0, y, Z0 + along) : new Vector3(X0 + W, y, Z0 + along);

    // ---------- 해치 주변의 황폐함 ----------

    // 방공호 해치 둘레: 달빛을 비추는 물웅덩이와 젖은 아스팔트, 쓰러진 가로등, 탄 차, 잿더미, 그을음, 흩어진 종이·벽돌
    static void HatchSurroundings(Transform g)
    {
        var r = new System.Random(2626);
        float F(float a, float b) => a + (float)r.NextDouble() * (b - a);
        // 젖은 아스팔트 (얇은 판) 위에 물웅덩이 (매끈한 검은 얼룩: 달빛과 붉은 등이 비친다)
        var wetSpots = new List<Vector3>();
        for (int i = 0; i < 7; i++)
        {
            Vector3 p;
            int tries = 0;
            do { p = new Vector3(F(-7f, 7f), 0, F(-6f, 7.5f)); tries++; }
            while (tries < 20 && (Mathf.Abs(p.x) < 2.3f && Mathf.Abs(p.z) < 2.1f));
            wetSpots.Add(p);
            float rx = F(0.7f, 1.7f), rz = F(0.6f, 1.4f);
            Box("WetPatch", g, p + new Vector3(0, 0.003f, 0), new Vector3(rx * 2.6f, 0.005f, rz * 2.4f), M["wetAsphalt"], 2f, false, new Vector3(0, F(0, 360), 0));
            Stain("Puddle", g, p + new Vector3(0, 0.012f, 0), rx, rz, M["puddle"], 600 + i * 7); // 차선 페인트보다 위 (z-fighting)
        }
        // 쓰러진 가로등 (받침이 뽑힌 채 길에 누워 있다)
        var pole = Group("FallenLamp", g, new Vector3(-4.6f, 0, 3.4f), F(0, 360));
        Prim(PrimitiveType.Cylinder, "Pole", pole, new Vector3(0, 0.09f, 0), new Vector3(0.14f, 3f, 0.14f), M["metal"], new Vector3(90, 0, 0), true);
        Box("Head", pole, new Vector3(0, 0.12f, 3.3f), new Vector3(0.3f, 0.12f, 0.5f), M["metal"], 1f, true, new Vector3(0, 0, 25));
        Box("Base", pole, new Vector3(0, 0.15f, -3.2f), new Vector3(0.5f, 0.3f, 0.5f), M["dirty"], 1f, true, new Vector3(15, 0, 0));
        // 탄 차 한 대, 모래주머니 더미 보강, 드럼통
        WreckedCarV2(g, new Vector3(6.4f, 0, -1.8f), F(55f, 75f), false, true, r);
        Box("Sandbag3", g, new Vector3(-1.9f, 0.25f, 1.3f), new Vector3(0.9f, 0.5f, 0.45f), M["cloth"], 1f, true, new Vector3(0, -20, 0));
        Box("Sandbag4", g, new Vector3(-1.95f, 0.68f, 1.25f), new Vector3(0.8f, 0.4f, 0.42f), M["cloth"], 1f, true, new Vector3(0, 12, 0));
        Prim(PrimitiveType.Cylinder, "Drum", g, new Vector3(2.9f, 0.45f, -2.6f), new Vector3(0.6f, 0.45f, 0.6f), M["paintBlue"], null, true);
        Prim(PrimitiveType.Cylinder, "DrumTipped", g, new Vector3(3.8f, 0.3f, -2.1f), new Vector3(0.6f, 0.45f, 0.6f), M["metal"], new Vector3(90, 30, 0), true);
        // 잿더미·그을음·잔해 (문 앞 길목 |x|<1.7, z 1.4~4.5는 비워 둔다: 돌아가기 연출에서 주인공이 서고 물러나는 자리)
        for (int i = 0; i < 4; i++)
        {
            var p = new Vector3(F(-6f, 6f), 0, F(-5f, 6f));
            if (Mathf.Abs(p.x) < 2.5f && Mathf.Abs(p.z) < 2.4f) continue;
            if (Mathf.Abs(p.x) < 1.7f && p.z > 1.4f && p.z < 4.5f) continue;
            Prim(PrimitiveType.Sphere, "AshPile", g, p + new Vector3(0, 0.04f, 0), new Vector3(F(0.8f, 1.4f), F(0.18f, 0.3f), F(0.7f, 1.2f)), M["ashPile"], new Vector3(0, F(0, 360), 0));
            if (i < 2) Stain("Scorch", g, p + new Vector3(F(-0.5f, 0.5f), 0.011f, F(-0.5f, 0.5f)), F(1f, 1.8f), F(0.9f, 1.5f), M["scorch"], 700 + i);
        }
        RubblePile(g, new Vector3(2.6f, 0, 3.6f), 6, 1.1f);
        RubblePile(g, new Vector3(-3.4f, 0, -3.4f), 5, 0.9f);
        // 흩어진 종이와 벽돌
        for (int i = 0; i < 8; i++)
        {
            var p = new Vector3(F(-6.5f, 6.5f), 0.0095f, F(-5.5f, 7f)); // 종이는 차선 페인트보다 위 (z-fighting)
            if (Mathf.Abs(p.x) < 2.3f && Mathf.Abs(p.z) < 2.2f) continue;
            Box("Paper", g, p, new Vector3(F(0.2f, 0.3f), 0.004f, F(0.25f, 0.4f)), M["cloth"], 1f, false, new Vector3(0, F(0, 360), 0));
        }
        for (int i = 0; i < 6; i++)
        {
            var p = new Vector3(F(-6f, 6f), 0.035f, F(-5f, 6.5f));
            if (Mathf.Abs(p.x) < 2.3f && Mathf.Abs(p.z) < 2.2f) continue;
            Box("Brick", g, p, new Vector3(0.2f, 0.07f, 0.095f), M["brick"], 1f, false, new Vector3(0, F(0, 360), 0));
        }
        // 벙커 벽의 그을음·녹물 (문 위와 환기구 아래)
        Stain("Soot", g, new Vector3(0, 2.35f, 1.5f + 0.012f), 0.9f, 0.3f, M["soot"], 710, new Vector3(-90, 0, 0));
        Stain("Soot", g, new Vector3(-1.1f, 1.2f, 1.5f + 0.012f), 0.45f, 0.7f, M["soot"], 711, new Vector3(-90, 0, 0));
        Box("RustStreak", g, new Vector3(0.9f, 2.05f, -1.5f - 0.006f), new Vector3(0.12f, 1.1f, 0.004f), M["metal"], 1f, false);
    }

    // 폭격 건물 위층 창문: 터져 나간 모퉁이(bx0~bx1 × bz0~bz1)에는 벽이 없으니 창문도 내지 않는다
    // (처음 버전은 FacadeWindows가 네 면을 다 돌아 창문이 허공에 떠 있었다). 나머지 로직은 FacadeWindows와 같다
    static void BombedFacadeWindows(Transform g, float x0, float z0, float x1, float z1, float baseY, int floors, float bx0, float bx1, float bz0, float bz1, bool west, bool south)
    {
        var parts = new List<CombineInstance>();
        var win = BoxMesh(new Vector3(1.1f, 1.5f, 0.06f), 1f);
        float w = x1 - x0, d = z1 - z0;
        for (int f = 0; f < floors; f++)
        {
            float y = baseY + f * 3.2f + 1.7f;
            for (int side = 0; side < 4; side++)
            {
                float len = side < 2 ? w : d;
                int cols = Mathf.Max(1, Mathf.FloorToInt(len / 3f));
                for (int i = 0; i < cols; i++)
                {
                    if (Chance(0.08)) continue;
                    float u = (i + 0.5f) * len / cols;
                    Vector3 p; Quaternion q;
                    if (side == 0) { p = new Vector3(x0 + u, y, z0 - 0.02f); q = Quaternion.identity; }
                    else if (side == 1) { p = new Vector3(x0 + u, y, z1 + 0.02f); q = Quaternion.identity; }
                    else if (side == 2) { p = new Vector3(x0 - 0.02f, y, z0 + u); q = Quaternion.Euler(0, 90, 0); }
                    else { p = new Vector3(x1 + 0.02f, y, z0 + u); q = Quaternion.Euler(0, 90, 0); }
                    // 바깥 면 중 터진 모퉁이에 걸리는 창문(폭 1.1m의 어느 부분이라도 무너진 범위에 들면)은 뺀다
                    bool outerZ = side == (south ? 0 : 1), outerX = side == (west ? 2 : 3);
                    if (outerZ && p.x + 0.6f > bx0 && p.x - 0.6f < bx1) continue;
                    if (outerX && p.z + 0.6f > bz0 && p.z - 0.6f < bz1) continue;
                    parts.Add(new CombineInstance { mesh = win, transform = Matrix4x4.TRS(p, q, Vector3.one) });
                }
            }
        }
        CombineInto(g, "Windows", parts, M["glass"]);
    }

    // ---------- 해치 벙커 ----------
    // 문 크기(1.1×2.1m)만큼 속이 빈 콘크리트 덩어리. 겉모습은 예전의 꽉 찬 3.2×2.6×3 상자와 같지만, 문이 열리면 안쪽의 어두운 통로가 보이고
    // 돌아가기 연출의 주인공이 그 안으로 걸어 들어간다 (예전에는 꽉 찬 상자라 문을 열어도 바로 벽이었다)
    const float HatchTunnelDepth = 2.7f; // 벙커 앞면(z=1.5)에서 막다른 벽까지

    static void HatchShell(Transform g)
    {
        // 바깥 껍데기: 좌·우 덩어리, 문 위 상인방, 뒷벽
        Box("ShellL", g, new Vector3(-1.075f, 1.3f, 0), new Vector3(1.05f, 2.6f, 3f), M["dirty"], 2f);
        Box("ShellR", g, new Vector3(1.075f, 1.3f, 0), new Vector3(1.05f, 2.6f, 3f), M["dirty"], 2f);
        Box("ShellTop", g, new Vector3(0, 2.35f, 0), new Vector3(1.1f, 0.5f, 3f), M["dirty"], 2f);
        Box("ShellBack", g, new Vector3(0, 1.05f, -1.35f), new Vector3(1.1f, 2.1f, 0.3f), M["dirty"], 2f);
        // 통로 안감: 그을린 어두운 콘크리트 판을 껍데기 안쪽에 붙인다 (바닥·양 벽·천장·막다른 벽)
        var dark = M["tunnel"];
        float zc = 1.5f - HatchTunnelDepth / 2;
        Box("TunnelFloor", g, new Vector3(0, 0.012f, zc), new Vector3(1.1f, 0.024f, HatchTunnelDepth), dark, 1.5f, false);
        Box("TunnelWallL", g, new Vector3(-0.544f, 1.05f, zc), new Vector3(0.012f, 2.1f, HatchTunnelDepth), dark, 1.5f, false);
        Box("TunnelWallR", g, new Vector3(0.544f, 1.05f, zc), new Vector3(0.012f, 2.1f, HatchTunnelDepth), dark, 1.5f, false);
        Box("TunnelCeil", g, new Vector3(0, 2.094f, zc), new Vector3(1.1f, 0.012f, HatchTunnelDepth), dark, 1.5f, false);
        Box("TunnelEnd", g, new Vector3(0, 1.05f, 1.5f - HatchTunnelDepth + 0.006f), new Vector3(1.1f, 2.1f, 0.012f), dark, 1.5f, false);
        // 천장 모서리의 배관, 바닥의 젖은 자국, 희미하게 깜빡이는 비상등 (어둡되 통로 윤곽은 보이게)
        float pipeLen = HatchTunnelDepth - 0.4f; // 문턱에서 0.4m 안쪽부터 (끝면이 문 앞에서 하얀 점으로 보이지 않게)
        Prim(PrimitiveType.Cylinder, "TunnelPipe", g, new Vector3(0.42f, 1.98f, zc - 0.2f), new Vector3(0.07f, pipeLen / 2, 0.07f), M["metal"], new Vector3(90, 0, 0));
        Stain("TunnelPuddle", g, new Vector3(-0.1f, 0.03f, zc - 0.3f), 0.35f, 0.5f, M["puddle"], 730);
        var l = PointLight("TunnelLight", g, new Vector3(0, 1.95f, 1.5f - HatchTunnelDepth + 0.6f), new Color(0.55f, 0.65f, 0.8f), 0.6f, 2.8f);
        Flicker(l, 0.25f, 5f, false);
    }

    // 웅덩이 재질: 물결·발자국 물결·빗방울·달빛 반사가 있는 전용 셰이더 (Resources/Shaders/Puddle.shader, 런타임은 PuddleFx.cs).
    // 셰이더가 없으면(임포트 전) 예전처럼 매끈한 Lit로 둔다
    static Material PuddleMat()
    {
        var shader = Shader.Find("BunkerLab/Puddle");
        var m = LoadOrCreate("M_Puddle", shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", new Color(0.1f, 0.11f, 0.13f, shader != null ? 0.85f : 1f)); // 살짝 투명: 젖은 바닥이 비친다
        m.SetFloat("_Smoothness", 0.95f);
        if (shader != null)
        {
            // 잔물결은 아주 잔잔하게 (크면 반사가 구겨진 은박처럼 깨진다)
            m.SetFloat("_WaveAmp", 0.0025f);
            m.SetFloat("_RippleAmp", 0.12f);
            m.SetFloat("_RainAmp", 0.025f);
            m.SetFloat("_SkyBoost", 1.2f);
            m.SetFloat("_EdgeFade", 0.3f);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- 메뉴: 방공호 씬만 다시 만든다 (발전기·미닫이 문을 고칠 때. 도시·메뉴 씬은 그대로) ----------

    [MenuItem("Tools/핵폭발 생존/방공호 씬만 생성")]
    static void BuildShelterOnlyMenu()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[GameSceneBuilder] 플레이 모드에서는 씬을 만들지 않습니다.");
            return;
        }
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Player.prefab");
        var zombie = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Zombie.prefab");
        if (player == null || zombie == null)
        {
            Debug.LogWarning("[GameSceneBuilder] 프리팹이 없습니다. 게임 씬 전체 생성을 먼저 하세요.");
            return;
        }
        CreateMaterials();
        BuildShelterScene(player, zombie);
        AssetDatabase.SaveAssets();
        Debug.Log("[GameSceneBuilder] Shelter 씬만 다시 생성 완료");
    }

    // ---------- 메뉴: 도시 씬만 다시 만든다 ----------

    [MenuItem("Tools/핵폭발 생존/도시 씬만 생성")]
    static void BuildCityOnlyMenu()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[GameSceneBuilder] 플레이 모드에서는 씬을 만들지 않습니다.");
            return;
        }
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Player.prefab");
        var zombie = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Zombie.prefab");
        var dog = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Dog.prefab");
        if (player == null || zombie == null || dog == null)
        {
            Debug.LogWarning("[GameSceneBuilder] 프리팹이 없습니다. 게임 씬 전체 생성을 먼저 하세요.");
            return;
        }
        CreateMaterials();
        BuildCityScene(player, zombie, dog);
        AssetDatabase.SaveAssets();
        Debug.Log("[GameSceneBuilder] City 씬만 다시 생성 완료");
    }
}
