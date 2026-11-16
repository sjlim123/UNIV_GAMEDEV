using UnityEngine;

// 높은 데서 떨어지면 다친다 (씬에 하나, 설정 없음). 공중에 떠 있던 한 구간마다 떨어진 높이를 재서 FreeDrop을 넘는 만큼만 피해를 준다
// (점프 0.9 + 한 층 2.8 = 3.7m까지는 다치지 않는다). 구간마다 따로 재므로 계단·경사로에서 발이 잠깐씩 뜨는 것은 쌓이지 않는다.
// 맵 아래로 빠지면 마지막으로 서 있던 자리로 되돌린다 (있어서는 안 되는 일이라 콘솔에 오류를 남긴다)
public class FallWatch : MonoBehaviour
{
    public const float FreeDrop = 4f, DamagePerMeter = 12f; // 임시 수치
    const float MinAirSeconds = 0.15f; // 이보다 짧게 뜬 구간은 떨어진 것으로 치지 않는다 (계단에서 발이 잠깐 뜨는 것)
    const float VoidY = -5f;           // 이 높이 아래는 맵 밖이다

    bool tracking, air;
    float peakY, airTime, safeClock;
    Vector3 lastSafe;

    void Update()
    {
        var pc = PlayerController.Instance;
        var gm = pc != null ? GameManager.Instance : null;
        if (gm == null || gm.IsGameOver || gm.IsTransitioning)
        {
            tracking = false; // 연출·씬 이동으로 자리가 바뀐 것을 떨어진 것으로 세지 않게, 끝난 뒤 처음부터 다시 잰다
            return;
        }
        var pos = pc.transform.position;
        if (!tracking)
        {
            tracking = true;
            air = false;
            peakY = pos.y;
            lastSafe = pos;
            safeClock = 0;
        }

        if (pc.IsGrounded)
        {
            if (air)
            {
                air = false;
                float drop = peakY - pos.y;
                if (airTime >= MinAirSeconds && drop > FreeDrop)
                {
                    gm.UI.ShowMessage("높은 데서 떨어졌다.", 2.5f);
                    gm.Damage((drop - FreeDrop) * DamagePerMeter, "발을 헛디뎌 떨어졌다.");
                }
            }
            peakY = pos.y;
            safeClock += Time.deltaTime;
            if (safeClock >= 0.5f)
            {
                safeClock = 0;
                lastSafe = pos;
            }
        }
        else
        {
            // 뜨는 순간의 높이(peakY)는 땅에 있던 마지막 프레임의 높이다. 점프로 더 올라가면 그만큼 높여 잡는다
            if (!air)
            {
                air = true;
                airTime = 0;
            }
            airTime += Time.deltaTime;
            peakY = Mathf.Max(peakY, pos.y);
        }

        if (pos.y < VoidY && lastSafe.y >= VoidY) Rescue(pc);
    }

    // 맵 아래로 빠졌다: 마지막으로 서 있던 자리로 되돌린다 (이 일로 다치지는 않는다)
    void Rescue(PlayerController pc)
    {
        Debug.LogError($"[FallWatch] 플레이어가 맵 아래로 빠졌습니다 {pc.transform.position}. 마지막으로 서 있던 자리 {lastSafe}(으)로 되돌립니다.");
        var cc = pc.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false; // 켜 둔 채로는 자리를 옮겨도 CharacterController가 제자리로 되돌린다
        pc.transform.position = lastSafe + Vector3.up * 0.1f;
        if (cc != null) cc.enabled = true;
        air = false;
        airTime = 0;
        peakY = pc.transform.position.y;
    }
}
