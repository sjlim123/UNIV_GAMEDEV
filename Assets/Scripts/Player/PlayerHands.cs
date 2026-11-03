using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 1인칭 손 (HandRig 두 개): 왼손은 손전등, 오른손은 핫바에서 고른 아이템을 아이템마다 다른 모양(HeldItems)으로 쥔다.
// 손가락은 쥔 물건 표면에 닿을 때까지 감는다(HandRig.Wrap). 바꿔 들 때는 손을 내렸다 올리며 펴진 손가락을 오므려 쥔다.
// 주먹·찌르기·휘두르기·새총·먹기·마시기·붕대 감기·구급상자·라이터 켜기·손전등 딸깍 같은 몸짓은 기본 자세 위에 얹는다.
// 조명 아래 플레이어 그림자는 그림자 전용 몸(PlayerShadowBody)이 이 팔들을 따라 움직이며 드리운다.
public class PlayerHands : MonoBehaviour
{
    public static PlayerHands Instance { get; private set; }

    // 손잡이를 쥔 손 안의 손잡이 축 (손 좌표): 새끼 쪽 손목 가까이에서 검지 뿌리 쪽으로 비스듬히 지난다
    static readonly Vector3 PowerAxis = new Vector3(0, 1f, 0.4f).normalized;

    PlayerController pc;
    PlayerFlashlight flashlight;
    PlayerHotbar hotbar;
    Transform handsRoot;
    HandRig right, left;

    // 오른손
    string heldId;
    GameObject heldModel;
    HeldSpec heldSpec;
    GameObject lighterFlame;
    readonly float[] gripPose = new float[HandRig.PoseLength]; // 쥔 손가락 (빈손이면 힘 뺀 손)
    readonly float[] fingers = new float[HandRig.PoseLength];  // 지금 손가락
    readonly float[] target = new float[HandRig.PoseLength];
    float lower = 1f; // 바꿔 들며 손을 내린 정도 (1 = 화면 아래)
    float grab = 1f;  // 올라오며 손가락을 오므린 정도
    bool flickPending;

    // 몸짓
    enum Move { None, Punch, Stab, Swing, Shoot, Use, Flick }
    Move move;
    float moveT = 1f, moveTime = 1f;
    UseGesture useKind;
    string useItem; // 쓰는 몸짓 동안 손에 보일 아이템 (소지품 창에서 쓴 것도 손에 들려 보인다)
    bool useQueued;

    // 왼손 (손전등)
    readonly float[] leftGrip = new float[HandRig.PoseLength];
    readonly float[] leftFingers = new float[HandRig.PoseLength];
    float lowerLeft = 1f, clickT = 1f;
    bool wasOn;

