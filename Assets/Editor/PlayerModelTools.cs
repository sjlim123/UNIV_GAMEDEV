using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 플레이어 모델 (2026-11-24, 사용자 "플레이어 모델링 업그레이드": 모델링·리깅은 Tripo, 폴리곤 30만).
// Tripo 웹 자동 리깅은 30만 면 모델을 '폴리곤 한도 초과'로 거절해, 같은 모델을 5만으로 리토폴로지해 리깅했다 (뼈 67개, Unity Humanoid 이름).
// 이 도구는 Tripo가 만든 뼈·가중치를 30만 원본 메시로 옮긴다: 원본 꼭짓점마다 리깅 메시에서 가장 가까운 면(같은 쪽을 보는 면만)을 찾아
// 그 면 세 꼭짓점의 가중치를 무게중심 비율로 섞는다 (상위 4개, 합 1). 뼈·바인드 포즈는 Tripo 리깅 그대로.
// 결과: player_survivor_02_q3_skinned.asset (메시) + player_survivor_02_q3.prefab (Tripo 뼈대 + 30만 스킨 메시)
public static class PlayerModelTools
{
    // 모델 이름 (폴더·파일 이름 앞부분): player_survivor_02 = 방독면 탐험 차림, player_civilian_01 = 얼굴 있는 집 차림 (도입 컷씬)
    static string Name = "player_survivor_02";
    // 모델 폴더의 부모 (감염자 Tripo 모델은 Assets/ThirdParty/Tripo/Enemies/). Fit: 원본 메시를 리깅 메시 크기로 맞추는 배율
    // (감염자 리깅 메시는 원본의 0.98배로 나왔다 — Blender에서 0.98배로 줄이면 평균 0.43 mm로 겹침, 11-26)
    static string Base = "Assets/ThirdParty/Tripo/Player/";
    static float Fit = 1f;
    static string Dir => Base + Name + "/";
    static string HiFbx => Dir + Name + "_tex.fbx";
    static string RigFbx => Dir + "_리깅5만/" + Name + "_rig50k.fbx";

