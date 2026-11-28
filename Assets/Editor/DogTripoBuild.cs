using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 들개 Tripo 모델 (2026-11-28, 사용자 "네 2 8로 작업하시고 유일 동물이다보니 리깅에 매우 체계적으로").
// 개 a = 그림 2 (마른 들개), 개 b = 그림 8 (감염된 개). Tripo 'Other' 자동 리깅 (뼈 37개) → 감염자와 같은 길(가중치를 30만으로 옮김 → 품질 4단계 → Resources/Enemies/Dog/).
// 가중치를 옮긴 뒤 두 가지를 손질한다 (Blender 점검에서 찾은 것, ModelWork/Enemies/dog/tripo/SOURCE.md):
//   1. 개 a는 Tripo 리깅부터 꼬리에 귀·머리 뼈 가중치가 섞여 있다: 꼬리가 흔들리면 엉덩이·꼬리 살이 귀에 끌려 찢어졌다.
//      → 머리 가지(목 밑동 아래) 뼈의 가중치를 목 밑동보다 키 30% 넘게 뒤의 꼭짓점에서, 꼬리 뼈의 가중치를 꼬리 밑동보다 30% 넘게 앞의 꼭짓점에서 뺀다.
//      (10%로 하면 목~어깨의 정상 섞임까지 지웠고, 거리·뼈 칸 수로 일반 규칙을 쓰면 오히려 나빠졌다)
//   2. 가중치를 하나도 못 받은 꼭짓점 → 가장 가까운 정상 꼭짓점의 가중치 (Blender에서는 쓰러질 때 엉덩이에서 가시처럼 솟았다)
public static class DogTripoBuild
{
    public const string OutDir = "Assets/Resources/Enemies/Dog/";
    public static readonly string[] Names = { "dog_a", "dog_b" };

