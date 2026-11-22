using System.Collections.Generic;
using UnityEngine;

// 품질별 판을 든 LOD 묶음 (도시 Tripo 소품 8종, 2026-11-23. 만드는 곳: GameSceneBuilder.Tripo.cs TripoTierModel).
// 세부 묘사 품질이 '가까이서 쓰는 가장 좋은 판'을 정한다: 낮음 → 낮음 판(q0)만, 중간 → 중간 판(q1)부터, 높음 → 높음(q2)부터, 매우 높음 → 매우 높음(q3)부터.
// 처음엔 전체 설정 QualitySettings.maximumLODLevel로 했는데, 그러면 다른 LOD 묶음(감염자 모델 LOD 3단계)까지 함께 낮아져서
// 묶음마다 이 스크립트가 자기 LOD 칸을 고쳐 쓴다. LOD 칸은 늘 4개: 칸 l에는 판 min(3 - l, 품질)을 넣고, 쓰지 않는 더 좋은 판은 꺼 둔다
// (칸 수를 줄이면 전체 설정 maximumLODLevel이 1일 때 칸 하나짜리 묶음이 통째로 안 보일 수 있다).
// 씬에는 매우 높음 상태(판 4개 모두)로 저장한다. 플레이 중 켜질 때와 설정 창에서 품질을 바꿀 때 GameSettings.ApplyScene이 ApplyAll을 부른다
public class QualityLod : MonoBehaviour
{
    public Renderer[] tiers = new Renderer[4];                 // 판 번호 q = 0 낮음 · 1 중간 · 2 높음 · 3 매우 높음
    public float[] heights = { 0.25f, 0.1f, 0.035f, 0.004f };  // LOD 칸마다 화면 높이 비율 (마지막보다 작으면 안 그림)

    static readonly List<QualityLod> live = new List<QualityLod>();
    int applied = -1;

    void OnEnable()
    {
        if (!Application.isPlaying) return; // 편집 모드에서는 씬을 바꾸지 않는다 (FacadeDetail과 같음)
        if (!live.Contains(this)) live.Add(this);
        Apply(GameSettings.DetailQ);
    }

    void OnDisable() => live.Remove(this);

    public static void ApplyAll(int quality)
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            if (live[i] == null) { live.RemoveAt(i); continue; }
            live[i].Apply(quality);
        }
    }

    public void Apply(int quality)
    {
        int d = Mathf.Clamp(quality, 0, 3);
        if (d == applied) return;
        var lg = GetComponent<LODGroup>();
        if (lg == null || tiers == null || tiers.Length < 4) return;
        for (int q = 0; q < 4; q++) if (tiers[q] != null && tiers[q].gameObject.activeSelf != (q <= d)) tiers[q].gameObject.SetActive(q <= d);
        var lods = new LOD[4];
        for (int l = 0; l < 4; l++)
        {
            var r = tiers[Mathf.Min(3 - l, d)];
            lods[l] = new LOD(heights[l], r != null ? new[] { r } : new Renderer[0]);
        }
        lg.SetLODs(lods);
        applied = d;
    }
}
