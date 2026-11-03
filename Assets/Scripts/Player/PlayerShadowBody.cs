using UnityEngine;
using UnityEngine.Rendering;

// 그림자 전용 몸 (화면에는 그려지지 않고 그림자만 드리운다). 매 프레임 플레이어 움직임을 따라 자세를 잡는다:
// - 다리: 걸음 주기에 맞춰 발을 땅에 딛고 들어 옮긴다 (두 마디 IK). 걷기·달리기·앉아 걷기·뒷걸음·옆걸음, 공중에서는 다리를 접는다
// - 몸통·머리: 머리는 카메라(눈) 자리와 시선을 따라가고, 앉으면 엉덩이를 뒤로 빼며 몸을 숙인다
// - 팔: 1인칭 팔이 화면에 있으면 그 팔꿈치·손목을 그대로 따라가서 휘두르기·먹기 같은 몸짓도 그림자에 보인다.
//        팔이 없으면(손전등 없는 왼손) 걸음에 맞춰 흔든다
// 침대에서 잘 때는 감춘다. PlayerHands가 붙인다
public class PlayerShadowBody : MonoBehaviour
{
    const float Thigh = 0.45f, Shin = 0.43f, Ankle = 0.07f, Torso = 0.55f;
    const float UpperArm = 0.29f, LowerArm = 0.27f;
    static readonly Color Cloth = new Color(0.2f, 0.22f, 0.18f);

    // 팔·다리 한 마디: from에서 to까지 뻗는 캡슐
    class Seg
    {
        public Transform T, Body;
        public float R;

        public Seg(Transform parent, float radius, PrimitiveType type = PrimitiveType.Capsule)
        {
            R = radius;
            T = new GameObject("Seg").transform;
            T.SetParent(parent, false);
            Body = RuntimeVisuals.Part(type, T, Vector3.zero, Vector3.one, Cloth, 0.2f, new Vector3(90, 0, 0)).transform;
        }

        public void Set(Vector3 from, Vector3 to, Vector3 upHint)
        {
            var d = to - from;
            float len = Mathf.Max(0.01f, d.magnitude);
            T.localPosition = from;
            T.localRotation = Quaternion.LookRotation(d / len, upHint);
            Body.localPosition = new Vector3(0, 0, len * 0.5f);
            Body.localScale = new Vector3(R * 2, (len + R) * 0.5f, R * 2);
        }
    }

    PlayerController pc;
    PlayerHands hands;
    CharacterController cc;
    Transform root, spine, head;
    Seg[] thigh = new Seg[2], shin = new Seg[2], foot = new Seg[2];
    Seg[] upper = new Seg[2], lower = new Seg[2];
    Transform[] hand = new Transform[2];

    float phase, legYaw, moveAmount, air, crouch, hipBob;
    bool backward;
    readonly float[] armSwing = new float[2];

    void Start()
    {
        pc = GetComponent<PlayerController>();
        hands = GetComponent<PlayerHands>();
        cc = GetComponent<CharacterController>();
        Build();
    }

    void Build()
    {
        root = new GameObject("ShadowBody").transform;
        root.SetParent(transform, false);

        // 몸통: 허리에서 가슴으로 넓어졌다가 어깨에서 좁아진다 (길이 Torso, 위쪽 +y)
        spine = new GameObject("Spine").transform;
        spine.SetParent(root, false);
        var torso = new GameObject("Torso");
        torso.transform.SetParent(spine, false);
        torso.AddComponent<MeshFilter>().sharedMesh = ProcMesh.Tube("shadow_torso", 2.4f, 14,
            -0.06f, 0.15f, 0.11f, 0f,
            0.08f, 0.15f, 0.1f, 0f,
            0.25f, 0.16f, 0.1f, 0f,
            0.42f, 0.19f, 0.11f, 0f,
            0.5f, 0.18f, 0.1f, 0f,
            0.56f, 0.1f, 0.07f, 0f,
            0.6f, 0.05f, 0.05f, 0f);
        torso.AddComponent<MeshRenderer>().sharedMaterial = RuntimeVisuals.Mat(Cloth, 0.2f);

        head = RuntimeVisuals.Part(PrimitiveType.Sphere, root, Vector3.zero, new Vector3(0.19f, 0.23f, 0.21f), Cloth, 0.2f).transform;

        for (int i = 0; i < 2; i++)
        {
            thigh[i] = new Seg(root, 0.075f);
            shin[i] = new Seg(root, 0.055f);
            foot[i] = new Seg(root, 0.045f);
            upper[i] = new Seg(root, 0.042f);
            lower[i] = new Seg(root, 0.032f);
            hand[i] = RuntimeVisuals.Part(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one, Cloth, 0.2f).transform;
        }
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            r.receiveShadows = false;
        }
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (root == null || dt <= 0) return;
        bool asleep = pc.SleepBlend > 0.05f;
        if (root.gameObject.activeSelf == asleep) root.gameObject.SetActive(!asleep);
        if (asleep) return;

