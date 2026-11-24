using UnityEngine;
using UnityEngine.Rendering;

// 그림자 전용 몸 (화면에는 그려지지 않고 그림자만 드리운다). 매 프레임 플레이어 움직임을 따라 자세를 잡는다:
// - 다리: 걸음 주기에 맞춰 발을 땅에 딛고 들어 옮긴다 (두 마디 IK). 걷기·달리기·앉아 걷기·뒷걸음·옆걸음, 공중에서는 다리를 접는다
// - 몸통·머리: 머리는 카메라(눈) 자리와 시선을 따라가고, 앉으면 엉덩이를 뒤로 빼며 몸을 숙인다
// - 팔: 1인칭 팔이 화면에 있으면 그 팔꿈치·손목을 그대로 따라가서 휘두르기·먹기 같은 몸짓도 그림자에 보인다.
//        팔이 없으면(손전등 없는 왼손) 걸음에 맞춰 흔든다
// 몸 모양 (2026-11-24부터): 플레이어 모델(Tripo, Resources/Player/player_shadow_body)의 뼈를 위 자세대로 돌린다.
//   모델 판은 세부 묘사 품질로 고른다 (낮음·중간 6천, 높음·매우 높음 2.4만 삼각형). 그림자를 끄면 몸도 끈다 (뼈 계산·스키닝 없음).
//   모델을 못 읽으면 예전처럼 캡슐로 만든 몸을 쓴다.
// 침대에서 잘 때·죽는 연출·3인칭 장면(손을 감출 때)에는 감춘다. PlayerHands가 붙인다
public class PlayerShadowBody : MonoBehaviour
{
    static readonly Color Cloth = new Color(0.2f, 0.22f, 0.18f);

    // 몸 치수 (캡슐 몸 기본값, 모델을 쓰면 모델 뼈 길이로 바뀐다)
    float thighLen = 0.45f, shinLen = 0.43f, ankleH = 0.07f, torsoLen = 0.55f;
    float upperArmLen = 0.29f, lowerArmLen = 0.27f;
    float standHip = 0.96f, crouchHip = 0.5f;
    Vector3 eyeToNeck = new Vector3(0, -0.12f, -0.1f);

    // 팔·다리 한 마디: from에서 to까지 뻗는 캡슐 (캡슐 몸)
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

    // 모델 뼈 하나: 바인드(T자세) 때 몸 기준 회전과, 겨누기용 바인드 방향
    class Bone
    {
        public Transform T;
        public Quaternion BindRel;           // 몸 기준 회전 (바인드)
        public Quaternion AimOffset;         // LookRotation(겨눌 방향, 위) 뒤에 곱한다
        public Quaternion BindLocal;         // 부모 기준 회전 (바인드)
        public Vector3 BindLocalPos;
        public float CurlRest, CurlGrip;     // 손가락 뼈: 늘어뜨릴 때·쥘 때 굽힘 (도)
    }

    PlayerController pc;
    PlayerHands hands;
    CharacterController cc;
    Transform root, spine, head;

    // 캡슐 몸
    Seg[] thigh = new Seg[2], shin = new Seg[2], foot = new Seg[2];
    Seg[] upper = new Seg[2], lower = new Seg[2];
    Transform[] hand = new Transform[2];

    // 모델 몸
    bool skinned;
    Transform model;
    SkinnedMeshRenderer[] tiers;
    int tierOn = -2;
    Bone hips, chest, neckB, headB;
    Bone[] upLeg = new Bone[2], loLeg = new Bone[2], footB = new Bone[2];
    Bone[] upArm = new Bone[2], loArm = new Bone[2], handB = new Bone[2];
    Bone[][] fingers = new Bone[2][];
    Vector3[] curlAxis = new Vector3[2];     // 손가락을 손바닥 쪽으로 굽히는 축 (각 손가락 뼈 기준은 Bone마다)
    Vector3[][] fingerAxis = new Vector3[2][];
    Vector3 torsoBindDir;

