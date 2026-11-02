using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 1인칭 손 (임시 모델): 왼손은 손전등, 오른손은 핫바에서 고른 아이템을 든다.
// 그림자 전용 몸통도 만들어서 조명 아래 플레이어 그림자가 보이게 한다.
public class PlayerHands : MonoBehaviour
{
    public static PlayerHands Instance { get; private set; }

    static readonly Color Sleeve = new Color(0.24f, 0.26f, 0.21f); // 낡은 야상
    static readonly Color Cuff = new Color(0.15f, 0.16f, 0.13f);
    static readonly Color Skin = new Color(0.72f, 0.56f, 0.46f);
    static readonly Vector3 RightBase = new Vector3(0.19f, -0.2f, 0.42f);
    static readonly Vector3 LeftBase = new Vector3(-0.2f, -0.21f, 0.42f);

    PlayerController pc;
    PlayerFlashlight flashlight;
    PlayerHotbar hotbar;
    CharacterController cc;
    Transform handsRoot, rightArm, leftArm, rightHeld, leftHeld, shadowBody;
    GameObject heldVisual;
    string heldId;
    Vector3 sway;
    float bobPhase, lowerLeft = 1f, lowerRight;
    float actionT = 1f, actionTime = 1f;
    int action; // 1 주먹·찌르기, 2 휘두르기, 3 사용, 4 새총 쏘기

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        pc = GetComponent<PlayerController>();
        flashlight = GetComponent<PlayerFlashlight>();
        hotbar = GetComponent<PlayerHotbar>();
        cc = GetComponent<CharacterController>();

        handsRoot = new GameObject("Hands").transform;
        handsRoot.SetParent(pc.cameraRoot, false);
        rightArm = BuildArm("RightArm", RightBase, 1, out rightHeld);
        leftArm = BuildArm("LeftArm", LeftBase, -1, out leftHeld);

        // 왼손 손전등. 실제 불빛(Spot Light)도 손전등 끝으로 옮겨 손을 따라 흔들린다
        var model = new GameObject("FlashlightModel").transform;
        model.SetParent(leftHeld, false);
        RuntimeVisuals.Part(PrimitiveType.Cylinder, model, new Vector3(0, 0, 0.03f), new Vector3(0.036f, 0.075f, 0.036f), new Color(0.08f, 0.08f, 0.08f), 0.6f, new Vector3(90, 0, 0));
        RuntimeVisuals.Part(PrimitiveType.Cylinder, model, new Vector3(0, 0, 0.12f), new Vector3(0.05f, 0.022f, 0.05f), new Color(0.12f, 0.12f, 0.12f), 0.7f, new Vector3(90, 0, 0));
        RuntimeVisuals.Part(PrimitiveType.Cylinder, model, new Vector3(0, 0, 0.143f), new Vector3(0.042f, 0.003f, 0.042f), new Color(1f, 0.95f, 0.8f), 0.95f, new Vector3(90, 0, 0));
        var tip = new GameObject("Tip").transform;
        tip.SetParent(model, false);
        tip.localPosition = new Vector3(0, 0, 0.16f);
        if (flashlight != null && flashlight.spot != null)
        {
            flashlight.spot.transform.SetParent(tip, false);
            flashlight.spot.transform.localPosition = Vector3.zero;
            flashlight.spot.transform.localRotation = Quaternion.identity;
        }

        foreach (var r in handsRoot.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = ShadowCastingMode.Off;
        BuildShadowBody();
    }