        var balance = GameBalance.Data;
        // 플레이어 기준 이동 방향과 빠르기
        var v = transform.InverseTransformDirection(cc.velocity);
        v.y = 0;
        float speed = pc.HorizontalSpeed;
        bool grounded = pc.IsGrounded;
        moveAmount = Mathf.MoveTowards(moveAmount, grounded ? Mathf.Clamp01(speed / Mathf.Max(0.1f, balance.WalkSpeed * 0.8f)) : 0f, dt * 5);
        air = Mathf.MoveTowards(air, grounded ? 0f : 1f, dt * 6);
        crouch = Mathf.Clamp01(Mathf.InverseLerp(pc.standHeight, pc.crouchHeight, cc.height));

        // 다리는 가는 쪽을 본다. 뒤로 가면 뒷걸음, 옆으로 가면 엉덩이를 틀어 옆걸음
        if (speed > 0.3f)
        {
            float a = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
            backward = Mathf.Abs(a) > 100f;
            float want = backward ? a - Mathf.Sign(a) * 180f : a;
            legYaw = Mathf.MoveTowardsAngle(legYaw, Mathf.Clamp(want, -70f, 70f), dt * 360f);
        }
        else legYaw = Mathf.MoveTowardsAngle(legYaw, 0, dt * 240f);

        // 걸음 주기: 두 걸음(왼발+오른발)에 가는 거리
        float cycle = pc.IsRunning ? 2.4f : Mathf.Lerp(1.5f, 1.0f, crouch);
        if (grounded) phase += speed * dt / cycle * Mathf.PI * 2;
        float stride = cycle * 0.25f * moveAmount;
        float lift = (pc.IsRunning ? 0.24f : 0.13f) * moveAmount;

        // 머리: 카메라(눈) 조금 뒤·위. 고개는 시선을 따라 숙이고 든다
        var cam = pc.cameraRoot;
        Vector3 eye = transform.InverseTransformPoint(cam.position);
        float pitch = Mathf.DeltaAngle(0, cam.localEulerAngles.x);
        var headRot = Quaternion.Euler(Mathf.Clamp(pitch, -60, 60) * 0.8f, 0, 0);
        Vector3 headPos = eye + headRot * new Vector3(0, 0.03f, -0.08f);
        head.localPosition = headPos;
        head.localRotation = headRot;

