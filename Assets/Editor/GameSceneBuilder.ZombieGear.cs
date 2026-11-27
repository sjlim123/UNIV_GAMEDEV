using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 가죽 감염자(감염된 군인)의 떨어지는 장비 (2026-11-27, 사용자 "몸과 갑옷을 따로", "갑바 정도의 군복").
// Tripo 장비 4종(방탄조끼·전투모·어깨 보호대·무릎 보호대, Assets/ThirdParty/Tripo/Enemies/soldier_gear/)을 몸 a·b마다 그 몸에 맞춰 붙인다.
//  1) 몸의 쉬는 자세(T자세) 30만 스킨 메시에서 뼈 가중치로 몸통·머리·위팔·무릎 둘레를 재고 (몸 기준 축: 위 U, 왼쪽 L, 앞 F)
//  2) 장비 메시를 그 둘레보다 조금 크게 축마다 늘여 맞추고 (어깨 보호대는 팔을 따라 눕히고, 오른쪽은 거울로 뒤집는다)
//  3) 뼈(Chest·Head·위팔·아래다리) 아래에 '갑옷 조각'으로 붙인다 — 이름 ChestArmor·HelmetArmor·ShoulderArmor·KneeArmor (Zombie.LoseArmor가 하나씩 떨어뜨린다)
//  4) 조각마다 품질 4단계(TtSimplifier로 줄임, 텍스처 128·256·512·1024)를 LOD 묶음 + QualityLod로
// 장비 모양은 Tripo 그대로라 Tripo가 정한 방향을 쓴다: 앞 = 몸 HD 메시와 같은 앞, 위 = 같은 위, 어깨 보호대의 바깥 껍데기 = 왼쪽(+L)
public static partial class GameSceneBuilder
{
    const string ZgGearDir = "Assets/ThirdParty/Tripo/Enemies/soldier_gear/";
    static readonly int[] ZgTex = { 128, 256, 512, 1024 };
    // 품질별 목표 삼각형 {낮음, 중간, 높음, 매우 높음} (원본 5.6만~5.8만)
    static readonly Dictionary<string, int[]> ZgTargets = new Dictionary<string, int[]>
    {
        { "vest",     new[] { 700, 2000, 5000, 10000 } },
        { "helmet",   new[] { 400, 1200, 3000, 6000 } },
        { "shoulder", new[] { 300, 900, 2000, 4000 } },
        { "knee",     new[] { 300, 900, 2000, 4000 } },
    };

    // 축 기준 좌표 (L, U, F)
    struct ZgBasis
    {
        public Vector3 o, L, U, F;
        public Vector3 To(Vector3 p) { var d = p - o; return new Vector3(Vector3.Dot(d, L), Vector3.Dot(d, U), Vector3.Dot(d, F)); }
        public Vector3 From(Vector3 c) => o + L * c.x + U * c.y + F * c.z;
        public Vector3 DirTo(Vector3 d) => new Vector3(Vector3.Dot(d, L), Vector3.Dot(d, U), Vector3.Dot(d, F));
        public Vector3 DirFrom(Vector3 c) => L * c.x + U * c.y + F * c.z;
    }

    public static string ZgBuild(string body, string prefabPath)
    {
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        var log = new List<string>();
        try
        {
            SkinnedMeshRenderer smr = null;
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.name.EndsWith("_q3")) smr = r;
            if (smr == null) return "q3 스킨 메시 없음";
            var mesh = smr.sharedMesh;
            var vs = mesh.vertices; var bw = mesh.boneWeights; var bones = smr.bones;
            var bone = new Dictionary<string, Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (!bone.ContainsKey(t.name)) bone[t.name] = t;
            Vector3 P(string n) => smr.transform.InverseTransformPoint(bone[n].position);

            // 몸 축
            var B = new ZgBasis();
            B.o = P("Hips");
            B.U = (P("Head") - P("Hips")).normalized;
            B.L = Vector3.ProjectOnPlane(P("Left_UpperArm") - P("Right_UpperArm"), B.U).normalized;
            var eyes = (P("Left_Eye") + P("Right_Eye")) * 0.5f;
            B.F = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(eyes - P("Head"), B.U), B.L).normalized;
            float umin = float.MaxValue, umax = float.MinValue;
            var dom = new string[vs.Length];
            var cs = new Vector3[vs.Length];
            for (int i = 0; i < vs.Length; i++)
            {
                cs[i] = B.To(vs[i]); umin = Mathf.Min(umin, cs[i].y); umax = Mathf.Max(umax, cs[i].y);
                var w = bw[i]; int bi = w.boneIndex0; float wm = w.weight0;
                if (w.weight1 > wm) { wm = w.weight1; bi = w.boneIndex1; }
                if (w.weight2 > wm) { wm = w.weight2; bi = w.boneIndex2; }
                if (w.weight3 > wm) { bi = w.boneIndex3; }
                dom[i] = bones[bi] != null ? bones[bi].name : "";
            }
            float H = umax - umin;
            log.Add($"키 {H:F3}");