    Transform BuildArm(string name, Vector3 basePos, float side, out Transform held)
    {
        var pivot = new GameObject(name).transform;
        pivot.SetParent(handsRoot, false);
        pivot.localPosition = basePos;
        RuntimeVisuals.Part(PrimitiveType.Cube, pivot, new Vector3(0, -0.01f, -0.14f), new Vector3(0.09f, 0.09f, 0.28f), Sleeve, 0.1f);
        RuntimeVisuals.Part(PrimitiveType.Cube, pivot, new Vector3(0, -0.005f, 0f), new Vector3(0.105f, 0.105f, 0.05f), Cuff, 0.1f);
        RuntimeVisuals.Part(PrimitiveType.Cube, pivot, new Vector3(0, 0, 0.08f), new Vector3(0.085f, 0.045f, 0.11f), Skin, 0.35f);
        RuntimeVisuals.Part(PrimitiveType.Cube, pivot, new Vector3(0, -0.022f, 0.14f), new Vector3(0.08f, 0.042f, 0.035f), Skin, 0.35f);
        RuntimeVisuals.Part(PrimitiveType.Cube, pivot, new Vector3(-side * 0.048f, -0.006f, 0.1f), new Vector3(0.028f, 0.03f, 0.065f), Skin, 0.35f, new Vector3(0, side * 20, 0));
        held = new GameObject("Held").transform;
        held.SetParent(pivot, false);
        held.localPosition = new Vector3(0, -0.012f, 0.12f);
        return pivot;
    }

    void BuildShadowBody()
    {
        shadowBody = new GameObject("ShadowBody").transform;
        shadowBody.SetParent(transform, false);
        RuntimeVisuals.Part(PrimitiveType.Capsule, shadowBody, new Vector3(0, 1.15f, 0), new Vector3(0.48f, 0.38f, 0.3f), Sleeve);
        RuntimeVisuals.Part(PrimitiveType.Sphere, shadowBody, new Vector3(0, 1.68f, 0), new Vector3(0.23f, 0.26f, 0.24f), Skin);
        RuntimeVisuals.Part(PrimitiveType.Cube, shadowBody, new Vector3(-0.11f, 0.42f, 0), new Vector3(0.17f, 0.84f, 0.2f), Cuff);
        RuntimeVisuals.Part(PrimitiveType.Cube, shadowBody, new Vector3(0.11f, 0.42f, 0), new Vector3(0.17f, 0.84f, 0.2f), Cuff);
        RuntimeVisuals.Part(PrimitiveType.Cube, shadowBody, new Vector3(-0.31f, 1.1f, 0), new Vector3(0.11f, 0.6f, 0.11f), Sleeve);
        RuntimeVisuals.Part(PrimitiveType.Cube, shadowBody, new Vector3(0.31f, 1.1f, 0), new Vector3(0.11f, 0.6f, 0.11f), Sleeve);
        foreach (var r in shadowBody.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
    }

    public void PlayPunch() => Play(1, 0.3f);
    public void PlaySwing() => Play(2, 0.5f);
    public void PlayUse() => Play(3, 0.7f);
    public void PlayShoot() => Play(4, 0.35f);

    void Play(int a, float time)
    {
        action = a;
        actionTime = time;
        actionT = 0;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0 || handsRoot == null) return;
        var s = GameManager.Instance.State;

        UpdateHeldItem();
        leftArm.gameObject.SetActive(s.Carried.Count("flashlight") > 0);
        if (shadowBody != null) shadowBody.localScale = new Vector3(1, cc.height / 1.8f, 1);

        // 마우스를 돌리면 손이 살짝 늦게 따라온다
        Vector2 md = Mouse.current != null && !pc.InputBlocked ? Mouse.current.delta.ReadValue() : Vector2.zero;
        var targetSway = Vector3.ClampMagnitude(new Vector3(-md.x, -md.y, 0) * 0.0004f, 0.03f);
        sway = Vector3.Lerp(sway, targetSway, dt * 8);

        // 걸을 때 흔들림과 숨쉬기
        float speed = pc.HorizontalSpeed;
        bobPhase += dt * speed * 2.4f;
        float moveAmount = Mathf.Clamp01(speed / 3f);
        var bob = new Vector3(Mathf.Cos(bobPhase * 0.5f) * 0.012f, -Mathf.Abs(Mathf.Sin(bobPhase * 0.5f)) * 0.014f, 0) * moveAmount;
        handsRoot.localPosition = sway + bob + new Vector3(0, Mathf.Sin(Time.time * 1.6f) * 0.003f, 0);

        // 달릴 때는 팔을 내리고, 손전등을 끄면 왼손을 내린다
        float run = pc.IsRunning ? 1f : 0f;
        lowerRight = Mathf.MoveTowards(lowerRight, run, dt * 4);
        lowerLeft = Mathf.MoveTowards(lowerLeft, flashlight != null && flashlight.IsOn ? run * 0.5f : 1f, dt * 4);

        actionT = Mathf.Min(1f, actionT + dt / actionTime);
        var rPos = RightBase + new Vector3(0, -0.06f, -0.03f) * lowerRight;
        var rRot = Quaternion.Euler(-14 + 30 * lowerRight, -6, 0);
        if (actionT < 1f)
        {
            float k = actionT;
            if (action == 1)
            {
                float p = Mathf.Sin(k * Mathf.PI);
                rPos += new Vector3(-0.07f, 0.05f, 0.2f) * p;
                rRot *= Quaternion.Euler(-10 * p, -12 * p, 0);
            }
            else if (action == 2)
            {
                float windUp = k < 0.3f ? k / 0.3f : 1 - (k - 0.3f) / 0.7f;
                float strike = k < 0.3f ? 0 : Mathf.Sin((k - 0.3f) / 0.7f * Mathf.PI);
                rPos += new Vector3(-0.06f * strike, 0.09f * windUp - 0.05f * strike, 0.12f * strike);
                rRot *= Quaternion.Euler(-35 * windUp + 65 * strike, -25 * strike, 30 * strike);
            }
            else if (action == 3)
            {
                float p = Mathf.Sin(k * Mathf.PI);
                rPos = Vector3.Lerp(rPos, new Vector3(0.04f, -0.1f, 0.3f), p);
                rRot *= Quaternion.Euler(-40 * p, -30 * p, 0);
            }
            else if (action == 4)
            {
                // 고무줄을 놓는 순간 손이 앞으로 튕겼다가 돌아온다
                float p = Mathf.Sin(k * Mathf.PI) * (1 - k);
                rPos += new Vector3(0, 0.02f, 0.06f) * p;
                rRot *= Quaternion.Euler(-12 * p, 0, 0);
            }
        }
        rightArm.localPosition = rPos;
        rightArm.localRotation = rRot;
        leftArm.localPosition = LeftBase + new Vector3(0, -0.13f, -0.06f) * lowerLeft;
        leftArm.localRotation = Quaternion.Euler(-12 + 45 * lowerLeft, 3, 0);
    }

