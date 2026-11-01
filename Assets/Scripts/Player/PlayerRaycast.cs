using UnityEngine;

// 플레이어 시선 Raycast. 카메라가 플레이어 캡슐(CharacterController) 안에 있어서,
// 아래를 30도 넘게 보면 일반 Raycast가 자기 몸에 먼저 맞아 바닥 아이템의 [F] 안내가 뜨지 않았다.
// 자기 콜라이더는 건너뛰고 가장 가까운 것을 돌려준다.
public static class PlayerRaycast
{
    static RaycastHit[] buffer = new RaycastHit[64];

    public static bool Raycast(Vector3 origin, Vector3 direction, float range, Transform self, QueryTriggerInteraction triggers, out RaycastHit hit)
    {
        int n;
        // 결과가 버퍼를 꽉 채우면 가장 가까운 것이 빠졌을 수 있으니 버퍼를 늘려 다시 찾는다
        while ((n = Physics.RaycastNonAlloc(origin, direction, buffer, range, ~0, triggers)) == buffer.Length)
            buffer = new RaycastHit[buffer.Length * 2];
        return Closest(n, self, false, out hit);
    }

    public static bool SphereCast(Vector3 origin, float radius, Vector3 direction, float range, Transform self, QueryTriggerInteraction triggers, out RaycastHit hit)
    {
        int n;
        while ((n = Physics.SphereCastNonAlloc(origin, radius, direction, buffer, range, ~0, triggers)) == buffer.Length)
            buffer = new RaycastHit[buffer.Length * 2];
        return Closest(n, self, true, out hit);
    }

    // sphereCast: 처음부터 구에 닿아 있던 물체(거리 0)는 공격 대상(좀비·들개)일 때만 인정한다.
    // 그렇지 않으면 몸을 기댄 벽·잔해가 앞에 있는 좀비보다 먼저 맞는다.
    static bool Closest(int count, Transform self, bool sphereCast, out RaycastHit hit)
    {
        hit = default;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            var h = buffer[i];
            if (h.collider.transform.IsChildOf(self)) continue;
            if (sphereCast && h.distance <= 0f && h.collider.GetComponentInParent<IDamageable>() == null) continue;
            if (!found || h.distance < hit.distance)
            {
                hit = h;
                found = true;
            }
        }
        return found;
    }
}
