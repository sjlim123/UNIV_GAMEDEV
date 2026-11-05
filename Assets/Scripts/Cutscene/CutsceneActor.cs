using UnityEngine;

// 컷씬의 주인공 (평범한 시민). 게임의 절차적 캐릭터(CharacterModels.Civilian)로 몸을 짓고 관절을 코드로 움직인다.
// 자세는 손·발이 닿을 곳(두 마디 IK)과 몸통·머리 각도로 적는다: 앉기 → 일어서기 → 굳기 → 달리기 → 빠른 걸음.
// 두 자세 사이는 부드럽게 섞이고, 달리기·걷기는 걸음 주기(디딤·흔들기 구간이 나뉜 발 궤적, 몸 출렁임, 팔꿈치를 굽혀 흔드는 팔)로 매 프레임 만든다. 좌표는 사람 기준 (발밑 원점, +z 앞, +x 오른쪽, m).
// 손은 바깥 세계의 한 점에 붙일 수도 있다(LockHand: 문을 미는 손). 굳은 자세의 왼손은 머리의 입 위치를 따라가 입을 가린다
public class CutsceneActor : MonoBehaviour
{
    public enum Mode { Sit, Stand, Freeze, Shield, Run, Walk }

    public class Pose
    {
        public float Drop;                      // 엉덩이를 내린 높이 (앉을 때)
        public Vector3 Body, Head;              // 몸통·머리 회전 (도)
        public Vector3 HandL, ElbowL, HandR, ElbowR; // 손목이 닿을 곳, 팔꿈치가 향할 곳
        public Vector3 FootL, KneeL, FootR, KneeR;   // 발목이 닿을 곳, 무릎이 향할 곳
        public Vector3 HandRotL, HandRotR;      // 손목 회전 (도)
        public float MouthL;                    // 왼손으로 입을 가리는 정도 (0~1): 손바닥이 머리의 입 위치를 따라간다
        public Pose Clone() => (Pose)MemberwiseClone();

        public static Pose Mix(Pose a, Pose b, float t)
        {
            return new Pose
            {
                Drop = Mathf.Lerp(a.Drop, b.Drop, t),
                Body = Vector3.Lerp(a.Body, b.Body, t), Head = Vector3.Lerp(a.Head, b.Head, t),
                HandL = Vector3.Lerp(a.HandL, b.HandL, t), ElbowL = Vector3.Lerp(a.ElbowL, b.ElbowL, t),
                HandR = Vector3.Lerp(a.HandR, b.HandR, t), ElbowR = Vector3.Lerp(a.ElbowR, b.ElbowR, t),
                FootL = Vector3.Lerp(a.FootL, b.FootL, t), KneeL = Vector3.Lerp(a.KneeL, b.KneeL, t),
                FootR = Vector3.Lerp(a.FootR, b.FootR, t), KneeR = Vector3.Lerp(a.KneeR, b.KneeR, t),
                HandRotL = Vector3.Lerp(a.HandRotL, b.HandRotL, t), HandRotR = Vector3.Lerp(a.HandRotR, b.HandRotR, t),
                MouthL = Mathf.Lerp(a.MouthL, b.MouthL, t),
            };
        }
    }

    // 소파에 기대 앉아 왼팔은 등받이에 걸치고 오른손은 무릎 위 (리모컨)
    public static readonly Pose Sit = new Pose
    {
        Drop = 0.43f,
        Body = new Vector3(-10, -6, 0), Head = new Vector3(5, -10, 0),
        HandL = new Vector3(-0.5f, 1.0f, -0.22f), ElbowL = new Vector3(-0.75f, 0.7f, -0.55f),
        HandR = new Vector3(0.16f, 0.6f, 0.3f), ElbowR = new Vector3(0.5f, 0.55f, -0.05f),
        FootL = new Vector3(-0.15f, 0.07f, 0.42f), KneeL = new Vector3(-0.15f, 0.95f, 0.7f),
        FootR = new Vector3(0.17f, 0.07f, 0.37f), KneeR = new Vector3(0.19f, 0.95f, 0.7f),
        HandRotL = new Vector3(0, 0, 0), HandRotR = new Vector3(-20, 0, 0),
    };