    // 핫바에서 고른 아이템을 오른손에 쥔다 (손전등은 왼손 담당)
    void UpdateHeldItem()
    {
        string id = hotbar != null ? hotbar.SelectedId : null;
        if (id == "flashlight") id = null;
        if (id == heldId) return;
        heldId = id;
        if (heldVisual != null) Destroy(heldVisual);
        if (id == null) return;

        heldVisual = new GameObject("Held_" + id);
        var t = heldVisual.transform;
        t.SetParent(rightHeld, false);
        ItemVisuals.Build(id, t);

        // 방망이류는 손잡이를 쥐고 위로 세워 든다 (모양은 +z 방향으로 길게 만든다)
        if (ItemDatabase.Get(id).Weapon == WeaponKind.Swing)
        {
            t.localRotation = Quaternion.Euler(-72, 0, 0);
            t.localScale = Vector3.one * 0.85f;
            t.localPosition = t.localRotation * new Vector3(0, -0.04f, 0.17f);
        }
        else
        {
            // 크기가 제각각인 아이템을 손바닥 크기에 맞춘다
            var bounds = new Bounds();
            bool first = true;
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                var lb = new Bounds(t.InverseTransformPoint(r.bounds.center), t.InverseTransformVector(r.bounds.size));
                if (first) { bounds = lb; first = false; }
                else bounds.Encapsulate(lb);
            }
            float size = Mathf.Max(Mathf.Abs(bounds.size.x), Mathf.Abs(bounds.size.y), Mathf.Abs(bounds.size.z), 0.01f);
            float scale = Mathf.Clamp(0.14f / size, 0.25f, 1.2f);
            t.localScale = Vector3.one * scale;
            t.localRotation = Quaternion.Euler(0, 25, 0);
            t.localPosition = new Vector3(0, 0.035f, 0.02f) - t.localRotation * (bounds.center * scale);
        }
        foreach (var r in heldVisual.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = ShadowCastingMode.Off;
    }
}
