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
        try { foreach (var n in Names) log.Append(ZombieTripoBuild.Build(n, OutDir)).Append(" || "); }
        finally { PlayerModelTools.WeightFix = null; }
        return log.ToString();
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