    float phase, legYaw, moveAmount, air, crouch, hipBob;
    bool backward;
    readonly float[] armSwing = new float[2];
    readonly float[] grip = new float[2];

    void Start()
    {
        pc = GetComponent<PlayerController>();
        hands = GetComponent<PlayerHands>();
        cc = GetComponent<CharacterController>();
        root = new GameObject("ShadowBody").transform;
        root.SetParent(transform, false);
        var prefab = Resources.Load<GameObject>("Player/player_shadow_body");
        if (prefab != null && BuildSkinned(prefab)) skinned = true;
        else BuildCapsules();
    }

    // ---------------- 모델 몸 ----------------

    bool BuildSkinned(GameObject prefab)
    {
        model = Instantiate(prefab, root, false).transform;
        model.name = "Model";
        model.localPosition = Vector3.zero; model.localRotation = Quaternion.identity; model.localScale = Vector3.one;
        tiers = new SkinnedMeshRenderer[2];
        foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (r.name == "Shadow_q0") tiers[0] = r;
            else if (r.name == "Shadow_q1") tiers[1] = r;
            r.enabled = false;
            r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            r.receiveShadows = false;
        }
        var all = model.GetComponentsInChildren<Transform>(true);
        System.Func<string, Transform> find = n => { foreach (var t in all) if (t.name == n) return t; return null; };
        var hipsT = find("Hips"); var neckT = find("Neck"); var eyeL = find("Left_Eye"); var eyeR = find("Right_Eye");
        if (tiers[0] == null || tiers[1] == null || hipsT == null || neckT == null || eyeL == null || eyeR == null)
        {
            Destroy(model.gameObject);
            return false;
        }

        // 키: 서 있을 때 모델 눈이 카메라 높이에 오게 키운다 (카메라 = 플레이어 발밑 + 키 - 0.15, 발밑은 바닥에서 피부 두께만큼 떠 있다)
        Vector3 eyeBind = (eyeL.position + eyeR.position) * 0.5f;
        float eyeH = root.InverseTransformPoint(eyeBind).y;
        float s = (pc.standHeight - 0.15f + cc.skinWidth) / Mathf.Max(0.1f, eyeH);
        model.localScale = Vector3.one * s;
        eyeBind = (eyeL.position + eyeR.position) * 0.5f;

        // 바인드 자세 기록 (아직 아무 뼈도 돌리지 않았다)
        System.Func<string, Bone> mk = n =>
        {
            var t = find(n);
            if (t == null) return null;
            return new Bone { T = t, BindRel = Quaternion.Inverse(root.rotation) * t.rotation, BindLocal = t.localRotation, BindLocalPos = t.localPosition };
        };
        System.Func<Vector3, Vector3> L = p => root.InverseTransformPoint(p);
        System.Func<Vector3, Vector3> LD = d => root.InverseTransformDirection(d);

        hips = mk("Hips"); chest = mk("UpperChest") ?? mk("Chest"); neckB = mk("Neck"); headB = mk("Head");
        torsoBindDir = (L(neckT.position) - L(hipsT.position)).normalized;
        torsoLen = (L(neckT.position) - L(hipsT.position)).magnitude;
        eyeToNeck = L(neckT.position) - L(eyeBind);