    Vector3 sway;
    float bobPhase, run;

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
        Init(pc.cameraRoot);
        if (GetComponent<PlayerShadowBody>() == null) gameObject.AddComponent<PlayerShadowBody>();
    }

    // 손 둘과 손전등을 만든다 (편집기 미리보기에서도 부른다)
    public void Init(Transform cameraRoot)
    {
        handsRoot = new GameObject("Hands").transform;
        handsRoot.SetParent(cameraRoot, false);
        right = new HandRig(handsRoot, "RightArm", 1);
        left = new HandRig(handsRoot, "LeftArm", -1);
        System.Array.Copy(HandRig.Relaxed, gripPose, HandRig.PoseLength);
        System.Array.Copy(HandRig.Open, fingers, HandRig.PoseLength);

        // 왼손 손전등. 실제 불빛(Spot Light)도 렌즈 앞으로 옮겨 손을 따라 흔들린다
        var model = new GameObject("FlashlightModel").transform;
        ItemVisuals.Build("flashlight", model);
        var spec = HeldItems.LeftFlashlight;
        Attach(left, model, spec);
        ComputeGrip(left, model, spec, leftGrip);
        System.Array.Copy(leftGrip, leftFingers, HandRig.PoseLength);
        left.Apply(leftFingers);
        var tip = new GameObject("Tip").transform;
        tip.SetParent(model, false);
        tip.localPosition = new Vector3(0, 0.028f, 0.125f);
        if (flashlight != null && flashlight.spot != null)
        {
            flashlight.spot.transform.SetParent(tip, false);
            flashlight.spot.transform.localPosition = Vector3.zero;
            flashlight.spot.transform.localRotation = Quaternion.identity;
        }
        foreach (var r in handsRoot.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = ShadowCastingMode.Off;
    }

    // 그림자 몸(PlayerShadowBody)이 따라갈 1인칭 팔: 화면에 보이면 true와 함께 팔꿈치·손목 자리와 손 방향 (월드 좌표)
    public bool ArmPose(bool rightArm, out Vector3 elbow, out Vector3 wrist, out Quaternion hand)
    {
        var rig = rightArm ? right : left;
        elbow = wrist = Vector3.zero;
        hand = Quaternion.identity;
        if (rig == null || handsRoot == null || !handsRoot.gameObject.activeInHierarchy || !rig.Elbow.gameObject.activeSelf) return false;
        elbow = rig.Elbow.position;
        wrist = rig.Wrist.position;
        hand = rig.Wrist.rotation;
        return true;
    }

    // ---------- 몸짓 시작 (다른 스크립트가 부른다) ----------

    // 공격 키: 빈손이면 주먹, 무언가 들었으면 그것으로 찌르듯 내민다 (단검·라이터)
    public void PlayPunch() => StartMove(heldId == null ? Move.Punch : Move.Stab, heldId == null ? 0.3f : 0.34f);
    public void PlaySwing() => StartMove(Move.Swing, 0.5f);
    public void PlayShoot() => StartMove(Move.Shoot, 0.35f);

    // 아이템을 쓴다: 그 아이템을 손에 들고(핫바에 없어도) 아이템마다 다른 몸짓을 한다
    public void PlayUse(string id)
    {
        useKind = HeldItems.Get(id).Use;
        useItem = id;
        useQueued = true;
    }

    void StartMove(Move m, float seconds)
    {
        move = m;
        moveTime = seconds;
        moveT = 0;
    }

    static float UseSeconds(UseGesture g) =>
        g == UseGesture.Drink ? 1.5f : g == UseGesture.Eat ? 1.3f : g == UseGesture.Bandage ? 1.4f : g == UseGesture.Medkit ? 1.1f : 0.8f;

    // ---------- 매 프레임 ----------

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0 || handsRoot == null) return;
        var s = GameManager.Instance.State;

        // 침대에 누우면 손을 감춘다
        bool asleep = pc.SleepBlend > 0.05f;
        if (handsRoot.gameObject.activeSelf == asleep) handsRoot.gameObject.SetActive(!asleep);

        string want = hotbar != null ? hotbar.SelectedId : null;
        if (want == "flashlight") want = null; // 손전등은 왼손 담당
        Vector2 look = Mouse.current != null && !pc.InputBlocked ? Mouse.current.delta.ReadValue() : Vector2.zero;
        Drive(dt, want, s.Carried.Count("flashlight") > 0, flashlight != null && flashlight.IsOn, pc.IsRunning, pc.HorizontalSpeed, look);
    }

    void Drive(float dt, string want, bool hasLight, bool lightOn, bool running, float speed, Vector2 look)
    {
        if (useItem != null) want = useItem;
        UpdateSwap(dt, want);

        // 마우스를 돌리면 손이 살짝 늦게 따라오고, 걸으면 흔들리고, 가만히 있으면 숨쉬듯 오르내린다
        var targetSway = Vector3.ClampMagnitude(new Vector3(-look.x, -look.y, 0) * 0.0004f, 0.03f);
        sway = Vector3.Lerp(sway, targetSway, dt * 8);
        bobPhase += dt * speed * 2.4f;
        float moveAmount = Mathf.Clamp01(speed / 3f);
        var bob = new Vector3(Mathf.Cos(bobPhase * 0.5f) * 0.012f, -Mathf.Abs(Mathf.Sin(bobPhase * 0.5f)) * 0.014f, 0) * moveAmount;
        handsRoot.localPosition = sway + bob + new Vector3(0, Mathf.Sin(Time.time * 1.6f) * 0.003f, 0);
        run = Mathf.MoveTowards(run, running ? 1f : 0f, dt * 4);

        if (moveT < 1f)
        {
            float before = moveT;
            moveT = Mathf.Min(1f, moveT + dt / moveTime);
            // 라이터: 엄지로 휠을 긁는 순간 불이 붙는다
            if (move == Move.Flick && before < 0.45f && moveT >= 0.45f && lighterFlame != null)
            {
                lighterFlame.SetActive(true);
                ProceduralAudio.Play2D(ProceduralAudio.Ignite(), 0.35f, 1.3f);
            }
            if (moveT >= 1f && move == Move.Use) useItem = null; // 다 쓰면 원래 들던 것으로 돌아간다
        }

        PoseRight(dt);
        PoseLeft(dt, hasLight, lightOn);
    }

    // 손에 든 것이 바뀌면 손을 화면 아래로 내렸다가 새 물건을 들고 올라온다
    void UpdateSwap(float dt, string want)
    {
        if (want != heldId)
        {
            lower = Mathf.MoveTowards(lower, 1f, dt / 0.14f);
            if (lower >= 1f) SetHeld(want);
            return;
        }
        lower = Mathf.MoveTowards(lower, 0f, dt / 0.24f);
        grab = Mathf.MoveTowards(grab, 1f, dt / 0.3f);
        if (lower > 0.05f) return;
        if (useQueued && useItem == heldId)
        {
            useQueued = false;
            StartMove(Move.Use, UseSeconds(useKind));
        }
        else if (flickPending && moveT >= 1f)
        {
            flickPending = false;
            StartMove(Move.Flick, 0.4f);
        }
    }

    void SetHeld(string id)
    {
        if (heldModel != null) Kill(heldModel);
        heldModel = null;
        lighterFlame = null;
        flickPending = false;
        heldId = id;
        heldSpec = id != null ? HeldItems.Get(id) : null;
        if (id == null) System.Array.Copy(HandRig.Relaxed, gripPose, HandRig.PoseLength);
        else
        {
            var model = new GameObject("Held_" + id).transform;
            ItemVisuals.Build(id, model);
            Attach(right, model, heldSpec);
            ComputeGrip(right, model, heldSpec, gripPose);
            heldModel = model.gameObject;
            // 라이터는 들고 나서 엄지로 켠다
            if (id == ItemDatabase.LighterId)
            {
                lighterFlame = AddLighterFlame(model);
                lighterFlame.SetActive(false);
                flickPending = true;
            }
        }
        // 펴진 손으로 올라오며 쥔다
        grab = 0;
        System.Array.Copy(HandRig.Open, fingers, HandRig.PoseLength);
        right.Apply(fingers);
    }

    static void Kill(Object o)
    {
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    // ---------- 쥐기 ----------

    // 아이템을 손에 놓는다: HeldSpec의 손잡이(또는 바닥)를 손 안의 쥐는 자리에 맞춘다
    static void Attach(HandRig rig, Transform model, HeldSpec spec)
    {
        float side = rig.Side;
        var point = spec.Point;
        if (spec.AutoPoint)
        {
            // 아이템 바닥(Axis 반대쪽 끝)의 내 쪽 가장자리를 손바닥에 올린다. 아이템은 손가락 너머 앞으로 뻗어 손이 가려지지 않는다
            var b = LocalBounds(model);
            var up = spec.Axis.normalized;
            var fwd = Vector3.ProjectOnPlane(spec.Second, up).normalized;
            point = b.center - up * Support(b, up) - fwd * Support(b, fwd) * 0.8f;
        }
        float r = spec.Radius * spec.Scale;
        Vector3 at, axis, second;
        switch (spec.Grip)
        {
            case GripKind.Power: // 손바닥과 손가락 사이 홈
                at = new Vector3(-side * (0.012f + r), 0, 0.07f);
                axis = PowerAxis;
                second = Vector3.forward;
                break;
            case GripKind.Hook: // 손잡이를 주먹으로 쥐고, 물건은 화면에서 똑바로 아래로 매달린다
            {
                at = new Vector3(-side * (0.012f + r), 0, 0.07f);
                axis = PowerAxis;
                BasePose(HoldPose.Hook, out _, out var arm, out var wristBend);
                second = Quaternion.Inverse(arm * wristBend) * Vector3.up;
                break;
            }
            default: // 손바닥 위 (손가락 뿌리 가까이)
                at = new Vector3(-side * 0.012f, 0, 0.062f);
                axis = new Vector3(-side, 0, 0);
                second = Vector3.forward;
                break;
        }
        var rot = Quaternion.LookRotation(axis, second) * Quaternion.Inverse(Quaternion.LookRotation(spec.Axis, spec.Second));
        model.SetParent(rig.Wrist, false);
        model.localScale = Vector3.one * spec.Scale;
        model.localRotation = rot;
        model.localPosition = at - rot * (point * spec.Scale);
        if (spec.Hide != null)
            foreach (int i in spec.Hide)
                if (i < model.childCount) model.GetChild(i).gameObject.SetActive(false);
        foreach (var rd in model.GetComponentsInChildren<Renderer>())
            rd.shadowCastingMode = ShadowCastingMode.Off;
    }

    // 경계 상자가 dir 쪽으로 가운데에서 뻗은 길이
    static float Support(Bounds b, Vector3 dir) => Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.y) * b.extents.y + Mathf.Abs(dir.z) * b.extents.z;

    // 아이템 좌표의 경계 상자 (아직 아무 데도 붙이지 않은 원점의 아이템)
    static Bounds LocalBounds(Transform model)
    {
        var b = new Bounds();
        bool first = true;
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    // 쥐는 모양마다 손가락 시작 자세와 최대로 굽히는 자세 (손가락마다 벌림, 뿌리, 가운데, 끝)
    static readonly float[] PowerStart = { 22, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, -3, 0, 0, 0, -6, 0, 0, 0 };
    static readonly float[] PowerMax = { 22, 40, 45, 55, 3, 100, 110, 80, 0, 100, 110, 80, -3, 100, 110, 80, -6, 100, 110, 80 };
    // 손바닥: 손가락은 받치듯 살짝 굽히고, 엄지는 아이템 위를 누른다
    static readonly float[] PalmStart = { 8, 0, 0, 0, 5, 0, 0, 0, 0, 0, 0, 0, -4, 0, 0, 0, -9, 0, 0, 0 };
    static readonly float[] PalmMax = { 8, 40, 45, 45, 5, 38, 46, 34, 0, 40, 48, 34, -4, 42, 50, 36, -9, 44, 52, 38 };

    static void ComputeGrip(HandRig rig, Transform model, HeldSpec spec, float[] pose)
    {
        var shapes = HandRig.ShapesOf(model);
        float[] start = PalmStart, max = PalmMax;
        int mask = 0b11111;
        if (spec.Grip == GripKind.Power) { start = PowerStart; max = PowerMax; }
        else if (spec.Grip == GripKind.Hook) { start = PowerStart; max = PowerMax; }
        System.Array.Copy(start, pose, HandRig.PoseLength);
        rig.Wrap(pose, max, shapes, mask);
    }

    // ---------- 자세 ----------

    // 오른손 기준 자세 (화면 좌표: 손목 자리, 팔뚝 방향, 손목 꺾임). 왼손은 좌우를 뒤집어 쓴다
    static void BasePose(HoldPose pose, out Vector3 wrist, out Quaternion rot, out Quaternion bend)
    {
        bend = Quaternion.identity;
        switch (pose)
        {
            case HoldPose.Upright:
                // 손잡이를 세우고 끝을 살짝 어깨 뒤로 젖힌다 (팔뚝은 오른쪽 아래에서 앞으로)
                wrist = new Vector3(0.21f, -0.165f, 0.4f);
                rot = Aim(new Vector3(0.2f, 0.85f, -0.35f), new Vector3(-0.3f, 0.3f, 0.9f));
                break;
            case HoldPose.Hold:
                // 똑바로 세워 든다 (팔뚝은 오른쪽 아래에서 앞으로)
                wrist = new Vector3(0.2f, -0.185f, 0.4f);
                rot = Aim(new Vector3(0.02f, 1f, 0.08f), new Vector3(-0.3f, 0.3f, 0.9f));
                break;
            case HoldPose.Forward:
                // 손잡이 축이 화면 가운데(앞)를 겨눈다. 팔뚝은 아래에서 올라온다
                wrist = new Vector3(0.2f, -0.17f, 0.42f);
                rot = Aim(new Vector3(-0.03f, 0.04f, 1f), new Vector3(-0.45f, 0.85f, 0.2f));
                break;
            case HoldPose.Palm:
                // 손바닥을 위로, 조금 내 쪽으로 기울여 내민다
                wrist = new Vector3(0.16f, -0.2f, 0.36f);
                rot = HandRig.Look(new Vector3(-0.3f, 0.2f, 0.93f), new Vector3(0.92f, -0.1f, 0.35f));
                break;
            case HoldPose.Hook:
                // 양동이 들 듯: 가로로 누운 손잡이를 주먹으로 쥐고 앞으로 내밀어, 물건이 주먹 아래로 매달린다
                wrist = new Vector3(0.19f, -0.09f, 0.5f);
                rot = Aim(new Vector3(-1f, 0.05f, 0.25f), new Vector3(-0.25f, 0.4f, 0.88f));
                break;
            default: // Ready: 힘을 뺀 빈손 (느슨한 주먹, 손등이 바깥)
                wrist = ReadyWrist;
                rot = HandRig.Look(ReadyFingers, ReadyThumb);
                break;
        }
    }

    // 빈손: 손끝이 화면 가운데 쪽 앞을 보고, 엄지가 위, 손등이 바깥 위로 보인다
    static readonly Vector3 ReadyWrist = new Vector3(0.21f, -0.21f, 0.36f), ReadyFingers = new Vector3(-0.2f, 0.22f, 0.95f), ReadyThumb = new Vector3(-0.3f, 0.92f, -0.1f);

    // 손잡이를 쥔 손을 놓는다: 손 안의 손잡이 축(PowerAxis)이 grip 방향을, 팔뚝이 forearm 방향을 향하게
    static Quaternion Aim(Vector3 grip, Vector3 forearm) =>
        Quaternion.LookRotation(grip, forearm) * Quaternion.Inverse(Quaternion.LookRotation(PowerAxis, Vector3.forward));

    static float Ease(float t) => t * t * (3 - 2 * t);

    // 몸짓의 돌림(화면 좌표) 가운데 팔뚝이 맡는 몫. 나머지는 손목이 꺾여 맡는다 (팔뚝이 크게 돌면 소매가 화면을 가로지른다)
    const float ArmShare = 0.55f;

    void PoseRight(float dt)
    {
        BasePose(heldSpec != null ? heldSpec.Pose : HoldPose.Ready, out var wrist, out var rot, out var bend);
        if (heldSpec != null)
        {
            wrist += heldSpec.Offset;
            rot = Quaternion.Euler(heldSpec.Euler) * rot;
        }
        HandRig.Blend(HandRig.Open, gripPose, Ease(grab), target);
        var turn = Quaternion.identity;
        if (moveT < 1f) ApplyMove(ref wrist, ref turn);

        // 달리면 팔을 내리고, 바꿔 들 때는 화면 아래로 내린다
        float l = Ease(lower);
        wrist += new Vector3(0, -0.06f, -0.03f) * run + new Vector3(0.03f, -0.2f, -0.08f) * l;
        rot = Quaternion.Euler(22 * run + 45 * l, 0, 0) * rot;
        // 손은 turn만큼 돌고, 팔뚝은 그 일부만 돈다
        var forearm = Quaternion.Slerp(Quaternion.identity, turn, ArmShare) * rot;
        right.Place(wrist, forearm, Quaternion.Inverse(forearm) * turn * rot * bend);

        float k = 1 - Mathf.Exp(-dt * 28);
        for (int i = 0; i < HandRig.PoseLength; i++) fingers[i] = Mathf.Lerp(fingers[i], target[i], k);
        right.Apply(fingers);
    }

    // 몸짓을 기본 자세 위에 얹는다 (손목을 화면 좌표로 옮기고, 손을 turn만큼 돌린다)
    void ApplyMove(ref Vector3 wrist, ref Quaternion turn)
    {
        float t = moveT;
        float p = Mathf.Sin(t * Mathf.PI);
        // 빨리 나갔다가 천천히 돌아오는 곡선
        float thrust = t < 0.3f ? Ease(t / 0.3f) : 1 - Ease((t - 0.3f) / 0.7f);
        Vector3 d = Vector3.zero, e = Vector3.zero;
        switch (move)
        {
            case Move.Punch:
                d = new Vector3(-0.08f, 0.06f, 0.24f) * thrust;
                e = new Vector3(-10, -15, 0) * thrust;
                HandRig.Blend(target, HandRig.Fist, Mathf.Clamp01(p * 3), target);
                break;
            case Move.Stab:
                d = new Vector3(-0.06f, 0.04f, 0.22f) * thrust;
                e = new Vector3(-6, -8, 0) * thrust;
                break;
            case Move.Swing:
            {
                // 오른쪽 위로 젖혔다가 왼쪽 아래로 크게 휘두른다
                float w = t < 0.3f ? Ease(t / 0.3f) : 1 - Ease((t - 0.3f) / 0.7f);
                float s = t < 0.3f ? 0 : Mathf.Sin((t - 0.3f) / 0.7f * Mathf.PI);
                d = new Vector3(0.05f * w - 0.25f * s, 0.06f * w - 0.08f * s, -0.02f * w + 0.08f * s);
                e = new Vector3(-20 * w + 45 * s, 0, -20 * w + 75 * s);
                break;
            }
            case Move.Shoot:
            {
                // 고무줄을 놓는 순간 손이 앞으로 튕겼다가 돌아온다
                float k = p * (1 - t);
                d = new Vector3(0, 0.02f, 0.06f) * k;
                e = new Vector3(-12 * k, 0, 0);
                break;
            }
            case Move.Flick:
                // 엄지로 라이터 휠을 긁어 내린다
                target[1] += 30 * p;
                target[2] += 40 * p;
                target[3] += 45 * p;
                e = new Vector3(0, 0, 5 * p);
                break;
            case Move.Use:
                UseMove(t, ref d, ref e);
                break;
        }
        wrist += d;
        turn = Quaternion.Euler(e);
    }

    // 쓰는 몸짓: 입·팔목 쪽으로 가져가서(r) 아이템마다 다른 동작을 하고 돌아온다
    void UseMove(float t, ref Vector3 d, ref Vector3 e)
    {
        float r = t < 0.25f ? Ease(t / 0.25f) : t > 0.8f ? 1 - Ease((t - 0.8f) / 0.2f) : 1f;
        float hold = Mathf.Clamp01((t - 0.25f) / 0.55f);
        float during = hold > 0 && hold < 1 ? 1f : 0f;
        switch (useKind)
        {
            case UseGesture.Drink:
            {
                // 병을 입에 대고 뒤로 기울여 꿀꺽꿀꺽
                float gulp = Mathf.Sin(hold * Mathf.PI * 6) * 4 * during;
                d = new Vector3(-0.11f, 0.13f, -0.15f) * r;
                e = new Vector3(-75 * r + gulp, 0, 15 * r);
                break;
            }
            case UseGesture.Eat:
            {
                // 입으로 가져가 세 번 베어 문다
                float bite = Mathf.Abs(Mathf.Sin(hold * Mathf.PI * 3)) * during;
                d = new Vector3(-0.11f, 0.1f, -0.14f) * r + new Vector3(0, 0.012f, 0.015f) * bite;
                e = new Vector3(-40 * r - 8 * bite, 0, 15 * r);
                break;
            }
            case UseGesture.Bandage:
            {
                // 왼팔에 붕대를 두 번 돌려 감는다
                float a = hold * Mathf.PI * 4;
                d = new Vector3(-0.18f, 0.07f, -0.06f) * r + new Vector3(Mathf.Cos(a) * 0.035f, Mathf.Sin(a) * 0.03f, 0) * during;
                e = new Vector3(-15 * r, 0, 50 * r + Mathf.Sin(a) * 25 * during);
                break;
            }
            case UseGesture.Medkit:
            {
                // 구급상자를 들어 올려 흔들어 연다
                float shake = Mathf.Sin(hold * Mathf.PI * 8) * during;
                d = new Vector3(-0.09f, 0.08f, -0.06f) * r + new Vector3(0, 0.006f, 0) * shake;
                e = new Vector3(-25 * r, 0, 6 * shake);
                break;
            }
            default:
                d = new Vector3(-0.12f, 0.1f, -0.12f) * r;
                e = new Vector3(-35 * r, -20 * r, 0);
                break;
        }
    }

    // 손잡이를 쥔 손을 놓는다: 손잡이(PowerAxis)가 grip 쪽을, 팔꿈치가 elbowSide 쪽을 향하고,
    // 손목은 새끼손가락 쪽으로 deviation도만큼 꺾인다 (음수면 엄지 쪽으로).
    // forearm = 팔뚝 방향, bend = 팔뚝에 대한 손 방향 (HandRig.Place에 그대로 넘긴다)
    static void GripAlong(Vector3 grip, Vector3 elbowSide, float deviation, out Quaternion forearm, out Quaternion bend)
    {
        float r = deviation * Mathf.Deg2Rad;
        var toElbow = new Vector3(0, -Mathf.Sin(r), -Mathf.Cos(r)); // 손 좌표에서 팔꿈치 쪽
        var hand = Quaternion.LookRotation(grip, elbowSide) * Quaternion.Inverse(Quaternion.LookRotation(PowerAxis, toElbow));
        bend = Quaternion.AngleAxis(deviation, Vector3.right);
        forearm = hand * Quaternion.Inverse(bend);
    }

    // 왼손 손전등 자세 (화면 좌표): 손등이 위로 오게 위에서 감아쥐고(오버핸드), 렌즈는 새끼손가락 쪽으로 나와 앞을 비춘다.
    // 팔뚝은 화면 왼쪽에서 들어오고 손목은 엄지 쪽으로 조금 꺾는다. 끄면 왼쪽 아래로 내려 바닥 쪽을 향한다
    static readonly Vector3 LeftOnWrist = new Vector3(-0.18f, -0.17f, 0.4f), LeftOnAim = new Vector3(0.12f, 0.06f, 1f), LeftOnElbow = new Vector3(-0.9f, -0.35f, -0.25f);
    static readonly Vector3 LeftOffWrist = new Vector3(-0.25f, -0.36f, 0.3f), LeftOffAim = new Vector3(0.05f, -0.75f, 0.65f), LeftOffElbow = new Vector3(-0.8f, -0.2f, -0.5f);
    const float LeftDeviation = -22f;

    // 왼손: 손전등을 켜면 들어 올리고, 끄면 내린다. 켜고 끌 때 엄지로 버튼을 딸깍 누른다
    void PoseLeft(float dt, bool hasLight, bool on)
    {
        if (left.Elbow.gameObject.activeSelf != hasLight) left.Elbow.gameObject.SetActive(hasLight);
        if (!hasLight) return;
        if (on != wasOn)
        {
            wasOn = on;
            clickT = 0;
        }
        lowerLeft = Mathf.MoveTowards(lowerLeft, on ? run * 0.5f : 1f, dt * 4);
        clickT = Mathf.Min(1f, clickT + dt / 0.22f);

        float l = Ease(lowerLeft);
        float c = clickT < 1f ? Mathf.Sin(clickT * Mathf.PI) : 0;
        var wrist = Vector3.Lerp(LeftOnWrist, LeftOffWrist, l) + new Vector3(0, -0.006f, 0) * c;
        // 손잡이 축(엄지 쪽)은 렌즈 반대쪽을 향한다
        GripAlong(-Vector3.Slerp(LeftOnAim.normalized, LeftOffAim.normalized, l), Vector3.Slerp(LeftOnElbow.normalized, LeftOffElbow.normalized, l), LeftDeviation, out var forearm, out var bend);
        left.Place(wrist, forearm, bend);

        System.Array.Copy(leftGrip, leftFingers, HandRig.PoseLength);
        leftFingers[2] += 25 * c;
        leftFingers[3] += 30 * c;
        left.Apply(leftFingers);
    }

    GameObject AddLighterFlame(Transform lighter)
    {
        var flame = new GameObject("Flame").transform;
        flame.SetParent(lighter, false);
        flame.localPosition = new Vector3(0.012f, 0.142f, 0);
        var outer = RuntimeVisuals.Part(PrimitiveType.Sphere, flame, Vector3.zero, new Vector3(0.022f, 0.05f, 0.022f), Color.white);
        outer.GetComponent<Renderer>().sharedMaterial = ProcTex.Glow(new Color(1f, 0.55f, 0.15f), new Color(3.2f, 1.3f, 0.25f));
        var inner = RuntimeVisuals.Part(PrimitiveType.Sphere, flame, new Vector3(0, -0.006f, 0), new Vector3(0.011f, 0.028f, 0.011f), Color.white);
        inner.GetComponent<Renderer>().sharedMaterial = ProcTex.Glow(new Color(1f, 0.95f, 0.7f), new Color(4f, 3.4f, 1.8f));
        foreach (var r in flame.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        var lg = new GameObject("FlameLight").AddComponent<Light>();
        lg.transform.SetParent(flame, false);
        lg.type = LightType.Point;
        lg.color = new Color(1f, 0.6f, 0.25f);
        lg.range = 2.8f;
        lg.intensity = 0.45f;
        lg.shadows = LightShadows.None;
        var flicker = lg.gameObject.AddComponent<FlickerLight>();
        flicker.baseIntensity = 0.45f;
        flicker.flickerAmount = 0.35f;
        flicker.speed = 14f;
        flame.gameObject.AddComponent<FlameWobble>();
        return flame.gameObject;
    }

    // ---------- 편집기 미리보기 (손 모양을 맞추는 도구에서 쓴다) ----------

    // 카메라 아래에 손을 만들고 id를 든 채 몸짓(move 0 없음, 1 주먹, 2 찌르기, 3 휘두르기, 4 새총, 5 쓰기, 6 라이터)을 t(0~1)만큼 진행한 모습으로 멈춘다
    public void Preview(string id, int moveIndex, float t, bool lightOn)
    {
        if (id == "flashlight") id = null;
        if (id != heldId) SetHeld(id);
        lower = 0;
        grab = 1;
        run = 0;
        move = (Move)moveIndex;
        if (move == Move.Use && id != null) useKind = HeldItems.Get(id).Use;
        moveT = move == Move.None ? 1f : t;
        if (lighterFlame != null) lighterFlame.SetActive(move != Move.Flick || t >= 0.45f);
        lowerLeft = lightOn ? 0 : 1;
        wasOn = lightOn;
        clickT = 1;
        handsRoot.localPosition = Vector3.zero;
        PoseRight(10f);
        PoseLeft(10f, true, lightOn);
        lowerLeft = lightOn ? 0 : 1;
        PoseLeft(0.0001f, true, lightOn);
    }
}
