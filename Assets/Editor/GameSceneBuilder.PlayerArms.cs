using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 1인칭 팔 (2026-11-24, 사용자 "통과, 1인칭 팔로"): 플레이어 모델(Tripo)의 아래팔·장갑 손을 잘라 HandRig 뼈대에 묶는다.
// - 손 크기: 손목에서 가운데 손가락 뿌리까지가 예전 1인칭 손과 같게 (0.091 m) 줄인다. 그래야 쥐는 물건 자리·손가락 감기가 그대로 맞는다
// - 뼈: 0 팔꿈치(아래팔·위팔·어깨 가중치), 1 손목(손), 2.. 손가락 다섯 × 세 마디
// - 묶는 자세: 모델 T자세 그대로(손가락이 조금 벌어지고 굽어 있다). HandRig의 '곧게 편 자세'(rootRest)는 손가락을 앞으로 나란히 둔 것이라
//   실행 중에 손가락 각도 0이면 모델 손가락이 나란히 펴진다 → 예전에 맞춘 쥐기·주먹 자세가 그대로 통한다
// - 텍스처: 팔 조각(UV 섬)만 원본 4K에서 오려 작은 아틀라스로 모은다 (팔이 원본 UV 곳곳에 흩어져 있다).
//   높음·매우 높음 = 30만 메시 + 2048 아틀라스(+노멀), 낮음·중간 = 9.5만 메시 + 1024 아틀라스
// 결과: Assets/Resources/Player/player_fp_arms.asset (PlayerArmsData) + player_survivor_02/_1인칭팔/
public static partial class GameSceneBuilder
{
    const string PaOut = "Assets/Resources/Player/player_fp_arms.asset";
    const string PaDir = PmDir + "_1인칭팔";
    const float PaMcp = 0.091f;      // 손목 → 가운데 손가락 뿌리 (예전 1인칭 손, HandRig zs[1])
    const float PaBehind = 0.45f;    // 손목 뒤로 이만큼까지 소매를 남긴다 (HandRig 팔꿈치 0.27 뒤 + 위팔 조금: 찌르기·주먹으로 팔을 뻗어도 잘린 끝이 화면에 안 보이게)
    static readonly string[] PaFingers = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
    static readonly string[] PaSegs = { "Proximal", "Intermediate", "Distal" };

