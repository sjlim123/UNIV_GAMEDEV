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
            foreach (var offer in ItemDatabase.TradeOffers)
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var kv in offer.Give)
                    parts.Add($"{ItemDatabase.Name(kv.Key)} {kv.Value} <color=#8a8270>({s.Carried.Count(kv.Key)})</color>");
                bool can = s.Carried.Has(offer.Give);
                var row = ui.Row(c);
                ui.RowLabel(row, $"{ItemDatabase.Name(offer.GetId)} x{offer.GetCount}   <size=18>←  {string.Join(", ", parts)}</size>");
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
        foreach (var kv in offer.Give) s.Carried.Remove(kv.Key, kv.Value);
        s.Carried.Add(offer.GetId, offer.GetCount);
        gm.UI.ShowMessage($"{ItemDatabase.Name(offer.GetId)} x{offer.GetCount} 받았다.");
        ProceduralAudio.Play2D(ProceduralAudio.Rustle(), 0.6f);
        gm.UI.RefreshPanel();
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
