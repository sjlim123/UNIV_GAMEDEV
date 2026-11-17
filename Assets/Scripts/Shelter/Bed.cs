using UnityEngine;

// 침대: 쉬면 하루가 지나고 체력이 오른다. 자는 동안 베개에 누워 천장을 보며 화면이 서서히 어두워진다 (GameManager.Rest).
// 창은 UIManager.OpenRest가 그린다 (자고 난 뒤의 날짜·체력·배고픔·갈증·전력, 경고 줄)
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

    public override string Prompt => ShelterInvasion.ZombiesInside ? "침대 - 감염자가 있어 쉴 수 없다" : "침대 - 휴식 (하루)";

    public override void Interact(PlayerInteractor player)
    {
        // 정전 때 들어온 좀비가 살아 있으면 쉴 수 없다
        if (ShelterInvasion.BlockedByZombies(ZombieWarning)) return;
        var gm = GameManager.Instance;
        var ui = gm.UI;
        ui.OpenRest(() =>
        {
            // 창을 띄워 둔 사이에 좀비가 들어왔을 수도 있다
            if (ShelterInvasion.BlockedByZombies(ZombieWarning))
            {
                ui.ClosePanel();
                return;
            }
            gm.Rest(SleepAnchor);
        });
    }
}