            Bounds Region(System.Func<int, bool> pick)
            {
                bool any = false; var b = new Bounds();
                for (int i = 0; i < cs.Length; i++)
                    if (pick(i)) { if (!any) { b = new Bounds(cs[i], Vector3.zero); any = true; } else b.Encapsulate(cs[i]); }
                return b;
            }
            var hipsC = B.To(P("Hips")); var neckC = B.To(P("Neck")); var headC = B.To(P("Head")); var eyeC = B.To(eyes);

            // 몸통 (방탄조끼)
            var torso = Region(i => dom[i] == "Spine" || dom[i] == "Chest" || dom[i] == "UpperChest");
            float vTop = neckC.y + 0.01f * H, vBot = torso.min.y + 0.22f * (torso.size.y);
            var vestT = new Bounds(new Vector3(torso.center.x, (vTop + vBot) * 0.5f, torso.center.z + 0.04f * torso.size.z),
                                   new Vector3(torso.size.x * 1.10f, vTop - vBot, torso.size.z * 1.30f));
            // 머리 (전투모): 정수리 위 1%, 너비는 머리보다 22% 크게
            var head = Region(i => dom[i] == "Head" && cs[i].y > neckC.y + 0.02f * H);
            // 무릎
            Bounds Knee(string side)
            {
                var k = B.To(P(side + "_LowerLeg"));
                return Region(i => (dom[i] == side + "_UpperLeg" || dom[i] == side + "_LowerLeg") && Mathf.Abs(cs[i].y - k.y) < 0.045f * H);
            }
            // 위팔 (어깨 보호대): 어깨 관절에서 팔 쪽으로 키의 13%까지
            Bounds Arm(string side, out Vector3 sh)
            {
                var s = B.To(P(side + "_UpperArm")); sh = s; float sg = side == "Left" ? 1 : -1;
                return Region(i => (dom[i] == side + "_UpperArm" || dom[i] == side + "_Shoulder") && (cs[i].x - s.x) * sg > -0.01f * H && (cs[i].x - s.x) * sg < 0.13f * H);
            }

            
            var made = new List<string>();
            // 조각: 이름, 장비, 붙일 뼈, 목표 상자, 축 바꾸기(장비 좌표 → 몸 좌표), 거울
            void Piece(string armor, string gear, string boneName, Bounds target, System.Func<Vector3, Vector3> remap, bool mirror, string tag)
            {
                var res = ZgFitPiece(gear, smr, B, target, remap, mirror, bone[boneName], body, tag);
                var holder = new GameObject(armor).transform;
                holder.SetParent(bone[boneName], false);
                var lods = new LOD[4]; var tiers = new Renderer[4];
                for (int l = 0; l < 4; l++)
                {
                    int q = 3 - l;
                    var go = new GameObject("q" + q);
                    go.transform.SetParent(holder, false);
                    go.AddComponent<MeshFilter>().sharedMesh = res.meshes[q];
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = res.mats[q];
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    lods[l] = new LOD(TripoLodHeights[l], new Renderer[] { mr });
                    tiers[q] = mr;
                }
                var lg = holder.gameObject.AddComponent<LODGroup>(); lg.fadeMode = LODFadeMode.None; lg.SetLODs(lods); lg.RecalculateBounds();
                var ql = holder.gameObject.AddComponent<QualityLod>(); ql.tiers = tiers; ql.heights = (float[])TripoLodHeights.Clone();
                made.Add($"{armor}({boneName}) {res.info}");
            }

