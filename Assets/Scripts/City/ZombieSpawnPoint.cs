using UnityEngine;

// 좀비(와 들개)가 나타날 수 있는 자리 (씬에서 빨간 공으로 보인다)
public class ZombieSpawnPoint : MonoBehaviour
{
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.15f, 0.8f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.5f);
    }
}
