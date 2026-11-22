using System.Collections.Generic;
using UnityEngine;

// 건물 겉면 디테일을 그래픽 품질에 맞춘다 (도시 생성기가 아파트 동마다 붙인다: GameSceneBuilder.KoreaWindows.cs의 KAptWindowSets).
// 품질 '낮음'(저사양): 4층부터의 창틀·유리·발코니 난간을 알파 클립 카드 벌(cards)로 그려 가볍게 한다.
// '중간'·'높음': 예전 막대 메시 벌(full) 그대로. 씬에는 full을 켠 채로 저장하므로, 이 스크립트가 돌지 않아도 원래 모습이 보인다.
// 씬이 열릴 때와 설정 창에서 품질을 바꿀 때 GameSettings.ApplyScene이 ApplyAll을 부른다
public class FacadeDetail : MonoBehaviour
{
    public GameObject full;   // 예전 막대 메시 창 (중간·높음)
    public GameObject cards;  // 카드 창 (낮음)

    static readonly List<FacadeDetail> live = new List<FacadeDetail>();

    void OnEnable()
    {
        // 편집 모드에서는 아무것도 바꾸지 않는다: GameSettings.Load 전이라 Quality가 0(낮음)으로 읽혀, 씬이 카드 상태로 저장될 수 있다
        // (지금은 [ExecuteAlways]가 없어 편집 모드에서 불리지 않지만, 나중에 붙여도 안전하게)
        if (!Application.isPlaying) return;
        if (!live.Contains(this)) live.Add(this);
        Apply(GameSettings.DetailQ); // 세부 묘사 품질 (2026-11-22부터, 전에는 그래픽 품질). GameManager가 씬보다 먼저 설정을 읽어 둔다 (BeforeSceneLoad)
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

    void Apply(int quality)
    {
        bool low = quality == 0;
        if (full != null && full.activeSelf == low) full.SetActive(!low);
        if (cards != null && cards.activeSelf != low) cards.SetActive(low);
    }
}
