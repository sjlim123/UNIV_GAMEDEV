using UnityEngine;

// 지킴이 자리에 세울 종류. Any = 날짜에 맞춰 고른다 (도시), Dog = 들개
public enum SpawnKind { Any, Normal, Leather, Ranged, Heavy, Raider, Dog }

// 좀비(와 들개)가 나타날 수 있는 자리 (씬에서 빨간 공으로 보인다)
public class ZombieSpawnPoint : MonoBehaviour
{
    // 건물 2층 안쪽 자리. 탐험 시작 때 이런 자리에 좀비를 미리 세워 둔다
    public bool upstairs;
    // 아래 셋은 지킴이 자리(upstairs)에서만 본다. 기본값이면 도시와 똑같이 움직인다
    public SpawnKind kind = SpawnKind.Any; // 세울 종류 (Dog는 CityDirector.dogPrefab을 쓴다)
    public bool guaranteed;                // 반드시 선다 (비율·안전 거리를 따지지 않는다). 이 자리가 보는 쪽을 보고 선다
    public int tier;                       // 0 = 언제나, 1 이상 = 목적지의 LateTierTurn 날부터

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.15f, 0.8f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.5f);
    }
}
