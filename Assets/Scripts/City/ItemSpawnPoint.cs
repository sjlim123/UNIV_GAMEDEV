using UnityEngine;

// 아이템이 생길 수 있는 자리 (씬에서 노란 공, 바깥 자리는 초록 공으로 보인다).
// 돌은 건물 밖 바닥(outdoor) 자리에서만 나온다
public class ItemSpawnPoint : MonoBehaviour
{
    public bool outdoor;

    void OnDrawGizmos()
    {
        Gizmos.color = outdoor ? new Color(0.4f, 1f, 0.4f, 0.8f) : new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.15f, 0.2f);
    }
}
