using System.Collections;
using UnityEngine;

// 창고 (녹슨 강철 양문 수납장): F를 누르면 걸쇠가 풀리고 경첩 달린 두 문짝이 끼익 양옆으로 열려 안의 선반이 보이며 창고 창이 뜬다.
// 창을 닫으면 문짝이 다시 닫히며 쿵 소리가 난다. 문짝·경첩은 GameSceneBuilder.Shelter.cs의 BuildStorage가 만든다
public class StorageBox : Interactable
{
    public Transform doorL, doorR;  // 경첩 축 (바깥 가장자리). 왼쪽은 -y, 오른쪽은 +y로 돌아 앞으로 열린다
    public float openAngle = 110f;
    const float OpenSeconds = 0.62f, CloseSeconds = 0.5f, PanelAt = 0.3f;

    float angle;   // 지금 열린 각도
    bool busy, open;

    public override string Prompt => "창고 - 물건 정리";
    public override bool CanInteract => !busy && !open;

    public override void Interact(PlayerInteractor player)
    {
        if (busy || open) return;
        StartCoroutine(OpenRoutine());
    }

    IEnumerator OpenRoutine()
    {
        busy = true;
        open = true;
        var at = transform.position + Vector3.up * 1.0f;
        ProceduralAudio.PlayAt(ProceduralAudio.Latch(), at, 0.35f, 1.35f);
        ProceduralAudio.PlayAt(ProceduralAudio.Creak(), at, 0.3f, 1.5f);
        bool shown = false;
        float from = angle;
        for (float t = 0; t < OpenSeconds; t += Time.deltaTime)
        {
            float k = t / OpenSeconds;
            Apply(Mathf.Lerp(from, openAngle, 1 - (1 - k) * (1 - k))); // 힘껏 당겨 열리다 끝에서 느려진다
            if (!shown && t >= PanelAt) { shown = true; OpenPanel(); }
            yield return null;
        }
        Apply(openAngle);
        ProceduralAudio.PlayAt(ProceduralAudio.Click(), at, 0.2f, 0.6f);
        if (!shown) OpenPanel();
        busy = false;
    }

    IEnumerator CloseRoutine()
    {
        busy = true;
        var at = transform.position + Vector3.up * 1.0f;
        float from = angle;
        for (float t = 0; t < CloseSeconds; t += Time.deltaTime)
        {
            float k = t / CloseSeconds;
            Apply(Mathf.Lerp(from, 0f, k * k)); // 점점 빨라지며 닫힌다
            yield return null;
        }
        Apply(0f);
        ProceduralAudio.PlayAt(ProceduralAudio.Clang(), at, 0.3f, 0.75f);
        busy = false;
        open = false;
    }

    void Apply(float a)
    {
        angle = a;
        if (doorL != null) doorL.localRotation = Quaternion.Euler(0, -a, 0);
        if (doorR != null) doorR.localRotation = Quaternion.Euler(0, a, 0);
    }

    // 창이 닫히면 문짝을 닫는다 (씬이 바뀌며 닫힌 경우는 그냥 둔다)
    void OnPanelClosed()
    {
        if (this == null || !isActiveAndEnabled) return;
        StartCoroutine(CloseRoutine());
    }

    void OpenPanel()
    {
        var gm = GameManager.Instance;
        var ui = gm.UI;
        var s = gm.State;
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
                ui.RowButton(row, "1개 넣기", () => { if (s.Carried.Remove(id)) s.Storage.Add(id); ui.RefreshPanel(); });
                ui.RowButton(row, "모두 넣기", () => { int n = s.Carried.Count(id); s.Carried.Remove(id, n); s.Storage.Add(id, n); ui.RefreshPanel(); });
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
        }, "storage", true, OnPanelClosed);
    }
}
