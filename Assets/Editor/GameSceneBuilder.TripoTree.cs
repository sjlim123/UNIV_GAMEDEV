using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 죽은 나무 품질 4단계 (11-22, 맵 모델링 세션. 사용자 "나무도 4단계로").
// 원본 Tripo 나무(dead_tree_01)에서 찾은 같은 가지 뼈대(LpTreeSkeleton)로 네 판을 짠다: 가지 자리가 같아 판이 바뀌어도 모양이 튀지 않는다.
// q0 낮음: 굵은 가지만, 3~5각 / q1 중간: 지금까지의 저폴리 판 / q2 높음: 6~12각, 곡선을 매끄럽게(캣멀–롬), 밑동 뿌리 5갈래, 가는 가지 끝 잔가지 1개
// q3 매우 높음: 8~18각, 더 촘촘한 곡선, 나무껍질 골(둘레 반지름 물결), 잔가지 2~3개(첫 잔가지는 높음과 같은 자리), 텍스처 1024(HighRes 묶음)
// 결과는 dead_tree_01/Q0_낮음 ~ Q3_매우높음 (LpTierDir), 파일 이름 dead_tree_01_q번호
public static partial class GameSceneBuilder
{
    static int LpTreeSides(int q, float r)
    {
        switch (q)
        {
            case 0: return r > 0.012f ? 5 : 3;
            case 1: return LpSidesFor(r);
            case 2: return r > 0.015f ? 12 : r > 0.007f ? 8 : r > 0.003f ? 6 : 4;
            default: return r > 0.015f ? 18 : r > 0.007f ? 12 : r > 0.003f ? 8 : 5;
        }
    }