        // 엉덩이: 서면 0.96m(무릎이 거의 펴진다), 앉으면 낮아지며 뒤로 빠진다. 걸으면 걸음마다 살짝 출렁인다
        hipBob = -Mathf.Abs(Mathf.Sin(phase)) * 0.035f * moveAmount;
        Vector3 neck = new Vector3(0, headPos.y - 0.15f, headPos.z - 0.02f);
        float hipY = Mathf.Lerp(0.96f, 0.5f, crouch) + hipBob + air * 0.12f;
        hipY = Mathf.Min(hipY, neck.y - 0.3f);
        float dy = neck.y - hipY;
        float lean = Mathf.Sqrt(Mathf.Max(0, Torso * Torso - dy * dy));
        Vector3 pelvis = new Vector3(0, hipY, neck.z - lean - (pc.IsRunning ? -0.05f : 0f));
        var up = (neck - pelvis).normalized;
        spine.localPosition = pelvis;
        spine.localRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.forward, up).normalized, up);
        spine.localScale = new Vector3(1, Mathf.Max(0.6f, (neck - pelvis).magnitude / Torso), 1);

        // 다리
        var yaw = Quaternion.Euler(0, legYaw, 0);
        float dir = backward ? -1f : 1f;
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            float ph = phase + (i == 0 ? 0 : Mathf.PI);
            Vector3 hip = pelvis + yaw * new Vector3(side * 0.1f, -0.04f, 0);
            Vector3 footRest = new Vector3(side * 0.11f, Ankle, Mathf.Lerp(0.02f, -0.05f, crouch));
            Vector3 swing = new Vector3(0, Mathf.Max(0, Mathf.Cos(ph)) * lift, dir * Mathf.Sin(ph) * stride);
            // 공중: 무릎을 굽혀 발을 끌어올린다 (한 발은 조금 앞)
            Vector3 tuck = new Vector3(side * 0.12f, 0.32f + hipY - 0.96f, i == 0 ? 0.12f : -0.1f);
            Vector3 ankle = Vector3.Lerp(yaw * (footRest + swing), tuck, air);
            Vector3 fwd = yaw * Vector3.forward;
            Vector3 knee = Knee(hip, ankle, Thigh, Shin, fwd);
            thigh[i].Set(hip, knee, fwd);
            shin[i].Set(knee, ankle, fwd);
            // 발끝: 발을 들 때 살짝 들린다
            float toeUp = Mathf.Max(0, Mathf.Cos(ph)) * 18f * moveAmount + air * 30f;
            Vector3 toe = ankle + yaw * (Quaternion.Euler(-toeUp, 0, 0) * new Vector3(0, -0.035f, 0.2f));
            foot[i].Set(ankle - yaw * new Vector3(0, 0, 0.05f), toe, Vector3.up);
        }

        // 팔: 1인칭 팔이 보이면 그 자리를 그대로, 없으면 걸음에 맞춰 흔든다
        for (int i = 0; i < 2; i++)
        {
            bool rightArm = i == 1;
            float side = rightArm ? 1f : -1f;
            Vector3 shoulder = pelvis + spine.localRotation * new Vector3(side * 0.19f, Torso * 0.86f * spine.localScale.y, 0);
            Vector3 elbow, wrist;
            Quaternion handRot;
            if (hands != null && hands.ArmPose(rightArm, out var e, out var w, out var hr))
            {
                elbow = transform.InverseTransformPoint(e);
                wrist = transform.InverseTransformPoint(w);
                handRot = Quaternion.Inverse(transform.rotation) * hr;
                upper[i].Set(shoulder, elbow, Vector3.up);
                // 1인칭 소매 안쪽으로 조금 가늘게 (1인칭 팔에 그림자 자국이 지지 않게)
                lower[i].Set(elbow, wrist, Vector3.up);
                hand[i].localPosition = wrist + handRot * new Vector3(side * 0.012f, 0, 0.045f);
                hand[i].localRotation = handRot;
                hand[i].localScale = new Vector3(0.022f, 0.06f, 0.08f);
                continue;
            }
            // 걸으면 같은 쪽 다리와 반대로 흔든다. 달리면 팔꿈치를 굽혀 크게 흔든다
            float ph = phase + (rightArm ? Mathf.PI : 0);
            float run = pc.IsRunning ? 1f : 0f;
            armSwing[i] = Mathf.Lerp(armSwing[i], -Mathf.Sin(ph) * dir * (22f + 18f * run) * moveAmount, dt * 12);
            var armRot = spine.localRotation * Quaternion.Euler(armSwing[i], 0, side * 8f);
            elbow = shoulder + armRot * new Vector3(0, -UpperArm, 0);
            float bend = 18f + 62f * run + 30f * crouch;
            wrist = elbow + armRot * (Quaternion.Euler(-bend, 0, 0) * new Vector3(0, -LowerArm, 0));
            upper[i].Set(shoulder, elbow, Vector3.forward);
            lower[i].Set(elbow, wrist, Vector3.forward);
            var lowerDir = (wrist - elbow).normalized;
            hand[i].localPosition = wrist + lowerDir * 0.05f;
            hand[i].localRotation = Quaternion.LookRotation(lowerDir, spine.localRotation * Vector3.right * side);
            hand[i].localScale = new Vector3(0.03f, 0.08f, 0.1f);
        }
    }

    // 두 마디 다리의 무릎 자리: 엉덩이~발목 거리에 맞춰 무릎을 앞(forward) 쪽으로 굽힌다
    static Vector3 Knee(Vector3 hip, Vector3 ankle, float a, float b, Vector3 forward)
    {
        var d = ankle - hip;
        float len = Mathf.Clamp(d.magnitude, 0.05f, a + b - 0.001f);
        var dirN = d.normalized;
        float cos = Mathf.Clamp((a * a + len * len - b * b) / (2 * a * len), -1f, 1f);
        float ang = Mathf.Acos(cos);
        var bendDir = Vector3.ProjectOnPlane(forward, dirN);
        if (bendDir.sqrMagnitude < 1e-6f) bendDir = Vector3.forward;
        bendDir.Normalize();
        return hip + (dirN * Mathf.Cos(ang) + bendDir * Mathf.Sin(ang)) * a;
    }
}
