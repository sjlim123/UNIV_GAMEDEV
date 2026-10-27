using UnityEngine;

public class ShelterExit : Interactable
{
    public override string Prompt => "출입구 - 탐험 출발 (1턴)";

    public override void Interact(PlayerInteractor player)
    {
        var gm = GameManager.Instance;
        var ui = gm.UI;
        var s = gm.State;
        ui.OpenPanel("탐험 출발", c =>
        {
            ui.Line(c, $"현재 턴 {s.Turn} / {GameState.MaxTurns}. 나가는 데 1턴, 돌아오는 데 1턴이 지난다.");
            ui.Line(c, $"제한시간 {CityDirector.DefaultTimeLimit / 60:0}분 안에 해치로 돌아와야 한다. 시간이 갈수록 놈들이 늘어난다.");
            ui.Line(c, $"소지 무게 {s.Carried.Weight:0.0} / {s.CarryCapacity:0.0} kg", 22, UIManager.Dim);
            if (s.Carried.Count("flashlight") == 0) ui.Line(c, "! 손전등이 없다. 바깥은 칠흑같이 어둡다.", 22, UIManager.Danger);
            if (s.Carried.Count("club") == 0) ui.Line(c, "! 무기가 없다. 맨주먹으로는 버티기 힘들다.", 22, UIManager.Danger);
            if (s.Hunger < 25 || s.Thirst < 25) ui.Line(c, "! 배고픔이나 갈증이 심하다. 먼저 먹고 마시자.", 22, UIManager.Danger);
            if (s.Health < 40) ui.Line(c, "! 체력이 낮다. 침대에서 쉬거나 치료하자.", 22, UIManager.Danger);
            ui.Line(c, "");
            var row = ui.Row(c);
            ui.RowButton(row, "출발한다", () => gm.StartExpedition(), true, 200);
            ui.RowButton(row, "아직 아니다", ui.ClosePanel, true, 200);
        });
    }
}
