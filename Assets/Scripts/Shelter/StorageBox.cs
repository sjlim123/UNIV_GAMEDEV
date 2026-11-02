using UnityEngine;

public class StorageBox : Interactable
{
    public override string Prompt => "창고 - 물건 정리";

    public override void Interact(PlayerInteractor player)
    {
        var gm = GameManager.Instance;
        var ui = gm.UI;
        var s = gm.State;
        ProceduralAudio.PlayAt(ProceduralAudio.Creak(), transform.position, 0.3f, 1.6f);
        ui.OpenPanel("창고", c =>
        {
            var top = ui.Row(c);
            ui.RowLabel(top, $"소지품  <size=18><color=#8a8270>{s.Carried.Weight:0.0} / {s.CarryCapacity:0.0} kg</color></size>", UIManager.Accent);
            ui.RowButton(top, "장비 빼고 모두 넣기", () =>
            {
                foreach (var kv in s.Carried.Sorted())
                {
                    if (ItemDatabase.Get(kv.Key).Category == ItemCategory.Equipment) continue;
                    s.Carried.Remove(kv.Key, kv.Value);
                    s.Storage.Add(kv.Key, kv.Value);
                }
                ui.RefreshPanel();
            }, true, 240);
            if (s.Carried.IsEmpty) ui.Line(c, "  (없음)", 20, UIManager.Dim);
            foreach (var kv in s.Carried.Sorted())
            {
                string id = kv.Key;
                var row = ui.Row(c);
                ui.RowIcon(row, id);
                ui.RowLabel(row, $"{UIManager.ColoredName(id)} x{kv.Value}");
                ui.RowWeight(row, ItemDatabase.Get(id).Weight * kv.Value);
                ui.RowButton(row, "1개 넣기", () => { if (CanStore(s, id, 1) && s.Carried.Remove(id)) s.Storage.Add(id); ui.RefreshPanel(); });
                ui.RowButton(row, "모두 넣기", () => { int n = s.Carried.Count(id); if (!CanStore(s, id, n)) return; s.Carried.Remove(id, n); s.Storage.Add(id, n); ui.RefreshPanel(); });
            }

            ui.Line(c, "");
            ui.Line(c, "창고", 22, UIManager.Accent);
            if (s.Storage.IsEmpty) ui.Line(c, "  (비어 있음)", 20, UIManager.Dim);
            foreach (var kv in s.Storage.Sorted())
            {
                string id = kv.Key;
                var def = ItemDatabase.Get(id);
                var row = ui.Row(c);
                ui.RowIcon(row, id);
                ui.RowLabel(row, $"{UIManager.ColoredName(id)} x{kv.Value}   <size=18>{UIManager.RarityTag(def.Rarity)}  <color=#8a8270>{def.Description}  ·  개당 {def.Weight:0.0} kg</color></size>");
                if (def.IsConsumable)
                    ui.RowButton(row, "사용", () => { gm.UseItem(id, s.Storage); ui.RefreshPanel(); }, true, 80);
                ui.RowButton(row, "꺼내기", () =>
                {
                    if (!s.CanCarry(id)) { ui.ShowMessage("너무 무겁다."); return; }
                    if (s.Storage.Remove(id)) s.Carried.Add(id);
                    ui.RefreshPanel();
                });
            }
        }, "storage");
    }

    // 가방을 넣으면 들 수 있는 무게가 줄어든다. 그래도 감당할 수 있을 때만 넣는다
    static bool CanStore(GameState s, string id, int n)
    {
        if (id != "backpack" || s.CanDropBackpack(n)) return true;
        GameManager.Instance.UI.ShowMessage("가방을 빼면 너무 무겁다. 다른 물건부터 넣어야 한다.");
        return false;
    }
}
