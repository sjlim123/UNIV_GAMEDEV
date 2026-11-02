using UnityEngine;

// 도시 쪽 방공호 해치. 여기로 돌아오면 탐험 성공.
public class CityExit : Interactable
{
    public override string Prompt => "방공호 해치 - 돌아가기 (1턴)";

    public override void Interact(PlayerInteractor player)
    {
        var gm = GameManager.Instance;
        var ui = gm.UI;
        ui.OpenPanel("방공호로 돌아가기", c =>
        {
            var city = CityDirector.Instance;
            float t = city != null ? city.TimeLeft : 0;
            if (city != null && city.IsRedNight) ui.Line(c, "붉은 밤이다! 체력이 계속 줄고 있다. 돌아가면 1턴이 지난다.", 22, UIManager.Danger);
            else ui.Line(c, $"붉은 밤까지 {(int)t / 60}:{(int)t % 60:00}. 돌아가면 1턴이 지난다.");
            ui.Line(c, $"소지 무게 {gm.State.Carried.Weight:0.0} / {gm.State.CarryCapacity:0.0} kg", 20, UIManager.Dim);
            var row = ui.Row(c);
            ui.RowButton(row, "돌아간다", () => gm.ReturnToShelter(), true, 200);
            ui.RowButton(row, "더 둘러본다", ui.ClosePanel, true, 200);
        });
    }
}
