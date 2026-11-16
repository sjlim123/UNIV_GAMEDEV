using UnityEngine;

// 아이템이 생길 수 있는 자리 (씬에서 노란 공, 바깥 자리는 초록 공으로 보인다).
// 건물 밖 길바닥(outdoor) 자리에는 돌만 나온다 (ItemDatabase.RollLoot)
public class ItemSpawnPoint : MonoBehaviour
{
    public bool outdoor;
    // 아래 넷은 자리마다 나올 것을 정해 둘 때 쓴다. 기본값이면 도시와 똑같이 움직인다
    public bool guaranteed;            // 확률을 굴리지 않고 반드시 놓는다
    public string[] items;             // 비어 있지 않으면 이 가운데 하나 (같은 id를 여러 번 적으면 그만큼 잘 나온다). 모르는 id면 RollLoot
    public int amount;                 // 0 = 예전 규칙 (천·막대기만 가끔 2개)
    [Range(0, 1)] public float chance; // 0 = GameBalance.itemSpawnChance

    void OnDrawGizmos()
    {
        Gizmos.color = outdoor ? new Color(0.4f, 1f, 0.4f, 0.8f) : new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.15f, 0.2f);
    }
}
