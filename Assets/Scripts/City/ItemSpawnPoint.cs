using UnityEngine;

// 아이템이 생길 수 있는 자리 (씬에서 노란 공으로 보인다)
public class ItemSpawnPoint : MonoBehaviour
{
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.15f, 0.2f);
    }
}