    // 똑바로 선 자세, 팔은 힘을 빼고 내린다 (팔꿈치가 살짝 굽고 손은 허벅지 조금 앞에 느슨하게)
    public static readonly Pose Stand = new Pose
    {
        Drop = 0f,
        Body = Vector3.zero, Head = Vector3.zero,
        HandL = new Vector3(-0.245f, 0.86f, 0.1f), ElbowL = new Vector3(-0.55f, 1.05f, -0.38f),
        HandR = new Vector3(0.245f, 0.86f, 0.1f), ElbowR = new Vector3(0.55f, 1.05f, -0.38f),
        HandRotL = new Vector3(-8, 0, -6), HandRotR = new Vector3(-8, 0, 6),
        FootL = new Vector3(-0.1f, 0.08f, 0f), KneeL = new Vector3(-0.1f, 0.5f, 1f),
        FootR = new Vector3(0.1f, 0.08f, 0f), KneeR = new Vector3(0.1f, 0.5f, 1f),
    };

    // 속보를 보고 굳어 선 자세: 왼손으로 입을 막고(MouthL: 손바닥이 입 위로 간다) 오른팔은 뻣뻣하게 내린 채, 고개를 살짝 든다
    public static readonly Pose Freeze = new Pose
    {
        Drop = 0.01f,
        Body = new Vector3(3, 0, 0), Head = new Vector3(-3, 0, 2),
        HandL = new Vector3(-0.06f, 1.5f, 0.14f), ElbowL = new Vector3(-0.42f, 1.0f, 0.22f),
        HandR = new Vector3(0.25f, 0.84f, 0.1f), ElbowR = new Vector3(0.5f, 1.1f, -0.2f),
        FootL = new Vector3(-0.15f, 0.08f, -0.03f), KneeL = new Vector3(-0.15f, 0.5f, 1f),
        FootR = new Vector3(0.14f, 0.08f, 0.07f), KneeR = new Vector3(0.14f, 0.5f, 1f),
        HandRotL = new Vector3(20, 0, -40),
        MouthL = 1f,
    };

    // 섬광에 오른팔로 눈을 가리고 몸을 뒤로 젖힌다
    public static readonly Pose Shield = new Pose
    {
        Drop = 0.03f,
        Body = new Vector3(-8, 0, 0), Head = new Vector3(-14, 0, 0),
        HandL = new Vector3(-0.3f, 0.95f, -0.1f), ElbowL = new Vector3(-0.6f, 1.0f, -0.4f),
        HandR = new Vector3(0.1f, 1.52f, 0.24f), ElbowR = new Vector3(0.55f, 1.25f, 0.15f),
        FootL = new Vector3(-0.17f, 0.08f, -0.12f), KneeL = new Vector3(-0.17f, 0.5f, 1f),
        FootR = new Vector3(0.15f, 0.08f, 0.1f), KneeR = new Vector3(0.15f, 0.5f, 1f),
        HandRotR = new Vector3(0, 0, 60),
    };

    // 일어서는 중간: 몸을 앞으로 숙이고 손으로 소파를 짚는다
    public static readonly Pose Rising = new Pose
    {
        Drop = 0.22f,
        Body = new Vector3(32, -4, 0), Head = new Vector3(-10, -8, 0),
        HandL = new Vector3(-0.36f, 0.6f, 0.0f), ElbowL = new Vector3(-0.6f, 0.9f, -0.3f),
        HandR = new Vector3(0.3f, 0.62f, 0.12f), ElbowR = new Vector3(0.6f, 0.9f, -0.2f),
        FootL = new Vector3(-0.14f, 0.08f, 0.25f), KneeL = new Vector3(-0.14f, 0.8f, 0.8f),
        FootR = new Vector3(0.16f, 0.08f, 0.2f), KneeR = new Vector3(0.18f, 0.8f, 0.8f),
    };

