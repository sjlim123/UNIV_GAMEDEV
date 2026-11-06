using UnityEngine;

// 1인칭 이동: 이동키(기본 WASD), 마우스 시점, 달리기(스테미너 소모), 앉기, 점프. 조작키는 설정 → 컨트롤에서 바꾼다 (KeyBindings).
// 소지 무게가 들 수 있는 무게의 80%를 넘으면 100%까지 걸음이 서서히 느려지고 (GameBalance.slowLoadRatio·fullLoadSpeedMultiplier),
// 90%를 넘으면 달리기·점프에 스테미너를 더 쓴다 (GameBalance.heavyLoadRatio). 방공호 안에서는 둘 다 적용하지 않고, 스테미너 자체가 줄지 않는다.
// 발소리와 소음 수치, 카메라 움직임(걸음 흔들림·옆걸음 기울기·달릴 때 시야 넓어짐·점프 착지 출렁임·지친 숨)도 여기서 만든다.
// 침대에서 잘 때는 SetSleep으로 카메라를 침대에 눕힌다
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    public Transform cameraRoot;
    public float mouseSensitivity = 0.08f;
    public float standHeight = 1.8f;
    public float crouchHeight = 1.1f;

    CharacterController cc;
    Camera cam;
    AudioSource stepSource;
    float pitch, verticalVelocity, bobTimer, stepDistance, shake, extraNoise;
    float staminaRegenTimer;
    bool exhausted; // 스테미너를 다 쓰면 일정량 찰 때까지 달릴 수 없다
    // 카메라 움직임: 착지 출렁임(스프링), 옆걸음 기울기, 시야 넓어짐, 공중에 있던 시간
    float landDip, landVelocity, strafeTilt, fovBoost, airTime, fallSpeed;
    bool wasGrounded = true;
    // 잠자기: 0 = 서 있음, 1 = 침대에 누움
    float sleepBlend;
    Transform sleepAnchor;
    // 연출(방공호 문 열고 나가기): 카메라를 anchor 자리·방향으로 옮긴다. 손은 그대로 보인다
    float cineBlend;
    Transform cineAnchor;

    public bool IsCrouching { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsGrounded => cc != null && cc.isGrounded;
    public float HorizontalSpeed { get; private set; }
    // 한 걸음 길이(m): 발소리 간격. 손 흔들림·팔 젓기(PlayerHands)가 같은 박자를 쓴다
    public float Stride => IsRunning ? 1.9f : IsCrouching ? 0.9f : 1.45f;
    // 0 ~ GameBalance.staminaMax. 달리면 줄고, 달리기 키를 떼고 있으면 staminaRegenInterval초마다 찬다
    public float Stamina { get; private set; }
    // 0 = 조용함, 0.25 = 앉아서 이동, 1 = 걷기, 2 = 달리기. 좀비가 이 값으로 소리를 듣는다
    public float Noise { get; private set; }
    // 잠든 정도 (손을 감추는 데 쓴다)
    public float SleepBlend => sleepBlend;

    // 짐이 무거운가 (들 수 있는 무게의 heavyLoadRatio 이상)
    public bool IsHeavy
    {
        get
        {
            var gm = GameManager.Instance;
            if (gm.InShelter) return false; // 방공호 안에서는 짐 무게로 불이익이 없다
            var s = gm.State;
            float cap = s.CarryCapacity;
            return cap > 0 && s.Carried.Weight >= cap * GameBalance.Data.heavyLoadRatio;
        }
    }

    float StaminaCostMultiplier => IsHeavy ? GameBalance.Data.heavyStaminaMultiplier : 1f;

    // 짐 무게에 따른 이동 속도 배율: slowLoadRatio(80%)까지는 1, 거기서 100%까지 fullLoadSpeedMultiplier로 서서히 줄어든다
    public static float LoadSpeedFor(float weight, float capacity)
    {
        var b = GameBalance.Data;
        if (capacity <= 0) return b.fullLoadSpeedMultiplier;
        float t = Mathf.InverseLerp(b.slowLoadRatio, 1f, weight / capacity);
        return Mathf.Lerp(1f, b.fullLoadSpeedMultiplier, t);
    }

    // 지금 걸음에 쓰는 배율 (짐을 줍거나 버리면 1초쯤에 걸쳐 바뀐다)
    public float LoadSpeed { get; private set; } = 1f;
    bool slowedNotice, loadChecked;

    void UpdateLoadSpeed(float dt)
    {
        var s = GameManager.Instance.State;
        // 방공호 안에서는 짐이 아무리 무거워도 느려지지 않는다 (창고·제작대 사이를 오가며 정리하는 데 불편하지 않게)
        float target = GameManager.Instance.InShelter ? 1f : LoadSpeedFor(s.Carried.Weight, s.CarryCapacity);
        bool slowed = target < 0.999f;
        if (!loadChecked)
        {
            // 장면을 막 열었을 때는 알림 없이 바로 맞춘다
            loadChecked = true;
            LoadSpeed = target;
            slowedNotice = slowed;
            return;
        }
        LoadSpeed = Mathf.MoveTowards(LoadSpeed, target, dt * 0.5f);
        // 짐 때문에 느려지기 시작하면 한 번 알려 준다
        if (slowed && !slowedNotice) GameManager.Instance.UI.ShowMessage("짐이 무거워 걸음이 느려진다.", 2f);
        slowedNotice = slowed;
    }

    public bool InputBlocked
    {
        get
        {
            var gm = GameManager.Instance;
            return gm.UI.IsBlocking || gm.IsGameOver || gm.IsTransitioning;
        }
    }

    void Awake()
    {
        Instance = this;
        cc = GetComponent<CharacterController>();
        cam = cameraRoot != null ? cameraRoot.GetComponent<Camera>() : null;
        stepSource = gameObject.AddComponent<AudioSource>();
        AudioCategory.Tag(stepSource, SoundCategory.Effects);
        stepSource.spatialBlend = 0;
        Stamina = GameBalance.Data.staminaMax;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 화면 흔들림 세기 배율 (1 = 예전 세기)
    const float ShakeScale = 0.8f;

    public void Shake(float amount) => shake = Mathf.Max(shake, amount * ShakeScale);

    public void MakeNoise(float level) => extraNoise = Mathf.Max(extraNoise, level);

    // 잠자기 연출: anchor(침대 베개 위, 천장을 보는 방향)로 카메라를 옮긴다. blend 0 = 서 있음, 1 = 누움
    public void SetSleep(Transform anchor, float blend)
    {
        sleepAnchor = anchor;
        sleepBlend = Mathf.Clamp01(blend);
    }

    // 연출용 카메라: anchor(월드 자리·방향)로 옮긴다. blend 0 = 평소, 1 = anchor 그대로
    public void SetCinematic(Transform anchor, float blend)
    {
        cineAnchor = anchor;
        cineBlend = Mathf.Clamp01(blend);
    }

    public float CinematicBlend => cineBlend;

    // 죽음 연출: seconds초에 걸쳐 무릎이 꺾이듯 가라앉으며 옆으로 쓰러진다 (GameManager.DeathRoutine이 부른다)
    float deathBlend = -1f, deathSeconds = 1.7f; // 0 이상이면 쓰러지는 중/쓰러짐

    public bool IsDead => deathBlend >= 0f;

    public void PlayDeath(float seconds)
    {
        if (deathBlend >= 0f) return;
        deathBlend = 0f;
        deathSeconds = Mathf.Max(0.2f, seconds);
    }

    void UpdateStamina(bool runHeld, float dt, GameBalance balance)
    {
        // 방공호 안에서는 스테미너가 줄지 않는다 (창고·제작대 사이를 뛰어다녀도 된다)
        if (IsRunning && !GameManager.Instance.InShelter)
        {
            Stamina = Mathf.Max(0, Stamina - balance.staminaRunCostPerSecond * StaminaCostMultiplier * dt);
            if (Stamina <= 0) exhausted = true;
        }
        if (runHeld)
        {
            staminaRegenTimer = 0;
            return;
        }
        staminaRegenTimer += dt;
        while (staminaRegenTimer >= balance.staminaRegenInterval)
        {
            staminaRegenTimer -= balance.staminaRegenInterval;
            Stamina = Mathf.Min(balance.staminaMax, Stamina + balance.staminaRegenAmount);
        }
        if (exhausted && Stamina >= balance.staminaMinToRun) exhausted = false;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0) return;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        bool blocked = InputBlocked;
        var balance = GameBalance.Data;

        Vector2 move = Vector2.zero;
        bool jump = false;
        if (!blocked)
        {
            if (KeyBindings.Held(GameAction.MoveForward)) move.y += 1;
            if (KeyBindings.Held(GameAction.MoveBack)) move.y -= 1;
            if (KeyBindings.Held(GameAction.MoveRight)) move.x += 1;
            if (KeyBindings.Held(GameAction.MoveLeft)) move.x -= 1;
            if (KeyBindings.Pressed(GameAction.Crouch))
            {
                // 머리 위가 막혀 있으면 일어설 수 없다
                if (!IsCrouching) IsCrouching = true;
                else if (!Physics.SphereCast(transform.position + Vector3.up * crouchHeight * 0.5f, cc.radius * 0.9f, Vector3.up, out _, standHeight - crouchHeight * 0.5f, ~0, QueryTriggerInteraction.Ignore))
                    IsCrouching = false;
            }
            jump = KeyBindings.Pressed(GameAction.Jump);
        }

        if (!blocked && mouse != null)
        {
            Vector2 d = mouse.delta.ReadValue() * mouseSensitivity * GameSettings.Sensitivity;
            if (GameSettings.InvertY) d.y = -d.y;
            transform.Rotate(0, d.x, 0);
            pitch = Mathf.Clamp(pitch - d.y, -85f, 85f);
        }

        bool runHeld = !blocked && KeyBindings.Held(GameAction.Run);
        IsRunning = runHeld && !IsCrouching && move.y > 0 && !exhausted && Stamina > 0;
        UpdateStamina(runHeld, dt, balance);
        UpdateLoadSpeed(dt);
        float speed = (IsCrouching ? balance.CrouchSpeed : IsRunning ? balance.RunSpeed : balance.WalkSpeed) * LoadSpeed;

        Vector3 dir = transform.right * move.x + transform.forward * move.y;
        if (dir.sqrMagnitude > 1) dir.Normalize();
        bool grounded = cc.isGrounded;
        if (grounded && verticalVelocity < 0) verticalVelocity = -2f;

        // 점프: 땅에 서 있고 앉지 않았을 때. 스테미너를 쓰고, 짐이 무거우면 덜 뛴다
        if (jump && grounded && !IsCrouching)
        {
            float cost = GameManager.Instance.InShelter ? 0f : balance.jumpStaminaCost * StaminaCostMultiplier; // 방공호 안에서는 점프도 스테미너를 쓰지 않는다
            if (Stamina >= cost)
            {
                Stamina -= cost;
                float height = balance.jumpHeight * (IsHeavy ? 0.75f : 1f);
                verticalVelocity = Mathf.Sqrt(2f * 9.81f * Mathf.Max(0.05f, height));
                stepSource.pitch = Random.Range(0.9f, 1.1f);
                stepSource.PlayOneShot(ProceduralAudio.JumpEffort(), 0.45f);
                MakeNoise(0.8f);
                landVelocity -= 0.25f; // 몸을 웅크렸다 뛰는 느낌
            }
            else GameManager.Instance.UI.ShowMessage("숨이 차서 뛸 수 없다.", 1.2f);
        }

        verticalVelocity -= 9.81f * dt;
        cc.Move((dir * speed + Vector3.up * verticalVelocity) * dt);

        // 착지: 공중에 있던 만큼 카메라가 푹 가라앉았다 올라오고 쿵 소리
        grounded = cc.isGrounded;
        if (!grounded)
        {
            airTime += dt;
            fallSpeed = Mathf.Max(fallSpeed, -verticalVelocity);
        }
        else if (!wasGrounded)
        {
            if (airTime > 0.18f)
            {
                float impact = Mathf.Clamp01(fallSpeed / 8f);
                landVelocity -= 0.6f + impact * 1.6f;
                stepSource.pitch = Random.Range(0.85f, 1.05f);
                stepSource.PlayOneShot(ProceduralAudio.Land(), 0.35f + impact * 0.5f);
                MakeNoise(0.8f + impact);
            }
            airTime = 0;
            fallSpeed = 0;
        }
        wasGrounded = grounded;

        float targetHeight = IsCrouching ? crouchHeight : standHeight;
        cc.height = Mathf.Lerp(cc.height, targetHeight, dt * 10);
        cc.center = new Vector3(0, cc.height / 2, 0);

        // 머리 흔들림과 발소리
        HorizontalSpeed = new Vector3(cc.velocity.x, 0, cc.velocity.z).magnitude;
        bool moving = HorizontalSpeed > 0.3f && grounded;
        float bobAmount = IsCrouching ? 0.025f : IsRunning ? 0.07f : 0.045f;
        if (moving)
        {
            bobTimer += dt * HorizontalSpeed * 2.4f;
            stepDistance += HorizontalSpeed * dt;
            float stride = Stride;
            if (stepDistance >= stride)
            {
                stepDistance = 0;
                stepSource.pitch = Random.Range(0.92f, 1.08f);
                // 발밑 바닥에 맞는 실제 녹음 발소리 (Footsteps). 웅덩이를 밟으면 물 발소리 (물결과 물방울은 PuddleFx가 낸다)
                if (PuddleFx.Step(transform.position, IsCrouching ? 0.35f : IsRunning ? 1f : 0.6f, false, transform.forward))
                    stepSource.PlayOneShot(Footsteps.Clip(Footsteps.Surface.Water), IsCrouching ? 0.3f : IsRunning ? 0.9f : 0.6f);
                else
                    stepSource.PlayOneShot(Footsteps.Clip(Footsteps.SurfaceUnder(transform.position, transform)), IsCrouching ? 0.18f : IsRunning ? 0.85f : 0.5f);
            }
        }
        else bobTimer = Mathf.Lerp(bobTimer, Mathf.Round(bobTimer / Mathf.PI) * Mathf.PI, dt * 5);

        // 착지 출렁임 스프링
        landVelocity += (-landDip * 120f - landVelocity * 14f) * dt;
        landDip += landVelocity * dt;
        // 옆으로 걸으면 살짝 기운다. 달리면 시야가 넓어진다
        strafeTilt = Mathf.Lerp(strafeTilt, -move.x * (IsRunning ? 2.2f : 1.4f), dt * 6);
        fovBoost = Mathf.Lerp(fovBoost, IsRunning ? 6f : 0f, dt * 5);
        if (cam != null && sleepBlend <= 0) cam.fieldOfView = GameSettings.Fov + fovBoost;
        // 서 있을 땐 숨쉬듯 아주 조금, 지쳤으면 크게 들썩인다
        float tired = 1 - Mathf.Clamp01(Stamina / 30f);
        float breath = Mathf.Sin(Time.time * (1.6f + tired * 2.4f)) * (0.004f + tired * 0.012f);

        shake = Mathf.MoveTowards(shake, 0, dt * 1.5f);
        Vector3 shakeOffset = Random.insideUnitSphere * shake * 0.08f;
        cameraRoot.localPosition = new Vector3(Mathf.Cos(bobTimer * 0.5f) * bobAmount * 0.6f, cc.height - 0.15f + Mathf.Sin(bobTimer) * bobAmount + landDip * 0.12f + breath, 0) + shakeOffset;
        cameraRoot.localRotation = Quaternion.Euler(pitch + shakeOffset.y * 20 - landDip * 4f, 0, shakeOffset.x * 15 + strafeTilt);

        // 잠자기: 침대에 누운 시점으로 옮긴다
        if (sleepBlend > 0 && sleepAnchor != null)
        {
            float k = Mathf.SmoothStep(0, 1, sleepBlend);
            cameraRoot.position = Vector3.Lerp(cameraRoot.position, sleepAnchor.position, k);
            cameraRoot.rotation = Quaternion.Slerp(cameraRoot.rotation, sleepAnchor.rotation, k);
        }

        // 연출: 카메라를 정해진 자리로
        if (cineBlend > 0 && cineAnchor != null)
        {
            float k = Mathf.SmoothStep(0, 1, cineBlend);
            cameraRoot.position = Vector3.Lerp(cameraRoot.position, cineAnchor.position, k);
            cameraRoot.rotation = Quaternion.Slerp(cameraRoot.rotation, cineAnchor.rotation, k);
        }

        // 죽음: 점점 빠르게 가라앉으며 옆으로 쓰러진다. 바닥에 닿는 순간 한 번 덜컹
        if (deathBlend >= 0f)
        {
            float before = deathBlend;
            deathBlend = Mathf.Min(1f, deathBlend + Time.unscaledDeltaTime / deathSeconds);
            float k = Mathf.Pow(deathBlend, 1.7f);
            if (before < 1f && deathBlend >= 1f) shake = Mathf.Max(shake, 0.5f);
            cameraRoot.localPosition = Vector3.Lerp(cameraRoot.localPosition, new Vector3(0.12f, 0.28f, 0.1f), k) + shakeOffset * k;
            cameraRoot.localRotation = Quaternion.Slerp(cameraRoot.localRotation, Quaternion.Euler(14f + shakeOffset.y * 20f, 0, 76f + shakeOffset.x * 15f), k);
            if (cam != null) cam.fieldOfView = GameSettings.Fov - 8f * k;
        }

        Noise = !moving ? 0 : IsCrouching ? 0.25f : IsRunning ? 2f : 1f;
        Noise = Mathf.Max(Noise, extraNoise);
        extraNoise = Mathf.MoveTowards(extraNoise, 0, dt * 1.5f);
    }
}
