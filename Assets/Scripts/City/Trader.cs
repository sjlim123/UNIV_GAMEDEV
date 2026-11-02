using UnityEngine;

// 잡상인: 물물교환으로 거래한다 (목록은 ItemDatabase.TradeOffers)
public class Trader : Interactable
{
    public Transform head;

    public override string Prompt => "잡상인 - 거래하기";

    public override void Interact(PlayerInteractor player)
    {
        var gm = GameManager.Instance;
        var ui = gm.UI;
        var s = gm.State;
        ui.OpenPanel("잡상인", c =>
        {
            ui.Line(c, "\"돈은 이제 휴지 조각이지. 물건 대 물건으로만 거래하네.\"", 22, UIManager.Dim);
            ui.Line(c, $"소지품 무게 {s.Carried.Weight:0.0} / {s.CarryCapacity:0.0} kg", 18, UIManager.Dim);
            foreach (var offer in ItemDatabase.TradeOffers)
            {
                // 내는 물건: 이름(등급 색) 개수 (가진 개수, 모자라면 빨강)
                var parts = new System.Collections.Generic.List<string>();
                foreach (var kv in offer.Give)
                {
                    int have = s.Carried.Count(kv.Key);
                    parts.Add($"{UIManager.ColoredName(kv.Key)} {kv.Value} <color={(have < kv.Value ? "#f05a48" : "#8a8270")}>({have})</color>");
                }
                bool can = s.Carried.Has(offer.Give) && FitsAfterTrade(s, offer);
                var get = ItemDatabase.Get(offer.GetId);
                var row = ui.Row(c);
                ui.RowIcon(row, offer.GetId);
                ui.RowLabel(row, $"{UIManager.ColoredName(offer.GetId)} x{offer.GetCount}  <size=16>{UIManager.RarityTag(get.Rarity)}</size>   <size=18>←  {string.Join(", ", parts)}</size>");
                ui.RowButton(row, "거래", () => Trade(offer), can);
            }
            ui.Line(c, "거래는 몸에 지닌 물건으로만 할 수 있다. 시간은 계속 흐른다.", 18, UIManager.Dim);
        }, "trade");
    }

    void Trade(TradeOffer offer)
    {
        var gm = GameManager.Instance;
        var s = gm.State;
        if (!s.Carried.Has(offer.Give)) return;
        if (!FitsAfterTrade(s, offer))
        {
            gm.UI.ShowMessage("너무 무겁다. 더는 들 수 없다.");
            return;
        }
        foreach (var kv in offer.Give) s.Carried.Remove(kv.Key, kv.Value);
        s.Carried.Add(offer.GetId, offer.GetCount);
        gm.UI.ShowMessage($"{UIManager.ColoredName(offer.GetId)} x{offer.GetCount} 받았다.");
        ProceduralAudio.Play2D(ProceduralAudio.Rustle(), 0.6f);
        gm.UI.RefreshPanel();
    }

    // 거래한 뒤에도 들 수 있는 무게 안인가 (내는 물건만큼 가벼워지고 받는 물건만큼 무거워진다)
    static bool FitsAfterTrade(GameState s, TradeOffer offer)
    {
        float after = s.Carried.Weight;
        foreach (var kv in offer.Give) after -= s.Carried.UnitWeight(kv.Key) * kv.Value;
        after += s.Carried.UnitWeight(offer.GetId) * offer.GetCount;
        return after <= s.CarryCapacity + 0.001f;
    }

    void Update()
    {
        // 플레이어를 바라본다
        var p = PlayerController.Instance;
        if (p == null || head == null) return;
        var dir = p.cameraRoot.position - head.position;
        if (dir.magnitude < 8f)
            head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 2);
    }
}