    public CharacterRig Rig { get; private set; }
    public Mode Current { get; private set; } = Mode.Sit;
    public float RunCadence = 1.5f;   // 달리기 한 주기(두 걸음)의 초당 횟수
    public float WalkCadence = 1.0f;  // 빠른 걸음 한 주기의 초당 횟수
    public float RunLean = 14f;       // 달릴 때 몸을 숙이는 각도
    public float Tremble;             // 굳어 떨리는 정도 (0~1)
    public float RunAmount;           // 0: 서 있음 ~ 1: 전력 질주 (달리기 자세 섞기)
    // 발이 땅을 디딜 때 (달리기 발소리용)
    public System.Action<Vector3> OnFootstep;

    const float PalmDepth = 0.065f;   // 손목에서 손바닥 가운데까지
    // 입을 가릴 때 손바닥이 입 앞에 떠 있는 거리, 입을 찾지 못할 때의 머리 기준 입 위치
    const float MouthGap = 0.012f;
    static readonly Vector3 MouthFallback = new Vector3(0, 0.061f, 0.096f);

    Transform model, backpack;
    Pose from, to;
    float blend = 1, blendTime = 1, phase, breath;
    bool lastLeftDown;
    // 바깥 좌표에 붙인 손 (문 밀기): 손바닥 가운데 자리, 손 회전, 팔꿈치가 향할 곳, 섞는 정도
    Vector3 lockPalmL, lockPalmR, lockPoleL, lockPoleR;
    Quaternion lockRotL = Quaternion.identity, lockRotR = Quaternion.identity;
    float lockWL, lockWR;
    bool lockPoleOnL, lockPoleOnR;

    // 몸을 짓는다. glow: 발광 재질 원본 (빌드에서 발광 셰이더가 빠지지 않게). variant: 0 집에서의 차림(인트로), 1 탐험 차림(1인칭과 같은 야상·반장갑·군화: 방공호로 돌아올 때)
    public void Build(Material glow = null, int variant = 0)
    {
        if (glow != null) ProcTex.GlowTemplate = glow;
        Rig = CharacterModels.Build(transform, FigureKind.Civilian, variant);
        model = Rig.Model;
        from = to = Sit;
        blend = 1;
        Apply(Sit);
    }

    // 등에 멘 가방 (방공호로 달릴 때)
    public void SetBackpack(bool on)
    {
        if (backpack == null)
        {
            var mat = ProcTex.Get(ProcTex.Kind.Cloth, new Color(0.2f, 0.25f, 0.18f), 95, 0, 0.5f, new Color(0.12f, 0.15f, 0.1f));
            var strap = ProcTex.Get(ProcTex.Kind.Cloth, new Color(0.13f, 0.15f, 0.11f), 96, 0, 0.5f);
            backpack = new GameObject("Backpack").transform;
            backpack.SetParent(Rig.Body, false);
            Part(backpack, ProcMesh.Box("cs_pack", 0.34f, 0.44f, 0.17f, 5), mat, new Vector3(0, 0.27f, -0.2f), Vector3.zero);
            Part(backpack, ProcMesh.Box("cs_packflap", 0.3f, 0.14f, 0.05f, 5), strap, new Vector3(0, 0.44f, -0.2f), new Vector3(8, 0, 0));
            Part(backpack, ProcMesh.Box("cs_packpocket", 0.22f, 0.16f, 0.05f, 5), strap, new Vector3(0, 0.14f, -0.3f), Vector3.zero);
            // 어깨끈: 가슴 앞에서 몸통 겉면을 타고 올라가 어깨를 넘어 등의 가방 윗모서리에 이어진다 (짧은 조각을 이어 붙인 띠)
            foreach (var s in new[] { -1f, 1f }) BuildStrap(strap, s * 0.1f);
            // 가슴 앞 가로 끈과 버클 (두 어깨끈을 잇는다)
            float chestZ = CharacterModels.TorsoSurfaceZ(0.3f, 0f, true) + 0.012f;
            Part(backpack, ProcMesh.Box("cs_chest", 0.19f, 0.022f, 0.012f, 3), strap, new Vector3(0, 0.3f, chestZ), Vector3.zero);
            Part(backpack, ProcMesh.Box("cs_buckle", 0.032f, 0.03f, 0.016f, 3), ProcTex.Plain(new Color(0.1f, 0.1f, 0.1f), 0.4f), new Vector3(0, 0.3f, chestZ + 0.003f), Vector3.zero);
        }
        backpack.gameObject.SetActive(on);
    }

