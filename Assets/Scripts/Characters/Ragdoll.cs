using System.Collections.Generic;
using UnityEngine;

// 죽은 적의 몸을 물리로 넘긴다: 관절마다 강체와 캡슐 콜라이더를 달고 CharacterJoint로 이어, 중력에 힘없이 무너진다.
// 사람(좀비·약탈자)은 몸통·머리·위팔·아래팔·허벅지·정강이, 들개는 몸통·목(머리)·다리 8마디.
// 관절 한계는 실제 관절처럼(팔꿈치·무릎은 한쪽으로만 굽는다). 부품은 Ignore Raycast 층에 둔다 (공격·상호작용 광선이 시체를 집지 않게)
public static class Ragdoll
{
    public const int Layer = 2; // Ignore Raycast

    // 사람 몸. impulse: 맞은 방향으로 미는 힘 (몸통에 준다). 돌려주는 목록의 첫 강체가 몸통
    public static List<Rigidbody> Human(CharacterRig rig, Vector3 impulse)
    {
        var list = new List<Rigidbody>();
        if (rig == null || rig.Body == null) return list;
        // 골반은 몸통 관절의 형제(Model 아래)라 몸통에 붙여 함께 움직이게 한다
        var hips = rig.Model != null ? rig.Model.Find("Hips") : null;
        if (hips != null) hips.SetParent(rig.Body, true);

        var body = Add(rig.Body, 32f);
        Capsule(rig.Body, new Vector3(0, 0.2f, 0), 0.16f, 0.76f);
        list.Add(body);
        if (rig.Head != null)
        {
            var head = Add(rig.Head, 5f);
            var sphere = rig.Head.gameObject.AddComponent<SphereCollider>();
            sphere.center = new Vector3(0, 0.12f, 0.01f);
            sphere.radius = 0.105f;
            Joint(head, body, Vector3.right, Vector3.forward, -30f, 40f, 35f, 40f);
            list.Add(head);
        }
        foreach (var side in new[] { -1, 1 })
        {
            var arm = side < 0 ? rig.ArmL : rig.ArmR;
            var fore = side < 0 ? rig.ForeL : rig.ForeR;
            var leg = side < 0 ? rig.LegL : rig.LegR;
            var shin = side < 0 ? rig.ShinL : rig.ShinR;
            if (arm != null)
            {
                var ua = Add(arm, 2.4f);
                Capsule(arm, new Vector3(0, -0.15f, 0), 0.046f, 0.34f);
                // 어깨: 팔 축으로 비틀림 ±40, 앞뒤 80, 옆 70
                Joint(ua, body, Vector3.up, Vector3.right, -40f, 40f, 80f, 70f);
                list.Add(ua);
                if (fore != null)
                {
                    var fa = Add(fore, 1.6f);
                    Capsule(fore, new Vector3(0, -0.18f, 0), 0.038f, 0.42f);
                    // 팔꿈치: x축으로 앞으로만 굽는다 (-120~3)
                    Joint(fa, ua, Vector3.right, Vector3.up, -120f, 3f, 8f, 8f);
                    list.Add(fa);
                }
            }
            if (leg != null)
            {
                var th = Add(leg, 7.5f);
                Capsule(leg, new Vector3(0, -0.22f, 0), 0.076f, 0.48f);
                Joint(th, body, Vector3.up, Vector3.right, -30f, 30f, 60f, 35f);
                list.Add(th);
                if (shin != null)
                {
                    var sh = Add(shin, 4.2f);
                    Capsule(shin, new Vector3(0, -0.22f, 0), 0.056f, 0.52f);
                    // 무릎: x축 +쪽(뒤)으로만 굽는다
                    Joint(sh, th, Vector3.right, Vector3.up, -3f, 125f, 6f, 6f);
                    list.Add(sh);
                }
            }
        }
        Release(list, rig.Body, impulse, rig.Head);
        return list;
    }