            // 기존 장비 지우기 (다시 만들 때)
            foreach (var n in new[] { "ChestArmor", "HelmetArmor", "ShoulderArmor", "KneeArmor" })
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t != null && t.name == n) Object.DestroyImmediate(t.gameObject);

            System.Func<Vector3, Vector3> same = g => g;
            Piece("ChestArmor", "vest", "Chest", vestT, same, false, "vest");

            float hw = Mathf.Max(head.size.x, head.size.z);
            float hTop = head.max.y + 0.012f * H;
            // 전투모 높이는 끈까지 포함한 모델 높이를 너비 배율로 같이 줄인다 (끈이 턱 아래로 늘어지게): 높이 = 너비 비율 그대로 → ZgFitPiece에서 keepY
            var helmT = new Bounds(new Vector3(head.center.x, hTop, head.center.z - 0.02f * head.size.z), new Vector3(head.size.x * 1.24f, 0f, head.size.z * 1.26f));
            Piece("HelmetArmor", "helmet", "Head", helmT, same, false, "helmet");

            foreach (var side in new[] { "Left", "Right" })
            {
                var arm = Arm(side, out var sh); float sg = side == "Left" ? 1 : -1;
                float len = 0.13f * H;
                var shT = new Bounds(new Vector3(sh.x + sg * len * 0.45f, arm.center.y + arm.size.y * 0.18f, arm.center.z),
                                     new Vector3(len, arm.size.y * 1.05f, arm.size.z * 1.20f));
                // 장비 좌표 (L=바깥 껍데기 쪽, U=끈 쪽 위, F=앞) → 몸 좌표: 팔을 따라 눕힌다 (끈이 어깨 쪽, 껍데기가 위)
                System.Func<Vector3, Vector3> rm = side == "Left" ? (System.Func<Vector3, Vector3>)(g => new Vector3(-g.y, g.x, g.z)) : (g => new Vector3(g.y, g.x, g.z));
                Piece("ShoulderArmor", "shoulder", side + "_UpperArm", shT, rm, side == "Right", "shoulder" + side[0]);

                var kn = Knee(side); var kc = B.To(P(side + "_LowerLeg"));
                var knT = new Bounds(new Vector3(kn.center.x, kc.y - 0.006f * H, kn.center.z + 0.06f * kn.size.z),
                                     new Vector3(kn.size.x * 1.08f, 0.105f * H, kn.size.z * 1.12f));
                Piece("KneeArmor", "knee", side + "_LowerLeg", knT, same, false, "knee" + side[0]);
            }
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            AssetDatabase.SaveAssets();
            log.AddRange(made);
            return string.Join(" · ", log);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static string ZgBase(string body) => "Assets/ThirdParty/Tripo/Enemies/" + body + "/";

    class ZgResult { public Mesh[] meshes = new Mesh[4]; public Material[] mats = new Material[4]; public string info; }

    // 장비 하나를 목표 상자에 맞춰 뼈 좌표 메시 4단계로 (target.size.y == 0이면 높이는 너비 배율의 평균으로)
    static ZgResult ZgFitPiece(string gear, SkinnedMeshRenderer smr, ZgBasis B, Bounds target, System.Func<Vector3, Vector3> remap, bool mirror, Transform boneT, string body, string tag)
    {
        var res = new ZgResult();
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ZgGearDir}{gear}/gear_{gear}.fbx");
        var mf = asset.GetComponentInChildren<MeshFilter>();
        var src = mf.sharedMesh;
        var pv = src.vertices; var pn = src.normals; var uv = src.uv; var tri = src.triangles;
        // 장비 메시 → 몸 스킨 메시 좌표 (몸 HD 메시와 같은 Tripo 방향) → 몸 축 좌표 → 축 바꾸기
        var toSmr = smr.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        var g = new Vector3[pv.Length]; var gn = new Vector3[pv.Length];
        var gb = new Bounds();
        for (int i = 0; i < pv.Length; i++)
        {
            g[i] = remap(B.DirTo(toSmr.MultiplyVector(pv[i])));
            gn[i] = remap(B.DirTo(toSmr.MultiplyVector(pn[i]))).normalized;
            if (i == 0) gb = new Bounds(g[i], Vector3.zero); else gb.Encapsulate(g[i]);
        }
        var s = new Vector3(target.size.x / gb.size.x, target.size.y / gb.size.y, target.size.z / gb.size.z);
        Vector3 center = target.center;
        if (target.size.y <= 0f)
        {
            s.y = (s.x + s.z) * 0.5f;
            center.y = target.center.y - gb.size.y * s.y * 0.5f; // target.center.y = 위 끝
        }
        // 맞춘 꼭짓점 (뼈 좌표)
        var pos = new Vector3[pv.Length]; var nrm = new Vector3[pv.Length];
        var toBone = boneT.worldToLocalMatrix * smr.transform.localToWorldMatrix;
        for (int i = 0; i < pv.Length; i++)
        {
            var c = center + Vector3.Scale(g[i] - gb.center, s);
            pos[i] = toBone.MultiplyPoint3x4(B.From(c));
            var cn = new Vector3(gn[i].x / s.x, gn[i].y / s.y, gn[i].z / s.z); // 늘인 만큼 법선은 반대로
            nrm[i] = toBone.MultiplyVector(B.DirFrom(cn)).normalized;
        }
        // 좌표 바꾸기의 행렬식이 음수면(거울) 면 방향을 뒤집는다
        if (mirror) for (int t = 0; t < tri.Length; t += 3) { int a = tri[t + 1]; tri[t + 1] = tri[t + 2]; tri[t + 2] = a; }

        var targets = ZgTargets[gear];
        var texSrc = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        texSrc.LoadImage(File.ReadAllBytes($"{ZgGearDir}{gear}/gear_{gear}_basecolor.jpg"));
        var outDir = ZgBase(body) + "_장비";
        if (!AssetDatabase.IsValidFolder(outDir)) AssetDatabase.CreateFolder(ZgBase(body).TrimEnd('/'), "_장비");
        var counts = new List<string>();
        for (int q = 0; q < 4; q++)
        {
            var simp = new TtSimplifier(pos, uv, tri, false, null);
            simp.Run(targets[q]);
            var idx = simp.Triangles();
            var remapIdx = new Dictionary<int, int>(); var p2 = new List<Vector3>(); var n2 = new List<Vector3>(); var u2 = new List<Vector2>();
            var t2 = new int[idx.Length];
            for (int i = 0; i < idx.Length; i++)
            {
                if (!remapIdx.TryGetValue(idx[i], out int w))
                {
                    w = p2.Count; remapIdx[idx[i]] = w;
                    p2.Add(pos[idx[i]]); n2.Add(simp.Normal(idx[i], nrm[idx[i]])); u2.Add(uv[idx[i]]);
                }
                t2[i] = w;
            }
            var m = new Mesh { name = $"{body}_{tag}_q{q}", indexFormat = p2.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(p2); m.SetNormals(n2); m.SetUVs(0, u2); m.SetTriangles(t2, 0);
            m.RecalculateBounds(); m.RecalculateTangents();
            string path = $"{outDir}/{body}_{tag}_q{q}.asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null) { PaOverwriteMesh(m, old); Object.DestroyImmediate(m); m = old; }
            else AssetDatabase.CreateAsset(m, path);
            res.meshes[q] = m;
            res.mats[q] = ZgMat(gear, q, texSrc);
            counts.Add((t2.Length / 3).ToString("N0"));
        }
        Object.DestroyImmediate(texSrc);
        res.info = $"배율 {s.x:F2}·{s.y:F2}·{s.z:F2}, 삼각형 {string.Join("·", counts)}";
        return res;
    }

    // 장비 재질 (몸 a·b가 함께 쓴다): 품질별 텍스처를 4K 원본에서 줄여 만든다
    static Material ZgMat(string gear, int q, Texture2D src)
    {
        string dir = $"{ZgGearDir}{gear}";
        string path = $"{dir}/M_gear_{gear}_q{q}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        string texPath = $"{dir}/gear_{gear}_q{q}_diff.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) == null)
        {
            int size = ZgTex[q];
            int mip = Mathf.Max(0, Mathf.RoundToInt(Mathf.Log(src.width / (float)size, 2f)));
            int mw = Mathf.Max(1, src.width >> mip), mh = Mathf.Max(1, src.height >> mip);
            var t = new Texture2D(mw, mh, TextureFormat.RGBA32, false); t.SetPixels32(src.GetPixels32(mip)); t.Apply();
            File.WriteAllBytes(texPath, t.EncodeToPNG()); Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            ti.maxTextureSize = size; ti.sRGBTexture = true; ti.mipmapEnabled = true; ti.textureCompression = TextureImporterCompression.Compressed;
            ti.alphaSource = TextureImporterAlphaSource.None; ti.mipmapLimitGroupName = q == 3 ? HighResGroup : "";
            ti.SaveAndReimport();
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetTexture("_BaseMap", tex); mat.SetTexture("_MainTex", tex);
        mat.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.85f)); mat.SetFloat("_Smoothness", 0.2f); mat.SetFloat("_Metallic", 0.05f);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
