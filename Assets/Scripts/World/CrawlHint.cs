using UnityEngine;

// 판자로 막힌 출입구 앞: 서서는 못 지나가고 앉으면 판자 아래 틈으로 지나갈 수 있다고 알려 준다.
// 좀비(키 1.8m)는 틈을 지나지 못해 안쪽이 잠깐 숨을 곳이 된다. 콜라이더 없이 플레이어 위치만 본다 (시선·공격을 막지 않게)
public class CrawlHint : MonoBehaviour
{
    public Vector3 halfSize = new Vector3(1.0f, 1.2f, 1.4f);

    float cooldown;

    void Update()
    {
        cooldown -= Time.deltaTime;
        var pc = PlayerController.Instance;
        if (pc == null || cooldown > 0) return;
        var local = transform.InverseTransformPoint(pc.transform.position);
        if (Mathf.Abs(local.x) > halfSize.x || Mathf.Abs(local.z) > halfSize.z || local.y < -0.5f || local.y > halfSize.y * 2) return;
        if (pc.IsCrouching) return;
        GameManager.Instance.UI.ShowMessage($"판자 아래 틈이 있다. {KeyBindings.Label(GameAction.Crouch)}를 눌러 앉으면 기어서 지나갈 수 있다. (감염자는 못 따라온다)", 3f);
        cooldown = 8f;
    }
}
