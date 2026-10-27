using UnityEngine;

// 밸런스 수치 모음. Assets/Resources/GameBalance.asset 을 선택하면 인스펙터에서 바로 고칠 수 있다.
[CreateAssetMenu(menuName = "핵폭발 생존/밸런스 설정", fileName = "GameBalance")]
public class GameBalance : ScriptableObject
{
    [Header("턴")]
    public int maxTurns = 200;
    public float hungerPerTurn = 3f;
    public float thirstPerTurn = 4f;
    public float powerPerTurn = 3f;
    [Tooltip("배고픔이나 갈증이 0일 때 턴마다 잃는 체력")]
    public float starveDamagePerTurn = 8f;
    public float restHeal = 30f;

    [Header("시작 상태")]
    public float startHealth = 100f;
    public float startHunger = 90f;
    public float startThirst = 90f;
    public float startPower = 60f;

    [Header("플레이어")]
    public float walkSpeed = 3.2f;
    public float runSpeed = 5.8f;
    public float crouchSpeed = 1.6f;
    public float carryCapacity = 15f;
    public float backpackBonus = 8f;
    public float fistDamage = 15f;
    public float clubDamage = 45f;
    [Tooltip("배터리 1개로 손전등을 켤 수 있는 시간(초)")]
    public float flashlightSecondsPerBattery = 300f;
    public float fuelPower = 35f;

    [Header("도시 탐험")]
    public float expeditionSeconds = 300f;
    [Range(0, 1)] public float itemSpawnChance = 0.45f;
    public int startZombies = 3;
    public int maxZombies = 10;
    public int dogCount = 2;
    [Tooltip("탐험 초반/막바지의 좀비 추가 간격(초)")]
    public float zombieSpawnIntervalStart = 50f;
    public float zombieSpawnIntervalEnd = 20f;
    [Tooltip("플레이어와 이 거리 안에서는 좀비가 생기지 않는다")]
    public float safeRadius = 30f;

    [Header("좀비")]
    public float zombieHealth = 100f;
    public float zombieWanderSpeed = 0.7f;
    [Tooltip("걷기 속도보다 조금 느리면 걸어서도 겨우 따돌릴 수 있다")]
    public float zombieChaseSpeed = 2.8f;
    public float zombieSightRange = 11f;
    [Tooltip("플레이어 소음 1당 들리는 거리")]
    public float zombieHearingRange = 8f;
    public float zombieDamage = 12f;
    public float zombieAttackCooldown = 1.6f;
    [Tooltip("손전등을 켜면 좀비 시야가 이 배율만큼 늘어난다")]
    public float flashlightVisibility = 1.5f;

    static GameBalance current;

    public static GameBalance Data
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<GameBalance>("GameBalance");
                if (current == null) current = CreateInstance<GameBalance>();
            }
            return current;
        }
    }
}
