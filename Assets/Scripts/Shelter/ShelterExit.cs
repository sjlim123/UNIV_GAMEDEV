using UnityEngine;

public class ShelterExit : Interactable
{
    public override string Prompt => ShelterInvasion.ZombiesInside ? "출입구 - 감염자를 먼저 처치해야 한다" : "출입구 - 탐험 출발 (하루)";

    static bool HasWeapon(GameState s)
    {
        foreach (var kv in s.Carried.Sorted())
            if (ItemDatabase.Get(kv.Key).IsWeapon) return true;
        return false;
    }

    public override void Interact(PlayerInteractor player)
    {
        // 정전 때 들어온 좀비가 살아 있으면 출발할 수 없다
        if (ShelterInvasion.BlockedByZombies()) return;
        var gm = GameManager.Instance;
        var ui = gm.UI;
        var s = gm.State;
        ui.OpenPanel("탐험 출발", c =>
        {
            ui.Line(c, $"지금은 {s.Turn}일차 / {GameState.MaxTurns}일. 나가는 데 하루, 돌아오는 데 하루가 지난다.");
            ui.Line(c, $"{CityDirector.DefaultTimeLimit / 60:0}분이 지나면 붉은 밤이 와서 {GameBalance.Data.redNightInterval:0}초마다 체력이 {GameBalance.Data.redNightDamage:0}씩 준다. 시간이 갈수록 놈들이 늘어난다.");
            ui.Line(c, $"소지 무게 {s.Carried.Weight:0.0} / {s.CarryCapacity:0.0} kg", 22, UIManager.Dim);
            if (s.Carried.Count("flashlight") == 0) ui.Line(c, "! 손전등이 없다. 바깥은 칠흑같이 어둡다.", 22, UIManager.Danger);
            if (!HasWeapon(s)) ui.Line(c, "! 무기가 없다. 맨주먹으로는 버티기 힘들다.", 22, UIManager.Danger);
            if (s.Hunger < 25 || s.Thirst < 25) ui.Line(c, "! 배고픔이나 갈증이 심하다. 먼저 먹고 마시자.", 22, UIManager.Danger);
            if (s.Health < 40) ui.Line(c, "! 체력이 낮다. 침대에서 쉬거나 치료하자.", 22, UIManager.Danger);
            if (s.Power <= 0) ui.Line(c, "! 전력이 없어 방폭문이 열려 있다. 돌아와도 놈들이 계속 들어온다.", 22, UIManager.Danger);
            string pace = EnemyPace.Warning(s.Turn); // 추격 속도가 오르기 며칠 전부터 경고
            if (pace != null) ui.Line(c, pace, 22, UIManager.Danger);
            string soon = Destinations.SoonLineFor(s); // 새 목적지가 열리기 며칠 전부터 알린다
            if (soon != null) ui.Line(c, soon, 20, UIManager.Dim);
            ui.Line(c, "");
            var choices = Destinations.Choices(s);
            System.Action<Destination> depart = d =>
            {
                // 창을 띄워 둔 사이에 좀비가 들어왔을 수도 있다
                if (ShelterInvasion.BlockedByZombies())
                {
                    ui.ClosePanel();
                    return;
                }
                gm.StartExpedition(d);
            };
            if (choices.Count <= 1)
            {
                // 갈 곳이 하나뿐이면 예전 그대로 두 단추만 보인다
                var row = ui.Row(c);
                ui.RowButton(row, "출발한다", () => depart(choices.Count == 1 ? choices[0] : null), true, 200);
                ui.RowButton(row, "아직 아니다", ui.ClosePanel, true, 200);
            }
            else
            {
                // 갈 곳이 여럿이면 한 줄에 하나씩: 이름 (+ 아직 안 가 본 곳 표시) + 한 줄 설명 + 출발 단추, 그 아래 위험 안내
                ui.Line(c, "어디로 갈까", 22, UIManager.Accent);
                foreach (var d in choices)
                {
                    var dest = d;
                    var rec = s.Record(dest.Id, false);
                    bool fresh = dest != Destinations.City && (rec == null || rec.visits == 0);
                    bool spent = dest.Rewards != null && dest.BlurbSpent != null && rec != null && rec.clears >= dest.Rewards.Length - 1;
                    var row = ui.Row(c);
                    ui.RowLabel(row, dest.Name + (fresh ? "  <size=16><color=#d8a523>새 목적지</color></size>" : "") + $"   <size=17><color=#8a8270>{(spent ? dest.BlurbSpent : dest.Blurb)}</color></size>");
                    ui.RowButton(row, "출발한다", () => depart(dest), true, 150);
                    // 위험 안내의 {0}에는 제한시간(분)을 넣는다 (위의 붉은 밤 줄과 같은 식이라 두 줄의 숫자가 늘 같다)
                    if (!string.IsNullOrEmpty(dest.Risk)) ui.Line(c, "    " + string.Format(dest.Risk, $"{CityDirector.DefaultTimeLimit / 60:0}"), 18, UIManager.Danger);
                }
                var last = ui.Row(c, false);
                ui.RowButton(last, "아직 아니다", ui.ClosePanel, true, 200);
            }
        });
    }

    float noticeClock = 6.5f; // 방공호에 들어와 이만큼 지난 뒤에 처음 살핀다 (도착 안내·일지가 지나간 뒤)

    // 새 목적지가 열렸으면 한 번 알린다 (Tip 스위치를 따른다). 다른 알림이나 창이 떠 있으면 사라질 때까지 기다린다
    void Update()
    {
        noticeClock -= Time.deltaTime;
        if (noticeClock > 0) return;
        noticeClock = 1f; // 그 뒤로는 1초에 한 번만 살핀다 (방공호에서 자고 일어나 날이 바뀌었을 수 있다)
        if (!GameSettings.Tips) return;
        var gm = GameManager.Instance;
        if (gm == null || gm.State == null || gm.IsGameOver || gm.IsTransitioning || gm.UI.IsBlocking || gm.UI.IsShowingMessage) return;
        var d = Destinations.PendingNotice(gm.State);
        if (d == null) return;
        if (!string.IsNullOrEmpty(d.UnlockNotice)) gm.UI.ShowMessage(d.UnlockNotice, 6f);
        gm.State.Record(d.Id).announced = true;
        gm.SaveGame();
    }
}