    // 어깨끈 하나 (x: 가로 자리): 가슴(y 0.17)에서 어깨 꼭대기(0.535)를 넘어 등의 가방 윗모서리(0.47, -0.118)까지, 몸통 겉면에서 1.1cm 떠서 따라간다
    void BuildStrap(Material mat, float x)
    {
        var pts = new System.Collections.Generic.List<Vector3>();
        foreach (var y in new[] { 0.17f, 0.25f, 0.33f, 0.4f, 0.45f, 0.49f, 0.515f })
            pts.Add(new Vector3(x, y, CharacterModels.TorsoSurfaceZ(y, x, true) + 0.011f));
        pts.Add(new Vector3(x, 0.535f, -0.006f));
        foreach (var y in new[] { 0.515f, 0.49f })
            pts.Add(new Vector3(x, y, CharacterModels.TorsoSurfaceZ(y, x, false) - 0.011f));
        pts.Add(new Vector3(x, 0.47f, -0.118f));
        for (int i = 0; i < pts.Count - 1; i++)
        {
            var a = pts[i];
            var b = pts[i + 1];
            var d = b - a;
            float len = d.magnitude;
            var go = new GameObject("cs_strapseg");
            go.transform.SetParent(backpack, false);
            go.transform.localPosition = (a + b) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d / len);
            go.AddComponent<MeshFilter>().sharedMesh = ProcMesh.Box($"cs_strapseg_{len:0.0000}", 0.046f, len + 0.014f, 0.016f, 3);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }

    static void Part(Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 euler)
    {
        var go = new GameObject(mesh.name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // 자세를 바꾼다 (transition초에 걸쳐 섞인다)
    public void Set(Mode mode, float transition = 0.8f)
    {
        from = Snapshot();
        Current = mode;
        to = PoseOf(mode);
        blendTime = Mathf.Max(0.01f, transition);
        blend = 0;
    }

    // 손을 바깥 세계의 한 점에 붙인다: 손바닥 가운데가 palm에 놓이고 손이 rot을 향한다(HandRotation으로 만든다).
    // pole: 팔꿈치가 향할 곳 (null이면 자세의 것), weight: 자세의 손 위치와 섞는 정도 (0이면 풀린다)
    public void LockHand(bool left, Vector3 palm, Quaternion rot, Vector3? pole, float weight)
    {
        if (left) { lockPalmL = palm; lockRotL = rot; lockPoleOnL = pole.HasValue; if (pole.HasValue) lockPoleL = pole.Value; lockWL = Mathf.Clamp01(weight); }
        else { lockPalmR = palm; lockRotR = rot; lockPoleOnR = pole.HasValue; if (pole.HasValue) lockPoleR = pole.Value; lockWR = Mathf.Clamp01(weight); }
    }

    public void ReleaseHands()
    {
        lockWL = lockWR = 0;
    }

    // 손바닥이 palmNormal 쪽을 보고 손가락이 fingers 쪽을 가리키는 손의 회전.
    // 손 모델은 손가락이 -y로 뻗고, 손바닥은 왼손이 +x, 오른손이 -x를 본다 (팔을 내렸을 때 허벅지 쪽)
    public static Quaternion HandRotation(bool left, Vector3 palmNormal, Vector3 fingers)
    {
        Vector3 x = (left ? palmNormal : -palmNormal).normalized;
        Vector3 y = -Vector3.ProjectOnPlane(fingers, x).normalized;
        if (y.sqrMagnitude < 1e-6f) y = Vector3.ProjectOnPlane(Vector3.down, x).normalized;
        Vector3 z = Vector3.Cross(x, y);
        return Quaternion.LookRotation(z, y);
    }

    Pose PoseOf(Mode m)
    {
        switch (m)
        {
            case Mode.Sit: return Sit;
            case Mode.Freeze: return Freeze;
            case Mode.Shield: return Shield;
            case Mode.Run: return RunPose(phase);
            case Mode.Walk: return WalkPose(phase);
            default: return Stand;
        }
    }

    Pose Snapshot() => blend >= 1 ? Moving(to) : Pose.Mix(from, Moving(to), Ease(blend));

    Pose Moving(Pose rest) => Current == Mode.Run ? RunPose(phase) : Current == Mode.Walk ? WalkPose(phase) : rest;

    static float Ease(float t) => t * t * (3 - 2 * t);

    // 매 프레임 (연출기가 부른다). moving: 이번 프레임 실제로 움직인 거리 (달리기 주기를 맞춘다)
    public void Tick(float dt, float moving = 0)
    {
        if (Rig == null) return;
        breath += dt;
        if (Current == Mode.Run) phase += dt * RunCadence * Mathf.PI * 2 * Mathf.Max(0.3f, RunAmount);
        else if (Current == Mode.Walk) phase += dt * WalkCadence * Mathf.PI * 2 * Mathf.Max(0.3f, RunAmount);
        if (blend < 1) blend = Mathf.Min(1, blend + dt / blendTime);

        Pose target = Moving(to);
        var p = blend >= 1 ? target.Clone() : Pose.Mix(from, target, Ease(blend));

        // 숨쉬기: 몸통이 아주 조금 오르내린다
        float br = Mathf.Sin(breath * (Current == Mode.Run ? 6f : Current == Mode.Walk ? 3.5f : Current == Mode.Freeze ? 3.2f : 1.5f));
        p.Body += new Vector3(br * (Current == Mode.Freeze ? 0.9f : 0.6f), 0, 0);
        p.Head += new Vector3(-br * 0.4f, 0, 0);
        // 굳어서 떨린다 (입을 가린 손은 머리와 함께 떨린다)
        if (Tremble > 0)
        {
            float t = Time.time * 23f;
            var j = new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, Mathf.PerlinNoise(t * 0.9f, t * 1.1f) - 0.5f) * (0.012f * Tremble);
            p.HandL += j;
            p.HandR += j * 0.5f;
            p.Head += new Vector3(j.x, j.y, j.z) * 120f * Tremble;
        }
        Apply(p);

        // 달리기·걷기: 발이 땅에 닿는 순간
        if (Current == Mode.Run || Current == Mode.Walk)
        {
            bool leftDown = Mathf.Sin(phase) > 0;
            if (leftDown != lastLeftDown && OnFootstep != null && blend > 0.3f) OnFootstep(transform.position);
            lastLeftDown = leftDown;
        }
    }

    // ---------- 걸음 (달리기·빠른 걸음) ----------
    // 한 주기 = 왼발 디딤 → 오른발 디딤 → 왼발 디딤. u = 그 발의 주기 위치(0~1): 0에 발이 땅에 닿고,
    // Stance까지는 땅을 딛고 뒤로 쓸려 가며(몸이 앞으로 나아가는 만큼), 나머지는 발을 들어 앞으로 옮긴다.
    // 달리기는 디딤이 짧아(34%) 두 발이 다 뜨는 순간이 있고, 걷기는 길어(62%) 늘 한 발은 땅에 있다
    struct Gait
    {
        public float Stance, HalfStride, Lift, Lean, BobDepth, BobAmp;
        public float ArmFwd, ArmBack, ElbowBase, ElbowExtra, ArmOut, HandIn;
    }

    static readonly Gait RunGait = new Gait
    {
        Stance = 0.34f, HalfStride = 0.42f, Lift = 0.30f, Lean = 1f, BobDepth = 0.09f, BobAmp = 0.03f,
        ArmFwd = 40f, ArmBack = 34f, ElbowBase = 90f, ElbowExtra = 14f, ArmOut = 0.14f, HandIn = 0.22f,
    };

    static readonly Gait WalkGait = new Gait
    {
        Stance = 0.62f, HalfStride = 0.31f, Lift = 0.085f, Lean = 0.3f, BobDepth = 0.035f, BobAmp = 0.016f,
        ArmFwd = 32f, ArmBack = 26f, ElbowBase = 34f, ElbowExtra = 22f, ArmOut = 0.11f, HandIn = 0.15f,
    };

    Pose RunPose(float ph) => GaitPose(ph, RunGait, RunLean);
    Pose WalkPose(float ph) => GaitPose(ph, WalkGait, 5f);

    // 발 하나의 자리: z(앞뒤), y(높이), lift(들린 정도 0~1)
    static void Foot(float u, Gait g, float k, out float z, out float y, out float lift)
    {
        float L = g.HalfStride * k;
        if (u < g.Stance)
        {
            // 디딤: 발이 땅에 붙은 채 몸 아래를 지나 뒤로 간다 (끝에 뒤꿈치가 살짝 들린다)
            float a = u / g.Stance;
            z = L * (1 - 2 * a);
            y = 0.08f + 0.014f * a * a * a * k;
            lift = 0;
            return;
        }
        // 흔들기: 뒤꿈치가 먼저 높이 올라오고(무릎이 접힌다) 발이 앞으로 나가며 내려와 닿는다
        float w = (u - g.Stance) / (1 - g.Stance);
        float e = w * w * (3 - 2 * w);
        z = -L + 2 * L * e;
        lift = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(w, 0.72f)), 1.3f);
        y = 0.08f + g.Lift * k * lift;
    }

    Pose GaitPose(float ph, Gait g, float leanDeg)
    {
        float k = Mathf.Clamp01(RunAmount);
        float uL = Mathf.Repeat(ph / (Mathf.PI * 2), 1f), uR = Mathf.Repeat(uL + 0.5f, 1f);
        var p = Stand.Clone();

        // 다리
        Foot(uL, g, k, out float zL, out float yL, out float liftL);
        Foot(uR, g, k, out float zR, out float yR, out float liftR);
        p.FootL = new Vector3(-0.11f, yL, zL);
        p.FootR = new Vector3(0.11f, yR, zR);
        p.KneeL = new Vector3(-0.14f, 0.5f + 0.2f * liftL, 1.1f);
        p.KneeR = new Vector3(0.14f, 0.5f + 0.2f * liftR, 1.1f);

        // 몸: 디딤 가운데서 가장 낮고 두 발이 뜰 때 가장 높다. 골반·어깨가 걸음에 맞춰 조금 돌고 기운다
        float cyc = Mathf.Sin(uL * Mathf.PI * 2), cyc2 = Mathf.Cos(uL * Mathf.PI * 2);
        float bob = -Mathf.Cos((uL - g.Stance * 0.5f) * Mathf.PI * 4); // +1 = 디딤 가운데
        float lean = leanDeg * g.Lean * k;
        p.Drop = g.BobDepth * k + g.BobAmp * k * (0.5f + 0.5f * bob);
        p.Body = new Vector3(lean + 1.5f * bob * k, 6f * cyc * k, 2.5f * cyc2 * k);
        p.Head = new Vector3(-lean * 0.45f + 3f - 1.2f * bob * k, -4f * cyc * k, -1.5f * cyc2 * k);

        // 팔: 같은 쪽 다리와 반대로. 어깨에서 흔들고 팔꿈치는 굽힌 채(달리기) 손은 안쪽으로 모인다. 아래팔·손목은 위팔보다 조금 늦게 따라온다 (채찍처럼)
        Arm(p, true, -Mathf.Cos(uL * Mathf.PI * 2), -Mathf.Cos((uL - 0.07f) * Mathf.PI * 2), g, k, lean);
        Arm(p, false, -Mathf.Cos(uR * Mathf.PI * 2), -Mathf.Cos((uR - 0.07f) * Mathf.PI * 2), g, k, lean);
        return p;
    }

    // 팔 하나. sw: -1(뒤) ~ 1(앞), swLag: 조금 늦은 흔들림 (팔꿈치 굽힘·손목에 쓴다). 어깨 자리는 몸통 기울기를 따라 돈다 (사람 기준 좌표)
    static void Arm(Pose p, bool left, float sw, float swLag, Gait g, float k, float leanDeg)
    {
        float side = left ? -1f : 1f;
        float lean = leanDeg * Mathf.Deg2Rad;
        var shoulder = new Vector3(side * 0.176f, CharacterModels.HipY - p.Drop + 0.455f * Mathf.Cos(lean), 0.455f * Mathf.Sin(lean) - 0.005f);
        float theta = (sw > 0 ? g.ArmFwd * sw : g.ArmBack * sw) * k * Mathf.Deg2Rad;
        float bend = (g.ElbowBase + g.ElbowExtra * (0.5f + 0.5f * swLag)) * Mathf.Deg2Rad * Mathf.Lerp(0.3f, 1f, k);
        float a = theta + lean;
        var upper = new Vector3(side * g.ArmOut, -Mathf.Cos(a), Mathf.Sin(a)).normalized;
        float f = a + bend;
        var fore = new Vector3(-side * g.HandIn * (0.5f + 0.5f * sw), -Mathf.Cos(f), Mathf.Sin(f)).normalized;
        var elbow = shoulder + upper * CharacterModels.UpperArm;
        var wrist = elbow + fore * CharacterModels.Forearm;
        // 서 있는 자세(k=0)의 손 자리와 섞어 멈춰 설 때 팔이 자연스럽게 내려온다
        var restHand = left ? Stand.HandL : Stand.HandR;
        var restElbow = left ? Stand.ElbowL : Stand.ElbowR;
        var pole = elbow + new Vector3(side * 0.28f, 0f, -0.18f);
        var hand = Vector3.Lerp(restHand, wrist, Mathf.Clamp01(k * 1.5f));
        var elbowPole = Vector3.Lerp(restElbow, pole, Mathf.Clamp01(k * 1.5f));
        // 손목: 앞으로 올 때 손등이 젖혀지고 뒤로 갈 때 손바닥이 뒤를 본다. 멈춰 서면 힘을 뺀 손으로 돌아간다
        var restRot = left ? Stand.HandRotL : Stand.HandRotR;
        var rot = Vector3.Lerp(restRot, new Vector3(-16f - 16f * swLag, side * 5f, side * (7f + 3f * sw)), Mathf.Clamp01(k * 1.5f));
        if (left) { p.HandL = hand; p.ElbowL = elbowPole; p.HandRotL = rot; }
        else { p.HandR = hand; p.ElbowR = elbowPole; p.HandRotR = rot; }
    }

    void Apply(Pose p)
    {
        model.localPosition = new Vector3(0, -p.Drop, 0);
        Rig.Body.localRotation = Quaternion.Euler(p.Body);
        Rig.Head.localRotation = Quaternion.Euler(p.Head);
        var fwd = transform.forward;
        var right = transform.right;
        Vector3 P(Vector3 local) => transform.TransformPoint(local);
        Reach(Rig.LegL, Rig.ShinL, CharacterModels.Thigh, CharacterModels.Shin, P(p.FootL), P(p.KneeL), fwd, right);
        Reach(Rig.LegR, Rig.ShinR, CharacterModels.Thigh, CharacterModels.Shin, P(p.FootR), P(p.KneeR), fwd, right);
        ArmIK(true, P(p.HandL), P(p.ElbowL), p.HandRotL, p.MouthL);
        ArmIK(false, P(p.HandR), P(p.ElbowR), p.HandRotR, 0f);
    }

    // 팔 하나: 자세의 손목 자리에 '입 가리기'와 '바깥 점에 붙이기'를 섞어 굽히고, 손목을 돌린다
    void ArmIK(bool left, Vector3 wrist, Vector3 pole, Vector3 handRot, float mouth)
    {
        var upper = left ? Rig.ArmL : Rig.ArmR;
        var fore = left ? Rig.ForeL : Rig.ForeR;
        var hand = left ? Rig.HandL : Rig.HandR;
        var fwd = transform.forward;
        var worldRot = Quaternion.identity;
        float w = 0;
        if (mouth > 0)
        {
            // 머리의 입 위치: 손바닥이 입을 보고, 손가락은 (사람의) 오른쪽 위로 입을 가로지른다
            var mouthPos = Rig.Mouth != null ? Rig.Mouth.position : Rig.Head.TransformPoint(MouthFallback);
            var hf = Rig.Head.forward;
            var fingers = (Rig.Head.right * 0.62f + Rig.Head.up * 0.78f).normalized;
            worldRot = HandRotation(left, -hf, fingers);
            var palm = mouthPos + hf * MouthGap;
            wrist = Vector3.Lerp(wrist, palm - worldRot * new Vector3(0, -PalmDepth, 0), mouth);
            w = mouth;
        }
        float lw = left ? lockWL : lockWR;
        if (lw > 0)
        {
            var lr = left ? lockRotL : lockRotR;
            var lp = left ? lockPalmL : lockPalmR;
            wrist = Vector3.Lerp(wrist, lp - lr * new Vector3(0, -PalmDepth, 0), lw);
            if (left ? lockPoleOnL : lockPoleOnR) pole = Vector3.Lerp(pole, left ? lockPoleL : lockPoleR, lw);
            worldRot = w > 0 ? Quaternion.Slerp(worldRot, lr, lw) : lr;
            w = Mathf.Max(w, lw);
        }
        Reach(upper, fore, CharacterModels.UpperArm, CharacterModels.Forearm, wrist, pole, fwd, transform.right);
        var local = Quaternion.Euler(handRot);
        if (w > 0) local = Quaternion.Slerp(local, Quaternion.Inverse(fore.rotation) * worldRot, w);
        hand.localRotation = local;
    }

    // 두 마디 팔다리를 목표 지점에 닿게 굽힌다 (코사인 법칙). pole: 팔꿈치·무릎이 향할 쪽 (월드 좌표). right: 사람의 오른쪽 (마디의 비틀림 기준)
    static void Reach(Transform upper, Transform lower, float a, float b, Vector3 target, Vector3 pole, Vector3 facing, Vector3 right)
    {
        Vector3 s = upper.position;
        Vector3 to = target - s;
        float d = Mathf.Clamp(to.magnitude, 0.05f, a + b - 0.002f);
        Vector3 u = to.normalized;
        Vector3 v = Vector3.ProjectOnPlane(pole - s, u);
        if (v.sqrMagnitude < 1e-6f) v = Vector3.ProjectOnPlane(facing, u);
        v.Normalize();
        float cos = Mathf.Clamp((a * a + d * d - b * b) / (2 * a * d), -1f, 1f);
        Vector3 joint = s + a * (cos * u + Mathf.Sqrt(1 - cos * cos) * v);
        Aim(upper, joint - s, facing, right);
        Aim(lower, s + u * d - joint, facing, right);
    }

    // 마디의 아래쪽(-y)이 dir을 향하게 돌린다. 앞(+z)은 마디가 앞뒤로 흔들리는 면 안에서 '사람의 오른쪽'과 직각인 쪽
    // (팔이 수평으로 앞을 가리킬 때도 손바닥이 안쪽을 보고, 발끝은 정강이를 따라간다). 마디가 옆을 가리키면 몸이 보는 쪽으로 돌린다
    static void Aim(Transform joint, Vector3 dir, Vector3 facing, Vector3 right)
    {
        Vector3 down = dir.normalized;
        Vector3 c = Vector3.Cross(down, right);
        float s = c.magnitude;
        Vector3 f = Vector3.ProjectOnPlane(facing, down);
        Vector3 fwd = c + (f.sqrMagnitude > 1e-6f ? f.normalized * (1 - s) : Vector3.zero);
        if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(Vector3.up, down);
        if (fwd.sqrMagnitude < 1e-6f) fwd = facing;
        joint.rotation = Quaternion.LookRotation(fwd.normalized, -down);
    }
}