    // 캣멀–롬 곡선으로 점 사이를 sub칸씩 나눈다 (반지름은 직선으로)
    static void LpSmoothPath(List<Vector3> p, List<float> r, int sub, out List<Vector3> op, out List<float> or)
    {
        op = new List<Vector3>(); or = new List<float>();
        if (sub <= 1 || p.Count < 3) { op.AddRange(p); or.AddRange(r); return; }
        for (int i = 0; i + 1 < p.Count; i++)
        {
            Vector3 p0 = p[Mathf.Max(0, i - 1)], p1 = p[i], p2 = p[i + 1], p3 = p[Mathf.Min(p.Count - 1, i + 2)];
            for (int s = 0; s < sub; s++)
            {
                float t = s / (float)sub, t2 = t * t, t3 = t2 * t;
                op.Add(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3));
                or.Add(Mathf.Lerp(r[i], r[i + 1], t));
            }
        }
        op.Add(p[p.Count - 1]); or.Add(r[r.Count - 1]);
    }

    // 뼈대로 나무 메시 짜기 (단계 q). tol = 점열 줄이기 허용 오차, minLen = 이보다 짧은 잔가지는 뺀다
    static LpMesh LpBuildTreeTier(List<KeyValuePair<List<Vector3>, List<float>>> branches, int q, float tol, float minLen)
    {
        var m = new LpMesh();
        int sub = q == 2 ? 2 : q == 3 ? 3 : 1;
        int trunk = -1; float lowY = float.MaxValue; // 가장 낮은 점에서 시작하는 가지 = 줄기
        for (int b = 0; b < branches.Count; b++)
            if (branches[b].Key.Count > 0 && branches[b].Key[0].y < lowY) { lowY = branches[b].Key[0].y; trunk = b; }
        for (int b = 0; b < branches.Count; b++)
        {
            var p0 = branches[b].Key; var r0 = branches[b].Value;
            if (p0.Count < 2) continue;
            float len = 0; for (int i = 1; i < p0.Count; i++) len += (p0[i] - p0[i - 1]).magnitude;
            if (len < minLen) continue;
            // 반지름 다듬기: 이웃 평균, 그리고 끝으로 갈수록 굵어지지 않게
            var rs = new List<float>(r0);
            for (int i = 1; i + 1 < rs.Count; i++) rs[i] = (r0[i - 1] + r0[i] * 2 + r0[i + 1]) * 0.25f;
            for (int i = 1; i < rs.Count; i++) rs[i] = Mathf.Min(rs[i], rs[i - 1]);
            LpSimplify(p0, rs, tol, out var ps, out var rsimp);
            if (ps.Count < 2) continue;
            LpSmoothPath(ps, rsimp, sub, out var p, out var r);
            int sides = LpTreeSides(q, r[0]);
            float baseR = r[0];
            r[r.Count - 1] = 0;
            System.Func<float, float, float> mul = null;
            bool isTrunk = b == trunk;
            if ((q == 3 && baseR > 0.004f) || (q == 2 && isTrunk))
            {
                float ph = b * 1.7f; bool bark = q == 3;
                mul = (a, s) =>
                {
                    // 나무껍질 골 (매우 높음): 둘레를 따라 7·13번 물결, 길이 방향으로 조금씩 비틀린다
                    float v = bark ? 1f + 0.09f * Mathf.Sin(7f * a + ph + s * 9f) + 0.04f * Mathf.Sin(13f * a - s * 23f + ph) : 1f;
                    // 밑동 뿌리 (높음·매우 높음): 줄기 아래 5 % 높이까지 5갈래 뿌리가 불룩하게 퍼진다
                    if (isTrunk && s < 0.05f)
                    {
                        float w = 1f - s / 0.05f, lobe = Mathf.Max(0f, Mathf.Cos(5f * a + 0.6f)); lobe *= lobe;
                        v *= 1f + w * w * (0.12f + 0.85f * lobe);
                    }
                    return v;
                };
            }
            LpTube(m, p, r, sides, true, false, 0.3f, mul);

            // 높음 1개·매우 높음 2~3개: 가는 가지 끝 근처 잔가지 (30~45°로 갈라지고 살짝 휜다). 가지마다 같은 씨앗이라 첫 잔가지는 두 단계에서 같은 자리
            if (q >= 2 && baseR < 0.008f && p.Count >= 3)
            {
                var rng = new System.Random(4127 + b * 31);
                var dirEnd = (p[p.Count - 1] - p[p.Count - 2]).normalized;
                int n3 = 2 + rng.Next(2), n = q == 3 ? n3 : 1;
                for (int k = 0; k < n; k++)
                {
                    int idx = Mathf.Max(0, p.Count - 2 - rng.Next(Mathf.Min(3, p.Count - 1)));
                    float t = (float)rng.NextDouble();
                    var start = Vector3.Lerp(p[idx], p[idx + 1], t);
                    float rAt = Mathf.Max(0.002f, Mathf.Lerp(r[idx], r[idx + 1], t) * 0.6f);
                    if (r[idx] <= 0.0005f) continue;
                    var perp = Vector3.Cross(dirEnd, Random3(rng)).normalized;
                    if (perp.sqrMagnitude < 1e-6f) perp = Vector3.Cross(dirEnd, Vector3.up).normalized;
                    var dir = Quaternion.AngleAxis(30f + 15f * (float)rng.NextDouble(), perp) * dirEnd;
                    float tl = 0.045f + 0.04f * (float)rng.NextDouble();
                    var bend = Quaternion.AngleAxis(12f, perp) * dir;
                    var tp = new List<Vector3> { start, start + dir * tl * 0.5f, start + dir * tl * 0.5f + bend * tl * 0.5f };
                    LpTube(m, tp, new List<float> { rAt, rAt * 0.6f, 0f }, 4, true, false);
                }
            }
        }
        return m;
    }

    static Vector3 Random3(System.Random rng) => new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1);

    static string LpMakeTreeTier(int q)
    {
        var src = LpLoadSource("dead_tree_01", TripoSpecs["dead_tree_01"].yaw);
        var skel = LpTreeSkeleton(src, 0.03f);
        LpMesh m = null; float tol;
        if (q <= 1)
        {
            // 낮음·중간: 허용 오차를 키워 가며 삼각형이 목표 이하가 되는 첫 판
            int target = q == 0 ? 320 : 1150; float minLen = q == 0 ? 0.08f : 0.035f;
            tol = 0.0015f;
            for (int it = 0; it < 40; it++, tol *= 1.2f)
            {
                m = LpBuildTreeTier(skel, q, tol, minLen);
                if (m.tri.Count / 3 <= target) break;
            }
        }
        else
        {
            tol = q == 2 ? 0.003f : 0.0015f;
            m = LpBuildTreeTier(skel, q, tol, q == 2 ? 0.02f : 0.012f);
        }
        int size = q == 0 ? 256 : q == 3 ? 1024 : 512;
        var uv = LpPackUv(m, size, 3);
        var px = LpBake(m, uv, src, size, 0.06f);
        Object.DestroyImmediate(src.tex);
        return LpSave("dead_tree_01", m, uv, px, size, "_q" + q, q == 3 ? "HighRes" : "", LpTierDir[q]) + $", 가지 {skel.Count}, 허용 오차 {tol:0.0000}";
    }
}
