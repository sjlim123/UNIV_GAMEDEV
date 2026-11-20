using UnityEngine;

// 판자로 막힌 출입구 앞: 서서는 못 지나가고 앉으면 판자 아래 틈으로 지나갈 수 있다고 알려 준다.
// 좀비(키 1.8m)는 틈을 지나지 못해 안쪽이 잠깐 숨을 곳이 된다. 콜라이더 없이 플레이어 위치만 본다 (시선·공격을 막지 않게)
// 2026-11-19 (멘토링 3, 안내 규칙): Tip이 켜져 있을 때만 알린다 (전에는 Tip과 상관없이 구역 안에서 8초마다 다시 떴다).
// 2026-11-20 (사용자 결정): 다른 구역 안내처럼 세이브마다 처음 한 번만 '힌트'로 알린다 (Tutorial.HintCrawl. 도시 9곳·102동 2곳이 함께 쓴다).
// 들어와서 아직 못 알렸으면 구역 안에 있는 동안 기다렸다가 띄운다: Tip이 꺼져 있거나(Tip을 끈 동안은 본 것으로 치지 않는다), 창·일시정지·전환 중이거나,
// 다른 알림이 떠 있거나, 앉아 있는 동안(이미 앉는 법을 쓰고 있다)은 기다린다. 판자 밑을 지나 반대쪽으로 넘어갔으면 앉는 법을 아는 것이니 본 것으로 친다.
// 가장자리를 넘나들어도 되풀이되지 않도록 나갈 때는 0.3m 더 넓은 상자로 본다
public class CrawlHint : MonoBehaviour
{
    public Vector3 halfSize = new Vector3(1.0f, 1.2f, 1.4f);

    const float ExitMargin = 0.3f;
    bool inside, done;
    float side; // 들어온 쪽 (판자를 기준으로 앞 +1 / 뒤 -1)

    void Update()
    {
        var pc = PlayerController.Instance;
        var gm = GameManager.Instance;
        if (pc == null || gm == null || gm.State == null) { inside = false; return; }
        if ((gm.State.HintsSeen & Tutorial.HintCrawl) != 0) return; // 이 세이브에서 이미 알렸다
        var local = transform.InverseTransformPoint(pc.transform.position);
        float m = inside ? ExitMargin : 0f;
        bool now = Mathf.Abs(local.x) <= halfSize.x + m && Mathf.Abs(local.z) <= halfSize.z + m && local.y >= -0.5f - m && local.y <= halfSize.y * 2 + m;
        if (now && !inside) { done = false; side = local.z < 0 ? -1f : 1f; } // 새로 들어왔다
        inside = now;
        if (!inside || done) return;
        if (local.z * side < -0.2f) { done = true; gm.State.HintsSeen |= Tutorial.HintCrawl; return; } // 판자 밑을 지나 반대쪽으로 넘어갔다 (앉는 법을 안다)
        if (pc.IsCrouching) return;
        if (!GameSettings.Tips || pc.InputBlocked || gm.UI.IsShowingMessage) return;
        Tutorial.Hint(Tutorial.HintCrawl, $"판자 아래 틈이 있다. {KeyBindings.Label(GameAction.Crouch)}를 눌러 앉으면 기어서 지나갈 수 있다. (감염자는 못 따라온다)");
        done = true;
    }
}
