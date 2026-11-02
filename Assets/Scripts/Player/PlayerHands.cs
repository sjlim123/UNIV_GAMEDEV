using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 1인칭 손: 낡은 야상 소매 + 가죽 작업 장갑을 낀 손. 손가락 마디 셋이 손잡이를 감아쥔다 (ProcMesh·ProcTex로 짓는다).
// 왼손은 손전등(손잡이 축이 앞), 오른손은 핫바에서 고른 아이템(손잡이 축이 위)을 든다.
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
        // 오른손: 손잡이를 세워 쥔다 (방망이·단검·아이템). 왼손: 손전등을 앞으로 겨눠 쥔다
        rightArm = BuildArm("RightArm", RightBase, 1, new Vector3(0, 0.95f, 0.3f), new Vector3(0.2f, -0.3f, 0.93f), out rightHeld);
        leftArm = BuildArm("LeftArm", LeftBase, -1, new Vector3(0, 0, 1f), new Vector3(-0.55f, 0.83f, 0), out leftHeld);

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

    static readonly Vector3 HeldPoint = new Vector3(0, -0.012f, 0.12f);

    // 팔 하나: 손목(pivot) 아래에 쥔 손과 팔꿈치 쪽으로 뻗은 소매.
    // gripAxis = 손잡이 축 방향, knuckleDir = 주먹 마디가 향하는 쪽. 손잡이 가운데가 held 자리에 오게 손목을 놓는다
    Transform BuildArm(string name, Vector3 basePos, float side, Vector3 gripAxis, Vector3 knuckleDir, out Transform held)
    {
        var pivot = new GameObject(name).transform;
        pivot.SetParent(handsRoot, false);
        pivot.localPosition = basePos;
        held = new GameObject("Held").transform;
        held.SetParent(pivot, false);
        held.localPosition = HeldPoint;

        // 주먹 좌표: +y = 손잡이 축, +z = 마디 쪽, 손바닥은 -x(오른손) 쪽. 손잡이 가운데는 손목에서 (-0.016, 0, 0.1)
        var rot = Quaternion.LookRotation(knuckleDir, gripAxis);
        var gripCenter = new Vector3(-0.016f * side, 0, 0.1f);
        var wrist = new GameObject("Wrist").transform;
        wrist.SetParent(pivot, false);
        wrist.localRotation = rot;
        wrist.localPosition = HeldPoint - rot * gripCenter;
        BuildFist(wrist, side);

        // 소매: 팔꿈치(화면 아래 뒤쪽)에서 손목까지
        var elbow = new Vector3(side * 0.06f, -0.24f, -0.34f);
        var toWrist = wrist.localPosition - elbow;
        var sleeve = new GameObject("Sleeve").transform;
        sleeve.SetParent(pivot, false);
        sleeve.localPosition = wrist.localPosition;
        sleeve.localRotation = Quaternion.FromToRotation(Vector3.up, toWrist.normalized);
        float L = toWrist.magnitude + 0.08f;
        var jacket = ProcTex.Get(ProcTex.Kind.Cloth, new Color(0.3f, 0.32f, 0.24f), 402, 0, 0.55f, new Color(0.2f, 0.21f, 0.16f));
        var cuffMat = ProcTex.Get(ProcTex.Kind.Cloth, new Color(0.24f, 0.25f, 0.19f), 403, 0, 0.6f);
        // 팔뚝 굵기가 줄어들며 군데군데 주름(굵기 들쭉날쭉)
        var sleeveMesh = ProcMesh.Tube("fp_sleeve" + side, 2, 18,
            -L, 0.058f, 0.054f, 0f,
            -L * 0.8f, 0.056f, 0.053f, 0.002f,
            -L * 0.62f, 0.052f, 0.05f, 0f,
            -L * 0.55f, 0.055f, 0.052f, 0.003f,
            -L * 0.4f, 0.05f, 0.048f, 0f,
            -L * 0.3f, 0.052f, 0.05f, 0.002f,
            -0.07f, 0.047f, 0.045f, 0f,
            -0.045f, 0.046f, 0.044f, 0f);
        Part(sleeve, sleeveMesh, jacket, Vector3.zero, Quaternion.identity);
        // 접어 올린 소맷단
        Part(sleeve, ProcMesh.Tube("fp_cuff", 2, 18, -0.075f, 0.053f, 0.051f, 0f, -0.06f, 0.056f, 0.054f, 0f, -0.035f, 0.055f, 0.053f, 0f, -0.025f, 0.05f, 0.048f, 0f), cuffMat, Vector3.zero, Quaternion.identity);
        return pivot;
    }

    // 장갑 낀 주먹 (오른손 기준, side = -1이면 왼손으로 뒤집는다). 손잡이를 감아쥔 모양
    void BuildFist(Transform wrist, float side)
    {
        // 길든 가죽 장갑: 가까이서 보므로 잔주름 대신 매끈한 결(피부 무늬)에 손때를 입힌다
        var glove = ProcTex.Get(ProcTex.Kind.Skin, new Color(0.44f, 0.32f, 0.21f), 401, 0, 0.5f, new Color(0.3f, 0.21f, 0.13f));
        var seam = ProcTex.Get(ProcTex.Kind.Skin, new Color(0.3f, 0.21f, 0.13f), 404, 0, 0.5f, new Color(0.2f, 0.14f, 0.09f));
        var along = Quaternion.Euler(90, 0, 0); // 관(+y)을 앞(+z)으로 눕힌다
        // 손등·손바닥 (손목 → 마디)
        Part(wrist, ProcMesh.Tube("fp_palm", 3, 16,
            -0.012f, 0.021f, 0.029f, 0f,
            0.02f, 0.022f, 0.035f, 0f,
            0.05f, 0.02f, 0.04f, 0f,
            0.075f, 0.018f, 0.042f, 0f,
            0.09f, 0.012f, 0.036f, 0f), glove, new Vector3(side * 0.012f, 0, 0), along);
        // 엄지 뿌리 근육
        Part(wrist, ProcMesh.Ellipsoid("fp_thenar", 0.017f, 0.02f, 0.03f), glove, new Vector3(-side * 0.004f, 0.022f, 0.03f), Quaternion.Euler(0, side * 20, 0));
        // 장갑 손목띠 (손목 뒤로 소매 속까지)
        Part(wrist, ProcMesh.Tube("fp_glovecuff", 2, 16, -0.06f, 0.033f, 0.038f, 0f, -0.02f, 0.031f, 0.036f, 0f, 0.0f, 0.027f, 0.033f, 0f), seam, new Vector3(side * 0.01f, 0, 0), along);

        // 손가락 넷: 위(검지)부터 아래(새끼)로. 마디마다 손잡이 쪽(-x)으로 굽는다
        float[] ys = { 0.027f, 0.009f, -0.009f, -0.027f };
        float[] scale = { 1f, 1.06f, 1f, 0.84f };
        for (int i = 0; i < 4; i++)
        {
            float k = scale[i], r = 0.0098f * (i == 3 ? 0.88f : 1f);
            var knuckle = Joint(wrist, new Vector3(side * 0.012f, ys[i], 0.086f), Quaternion.Euler(0, -side * 30, 0));
            Part(wrist, ProcMesh.Ellipsoid("fp_knuckle", 0.011f, 0.0095f, 0.01f), glove, new Vector3(side * 0.022f, ys[i], 0.083f), Quaternion.identity);
            var seg1 = FingerSegment(knuckle, 0.04f * k, r, glove);
            var mid = Joint(knuckle, new Vector3(0, 0, 0.04f * k), Quaternion.Euler(0, -side * 80, 0));
            FingerSegment(mid, 0.026f * k, r * 0.93f, glove);
            var tip = Joint(mid, new Vector3(0, 0, 0.026f * k), Quaternion.Euler(0, -side * 55, 0));
            FingerSegment(tip, 0.022f * k, r * 0.86f, glove);
            // 손가락 등의 장갑 박음질
            Part(seg1, ProcMesh.Box("fp_stitch", 0.002f, 0.0025f, 0.03f), seam, new Vector3(side * r * 0.95f, 0, 0.02f * k), Quaternion.identity);
        }
        // 엄지: 손잡이 위를 넘어 앞으로 감는다
        var thumb = Joint(wrist, new Vector3(-side * 0.004f, 0.036f, 0.036f), Quaternion.Euler(-12, -side * 42, 0));
        FingerSegment(thumb, 0.034f, 0.0115f, glove);
        var thumbTip = Joint(thumb, new Vector3(0, 0, 0.034f), Quaternion.Euler(6, -side * 40, 0));
        FingerSegment(thumbTip, 0.027f, 0.01f, glove);
    }

    // 손가락 한 마디 (+z로 길이 len, 끝이 둥글다). 다음 마디를 달 수 있게 자기 Transform을 돌려준다
    Transform FingerSegment(Transform joint, float len, float r, Material mat)
    {
        var mesh = ProcMesh.Tube($"fp_seg_{len:0.000}_{r:0.0000}", 2, 10,
            -r * 0.55f, r * 0.7f, r * 0.7f, 0f,
            -r * 0.2f, r, r, 0f,
            len * 0.55f, r * 0.97f, r * 0.94f, 0f,
            len, r * 0.9f, r * 0.88f, 0f,
            len + r * 0.6f, r * 0.55f, r * 0.55f, 0f);
        Part(joint, mesh, mat, Vector3.zero, Quaternion.Euler(90, 0, 0));
        return joint;
    }

    static Transform Joint(Transform parent, Vector3 pos, Quaternion rot)
    {
        var t = new GameObject("Joint").transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        t.localRotation = rot;
        return t;
    }

    static Transform Part(Transform parent, Mesh mesh, Material mat, Vector3 pos, Quaternion rot)
    {
        var go = new GameObject(mesh.name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        return go.transform;
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
        // 침대에 누우면 손을 감춘다
        bool asleep = pc.SleepBlend > 0.05f;
        if (handsRoot.gameObject.activeSelf == asleep) handsRoot.gameObject.SetActive(!asleep);
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

        var weapon = ItemDatabase.Get(id).Weapon;
        // 방망이류는 손잡이를 쥐고 위로 세워 든다 (모양은 +z 방향으로 길게 만든다)
        if (weapon == WeaponKind.Swing)
        {
            t.localRotation = Quaternion.Euler(-72, 0, 0);
            t.localScale = Vector3.one * 0.85f;
            t.localPosition = t.localRotation * new Vector3(0, -0.04f, 0.17f);
        }
        // 단검: 실제 크기(날 약 20cm)로 손잡이를 쥐고 칼끝을 앞으로, 날을 세워 든다
        else if (weapon == WeaponKind.Stab)
        {
            t.localRotation = Quaternion.Euler(-14, -4, 90);
            t.localScale = Vector3.one * 1.05f;
            t.localPosition = new Vector3(0.005f, 0.0f, -0.03f);
        }
        // 새총: 실제 크기로 갈래를 위로 세워 든다
        else if (weapon == WeaponKind.Sling)
        {
            t.localRotation = Quaternion.Euler(-8, 10, 0);
            t.localScale = Vector3.one;
            t.localPosition = new Vector3(0, -0.06f, 0.02f);
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
        // 라이터는 불을 켜 든다 (일렁이는 불꽃 + 작은 불빛)
        if (id == ItemDatabase.LighterId) AddLighterFlame(t);
        foreach (var r in heldVisual.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = ShadowCastingMode.Off;
    }

    void AddLighterFlame(Transform lighter)
    {
        var flame = new GameObject("Flame").transform;
        flame.SetParent(lighter, false);
        flame.localPosition = new Vector3(0.012f, 0.15f, 0);
        var outer = RuntimeVisuals.Part(PrimitiveType.Sphere, flame, Vector3.zero, new Vector3(0.022f, 0.05f, 0.022f), Color.white);
        outer.GetComponent<Renderer>().sharedMaterial = ProcTex.Glow(new Color(1f, 0.55f, 0.15f), new Color(3.2f, 1.3f, 0.25f));
        var inner = RuntimeVisuals.Part(PrimitiveType.Sphere, flame, new Vector3(0, -0.006f, 0), new Vector3(0.011f, 0.028f, 0.011f), Color.white);
        inner.GetComponent<Renderer>().sharedMaterial = ProcTex.Glow(new Color(1f, 0.95f, 0.7f), new Color(4f, 3.4f, 1.8f));
        var lg = new GameObject("FlameLight").AddComponent<Light>();
        lg.transform.SetParent(flame, false);
        lg.type = LightType.Point;
        lg.color = new Color(1f, 0.6f, 0.25f);
        lg.range = 3.5f;
        lg.intensity = 1.2f;
        lg.shadows = LightShadows.None;
        var flicker = lg.gameObject.AddComponent<FlickerLight>();
        flicker.baseIntensity = 1.2f;
        flicker.flickerAmount = 0.35f;
        flicker.speed = 14f;
        flame.gameObject.AddComponent<FlameWobble>();
    }
}
