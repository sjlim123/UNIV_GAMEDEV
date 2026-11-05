using UnityEngine;

// 침대: 쉬면 1턴이 지나고 체력이 오른다. 자는 동안 베개에 누워 천장을 보며 화면이 서서히 어두워진다 (GameManager.Rest)
public class Bed : Interactable
{
    const string ZombieWarning = "방공호 안에 감염자가 있다. 이대로는 잠들 수 없다.";

    // 누웠을 때 눈 위치: 베개(+z 끝) 위에서 천장을 보고, 화면 위쪽이 머리 방향
    Transform sleepAnchor;

    public Transform SleepAnchor
    {
        get
        {
            if (sleepAnchor != null) return sleepAnchor;
            sleepAnchor = new GameObject("SleepEye").transform;
            sleepAnchor.SetParent(transform, false);
            sleepAnchor.localPosition = new Vector3(0.04f, 0.74f, 0.6f);
            var look = new Vector3(0.08f, 1f, -0.32f).normalized;
            sleepAnchor.localRotation = Quaternion.LookRotation(look, Vector3.forward) * Quaternion.Euler(0, 0, -8);
            return sleepAnchor;
        }
    }

    public override string Prompt => ShelterInvasion.ZombiesInside ? "침대 - 감염자가 있어 쉴 수 없다" : "침대 - 휴식 (1턴)";

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
            string pace = EnemyPace.Warning(gm.State.Turn); // 추격 속도가 오르기 며칠 전부터 경고
            if (pace != null) ui.Line(c, pace, 20, UIManager.Danger);
            var row = ui.Row(c);
            ui.RowButton(row, "잠을 잔다", () =>
            {
                if (ShelterInvasion.BlockedByZombies(ZombieWarning))
                {
                    ui.ClosePanel();
                    return;
                }
                gm.Rest(SleepAnchor);
            }, true, 200);
            ui.RowButton(row, "취소", ui.ClosePanel, true, 200);
        });
    }
}
