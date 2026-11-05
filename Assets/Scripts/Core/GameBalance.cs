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
    [Tooltip("배고픔이나 갈증이 0이면 starveInterval초마다 이만큼 체력을 잃는다")]
    public float starveDamage = 5f;
    public float starveInterval = 3f;
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
    [Tooltip("방어구를 입으면 방어구마다 더 늘어난다 (ItemDatabase의 HealthBonus)")]
    public float playerMaxHealth = 100f;
    [Tooltip("맨주먹. 무기의 공격력·공격 간격은 ItemDatabase에서 정한다")]
    public float playerDamage = 10f;
    public float playerSpeed = 8f;
    public float playerAttackDelay = 1f;
    [Tooltip("달리기 = 걷기 × 이 배율")]
    public float runMultiplier = 1.8f;
    [Tooltip("앉아 걷기 = 걷기 × 이 배율")]
    public float crouchMultiplier = 0.5f;

    [Header("스태미나")]
    public float staminaMax = 100f;
    [Tooltip("달리는 동안 초당 소모량")]
    public float staminaRunCostPerSecond = 20f;
    [Tooltip("Shift를 누르지 않을 때 staminaRegenInterval초마다 채워지는 양")]
    [UnityEngine.Serialization.FormerlySerializedAs("staminaRegenPerHalfSecond")]
    public float staminaRegenAmount = 5f;
    public float staminaRegenInterval = 0.3f;
    [Tooltip("다 쓴 뒤 이만큼 차야 다시 달릴 수 있다")]
    public float staminaMinToRun = 15f;
    [Tooltip("소지 무게가 들 수 있는 무게의 이 비율 이상이면 스태미나를 더 쓴다 (0.9 = 90%)")]
    [Range(0, 1)] public float heavyLoadRatio = 0.9f;
    [Tooltip("짐이 무거울 때 달리기·점프 스태미나 소모 배율")]
    public float heavyStaminaMultiplier = 1.6f;

    [Header("점프 (테스트하며 조정)")]
    [Tooltip("점프 높이(m). 짐이 무거우면 75%만 뛴다")]
    public float jumpHeight = 0.9f;
    [Tooltip("점프 한 번에 드는 스태미나")]
    public float jumpStaminaCost = 8f;

    [Header("화상 (라이터로 때리면)")]
    [Tooltip("불이 붙어 있는 시간(초)")]
    public float burnSeconds = 5f;
    [Tooltip("타는 동안 입히는 피해의 합")]
    public float burnDamage = 10f;

    [Header("적이 맞았을 때 (플레이어가 때리면)")]
    [Tooltip("맞은 적이 그 자리에 굳는 시간(초): 움직임·공격 준비·공격 간격이 모두 멈춘다")]
    public float enemyHitStun = 0.15f;
    [Tooltip("굳었다 풀린 뒤 잠깐 느려지는 정도 (1 = 그대로, 0.85 = 15% 느리게)")]
    [Range(0.3f, 1f)] public float enemyHitSlow = 0.85f;
    [Tooltip("느려지는 시간(초). 맞고 멈칫한 뒤 다시 움직일 때부터 센다")]
    public float enemyHitSlowSeconds = 0.5f;

    [Header("짐")]
    [Tooltip("가방 없이 들 수 있는 무게. 가방을 메면 가방마다 더 늘어난다 (ItemDatabase의 CarryBonus)")]
    public float carryCapacity = 5f;
    [Tooltip("소지 무게가 들 수 있는 무게의 이 비율부터 걸음이 느려지기 시작한다 (0.8 = 80%)")]
    [Range(0, 1)] public float slowLoadRatio = 0.8f;
    [Tooltip("짐이 가득 찼을 때(100%) 이동 속도 배율. slowLoadRatio부터 100%까지 서서히 이 값으로 줄어든다 (0.6 = 40% 느리게)")]
    [Range(0.1f, 1f)] public float fullLoadSpeedMultiplier = 0.6f;
    [Tooltip("배터리 1개로 손전등을 켤 수 있는 시간(초)")]
    public float flashlightSecondsPerBattery = 300f;
    public float fuelPower = 35f;

    [Header("도시 탐험")]
    [Tooltip("이 시간(초)이 지나면 붉은 밤이 된다")]
    public float expeditionSeconds = 300f;
    [Tooltip("붉은 밤: redNightInterval초마다 체력이 이만큼 준다")]
    public float redNightDamage = 3f;
    public float redNightInterval = 3f;
    [Tooltip("아이템 자리마다 아이템이 놓일 확률 (0.45 = 45%)")]
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
    public EnemyStats heavyZombie = new EnemyStats("거대 좀비", 300, 30, 8, 3f, 30);
    public EnemyStats dog = new EnemyStats("좀비개", 80, 8, 13, 2f, 1);
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