    // 들개 몸. impulse: 옆으로 쓰러뜨리는 힘
    public static List<Rigidbody> Dog(CharacterRig rig, Vector3 impulse)
    {
        var list = new List<Rigidbody>();
        if (rig == null || rig.Body == null) return list;
        var body = Add(rig.Body, 18f);
        var cap = rig.Body.gameObject.AddComponent<CapsuleCollider>();
        cap.direction = 2;
        cap.center = new Vector3(0, -0.03f, -0.04f);
        cap.radius = 0.14f;
        cap.height = 0.98f;
        list.Add(body);
        if (rig.Neck != null)
        {
            var neck = Add(rig.Neck, 3f);
            var sphere = rig.Neck.gameObject.AddComponent<SphereCollider>();
            sphere.center = new Vector3(0, 0.12f, 0.12f);
            sphere.radius = 0.1f;
            Joint(neck, body, Vector3.right, Vector3.up, -40f, 40f, 30f, 30f);
            list.Add(neck);
        }
        if (rig.Legs != null)
            for (int i = 0; i < rig.Legs.Length; i++)
            {
                var leg = rig.Legs[i];
                if (leg == null) continue;
                bool front = i < 2;
                var up = Add(leg, 1.6f);
                Capsule(leg, new Vector3(0, -0.11f, front ? 0.01f : 0.03f), 0.045f, 0.3f);
                Joint(up, body, Vector3.up, Vector3.right, -20f, 20f, 55f, 25f);
                list.Add(up);
                var knee = rig.Knees != null && i < rig.Knees.Length ? rig.Knees[i] : null;
                if (knee == null) continue;
                var lo = Add(knee, 0.9f);
                Capsule(knee, new Vector3(0, -0.15f, front ? 0f : -0.04f), 0.03f, 0.34f);
                // 앞다리 무릎은 +쪽, 뒷다리 발목은 -쪽으로만 굽는다
                if (front) Joint(lo, up, Vector3.right, Vector3.up, -3f, 110f, 6f, 6f);
                else Joint(lo, up, Vector3.right, Vector3.up, -110f, 3f, 6f, 6f);
                list.Add(lo);
            }
        Release(list, rig.Body, impulse, rig.Neck);
        return list;
    }

    // 모든 강체를 굳힌다 (가라앉힐 때 물리를 끈다)
    public static void Freeze(List<Rigidbody> parts)
    {
        foreach (var rb in parts)
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }
    }

    // 모두 거의 멈췄는가
    public static bool Settled(List<Rigidbody> parts, float speed = 0.1f)
    {
        foreach (var rb in parts)
            if (rb != null && !rb.isKinematic && (rb.linearVelocity.sqrMagnitude > speed * speed || rb.angularVelocity.sqrMagnitude > 1f)) return false;
        return true;
    }

    static Rigidbody Add(Transform t, float mass)
    {
        var rb = t.gameObject.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.isKinematic = true;
        rb.linearDamping = 0.35f;
        rb.angularDamping = 1.6f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.solverIterations = 10;
        rb.solverVelocityIterations = 4;
        rb.maxAngularVelocity = 18f;
        return rb;
    }

    static void Capsule(Transform t, Vector3 center, float radius, float height)
    {
        var c = t.gameObject.AddComponent<CapsuleCollider>();
        c.direction = 1;
        c.center = center;
        c.radius = radius;
        c.height = height;
    }

    // child를 parent에 잇는다. axis: 비틀림(twist) 축, swingAxis: 첫 흔들림 축. 한계는 도
    static CharacterJoint Joint(Rigidbody child, Rigidbody parent, Vector3 axis, Vector3 swingAxis, float twistLow, float twistHigh, float swing1, float swing2)
    {
        var j = child.gameObject.AddComponent<CharacterJoint>();
        j.connectedBody = parent;
        j.anchor = Vector3.zero;
        j.axis = axis;
        j.swingAxis = swingAxis;
        j.lowTwistLimit = new SoftJointLimit { limit = twistLow, bounciness = 0f, contactDistance = 0f };
        j.highTwistLimit = new SoftJointLimit { limit = twistHigh, bounciness = 0f, contactDistance = 0f };
        j.swing1Limit = new SoftJointLimit { limit = swing1, bounciness = 0f, contactDistance = 0f };
        j.swing2Limit = new SoftJointLimit { limit = swing2, bounciness = 0f, contactDistance = 0f };
        var spring = new SoftJointLimitSpring { spring = 0f, damper = 0.6f };
        j.twistLimitSpring = spring;
        j.swingLimitSpring = spring;
        j.enableProjection = true;
        j.projectionDistance = 0.05f;
        j.projectionAngle = 30f;
        j.enablePreprocessing = false;
        j.enableCollision = false;
        return j;
    }

    // 물리를 켜고 밀어낸다
    static void Release(List<Rigidbody> parts, Transform body, Vector3 impulse, Transform head)
    {
        foreach (var rb in parts)
        {
            if (rb == null) continue;
            SetLayer(rb.transform);
            rb.isKinematic = false;
            rb.WakeUp();
        }
        var torso = body != null ? body.GetComponent<Rigidbody>() : null;
        if (torso != null)
        {
            torso.AddForceAtPosition(impulse, body.position + Vector3.up * 0.35f, ForceMode.Impulse);
            torso.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
        }
        var h = head != null ? head.GetComponent<Rigidbody>() : null;
        if (h != null) h.AddForce(impulse * 0.12f, ForceMode.Impulse);
    }

    static void SetLayer(Transform t)
    {
        t.gameObject.layer = Layer;
        foreach (Transform c in t)
            if (c.GetComponent<Rigidbody>() == null) SetLayer(c);
    }
}