        string[] side = { "Left_", "Right_" };
        string[] fn = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
        string[] seg = { "Proximal", "Intermediate", "Distal" };
        for (int i = 0; i < 2; i++)
        {
            string p = side[i];
            upLeg[i] = mk(p + "UpperLeg"); loLeg[i] = mk(p + "LowerLeg"); footB[i] = mk(p + "Foot");
            upArm[i] = mk(p + "UpperArm"); loArm[i] = mk(p + "LowerArm"); handB[i] = mk(p + "Hand");
            if (upLeg[i] == null || loLeg[i] == null || footB[i] == null || upArm[i] == null || loArm[i] == null || handB[i] == null)
            {
                Destroy(model.gameObject);
                return false;
            }
            // 다리: 아래로 겨누고 무릎은 앞(+z)으로 굽는다
            Aim(upLeg[i], L(loLeg[i].T.position) - L(upLeg[i].T.position), Vector3.forward);
            Aim(loLeg[i], L(footB[i].T.position) - L(loLeg[i].T.position), Vector3.forward);
            // 팔: T자세로 옆으로 뻗고, 팔꿈치는 앞(+z)으로 굽는다
            Aim(upArm[i], L(loArm[i].T.position) - L(upArm[i].T.position), Vector3.forward);
            // 손: 손가락 쪽과 엄지 쪽
            var mid = find(p + "MiddleProximal"); var thumb = find(p + "ThumbDistal") ?? find(p + "ThumbProximal");
            Vector3 fDir = L(mid != null ? mid.position : handB[i].T.position + (handB[i].T.position - loArm[i].T.position)) - L(handB[i].T.position);
            Vector3 tDir = thumb != null ? Vector3.ProjectOnPlane(L(thumb.position) - L(handB[i].T.position), fDir.normalized) : Vector3.forward;
            if (tDir.sqrMagnitude < 1e-6f) tDir = Vector3.forward;
            Aim(handB[i], fDir, tDir);
            // 아래팔은 엄지 쪽을 위로 삼아 손과 같이 비튼다
            Aim(loArm[i], L(handB[i].T.position) - L(loArm[i].T.position), tDir);
            // 손바닥 쪽: 왼손은 엄지×손가락, 오른손은 그 반대 (1인칭 손: +z 손가락, +y 엄지, 손바닥 -x·Side와 같은 짝)
            Vector3 palm = (i == 0 ? Vector3.Cross(tDir, fDir) : Vector3.Cross(fDir, tDir)).normalized;
            curlAxis[i] = Vector3.Cross(fDir.normalized, palm).normalized;
            upperArmLen = (L(loArm[i].T.position) - L(upArm[i].T.position)).magnitude;
            lowerArmLen = (L(handB[i].T.position) - L(loArm[i].T.position)).magnitude;

            var fl = new System.Collections.Generic.List<Bone>(); var fa = new System.Collections.Generic.List<Vector3>();
            foreach (var f in fn)
                foreach (var sg in seg)
                {
                    var b = mk(p + f + sg);
                    if (b == null) continue;
                    float k = f == "Thumb" ? 0.45f : 1f;
                    b.CurlGrip = (sg == "Proximal" ? 50f : sg == "Intermediate" ? 62f : 42f) * k;
                    b.CurlRest = (sg == "Proximal" ? 12f : sg == "Intermediate" ? 16f : 10f) * k;
                    fl.Add(b);
                    fa.Add(b.T.InverseTransformDirection(root.TransformDirection(curlAxis[i])));
                }
            fingers[i] = fl.ToArray(); fingerAxis[i] = fa.ToArray();
        }

