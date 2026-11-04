using UnityEngine;

// 컷씬의 주인공 (평범한 시민). 게임의 절차적 캐릭터(CharacterModels.Civilian)로 몸을 짓고 관절을 코드로 움직인다.
// 자세는 손·발이 닿을 곳(두 마디 IK)과 몸통·머리 각도로 적는다: 앉기 → 일어서기 → 굳기 → 달리기.
// 두 자세 사이는 부드럽게 섞이고, 달리기는 주기 함수로 매 프레임 만든다. 좌표는 사람 기준 (발밑 원점, +z 앞, +x 오른쪽, m).
// 손은 바깥 세계의 한 점에 붙일 수도 있다(LockHand: 문을 미는 손). 굳은 자세의 왼손은 머리의 입 위치를 따라가 입을 가린다
public class CutsceneActor : MonoBehaviour
{
    public enum Mode { Sit, Stand, Freeze, Shield, Run }

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

    // 똑바로 선 자세, 팔은 힘을 빼고 내린다
    public static readonly Pose Stand = new Pose
    {
        Drop = 0f,
        Body = Vector3.zero, Head = Vector3.zero,
        HandL = new Vector3(-0.22f, 0.82f, 0.05f), ElbowL = new Vector3(-0.5f, 1.05f, -0.4f),
        HandR = new Vector3(0.22f, 0.82f, 0.05f), ElbowR = new Vector3(0.5f, 1.05f, -0.4f),
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

    // 몸을 짓는다. glow: 발광 재질 원본 (빌드에서 발광 셰이더가 빠지지 않게)
    public void Build(Material glow = null)
    {
        if (glow != null) ProcTex.GlowTemplate = glow;
        Rig = CharacterModels.Build(transform, FigureKind.Civilian);
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
            foreach (var s in new[] { -1f, 1f })
            {
                Part(backpack, ProcMesh.Box("cs_strap", 0.045f, 0.34f, 0.016f, 4), strap, new Vector3(s * 0.1f, 0.33f, 0.125f), new Vector3(-6, 0, s * -4));
                Part(backpack, ProcMesh.Box("cs_straptop", 0.045f, 0.016f, 0.2f, 4), strap, new Vector3(s * 0.1f, 0.49f, 0.0f), Vector3.zero);
            }
        }
        backpack.gameObject.SetActive(on);
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
            default: return Stand;
        }
    }

    Pose Snapshot() => blend >= 1 ? (Current == Mode.Run ? RunPose(phase) : to) : Pose.Mix(from, Current == Mode.Run ? RunPose(phase) : to, Ease(blend));

    static float Ease(float t) => t * t * (3 - 2 * t);

    // 매 프레임 (연출기가 부른다). moving: 이번 프레임 실제로 움직인 거리 (달리기 주기를 맞춘다)
    public void Tick(float dt, float moving = 0)
    {
        if (Rig == null) return;
        breath += dt;
        if (Current == Mode.Run) phase += dt * RunCadence * Mathf.PI * 2 * Mathf.Max(0.3f, RunAmount);
        if (blend < 1) blend = Mathf.Min(1, blend + dt / blendTime);

        Pose target = Current == Mode.Run ? RunPose(phase) : to;
        var p = blend >= 1 ? target.Clone() : Pose.Mix(from, target, Ease(blend));

        // 숨쉬기: 몸통이 아주 조금 오르내린다
        float br = Mathf.Sin(breath * (Current == Mode.Run ? 6f : Current == Mode.Freeze ? 3.2f : 1.5f));
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

        // 달리기: 발이 땅에 닿는 순간
        if (Current == Mode.Run)
        {
            bool leftDown = Mathf.Sin(phase) > 0;
            if (leftDown != lastLeftDown && OnFootstep != null && blend > 0.3f) OnFootstep(transform.position);
            lastLeftDown = leftDown;
        }
    }

    // 달리기 한 주기: 왼발이 뒤로 갈 때 땅을 딛고 앞으로 올 때 든다. 팔은 반대쪽 다리와 같이 앞으로
    Pose RunPose(float ph)
    {
        float s = Mathf.Sin(ph), c = Mathf.Cos(ph);
        float k = Mathf.Clamp01(RunAmount);
        float stride = 0.36f * k, lift = 0.3f * k, swing = 0.28f * k;
        var p = Stand.Clone();
        p.Drop = 0.1f * k + 0.025f * Mathf.Abs(s) * k;
        p.FootL = new Vector3(-0.11f, 0.08f + lift * Mathf.Max(0, -s), stride * c);
        p.FootR = new Vector3(0.11f, 0.08f + lift * Mathf.Max(0, s), -stride * c);
        p.KneeL = new Vector3(-0.12f, 0.55f, 1.2f);
        p.KneeR = new Vector3(0.12f, 0.55f, 1.2f);
        p.Body = new Vector3(RunLean * k, 4f * s * k, 2f * c * k);
        p.Head = new Vector3(-RunLean * 0.45f * k + 3f, 0, -2f * c * k);
        p.HandL = new Vector3(-0.21f, 1.0f + 0.08f * k - 0.05f * c * k, -swing * c + 0.06f * k);
        p.ElbowL = new Vector3(-0.5f, 0.85f, -0.6f);
        p.HandR = new Vector3(0.21f, 1.0f + 0.08f * k + 0.05f * c * k, swing * c + 0.06f * k);
        p.ElbowR = new Vector3(0.5f, 0.85f, -0.6f);
        p.HandRotL = new Vector3(-30 * k, 0, 0);
        p.HandRotR = new Vector3(-30 * k, 0, 0);
        return p;
    }

    void Apply(Pose p)
    {
        model.localPosition = new Vector3(0, -p.Drop, 0);
        Rig.Body.localRotation = Quaternion.Euler(p.Body);
        Rig.Head.localRotation = Quaternion.Euler(p.Head);
        var fwd = transform.forward;
        Vector3 P(Vector3 local) => transform.TransformPoint(local);
        Reach(Rig.LegL, Rig.ShinL, CharacterModels.Thigh, CharacterModels.Shin, P(p.FootL), P(p.KneeL), fwd);
        Reach(Rig.LegR, Rig.ShinR, CharacterModels.Thigh, CharacterModels.Shin, P(p.FootR), P(p.KneeR), fwd);
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
        Reach(upper, fore, CharacterModels.UpperArm, CharacterModels.Forearm, wrist, pole, fwd);
        var local = Quaternion.Euler(handRot);
        if (w > 0) local = Quaternion.Slerp(local, Quaternion.Inverse(fore.rotation) * worldRot, w);
        hand.localRotation = local;
    }

    // 두 마디 팔다리를 목표 지점에 닿게 굽힌다 (코사인 법칙). pole: 팔꿈치·무릎이 향할 쪽 (월드 좌표)
    static void Reach(Transform upper, Transform lower, float a, float b, Vector3 target, Vector3 pole, Vector3 facing)
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
        Aim(upper, joint - s, facing);
        Aim(lower, s + u * d - joint, facing);
    }

    // 마디의 아래쪽(-y)이 dir을 향하게 돌린다. 앞(+z)은 몸이 보는 쪽에 가깝게 둔다
    static void Aim(Transform joint, Vector3 dir, Vector3 facing)
    {
        Vector3 down = dir.normalized;
        Vector3 fwd = Vector3.ProjectOnPlane(facing, down);
        if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(Vector3.up, down);
        joint.rotation = Quaternion.LookRotation(fwd.normalized, -down);
    }
}