    [MenuItem("Tools/핵폭발 생존/플레이어 모델: Tripo 가중치를 30만 메시로 옮기기")]
    public static void TransferMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[PlayerModelTools] 플레이 중에는 하지 않는다"); return; }
        Debug.Log("[PlayerModelTools] " + Transfer());
    }

    [MenuItem("Tools/핵폭발 생존/플레이어 모델: 얼굴 모델(집 차림) 가중치 옮기기")]
    public static void TransferCivilianMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[PlayerModelTools] 플레이 중에는 하지 않는다"); return; }
        Debug.Log("[PlayerModelTools] " + TransferFor("player_civilian_01"));
    }

    public static string Transfer() => TransferFor("player_survivor_02");

    public static string TransferFor(string name)
    {
        Name = name;
        try { return TransferCur(); }
        finally { Name = "player_survivor_02"; }
    }

    // baseDir 아래 name 모델 (예: 감염자 "Assets/ThirdParty/Tripo/Enemies/", 0.98)
    public static string TransferFor(string name, string baseDir, float fit)
    {
        Base = baseDir; Fit = fit;
        try { return TransferFor(name); }
        finally { Base = "Assets/ThirdParty/Tripo/Player/"; Fit = 1f; }
    }

    static string TransferCur()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var hiAsset = AssetDatabase.LoadAssetAtPath<GameObject>(HiFbx);
        var rigAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RigFbx);
        if (hiAsset == null || rigAsset == null) return "원본 FBX 없음";
        var hiGo = Object.Instantiate(hiAsset); var rigGo = Object.Instantiate(rigAsset);
        hiGo.hideFlags = rigGo.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var mf = hiGo.GetComponentInChildren<MeshFilter>();
            var smr = rigGo.GetComponentInChildren<SkinnedMeshRenderer>();
            var hiMesh = mf.sharedMesh; var rigMesh = smr.sharedMesh;

            // 리깅 메시 (쉬는 자세, smr 로컬) 와 그 꼭짓점 가중치
            var rv = rigMesh.vertices; var rt = rigMesh.triangles;
            var rbw = rigMesh.boneWeights;
            int T = rt.Length / 3;
            var tn = new Vector3[T]; var tlo = new Vector3[T]; var thi = new Vector3[T];
            for (int t = 0; t < T; t++)
            {
                Vector3 a = rv[rt[3 * t]], b = rv[rt[3 * t + 1]], c = rv[rt[3 * t + 2]];
                tn[t] = Vector3.Cross(b - a, c - a).normalized;
                tlo[t] = Vector3.Min(a, Vector3.Min(b, c)); thi[t] = Vector3.Max(a, Vector3.Max(b, c));
            }
            // 칸 격자 (리깅 메시 로컬 단위; 모델 키 약 1)
            const float cell = 0.008f;
            var grid = new Dictionary<Vector3Int, List<int>>();
            for (int t = 0; t < T; t++)
            {
                var l = Vector3Int.FloorToInt(tlo[t] / cell); var h = Vector3Int.FloorToInt(thi[t] / cell);
                for (int x = l.x; x <= h.x; x++) for (int y = l.y; y <= h.y; y++) for (int z = l.z; z <= h.z; z++)
                {
                    var k = new Vector3Int(x, y, z);
                    if (!grid.TryGetValue(k, out var lst)) grid[k] = lst = new List<int>();
                    lst.Add(t);
                }
            }

            // 30만 꼭짓점을 리깅 메시 로컬로
            var hv = hiMesh.vertices; var hn = hiMesh.normals;
            var lv = new Vector3[hv.Length]; var ln = new Vector3[hv.Length];
            for (int i = 0; i < hv.Length; i++)
            {
                lv[i] = smr.transform.InverseTransformPoint(mf.transform.TransformPoint(hv[i] * Fit));
                ln[i] = smr.transform.InverseTransformDirection(mf.transform.TransformDirection(hn[i])).normalized;
            }

            var outW = new BoneWeight[hv.Length];
            int fallback = 0, nearUsed = 0, over5 = 0; float maxDist = 0f; double sumDist = 0;
            var acc = new Dictionary<int, float>();
            var seen = new HashSet<int>();
            for (int i = 0; i < lv.Length; i++)
            {
                var p = lv[i]; var c0 = Vector3Int.FloorToInt(p / cell);
                int best = -1, bestAny = -1; float bd = float.MaxValue, bdAny = float.MaxValue; Vector3 bq = Vector3.zero, bqAny = Vector3.zero;
                seen.Clear();
                for (int ring = 0; ring <= 6; ring++)
                {
                    for (int x = -ring; x <= ring; x++) for (int y = -ring; y <= ring; y++) for (int z = -ring; z <= ring; z++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Max(Mathf.Abs(y), Mathf.Abs(z))) != ring) continue;
                        if (!grid.TryGetValue(c0 + new Vector3Int(x, y, z), out var lst)) continue;
                        foreach (int t in lst)
                        {
                            if (!seen.Add(t)) continue;
                            var q = ClosestOnTri(p, rv[rt[3 * t]], rv[rt[3 * t + 1]], rv[rt[3 * t + 2]]);
                            float d = (q - p).sqrMagnitude;
                            if (d < bdAny) { bdAny = d; bestAny = t; bqAny = q; }
                            if (d < bd && Vector3.Dot(tn[t], ln[i]) > 0.2f) { bd = d; best = t; bq = q; }
                        }
                    }
                    // 이 고리 밖은 적어도 ring*cell 떨어져 있다
                    float reach = ring * cell;
                    if (best >= 0 && bd <= reach * reach) break;
                }
                if (best < 0) { best = bestAny; bq = bqAny; bd = bdAny; fallback++; }
                // 같은 쪽을 보는 면이 가장 가까운 면보다 훨씬 멀면(6 mm 넘고 가장 가까운 면의 3배 넘게) 가장 가까운 면을 쓴다:
                // 얇은 끝(손가락 끝·끈·필터 테두리)에서는 법선이 맞는 면이 반대편 몸통에 있을 수 있다 (첫 실행 최대 63 mm)
                else if (bd > 0.006f * 0.006f && bd > bdAny * 9f) { best = bestAny; bq = bqAny; bd = bdAny; nearUsed++; }
                if (best < 0) { outW[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f }; fallback++; continue; }
                float dist = Mathf.Sqrt(bd); maxDist = Mathf.Max(maxDist, dist); sumDist += dist; if (dist > 0.005f) over5++;
                var bar = Bary(bq, rv[rt[3 * best]], rv[rt[3 * best + 1]], rv[rt[3 * best + 2]]);
                acc.Clear();
                for (int k = 0; k < 3; k++)
                {
                    var w = rbw[rt[3 * best + k]]; float f = bar[k];
                    Add(acc, w.boneIndex0, w.weight0 * f); Add(acc, w.boneIndex1, w.weight1 * f);
                    Add(acc, w.boneIndex2, w.weight2 * f); Add(acc, w.boneIndex3, w.weight3 * f);
                }
                outW[i] = Top4(acc);
            }

            // 새 메시 (리깅 메시 로컬 좌표, 원본 UV·삼각형)
            var mesh = new Mesh { name = Name + "_q3_skinned", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = lv; mesh.normals = ln; mesh.uv = hiMesh.uv; mesh.triangles = hiMesh.triangles;
            mesh.boneWeights = outW; mesh.bindposes = rigMesh.bindposes;
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            string meshPath = Dir + Name + "_q3_skinned.asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (old != null) { GameSceneBuilder.PaOverwriteMesh(mesh, old); Object.DestroyImmediate(mesh); mesh = old; }
            else AssetDatabase.CreateAsset(mesh, meshPath);

            // 프리팹: Tripo 뼈대 + 30만 스킨 메시
            var pf = Object.Instantiate(rigAsset); pf.name = Name + "_q3";
            var psmr = pf.GetComponentInChildren<SkinnedMeshRenderer>();
            psmr.sharedMesh = mesh; psmr.name = "Body_q3";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Dir + "M_" + Name + "_q3.mat");
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + Name + "_tex_basecolor.jpg"));
                mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + Name + "_tex_normal.jpg")); mat.EnableKeyword("_NORMALMAP");
                mat.SetFloat("_Smoothness", 0.22f);
                AssetDatabase.CreateAsset(mat, Dir + "M_" + Name + "_q3.mat");
            }
            psmr.sharedMaterial = mat;
            // 화면 밖 판정 상자는 뿌리 뼈(Hips) 기준이다: 메시 상자를 그대로 넣으면 멀리서 볼 때 통째로 잘려 안 그려졌다 (11-24 굽힘 사진).
            // 쉬는 자세 상자를 뿌리 뼈 좌표로 옮기고, 팔·다리를 크게 굽혀도 들어가게 넉넉히 키운다
            {
                var root = psmr.rootBone != null ? psmr.rootBone : psmr.transform;
                var mb = mesh.bounds; var rb = new Bounds(root.InverseTransformPoint(psmr.transform.TransformPoint(mb.center)), Vector3.zero);
                for (int k = 0; k < 8; k++)
                    rb.Encapsulate(root.InverseTransformPoint(psmr.transform.TransformPoint(mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)))));
                rb.Expand(rb.size.magnitude * 0.35f);
                psmr.localBounds = rb;
            }
            PrefabUtility.SaveAsPrefabAsset(pf, Dir + Name + "_q3.prefab");
            Object.DestroyImmediate(pf);
            return $"꼭짓점 {lv.Length:N0} · 삼각형 {hiMesh.triangles.Length / 3:N0} → 뼈 {smr.bones.Length}개 가중치, 평균 거리 {sumDist / lv.Length * 1000:F2} mm(모델 단위), 최대 {maxDist * 1000:F1}, 5 mm 넘는 꼭짓점 {over5}, 가장 가까운 면으로 바꾼 꼭짓점 {nearUsed}, 같은 쪽 면이 없던 꼭짓점 {fallback}, {sw.ElapsedMilliseconds} ms";
        }
        finally { Object.DestroyImmediate(hiGo); Object.DestroyImmediate(rigGo); }
    }

    static void Add(Dictionary<int, float> d, int bone, float w) { if (w <= 0f) return; d.TryGetValue(bone, out float v); d[bone] = v + w; }

    static BoneWeight Top4(Dictionary<int, float> d)
    {
        var l = new List<KeyValuePair<int, float>>(d); l.Sort((a, b) => b.Value.CompareTo(a.Value));
        float s = 0f; for (int k = 0; k < Mathf.Min(4, l.Count); k++) s += l[k].Value;
        if (s <= 0f) return new BoneWeight { boneIndex0 = 0, weight0 = 1f };
        var bw = new BoneWeight();
        if (l.Count > 0) { bw.boneIndex0 = l[0].Key; bw.weight0 = l[0].Value / s; }
        if (l.Count > 1) { bw.boneIndex1 = l[1].Key; bw.weight1 = l[1].Value / s; }
        if (l.Count > 2) { bw.boneIndex2 = l[2].Key; bw.weight2 = l[2].Value / s; }
        if (l.Count > 3) { bw.boneIndex3 = l[3].Key; bw.weight3 = l[3].Value / s; }
        return bw;
    }

    // 삼각형 위 가장 가까운 점 (Ericson, Real-Time Collision Detection 5.1.5)
    static Vector3 ClosestOnTri(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a, ac = c - a, ap = p - a;
        float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0f && d2 <= 0f) return a;
        Vector3 bp = p - b; float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0f && d4 <= d3) return b;
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
        Vector3 cp = p - c; float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0f && d5 <= d6) return c;
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
        float va = d3 * d6 - d5 * d4;
        if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
        float denom = 1f / (va + vb + vc);
        return a + ab * (vb * denom) + ac * (vc * denom);
    }

    static Vector3 Bary(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 v0 = b - a, v1 = c - a, v2 = p - a;
        float d00 = Vector3.Dot(v0, v0), d01 = Vector3.Dot(v0, v1), d11 = Vector3.Dot(v1, v1), d20 = Vector3.Dot(v2, v0), d21 = Vector3.Dot(v2, v1);
        float den = d00 * d11 - d01 * d01; if (Mathf.Abs(den) < 1e-20f) return new Vector3(1, 0, 0);
        float v = (d11 * d20 - d01 * d21) / den, w = (d00 * d21 - d01 * d20) / den;
        return new Vector3(Mathf.Clamp01(1f - v - w), Mathf.Clamp01(v), Mathf.Clamp01(w));
    }
}
