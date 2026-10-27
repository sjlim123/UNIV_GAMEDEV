using UnityEngine;

public class Bed : Interactable
{
    public override string Prompt => "침대 - 휴식 (1턴)";

    public override void Interact(PlayerInteractor player)
    {
        var gm = GameManager.Instance;
        var ui = gm.UI;
        ui.OpenPanel("휴식", c =>
        {
            ui.Line(c, $"쉬면 1턴이 지나고 체력이 {GameManager.RestHeal:0} 회복된다.");
            ui.Line(c, $"배고픔 -{GameManager.HungerPerTurn:0}, 갈증 -{GameManager.ThirstPerTurn:0}, 전력 -{GameManager.PowerPerTurn:0}%", 20, UIManager.Dim);
            var row = ui.Row(c);
            ui.RowButton(row, "잠을 잔다", () => gm.Rest(), true, 200);
            ui.RowButton(row, "취소", ui.ClosePanel, true, 200);
        });
    }
}
