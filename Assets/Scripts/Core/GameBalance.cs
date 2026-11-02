using System;
using UnityEngine;

// 적 한 종류의 수치 (기획 밸런스 표: 피 / 기본 데미지 / 기본 속도 / 기본 딜레이)
[Serializable]
public class EnemyStats
{
    public string displayName;
    public float health;
    public float damage;
    [Tooltip("기획 속도 단위. 실제 m/s = 속도 × speedUnit")]
    public float speed;
    [Tooltip("공격 간격(초)")]
    public float attackDelay;
    [Tooltip("이 턴부터 등장")]
    public int fromTurn;

    public EnemyStats(string name, float health, float damage, float speed, float attackDelay, int fromTurn)
    {
        displayName = name;
        this.health = health;
        this.damage = damage;
        this.speed = speed;
        this.attackDelay = attackDelay;
        this.fromTurn = fromTurn;
    }
}

// 밸런스 수치 모음. Assets/Resources/GameBalance.asset 을 선택하면 인스펙터에서 바로 고칠 수 있다.
[CreateAssetMenu(menuName = "벙커랩/밸런스 설정", fileName = "GameBalance")]
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

    [Header("속도 단위")]
    [Tooltip("기획 속도 1 = 몇 m/s 인지. 0.4면 플레이어 속도 8 = 걷기 3.2m/s")]
    public float speedUnit = 0.4f;

    [Header("플레이어 (피 100 / 데미지 10 / 속도 8 / 딜레이 1)")]
    public float playerMaxHealth = 100f;
    public float playerDamage = 10f;
    public float playerSpeed = 8f;
    public float playerAttackDelay = 1f;
    [Tooltip("달리기 = 걷기 × 이 배율")]
    public float runMultiplier = 1.8f;
    [Tooltip("앉아 걷기 = 걷기 × 이 배율")]
    public float crouchMultiplier = 0.5f;
    [Tooltip("둔기를 들었을 때")]
    public float clubDamage = 30f;
    public float clubAttackDelay = 1.2f;

    [Header("스태미나")]
    public float staminaMax = 100f;
    [Tooltip("달리는 동안 초당 소모량")]
    public float staminaRunCostPerSecond = 20f;
    [Tooltip("Shift를 누르지 않을 때 0.5초마다 채워지는 양")]
    public float staminaRegenPerHalfSecond = 5f;
    [Tooltip("다 쓴 뒤 이만큼 차야 다시 달릴 수 있다")]
    public float staminaMinToRun = 15f;

    [Header("짐")]
    public float carryCapacity = 5f;
    public float backpackBonus = 5f;
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
    [Tooltip("탐험마다 잡상인이 나타날 확률 (0.1 = 10번에 한 번)")]
    [Range(0, 1)] public float traderChance = 0.1f;
    [Tooltip("탐험마다 약탈자가 나타날 확률")]
    [Range(0, 1)] public float raiderChance = 0.3f;

    [Header("적 (피 / 데미지 / 속도 / 딜레이 / 등장 턴)")]
    public EnemyStats zombie = new EnemyStats("좀비", 100, 12, 8, 1.6f, 1);
    public EnemyStats leatherZombie = new EnemyStats("가죽 좀비", 200, 20, 10, 1.6f, 10);
    public EnemyStats rangedZombie = new EnemyStats("원거리 좀비", 150, 10, 8, 3f, 15);
    public EnemyStats heavyZombie = new EnemyStats("거대 좀비", 300, 30, 10, 3f, 30);
    public EnemyStats dog = new EnemyStats("들개", 80, 8, 13, 2f, 1);
    public EnemyStats raider = new EnemyStats("약탈자", 100, 15, 10, 1f, 5);
    [Tooltip("좀비가 배회할 때 속도(m/s)")]
    public float zombieWanderSpeed = 0.7f;
    public float zombieSightRange = 11f;
    [Tooltip("플레이어 소음 1당 들리는 거리")]
    public float zombieHearingRange = 8f;
    [Tooltip("손전등을 켜면 좀비 시야가 이 배율만큼 늘어난다")]
    public float flashlightVisibility = 1.5f;
    [Tooltip("원거리 좀비 투사체 사거리(m)")]
    public float rangedAttackRange = 12f;

    [Header("방공호 정전 (전력 0%)")]
    [Tooltip("정전 중 출입구로 좀비가 들어오는 간격(초)")]
    public float outageZombieInterval = 20f;

    // m/s로 바꾼 값
    public float WalkSpeed => playerSpeed * speedUnit;
    public float RunSpeed => WalkSpeed * runMultiplier;
    public float CrouchSpeed => WalkSpeed * crouchMultiplier;
    public float ToMetersPerSecond(float designSpeed) => designSpeed * speedUnit;

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