    [MenuItem("Tools/핵폭발 생존/플레이어 모델: 1인칭 팔 만들기")]
    static void PaBuildMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[GameSceneBuilder] 플레이 중에는 하지 않는다"); return; }
        Debug.Log("[GameSceneBuilder] 1인칭 팔: " + PaBuild());
    }

    // 한쪽 팔을 손목 좌표로 옮기는 규칙
    class PaSide
    {
        public float Side;          // -1 왼손, 1 오른손
        public string P;            // "Left_" / "Right_"
        public Vector3 Hand;        // 모델 손목 (바인드, 모델 좌표)
        public Quaternion R;        // 모델 → 손목 좌표 회전
        public float S;             // 크기
        public Vector3[] J = new Vector3[20];   // 손가락 f의 마디 끝점 4개 (손목 좌표): J[f*4 + k]
        public Vector3 ToWrist(Vector3 p) => R * (p - Hand) * S;
    }

    public static string PaBuild()
    {
        var rigAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PmRig);
        var hiMesh = AssetDatabase.LoadAssetAtPath<Mesh>(PmDir + "player_survivor_02_q3_skinned.asset");
        if (rigAsset == null || hiMesh == null) return "리깅 FBX 또는 30만 스킨 메시 없음";
        var log = new List<string>();
        if (!AssetDatabase.IsValidFolder(PaDir)) AssetDatabase.CreateFolder(PmDir.TrimEnd('/'), "_1인칭팔");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Player")) AssetDatabase.CreateFolder("Assets/Resources", "Player");

        var inst = Object.Instantiate(rigAsset);
        try
        {
            var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>();
            var bones = smr.bones; var names = new string[bones.Length];
            for (int i = 0; i < bones.Length; i++) names[i] = bones[i].name;
            System.Func<string, Vector3> B = n => { int i = System.Array.IndexOf(names, n); return i >= 0 ? bones[i].position : Vector3.zero; };
            var loMesh = smr.sharedMesh;

            var sides = new PaSide[2];
            var data = ScriptableObject.CreateInstance<PlayerArmsData>();
            data.name = "player_fp_arms";
            for (int s = 0; s < 2; s++)
            {
                var sd = new PaSide { Side = s == 0 ? -1f : 1f, P = s == 0 ? "Left_" : "Right_" };
                sd.Hand = B(sd.P + "Hand");
                var F = (B(sd.P + "MiddleProximal") - sd.Hand).normalized;
                // 손 위(+y, 엄지 쪽)는 손가락 뿌리 줄(새끼 → 검지)에서 잡는다. 엄지 끝으로 잡으면 T자세 엄지가 손바닥 쪽으로 처져 있어
                // 손 전체가 45° 가까이 돌아간다 (손가락이 대각선으로 굽고 손바닥 두께가 부풀어 재진다, 검토 11-24)
                var T = Vector3.ProjectOnPlane(B(sd.P + "IndexProximal") - B(sd.P + "PinkyProximal"), F).normalized;
                sd.R = Quaternion.Inverse(Quaternion.LookRotation(F, T));
                sd.S = PaMcp / Vector3.Dot(B(sd.P + "MiddleProximal") - sd.Hand, F);
                for (int f = 0; f < 5; f++)
                {
                    string fn = sd.P + PaFingers[f];
                    sd.J[f * 4 + 0] = sd.ToWrist(B(fn + "Proximal"));
                    sd.J[f * 4 + 1] = sd.ToWrist(B(fn + "Intermediate"));
                    sd.J[f * 4 + 2] = sd.ToWrist(B(fn + "Distal"));
                    sd.J[f * 4 + 3] = sd.ToWrist(B(fn + "DistalEnd"));
                }
                sides[s] = sd;
                var arm = s == 0 ? data.left : data.right;
                for (int f = 0; f < 5; f++)
                {
                    arm.rootPos[f] = sd.J[f * 4];
                    for (int k = 0; k < 3; k++) arm.lengths[f * 3 + k] = (sd.J[f * 4 + k + 1] - sd.J[f * 4 + k]).magnitude;
                    arm.rootRest[f] = f == 0 ? PaFrame(HandRigThumbDir(sd.Side), HandRigThumbFlex(sd.Side), sd.Side) : Quaternion.identity;
                }
                log.Add($"{(s == 0 ? "왼" : "오른")}손 크기 ×{sd.S:0.000}");
            }

            // 메시 두 판
            var hiSrc = PmLoadTex(PmDir + "player_survivor_02_tex_basecolor.jpg");
            var hiNrm = PmLoadTex(PmDir + "player_survivor_02_tex_normal.jpg");
            var loSrc = PmLoadTex(PmDir + "_리깅5만/player_survivor_02_rig50k_basecolor.jpg");
            for (int tier = 0; tier < 2; tier++)
            {
                var src = tier == 0 ? hiMesh : loMesh;
                string tname = tier == 0 ? "hi" : "lo";
                var meshToWorld = PaMeshToWorld(src, bones, names);
                var cut = new Mesh[2];
                var uvLists = new List<Vector2>[2];
                for (int s = 0; s < 2; s++)
                {
                    cut[s] = PaCut(src, meshToWorld, names, sides[s], s == 0 ? data.left : data.right, out uvLists[s]);
                    cut[s].name = $"player_fp_arm_{(s == 0 ? "L" : "R")}_{tname}";
                }
                // 두 팔 UV를 한 아틀라스로
                int atlas = tier == 0 ? 2048 : 1024;
                var srcTex = tier == 0 ? hiSrc : loSrc;
                var packed = PaPack(cut, uvLists, srcTex.width, srcTex.height, atlas, out float k);
                var baseTex = PaBake(srcTex, packed, atlas, false, k);
                string bpath = $"{PaDir}/player_fp_arms_{tname}_diff.png";
                var bt = PaSaveTex(baseTex, bpath, atlas, false, tier == 0);
                Texture2D nt = null;
                if (tier == 0)
                {
                    var nrm = PaBake(hiNrm, packed, atlas, true, k);
                    nt = PaSaveTex(nrm, $"{PaDir}/player_fp_arms_{tname}_nrm.png", atlas, true, true);
                }
                var mat = PaMat($"{PaDir}/M_player_fp_arms_{tname}.mat", bt, nt);
                for (int s = 0; s < 2; s++)
                {
                    string mname = cut[s].name;
                    var saved = PaSaveMesh(cut[s], $"{PaDir}/{mname}.asset"); // 이미 있으면 그 자산에 덮어쓰고 cut[s]는 지운다
                    var arm = s == 0 ? data.left : data.right;
                    if (tier == 0) arm.hi = saved; else arm.lo = saved;
                    log.Add($"{mname} 삼각형 {saved.triangles.Length / 3:N0}");
                }
                if (tier == 0) data.hiMat = mat; else data.loMat = mat;
                log.Add($"{tname} 아틀라스 {atlas} (원본 대비 ×{k:0.00})");
            }
            Object.DestroyImmediate(hiSrc); Object.DestroyImmediate(hiNrm); Object.DestroyImmediate(loSrc);

            var old = AssetDatabase.LoadAssetAtPath<PlayerArmsData>(PaOut);
            if (old != null) { EditorUtility.CopySerialized(data, old); EditorUtility.SetDirty(old); AssetDatabase.SaveAssetIfDirty(old); Object.DestroyImmediate(data); }
            else AssetDatabase.CreateAsset(data, PaOut);
        }
        finally { Object.DestroyImmediate(inst); }
        return string.Join(" / ", log);
    }

    // HandRig 엄지 기본 방향·굽힘 방향 (HandRig.BuildHand와 같은 값)
    static Vector3 HandRigThumbDir(float side) => new Vector3(-side * 0.3f, 0.62f, 0.72f).normalized;
    static Vector3 HandRigThumbFlex(float side) => new Vector3(-side * 0.55f, -0.8f, 0.1f);

    // HandRig.Frame과 같다: +z = 손가락 방향, 로컬 y축으로 -Side만큼 돌리면 flex 쪽으로 굽는다
    static Quaternion PaFrame(Vector3 dir, Vector3 flex, float side)
    {
        var x = Vector3.ProjectOnPlane(-side * flex, dir).normalized;
        return Quaternion.LookRotation(dir, Vector3.Cross(dir, x));
    }

    // 메시 좌표 → 모델(바인드) 좌표: 아무 뼈나 하나로 (bone.localToWorld × bindpose)
    static Matrix4x4 PaMeshToWorld(Mesh m, Transform[] bones, string[] names)
    {
        int h = System.Array.IndexOf(names, "Hips");
        return bones[h].localToWorldMatrix * m.bindposes[h];
    }

    // 한쪽 팔을 잘라 HandRig 뼈 17개에 묶은 메시 (팔꿈치 좌표). uvs = 새 메시의 UV (아틀라스로 옮기기 전)
    static Mesh PaCut(Mesh src, Matrix4x4 meshToWorld, string[] names, PaSide sd, PlayerArmsData.Arm arm, out List<Vector2> uvs)
    {
        // 모델 뼈 → HandRig 뼈 (-1 = 이 팔이 아님)
        var map = new int[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            string n = names[i]; map[i] = -1;
            if (!n.StartsWith(sd.P)) continue;
            string r = n.Substring(sd.P.Length);
            if (r == "UpperArm" || r == "LowerArm" || r == "Shoulder") map[i] = 0;
            else if (r == "Hand") map[i] = 1;
            else
                for (int f = 0; f < 5; f++)
                    if (r.StartsWith(PaFingers[f]))
                    {
                        int seg = r.EndsWith("Proximal") ? 0 : r.EndsWith("Intermediate") ? 1 : 2; // Distal·DistalEnd → 끝 마디
                        map[i] = 2 + f * 3 + seg;
                    }
        }
        bool[] armBone = new bool[names.Length];
        for (int i = 0; i < names.Length; i++) armBone[i] = map[i] >= 0 && !names[i].EndsWith("Shoulder");

        var v = src.vertices; var nrm = src.normals; var uv = src.uv; var bw = src.boneWeights; var tri = src.triangles;
        float L = HandRig.ForearmLength;
        var keep = new bool[v.Length]; var pos = new Vector3[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            var w = bw[i];
            float a = (armBone[w.boneIndex0] ? w.weight0 : 0) + (armBone[w.boneIndex1] ? w.weight1 : 0) + (armBone[w.boneIndex2] ? w.weight2 : 0) + (armBone[w.boneIndex3] ? w.weight3 : 0);
            var p = sd.ToWrist(meshToWorld.MultiplyPoint3x4(v[i]));
            pos[i] = p + new Vector3(0, 0, L);
            keep[i] = a >= 0.5f && p.z >= -PaBehind;
        }
        var remap = new Dictionary<int, int>(); var V = new List<Vector3>(); var N = new List<Vector3>(); var U = new List<Vector2>(); var W = new List<BoneWeight>(); var Tl = new List<int>();
        for (int t = 0; t < tri.Length; t += 3)
        {
            if (!keep[tri[t]] || !keep[tri[t + 1]] || !keep[tri[t + 2]]) continue;
            for (int j = 0; j < 3; j++)
            {
                int o = tri[t + j];
                if (!remap.TryGetValue(o, out int ni))
                {
                    ni = V.Count; remap[o] = ni;
                    V.Add(pos[o]);
                    N.Add((sd.R * meshToWorld.MultiplyVector(nrm[o])).normalized);
                    U.Add(uv[o]);
                    W.Add(PaWeight(bw[o], map));
                }
                Tl.Add(ni);
            }
        }
        // 묶는 자세: 팔꿈치 = 원점, 손목 = (0,0,L), 손가락 마디 = 모델 마디 자리·방향
        var bind = new Matrix4x4[17];
        bind[0] = Matrix4x4.identity;
        var wristM = Matrix4x4.Translate(new Vector3(0, 0, L));
        bind[1] = wristM.inverse;
        for (int f = 0; f < 5; f++)
            for (int k = 0; k < 3; k++)
            {
                Vector3 a = sd.J[f * 4 + k], b = sd.J[f * 4 + k + 1];
                var flex = f == 0 ? HandRigThumbFlex(sd.Side) : new Vector3(-sd.Side, 0, 0);
                var rot = PaFrame((b - a).normalized, flex, sd.Side);
                bind[2 + f * 3 + k] = (wristM * Matrix4x4.TRS(a, rot, Vector3.one)).inverse;
            }
        // 손바닥 겉면: 손가락 뿌리 앞(손목에서 5~8 cm), 손 가운데 줄(|y| < 1.2 cm)에서 손바닥 쪽으로 가장 나온 곳
        float palm = 0f;
        for (int i = 0; i < V.Count; i++)
        {
            var p = V[i] - new Vector3(0, 0, L);
            if (p.z < 0.05f || p.z > 0.08f || Mathf.Abs(p.y) > 0.012f) continue;
            palm = Mathf.Max(palm, -p.x * sd.Side);
        }
        if (src.vertexCount > 200000 || arm.palm <= 0f) arm.palm = palm;
        // 손가락 굵기: 끝 두 마디 꼭짓점이 손가락 줄기에서 떨어진 거리의 가운데 값
        for (int f = 0; f < 5; f++)
        {
            var d = new List<float>();
            Vector3 a = sd.J[f * 4], b = sd.J[f * 4 + 3];
            for (int i = 0; i < V.Count; i++)
            {
                var w = W[i];
                if (w.weight0 < 0.6f || (w.boneIndex0 != 2 + f * 3 + 1 && w.boneIndex0 != 2 + f * 3 + 2)) continue;
                var p = V[i] - new Vector3(0, 0, L);
                var ab = b - a; float tt = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
                d.Add((p - (a + ab * tt)).magnitude);
            }
            d.Sort();
            arm.radii[f] = d.Count > 0 ? d[d.Count / 2] : 0.0097f;
        }
        var m = new Mesh { indexFormat = V.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, U); m.SetTriangles(Tl, 0);
        m.boneWeights = W.ToArray(); m.bindposes = bind;
        m.RecalculateBounds();
        uvs = U;
        return m;
    }

    static BoneWeight PaWeight(BoneWeight w, int[] map)
    {
        var acc = new float[17];
        void Add(int bi, float wt) { if (wt <= 0) return; int t = map[bi] >= 0 ? map[bi] : 0; acc[t] += wt; }
        Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1); Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
        var idx = new int[4]; var val = new float[4];
        for (int i = 0; i < 17; i++)
            for (int s = 0; s < 4; s++)
                if (acc[i] > val[s])
                {
                    for (int q = 3; q > s; q--) { val[q] = val[q - 1]; idx[q] = idx[q - 1]; }
                    val[s] = acc[i]; idx[s] = i; break;
                }
        float sum = val[0] + val[1] + val[2] + val[3];
        if (sum <= 0) return new BoneWeight { boneIndex0 = 0, weight0 = 1 };
        return new BoneWeight { boneIndex0 = idx[0], weight0 = val[0] / sum, boneIndex1 = idx[1], weight1 = val[1] / sum, boneIndex2 = idx[2], weight2 = val[2] / sum, boneIndex3 = idx[3], weight3 = val[3] / sum };
    }

    // UV 섬 하나: 원본 화소 상자 → 아틀라스 자리
    class PaIsland { public int Mesh; public List<int> Verts = new List<int>(); public RectInt Src; public Vector2Int Dst; public Vector2Int Size; }

    // 두 팔 메시의 UV 섬을 찾아 아틀라스에 선반식으로 채우고, 메시 UV를 아틀라스 자리로 바꾼다. k = 원본 대비 배율
    static List<PaIsland> PaPack(Mesh[] meshes, List<Vector2>[] uvs, int sw, int sh, int atlas, out float k)
    {
        const int pad = 4;
        var islands = new List<PaIsland>();
        for (int mi = 0; mi < meshes.Length; mi++)
        {
            var t = meshes[mi].triangles; int n = uvs[mi].Count;
            var parent = new int[n]; for (int i = 0; i < n; i++) parent[i] = i;
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            for (int i = 0; i < t.Length; i += 3) { int a = Find(t[i]), b = Find(t[i + 1]), c = Find(t[i + 2]); parent[b] = a; parent[Find(c)] = a; }
            var byRoot = new Dictionary<int, PaIsland>();
            for (int i = 0; i < n; i++)
            {
                int r = Find(i);
                if (!byRoot.TryGetValue(r, out var isl)) { isl = new PaIsland { Mesh = mi }; byRoot[r] = isl; islands.Add(isl); }
                isl.Verts.Add(i);
            }
        }
        double area = 0;
        foreach (var isl in islands)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (int i in isl.Verts) { var u = uvs[isl.Mesh][i]; x0 = Mathf.Min(x0, u.x * sw); y0 = Mathf.Min(y0, u.y * sh); x1 = Mathf.Max(x1, u.x * sw); y1 = Mathf.Max(y1, u.y * sh); }
            isl.Src = new RectInt(Mathf.FloorToInt(x0), Mathf.FloorToInt(y0), Mathf.Max(1, Mathf.CeilToInt(x1) - Mathf.FloorToInt(x0)), Mathf.Max(1, Mathf.CeilToInt(y1) - Mathf.FloorToInt(y0)));
            area += (double)(isl.Src.width + 2 * pad) * (isl.Src.height + 2 * pad);
        }
        k = Mathf.Min(1f, Mathf.Sqrt((float)(atlas * (double)atlas * 0.82 / area)));
        islands.Sort((a, b) => b.Src.height.CompareTo(a.Src.height));
        for (int tries = 0; tries < 30; tries++, k *= 0.95f)
        {
            int x = 0, y = 0, rowH = 0; bool ok = true;
            foreach (var isl in islands)
            {
                int w = Mathf.CeilToInt(isl.Src.width * k) + 2 * pad, h = Mathf.CeilToInt(isl.Src.height * k) + 2 * pad;
                if (w > atlas || h > atlas) { ok = false; break; }
                if (x + w > atlas) { x = 0; y += rowH; rowH = 0; }
                if (y + h > atlas) { ok = false; break; }
                isl.Dst = new Vector2Int(x + pad, y + pad); isl.Size = new Vector2Int(w - 2 * pad, h - 2 * pad);
                x += w; rowH = Mathf.Max(rowH, h);
            }
            if (ok) break;
        }
        // 메시 UV를 아틀라스로
        foreach (var isl in islands)
        {
            var list = uvs[isl.Mesh];
            foreach (int i in isl.Verts)
            {
                var u = list[i];
                float px = (u.x * sw - isl.Src.x) * k + isl.Dst.x, py = (u.y * sh - isl.Src.y) * k + isl.Dst.y;
                list[i] = new Vector2(px / atlas, py / atlas);
            }
        }
        for (int mi = 0; mi < meshes.Length; mi++) meshes[mi].SetUVs(0, uvs[mi]);
        return islands;
    }

    // 아틀라스 그리기: 섬마다 둘레 4화소까지 원본에서 옮겨 온다 (이음매 번짐 막기)
    static Texture2D PaBake(Texture2D src, List<PaIsland> islands, int atlas, bool normal, float k)
    {
        var t = new Texture2D(atlas, atlas, TextureFormat.RGBA32, false);
        var fill = new Color32[atlas * atlas];
        var bg = normal ? new Color32(128, 128, 255, 255) : new Color32(40, 42, 36, 255);
        for (int i = 0; i < fill.Length; i++) fill[i] = bg;
        const int pad = 4;
        float sw = src.width, sh = src.height;
        foreach (var isl in islands)
        {
            for (int y = -pad; y < isl.Size.y + pad; y++)
                for (int x = -pad; x < isl.Size.x + pad; x++)
                {
                    int ax = isl.Dst.x + x, ay = isl.Dst.y + y;
                    if (ax < 0 || ay < 0 || ax >= atlas || ay >= atlas) continue;
                    // GetPixelBilinear는 텍셀 n을 u·W = n 자리에 둔다 (n+0.5가 아니다): 반 텍셀 빼야 원본 텍셀을 그대로 옮긴다
                    float sx = isl.Src.x + (x + 0.5f) / k - 0.5f, sy = isl.Src.y + (y + 0.5f) / k - 0.5f;
                    fill[ay * atlas + ax] = src.GetPixelBilinear(sx / sw, sy / sh);
                }
        }
        t.SetPixels32(fill); t.Apply();
        return t;
    }

    static Texture2D PaSaveTex(Texture2D t, string path, int size, bool normal, bool highRes)
    {
        File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = !normal; ti.maxTextureSize = size; ti.mipmapEnabled = true;
        ti.textureCompression = TextureImporterCompression.Compressed; ti.alphaSource = TextureImporterAlphaSource.None;
        ti.mipmapLimitGroupName = highRes ? HighResGroup : "";
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material PaMat(string path, Texture2D baseTex, Texture2D nrm)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", baseTex); m.SetTexture("_MainTex", baseTex);
        m.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.85f)); m.SetFloat("_Smoothness", 0.22f); m.SetFloat("_Metallic", 0.05f);
        if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
        else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
        EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m);
        return m;
    }

    static Mesh PaSaveMesh(Mesh m, string path)
    {
        m.RecalculateTangents();
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) { PaOverwriteMesh(m, old); Object.DestroyImmediate(m); return old; }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // 이미 있는 메시 자산을 새 메시로 덮어쓴다 (GUID 그대로). CopySerialized로 메시를 덮으면 편집기 메모리의 정점 버퍼가
    // 새 정점 배치와 어긋나 SkinnedMeshRenderer가 "mesh data size and vertex stride" 오류로 그리기를 멈춘다 (11-24) → 비우고 하나씩 채운다
    public static void PaOverwriteMesh(Mesh src, Mesh dst)
    {
        dst.Clear();
        dst.indexFormat = src.indexFormat;
        dst.SetVertices(src.vertices);
        dst.SetNormals(src.normals);
        if (src.tangents.Length == src.vertexCount) dst.SetTangents(src.tangents);
        dst.SetUVs(0, src.uv);
        dst.subMeshCount = src.subMeshCount;
        for (int i = 0; i < src.subMeshCount; i++) dst.SetTriangles(src.GetTriangles(i), i);
        dst.boneWeights = src.boneWeights;
        dst.bindposes = src.bindposes;
        dst.bounds = src.bounds;
        EditorUtility.SetDirty(dst);
        AssetDatabase.SaveAssetIfDirty(dst);
    }
}
