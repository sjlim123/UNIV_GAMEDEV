using UnityEngine;

public class Bed : Interactable
{
    const string ZombieWarning = "방공호 안에 좀비가 있다. 이대로는 잠들 수 없다.";

    public override string Prompt => ShelterInvasion.ZombiesInside ? "침대 - 좀비가 있어 쉴 수 없다" : "침대 - 휴식 (1턴)";

    public override void Interact(PlayerInteractor player)
    {
        // 정전 때 들어온 좀비가 살아 있으면 쉴 수 없다
        if (ShelterInvasion.BlockedByZombies(ZombieWarning)) return;
        var gm = GameManager.Instance;
        var ui = gm.UI;
        ui.OpenPanel("휴식", c =>
        {
            ui.Line(c, $"쉬면 1턴이 지나고 체력이 {GameManager.RestHeal:0} 회복된다.");
            ui.Line(c, $"배고픔 -{GameManager.HungerPerTurn:0}, 갈증 -{GameManager.ThirstPerTurn:0}, 전력 -{GameManager.PowerPerTurn:0}%", 20, UIManager.Dim);
            var row = ui.Row(c);
            ui.RowButton(row, "잠을 잔다", () =>
            {
                if (ShelterInvasion.BlockedByZombies(ZombieWarning))
                {
                    ui.ClosePanel();
                    return;
                }
                gm.Rest();
            }, true, 200);
            ui.RowButton(row, "취소", ui.ClosePanel, true, 200);
        });
    }
}