        thighLen = (L(loLeg[0].T.position) - L(upLeg[0].T.position)).magnitude;
        shinLen = (L(footB[0].T.position) - L(loLeg[0].T.position)).magnitude;
        ankleH = L(footB[0].T.position).y;
        float hipDrop = L(hips.T.position).y - L(upLeg[0].T.position).y;
        standHip = ankleH + (thighLen + shinLen) * 0.975f + hipDrop;
        crouchHip = Mathf.Min(0.5f, standHip * 0.55f);
        return true;
    }

    // 겨누기 준비: 바인드 때 이 뼈가 aimDir(몸 기준)을 향하고 up 쪽이 upDir이었다
    void Aim(Bone b, Vector3 aimDir, Vector3 upDir)
    {
        b.AimOffset = Quaternion.Inverse(Quaternion.LookRotation(aimDir, upDir)) * b.BindRel;
    }

    // 뼈를 몸 기준 aim 방향으로 겨눈다 (up은 굽는 쪽·비틀림)
    void PointBone(Bone b, Vector3 aim, Vector3 up)
    {
        if (aim.sqrMagnitude < 1e-8f) return;
        if (Vector3.Cross(aim, up).sqrMagnitude < 1e-8f) up = Vector3.ProjectOnPlane(Vector3.forward, aim.normalized) + Vector3.up * 0.01f;
        b.T.rotation = root.rotation * Quaternion.LookRotation(aim, up) * b.AimOffset;
    }

    // 몸 기준 회전 rel만큼 바인드에서 돌린다
    void TurnBone(Bone b, Quaternion rel)
    {
        b.T.rotation = root.rotation * rel * b.BindRel;
    }

    Vector3 Local(Transform t) => root.InverseTransformPoint(t.position);

    void ApplyTier()
    {
        int want = !GameSettings.Shadows ? -1 : GameSettings.DetailQ >= 2 ? 1 : 0;
        if (want == tierOn) return;
        tierOn = want;
        for (int i = 0; i < tiers.Length; i++) if (tiers[i] != null) tiers[i].enabled = i == want;
    }

    // ---------------- 캡슐 몸 (모델이 없을 때) ----------------

    void BuildCapsules()
    {
        // 몸통: 허리에서 가슴으로 넓어졌다가 어깨에서 좁아진다 (길이 torsoLen, 위쪽 +y)
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

    // ---------------- 자세 ----------------

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (root == null || dt <= 0) return;
        bool hide = pc.SleepBlend > 0.05f || pc.IsDead || (hands != null && hands.IsHidden);
        if (root.gameObject.activeSelf == hide) root.gameObject.SetActive(!hide);
        if (hide) return;
        if (skinned)
        {
            ApplyTier();
            if (tierOn < 0) return; // 그림자를 끄면 자세도 잡지 않는다
        }

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

        // 몸은 카메라(눈) 아래 바닥에 선다: 머리 흔들림·출발 연출로 카메라가 옆으로 가면 몸도 같이 간다.
        // 플레이어 발밑은 바닥에서 캐릭터 컨트롤러 피부 두께만큼 떠 있으니 몸은 그만큼 내린다
        var cam = pc.cameraRoot;
        Vector3 eyeP = transform.InverseTransformPoint(cam.position);
        float skin = cc.skinWidth;
        root.localPosition = new Vector3(eyeP.x, -skin, eyeP.z);
        Vector3 eye = new Vector3(0, eyeP.y + skin, 0);

        // 머리: 고개는 시선을 따라 숙이고 든다 (목을 축으로 돈다)
        float pitch = Mathf.DeltaAngle(0, cam.localEulerAngles.x);
        var headRot = Quaternion.Euler(Mathf.Clamp(pitch, -60, 60) * 0.8f, 0, 0);
        Vector3 neck = eye + headRot * eyeToNeck;

        // 엉덩이: 서면 무릎이 거의 펴지는 높이, 앉으면 낮아지며 뒤로 빠진다. 걸으면 걸음마다 살짝 출렁인다
        hipBob = -Mathf.Abs(Mathf.Sin(phase)) * 0.035f * moveAmount;
        float hipY = Mathf.Lerp(standHip, crouchHip, crouch) + hipBob + air * 0.12f;
        hipY = Mathf.Min(hipY, neck.y - 0.3f);
        float dy = neck.y - hipY;
        float lean = Mathf.Sqrt(Mathf.Max(0, torsoLen * torsoLen - dy * dy));
        Vector3 pelvis = new Vector3(0, hipY, neck.z - lean - (pc.IsRunning ? -0.05f : 0f));
        var up = (neck - pelvis).normalized;
        var yaw = Quaternion.Euler(0, legYaw, 0);
        float dir = backward ? -1f : 1f;

        Quaternion torsoRot;
        if (skinned)
        {
            torsoRot = Quaternion.FromToRotation(torsoBindDir, up);
            hips.T.position = root.TransformPoint(pelvis);
            TurnBone(hips, torsoRot * Quaternion.Euler(0, legYaw * 0.35f, 0));
            if (chest != null) TurnBone(chest, torsoRot);
            if (neckB != null) TurnBone(neckB, Quaternion.Slerp(torsoRot, headRot, 0.5f));
            if (headB != null) TurnBone(headB, headRot);
        }
        else
        {
            torsoRot = Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.forward, up).normalized, up);
            head.localPosition = eye + headRot * new Vector3(0, 0.03f, -0.08f);
            head.localRotation = headRot;
            spine.localPosition = pelvis;
            spine.localRotation = torsoRot;
            spine.localScale = new Vector3(1, Mathf.Max(0.6f, (neck - pelvis).magnitude / torsoLen), 1);
        }

        // 다리
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            float ph = phase + (i == 0 ? 0 : Mathf.PI);
            Vector3 hip = skinned ? Local(upLeg[i].T) : pelvis + yaw * new Vector3(side * 0.1f, -0.04f, 0);
            // 서면 발은 목·엉덩이 바로 아래 (눈은 목보다 앞이다), 앉으면 조금 앞으로
            Vector3 footRest = new Vector3(side * 0.11f, ankleH, Mathf.Lerp(Mathf.Min(0.02f, neck.z + 0.03f), -0.05f, crouch));
            Vector3 swing = new Vector3(0, Mathf.Max(0, Mathf.Cos(ph)) * lift, dir * Mathf.Sin(ph) * stride);
            // 공중: 무릎을 굽혀 발을 끌어올린다 (한 발은 조금 앞)
            Vector3 tuck = new Vector3(side * 0.12f, 0.32f + hipY - standHip, i == 0 ? 0.12f : -0.1f);
            Vector3 ankle = Vector3.Lerp(yaw * (footRest + swing), tuck, air);
            Vector3 fwd = yaw * Vector3.forward;
            Vector3 knee = Knee(hip, ankle, thighLen, shinLen, fwd);
            // 발끝: 발을 들 때 살짝 들린다
            float toeUp = Mathf.Max(0, Mathf.Cos(ph)) * 18f * moveAmount + air * 30f;
            if (skinned)
            {
                // 비틀림 기준은 무릎 경첩 축(엉덩이 오른쪽)에서 얻는다: 허벅지가 수평을 넘어도 뼈가 반 바퀴 돌지 않는다
                Vector3 hinge = yaw * Vector3.right;
                PointBone(upLeg[i], knee - hip, Vector3.Cross(knee - hip, hinge));
                PointBone(loLeg[i], ankle - knee, Vector3.Cross(ankle - knee, hinge));
                TurnBone(footB[i], yaw * Quaternion.Euler(-toeUp, 0, 0));
                continue;
            }
            thigh[i].Set(hip, knee, fwd);
            shin[i].Set(knee, ankle, fwd);
            Vector3 toe = ankle + yaw * (Quaternion.Euler(-toeUp, 0, 0) * new Vector3(0, -0.035f, 0.2f));
            foot[i].Set(ankle - yaw * new Vector3(0, 0, 0.05f), toe, Vector3.up);
        }

        // 팔: 1인칭 팔이 보이면 그 자리를 그대로, 없으면 걸음에 맞춰 흔든다
        for (int i = 0; i < 2; i++)
        {
            bool rightArm = i == 1;
            float side = rightArm ? 1f : -1f;
            Vector3 shoulder = skinned ? Local(upArm[i].T) : pelvis + torsoRot * new Vector3(side * 0.19f, torsoLen * 0.86f * spine.localScale.y, 0);
            Vector3 elbow, wrist;
            Quaternion handRot;
            Vector3 e = Vector3.zero, w = Vector3.zero;
            Quaternion hr = Quaternion.identity;
            bool follow = hands != null && hands.ArmPose(rightArm, out e, out w, out hr);
            if (follow)
            {
                elbow = root.InverseTransformPoint(e);
                wrist = root.InverseTransformPoint(w);
                handRot = Quaternion.Inverse(root.rotation) * hr;
            }
            else
            {
                // 걸으면 같은 쪽 다리와 반대로 흔든다. 달리면 팔꿈치를 굽혀 크게 흔든다
                float ph = phase + (rightArm ? Mathf.PI : 0);
                float run = pc.IsRunning ? 1f : 0f;
                armSwing[i] = Mathf.Lerp(armSwing[i], -Mathf.Sin(ph) * dir * (22f + 18f * run) * moveAmount, dt * 12);
                var armRot = torsoRot * Quaternion.Euler(armSwing[i], 0, side * 8f);
                elbow = shoulder + armRot * new Vector3(0, -upperArmLen, 0);
                float bend = 18f + 62f * run + 30f * crouch;
                wrist = elbow + armRot * (Quaternion.Euler(-bend, 0, 0) * new Vector3(0, -lowerArmLen, 0));
                var lowerDir = (wrist - elbow).normalized;
                // 늘어뜨린 손: 손가락은 아래팔 방향, 엄지는 앞
                handRot = Quaternion.LookRotation(lowerDir, Vector3.ProjectOnPlane(torsoRot * Vector3.forward, lowerDir));
            }

            if (skinned)
            {
                PoseArm(i, shoulder, elbow, wrist, handRot, follow, dt);
                continue;
            }
            if (follow)
            {
                upper[i].Set(shoulder, elbow, Vector3.up);
                // 1인칭 소매 안쪽으로 조금 가늘게 (1인칭 팔에 그림자 자국이 지지 않게)
                lower[i].Set(elbow, wrist, Vector3.up);
                hand[i].localPosition = wrist + handRot * new Vector3(side * 0.012f, 0, 0.045f);
                hand[i].localRotation = handRot;
                hand[i].localScale = new Vector3(0.022f, 0.06f, 0.08f);
                continue;
            }
            upper[i].Set(shoulder, elbow, Vector3.forward);
            lower[i].Set(elbow, wrist, Vector3.forward);
            var ld = (wrist - elbow).normalized;
            hand[i].localPosition = wrist + ld * 0.05f;
            hand[i].localRotation = Quaternion.LookRotation(ld, torsoRot * Vector3.right * side);
            hand[i].localScale = new Vector3(0.03f, 0.08f, 0.1f);
        }
    }

    // 모델 팔: 어깨에서 손목까지 두 마디 IK (팔꿈치는 1인칭 팔꿈치 쪽으로), 모자라면 팔을 조금 늘인다. 손은 1인칭 손 방향, 손가락은 쥔다
    void PoseArm(int i, Vector3 shoulder, Vector3 elbowHint, Vector3 wrist, Quaternion handRot, bool follow, float dt)
    {
        float reach = (wrist - shoulder).magnitude;
        float stretch = Mathf.Clamp(reach / ((upperArmLen + lowerArmLen) * 0.98f), 1f, 1.35f);
        loArm[i].T.localPosition = loArm[i].BindLocalPos * stretch;
        handB[i].T.localPosition = handB[i].BindLocalPos * stretch;
        float a = upperArmLen * stretch, b = lowerArmLen * stretch;
        Vector3 bendHint = elbowHint - (shoulder + wrist) * 0.5f;
        Vector3 elbow = Knee(shoulder, wrist, a, b, bendHint);
        Vector3 fingersDir = handRot * Vector3.forward, thumbDir = handRot * Vector3.up;
        Vector3 upperDir = elbow - shoulder;
        Vector3 bendDir = Vector3.ProjectOnPlane(wrist - elbow, upperDir.normalized);
        if (bendDir.sqrMagnitude < 1e-5f) bendDir = thumbDir;
        PointBone(upArm[i], upperDir, bendDir);
        PointBone(loArm[i], wrist - elbow, thumbDir);
        PointBone(handB[i], fingersDir, thumbDir);
        // 손가락: 1인칭 손이 무엇을 쥐고 있으면 주먹에 가깝게, 늘어뜨리면 살짝 굽힌다
        grip[i] = Mathf.MoveTowards(grip[i], follow ? 1f : 0f, dt * 4f);
        var fl = fingers[i]; var fa = fingerAxis[i];
        for (int k = 0; k < fl.Length; k++)
            fl[k].T.localRotation = fl[k].BindLocal * Quaternion.AngleAxis(Mathf.Lerp(fl[k].CurlRest, fl[k].CurlGrip, grip[i]), fa[k]);
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