    [MenuItem("Tools/핵폭발 생존/들개 Tripo 모델: 개 a·b")]
    static void BuildMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[DogTripoBuild] 플레이 중에는 하지 않는다"); return; }
        Debug.Log("[DogTripoBuild] " + BuildAll());
    }

    public static string BuildAll()
    {
        if (!AssetDatabase.IsValidFolder(OutDir.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Resources/Enemies", "Dog");
        var log = new System.Text.StringBuilder();
        PlayerModelTools.WeightFix = FixWeights;
        try { foreach (var n in Names) log.Append(ZombieTripoBuild.Build(n, OutDir)).Append(" / ").Append(MarkEyes(OutDir + n + ".prefab")).Append(" || "); }
        finally { PlayerModelTools.WeightFix = null; }
        return log.ToString();
    }

    [MenuItem("Tools/핵폭발 생존/들개 Tripo 모델: 눈 자리만 다시 표시")]
    static void MarkEyesMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[DogTripoBuild] 플레이 중에는 하지 않는다"); return; }
        foreach (var n in Names) Debug.Log("[DogTripoBuild] " + MarkEyes(OutDir + n + ".prefab"));
    }

    // 눈 자리 (2026-11-28, 사용자 "들개 눈·입 손보기"): 텍스처에서 노란 눈 칠을 찾아 머리뼈 아래에 'Eye_0'·'Eye_1'을 둔다.
    // CharacterModels.TripoDog이 그 자리에 빛나는 눈알을 얹는다 (예전 개처럼 어둠 속에서 노란 눈이 보이게).
    //   - 가장 고운 몸(LOD0)의 삼각형마다 7곳을 텍스처에서 읽어, 머리뼈 앞·위 칸의 노란색(색상 0.08~0.2, 채도 0.4+, 밝기 0.5+)만 모은다
    //   - 머리 왼·오로 나눠 무게중심 = 눈 자리, 삼각형 법선의 평균 = 눈이 보는 쪽, 평면 안 2차 모멘트 = 눈 크기·기울기
    //   - Eye_ 의 z = 바깥쪽, x = 긴 축, 크기(localScale) = 반지름 (월드 기준)
    public static string MarkEyes(string prefabPath)
    {
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var olds = new List<GameObject>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Eye_")) olds.Add(t.gameObject);
            foreach (var o in olds) Object.DestroyImmediate(o);
            SkinnedMeshRenderer smr = null;
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr == null || r.sharedMesh.vertexCount > smr.sharedMesh.vertexCount) smr = r;
            if (smr == null) return "눈: 스킨 없음";
            var map = DogRigMap.Find(smr.bones, root.transform.up);
            if (map.Error != null) return "눈: 뼈 찾기 실패 " + map.Error;
            var texAsset = smr.sharedMaterial != null ? smr.sharedMaterial.mainTexture : null;
            if (texAsset == null) return "눈: 텍스처 없음";
            var tex = new Texture2D(2, 2);
            tex.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(texAsset)));
            var Hd = map.Head.position; var F = map.Fwd; var U = map.Up; var S = Vector3.Cross(U, F);
            float c = 0.03f * map.H; // 한 칸
            var mesh = smr.sharedMesh; var vs = mesh.vertices; var uv = mesh.uv; var tr = mesh.triangles;
            var w = new Vector3[vs.Length]; for (int i = 0; i < vs.Length; i++) w[i] = smr.transform.TransformPoint(vs[i]);
            var bary = new[] { new Vector3(1, 1, 1) / 3f, new Vector3(0.5f, 0.5f, 0), new Vector3(0, 0.5f, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.66f, 0.17f, 0.17f), new Vector3(0.17f, 0.66f, 0.17f), new Vector3(0.17f, 0.17f, 0.66f) };
            var log = new System.Text.StringBuilder("눈");
            for (int side = -1, k = 0; side <= 1; side += 2, k++)
            {
                var pts = new List<Vector3>(); var nsum = Vector3.zero;
                for (int t = 0; t < tr.Length; t += 3)
                {
                    Vector3 a = w[tr[t]], b = w[tr[t + 1]], cc = w[tr[t + 2]];
                    var d0 = (a + b + cc) / 3f - Hd;
                    float f0 = Vector3.Dot(d0, F) / c, u0 = Vector3.Dot(d0, U) / c, s0 = Vector3.Dot(d0, S) / c;
                    if (f0 < 0f || f0 > 10f || u0 < -0.5f || u0 > 4f || s0 * side < 0.3f) continue;
                    var tn = Vector3.Cross(b - a, cc - a);
                    foreach (var br in bary)
                    {
                        var uvp = uv[tr[t]] * br.x + uv[tr[t + 1]] * br.y + uv[tr[t + 2]] * br.z;
                        Color.RGBToHSV(tex.GetPixelBilinear(uvp.x, uvp.y), out float h, out float sat, out float val);
                        if (!(h > 0.08f && h < 0.2f && sat > 0.4f && val > 0.5f)) continue;
                        pts.Add(a * br.x + b * br.y + cc * br.z); nsum += tn;
                    }
                }
                if (pts.Count < 30) { log.Append($" {k}: 못 찾음({pts.Count})"); continue; }
                var ctr = Vector3.zero; foreach (var p in pts) ctr += p; ctr /= pts.Count;
                var n = nsum.normalized;
                if (Vector3.Dot(n, ctr - Hd) < 0) n = -n; // 바깥쪽
                // 평면 안 두 축의 2차 모멘트 → 긴 축
                var e1 = Vector3.Cross(n, U).normalized; if (e1.sqrMagnitude < 1e-6f) e1 = Vector3.Cross(n, F).normalized;
                var e2 = Vector3.Cross(n, e1);
                float xx = 0, yy = 0, xy = 0;
                foreach (var p in pts) { var d = p - ctr; float x = Vector3.Dot(d, e1), y = Vector3.Dot(d, e2); xx += x * x; yy += y * y; xy += x * y; }
                xx /= pts.Count; yy /= pts.Count; xy /= pts.Count;
                float ang = 0.5f * Mathf.Atan2(2 * xy, xx - yy);
                var major = e1 * Mathf.Cos(ang) + e2 * Mathf.Sin(ang); var minor = Vector3.Cross(n, major);
                float l1 = 0, l2 = 0;
                foreach (var p in pts) { var d = p - ctr; float x = Vector3.Dot(d, major), y = Vector3.Dot(d, minor); l1 += x * x; l2 += y * y; }
                float r1 = 2f * Mathf.Sqrt(l1 / pts.Count), r2 = 2f * Mathf.Sqrt(l2 / pts.Count);
                var eye = new GameObject("Eye_" + k).transform;
                eye.SetParent(map.Head, false);
                eye.position = ctr + n * (0.12f * r2);
                eye.rotation = Quaternion.LookRotation(n, minor);
                var ls = map.Head.lossyScale;
                eye.localScale = new Vector3(r1 / ls.x, r2 / ls.y, 0.45f * r2 / ls.z);
                log.Append($" {k}: 표본 {pts.Count}, 반지름 {r1 * 1000f:F1}×{r2 * 1000f:F1} mm");
            }
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            return log.ToString();
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static string FixWeights(Vector3[] lv, BoneWeight[] w, SkinnedMeshRenderer smr, List<int> zero)
    {
        var bones = smr.bones;
        var map = DogRigMap.Find(bones, smr.transform.root.up);
        if (map.Error != null) return "뼈 찾기 실패 (" + map.Error + "), 손질 안 함";
        var idx = new Dictionary<Transform, int>(); for (int i = 0; i < bones.Length; i++) idx[bones[i]] = i;
        var headB = new HashSet<int>(); foreach (var t in map.HeadBranch) if (idx.TryGetValue(t, out int k)) headB.Add(k);
        var tailB = new HashSet<int>(); foreach (var t in map.Tail) if (idx.TryGetValue(t, out int k)) tailB.Add(k);
        float neckX = Vector3.Dot(map.Neck.position, map.Fwd), tailX = map.Tail.Count > 0 ? Vector3.Dot(map.Tail[0].position, map.Fwd) : float.MinValue;
        float lim = 0.30f * map.H;
        int changed = 0;
        var wi = new int[4]; var ww = new float[4];
        for (int i = 0; i < w.Length; i++)
        {
            float x = Vector3.Dot(smr.transform.TransformPoint(lv[i]), map.Fwd);
            bool farBack = x < neckX - lim, farFront = x > tailX + lim;
            if (!farBack && !farFront) continue;
            var b = w[i];
            wi[0] = b.boneIndex0; wi[1] = b.boneIndex1; wi[2] = b.boneIndex2; wi[3] = b.boneIndex3;
            ww[0] = b.weight0; ww[1] = b.weight1; ww[2] = b.weight2; ww[3] = b.weight3;
            bool any = false; float s = 0f;
            for (int j = 0; j < 4; j++)
            {
                if (ww[j] <= 0f) continue;
                if ((farBack && headB.Contains(wi[j])) || (farFront && tailB.Contains(wi[j]))) { ww[j] = 0f; any = true; }
                s += ww[j];
            }
            if (!any || s <= 1e-5f) continue;
            changed++;
            w[i] = new BoneWeight { boneIndex0 = wi[0], weight0 = ww[0] / s, boneIndex1 = wi[1], weight1 = ww[1] / s, boneIndex2 = wi[2], weight2 = ww[2] / s, boneIndex3 = wi[3], weight3 = ww[3] / s };
        }
        // 가중치 없는 꼭짓점: 가장 가까운 정상 꼭짓점의 가중치
        int filled = 0;
        if (zero.Count > 0)
        {
            var bad = new HashSet<int>(zero);
            foreach (int i in zero)
            {
                int best = -1; float bd = float.MaxValue;
                for (int j = 0; j < lv.Length; j++)
                {
                    if (bad.Contains(j)) continue;
                    float d = (lv[j] - lv[i]).sqrMagnitude;
                    if (d < bd) { bd = d; best = j; }
                }
                if (best >= 0) { w[i] = w[best]; filled++; }
            }
        }
        return $"{map.Describe()} / 머리·꼬리 엉뚱한 가중치 뺀 꼭짓점 {changed:N0}, 가중치 없던 꼭짓점 {filled}개 채움";
    }
}
