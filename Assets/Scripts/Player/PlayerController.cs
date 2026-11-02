using UnityEngine;
using UnityEngine.InputSystem;

// 1인칭 이동: WASD, 마우스 시점, Shift 달리기(스테미너 소모), C 앉기. 발소리와 소음 수치도 여기서 만든다.
// 이동 속도는 GameBalance, 마우스 감도는 설정 메뉴에서 바꾼다.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    public Transform cameraRoot;
    public float mouseSensitivity = 0.08f;
    public float standHeight = 1.8f;
    public float crouchHeight = 1.1f;

    CharacterController cc;
    AudioSource stepSource;
    float pitch, verticalVelocity, bobTimer, stepDistance, shake, extraNoise;
    float staminaRegenTimer;
    bool exhausted; // 스테미너를 다 쓰면 일정량 찰 때까지 달릴 수 없다

    public bool IsCrouching { get; private set; }
    public bool IsRunning { get; private set; }
    public float HorizontalSpeed { get; private set; }
    // 0 ~ GameBalance.staminaMax. 달리면 줄고, Shift를 떼고 있으면 0.5초마다 찬다
    public float Stamina { get; private set; }
    // 0 = 조용함, 0.25 = 앉아서 이동, 1 = 걷기, 2 = 달리기. 좀비가 이 값으로 소리를 듣는다
    public float Noise { get; private set; }

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
        stepSource = gameObject.AddComponent<AudioSource>();
        stepSource.spatialBlend = 0;
        Stamina = GameBalance.Data.staminaMax;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Shake(float amount) => shake = Mathf.Max(shake, amount);

    void UpdateStamina(bool shift, float dt, GameBalance balance)
    {
        if (IsRunning)
        {
            Stamina = Mathf.Max(0, Stamina - balance.staminaRunCostPerSecond * dt);
            if (Stamina <= 0) exhausted = true;
        }
        if (shift)
        {
            staminaRegenTimer = 0;
            return;
        }
        staminaRegenTimer += dt;
        while (staminaRegenTimer >= 0.5f)
        {
            staminaRegenTimer -= 0.5f;
            Stamina = Mathf.Min(balance.staminaMax, Stamina + balance.staminaRegenPerHalfSecond);
        }
        if (exhausted && Stamina >= balance.staminaMinToRun) exhausted = false;
    }
    public void MakeNoise(float level) => extraNoise = Mathf.Max(extraNoise, level);

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0) return;
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        bool blocked = InputBlocked;
        var balance = GameBalance.Data;

        Vector2 move = Vector2.zero;
        if (!blocked && kb != null)
        {
            if (kb.wKey.isPressed) move.y += 1;
            if (kb.sKey.isPressed) move.y -= 1;
            if (kb.dKey.isPressed) move.x += 1;
            if (kb.aKey.isPressed) move.x -= 1;
            if (kb.cKey.wasPressedThisFrame)
            {
                // 머리 위가 막혀 있으면 일어설 수 없다
                if (!IsCrouching) IsCrouching = true;
                else if (!Physics.SphereCast(transform.position + Vector3.up * crouchHeight * 0.5f, cc.radius * 0.9f, Vector3.up, out _, standHeight - crouchHeight * 0.5f, ~0, QueryTriggerInteraction.Ignore))
                    IsCrouching = false;
            }
        }

        if (!blocked && mouse != null)
        {
            Vector2 d = mouse.delta.ReadValue() * mouseSensitivity * GameSettings.Sensitivity;
            if (GameSettings.InvertY) d.y = -d.y;
            transform.Rotate(0, d.x, 0);
            pitch = Mathf.Clamp(pitch - d.y, -85f, 85f);
        }

        bool shift = !blocked && kb != null && kb.leftShiftKey.isPressed;
        IsRunning = shift && !IsCrouching && move.y > 0 && !exhausted && Stamina > 0;
        UpdateStamina(shift, dt, balance);
        float speed = IsCrouching ? balance.CrouchSpeed : IsRunning ? balance.RunSpeed : balance.WalkSpeed;

        Vector3 dir = transform.right * move.x + transform.forward * move.y;
        if (dir.sqrMagnitude > 1) dir.Normalize();
        if (cc.isGrounded && verticalVelocity < 0) verticalVelocity = -2f;
        verticalVelocity -= 9.81f * dt;
        cc.Move((dir * speed + Vector3.up * verticalVelocity) * dt);

        float targetHeight = IsCrouching ? crouchHeight : standHeight;
        cc.height = Mathf.Lerp(cc.height, targetHeight, dt * 10);
        cc.center = new Vector3(0, cc.height / 2, 0);

        // 머리 흔들림과 발소리
        HorizontalSpeed = new Vector3(cc.velocity.x, 0, cc.velocity.z).magnitude;
        bool moving = HorizontalSpeed > 0.3f && cc.isGrounded;
        float bobAmount = IsCrouching ? 0.025f : IsRunning ? 0.07f : 0.045f;
        if (moving)
        {
            bobTimer += dt * HorizontalSpeed * 2.4f;
            stepDistance += HorizontalSpeed * dt;
            float stride = IsRunning ? 1.9f : IsCrouching ? 0.9f : 1.45f;
            if (stepDistance >= stride)
            {
                stepDistance = 0;
                stepSource.pitch = Random.Range(0.85f, 1.1f);
                stepSource.PlayOneShot(ProceduralAudio.Footstep(), IsCrouching ? 0.12f : IsRunning ? 0.7f : 0.38f);
            }
        }
        else bobTimer = Mathf.Lerp(bobTimer, Mathf.Round(bobTimer / Mathf.PI) * Mathf.PI, dt * 5);

        shake = Mathf.MoveTowards(shake, 0, dt * 1.5f);
        Vector3 shakeOffset = Random.insideUnitSphere * shake * 0.08f;
        cameraRoot.localPosition = new Vector3(Mathf.Cos(bobTimer * 0.5f) * bobAmount * 0.6f, cc.height - 0.15f + Mathf.Sin(bobTimer) * bobAmount, 0) + shakeOffset;
        cameraRoot.localRotation = Quaternion.Euler(pitch + shakeOffset.y * 20, 0, shakeOffset.x * 15);

        Noise = !moving ? 0 : IsCrouching ? 0.25f : IsRunning ? 2f : 1f;
        Noise = Mathf.Max(Noise, extraNoise);
        extraNoise = Mathf.MoveTowards(extraNoise, 0, dt * 1.5f);
    }
}
