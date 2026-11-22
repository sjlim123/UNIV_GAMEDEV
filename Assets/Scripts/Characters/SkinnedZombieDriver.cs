using System.Collections.Generic;
using UnityEngine;

// 감염자 피부 메시의 뼈 53개를 게임 관절 7종(Zombie.cs·래그돌이 움직이는 것)에서 매 프레임 따라 움직인다.
// 규칙 (ModelWork/Enemies/zombie_common/export/manifest.json game_joint_driving과 같다):
//   척추 3마디 = 몸통 회전을 3등분, 목 40% + 머리 60%, 쇄골 = 팔을 든 만큼 어깨가 올라가고 앞으로,
//   팔·아래팔·손·다리·정강이 = 관절 그대로, 발 = 정강이 기울기를 반쯤 되돌림, 손가락 = 팔을 높이 들수록 갈고리처럼 오므림.
// 뼈의 쉬는 자세 회전은 Model 기준으로 기억해 두고 '프레임 회전 × 쉬는 회전'으로 놓는다 (Blender 뼈 축이 달라도 맞는다).
public class SkinnedZombieDriver : MonoBehaviour
{
    static readonly string[] Fingers = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
    // 손바닥이 보는 쪽 (Model 기준, Blender에서 잰 값). 손가락이 이쪽으로 굽는다
    static readonly Vector3 PalmL = new Vector3(0.9653f, 0.0124f, -0.261f), PalmR = new Vector3(-0.8566f, 0.1377f, -0.4973f);

    CharacterRig rig;
    Transform model, hips;
    readonly List<Transform> order = new List<Transform>();
    readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
    readonly Dictionary<string, Transform> bone = new Dictionary<string, Transform>();
    readonly Dictionary<string, Quaternion> g = new Dictionary<string, Quaternion>();
    readonly Dictionary<string, Vector3> fingerAxis = new Dictionary<string, Vector3>();
    float phase;

    public void Init(CharacterRig r, Transform hipsJoint, Dictionary<string, Transform> bones)
    {
        rig = r;
        model = r.Model;
        hips = hipsJoint;
        var inv = Quaternion.Inverse(model.rotation);
        foreach (var kv in bones)
        {
            var smr = kv.Value.GetComponent<SkinnedMeshRenderer>();
            if (smr != null || kv.Value == transform) continue;
            bone[kv.Key] = kv.Value;
            rest[kv.Value] = inv * kv.Value.rotation;
        }
        // 부모 먼저 놓이게 깊이 순서로
        foreach (var t in bone.Values) order.Add(t);
        order.Sort((a, b) => Depth(a).CompareTo(Depth(b)));
        // 손가락 굽힘 축: 손가락 방향 × 손바닥 방향 (손끝이 손바닥 쪽으로 가는 쪽으로 부호를 맞춘다)
        foreach (var s in new[] { "L", "R" })
            foreach (var f in Fingers)
            {
                if (!bone.TryGetValue(s + "_" + f + "1", out var f1) || !bone.TryGetValue(s + "_" + f + "3", out var f3)) continue;
                var palm = s == "L" ? PalmL : PalmR;
                var dir = (model.InverseTransformPoint(f3.position) - model.InverseTransformPoint(f1.position)).normalized;
                var axis = Vector3.Cross(dir, palm).normalized;
                if (Vector3.Dot(Quaternion.AngleAxis(20f, axis) * dir, palm) < Vector3.Dot(dir, palm)) axis = -axis;
                fingerAxis[s + "_" + f] = axis;
            }
        phase = Random.value * 10f;
    }

    static int Depth(Transform t)
    {
        int d = 0;
        while (t.parent != null) { t = t.parent; d++; }
        return d;
    }

    static Quaternion Part(Quaternion q, float f) => Quaternion.Slerp(Quaternion.identity, q, f);

    void LateUpdate()
    {
        if (rig == null || rig.Body == null || model == null) return;
        phase += Time.deltaTime * 1.5f;
        var M = model.rotation;
        var bodyW = rig.Body.rotation;
        var bd = Quaternion.Inverse(M) * bodyW;
        g["Hips"] = hips != null ? hips.rotation : M;
        g["Spine"] = M * Part(bd, 1f / 3f);
        g["Spine1"] = M * Part(bd, 2f / 3f);
        g["Spine2"] = bodyW;
        var hl = rig.Head != null ? rig.Head.localRotation : Quaternion.identity;
        g["Neck"] = g["Spine2"] * Part(hl, 0.4f);
        g["Head"] = g["Neck"] * Part(hl, 0.6f);
        g["Jaw"] = g["Head"] * (rig.Jaw != null ? rig.Jaw.localRotation : Quaternion.identity);
        Side("L", -1, rig.ArmL, rig.ForeL, rig.HandL, rig.LegL, rig.ShinL);
        Side("R", 1, rig.ArmR, rig.ForeR, rig.HandR, rig.LegR, rig.ShinR);

        // 뿌리 뼈는 관절 자리로 (래그돌로 쓰러질 때 몸이 따라간다)
        Place("Hips", hips);
        Place("Spine", rig.Body);
        Place("LegL", rig.LegL);
        Place("LegR", rig.LegR);
        foreach (var t in order)
            if (g.TryGetValue(t.name, out var q)) t.rotation = q * rest[t];
    }

    void Place(string name, Transform joint)
    {
        if (joint != null && bone.TryGetValue(name, out var t)) t.position = joint.position;
    }

    void Side(string s, int u, Transform arm, Transform fore, Transform hand, Transform leg, Transform shin)
    {
        if (arm == null) return;
        // 쇄골: 팔을 든 각도 p의 16%(최대 20°) 들어올리고, 앞으로 뻗으면 12%(최대 15°) 앞으로
        var down = arm.localRotation * Vector3.down;
        float pitch = Mathf.Acos(Mathf.Clamp(-down.y, -1f, 1f)) * Mathf.Rad2Deg;
        float lift = Mathf.Min(20f, 0.16f * pitch), fwd = down.z > 0 ? Mathf.Min(15f, 0.12f * pitch) : 0f;
        g["Clav" + s] = g["Spine2"] * Quaternion.AngleAxis(u * lift, Vector3.forward) * Quaternion.AngleAxis(-u * fwd, Vector3.up);
        g["Arm" + s] = arm.rotation;
        if (fore != null) g["Fore" + s] = fore.rotation;
        g["Hand" + s] = hand != null ? hand.rotation : g["Arm" + s];
        if (leg != null && shin != null)
        {
            g["Leg" + s] = leg.rotation;
            g["Shin" + s] = shin.rotation;
            g["Foot" + s] = g["Shin" + s] * Part(Quaternion.Inverse(leg.localRotation * shin.localRotation), 0.5f);
            g["Toe" + s] = g["Foot" + s];
        }
        // 손가락: 평소 살짝 오므리고, 팔을 높이 들수록(덮치기·할퀴기) 갈고리처럼
        float grip = Mathf.Clamp01((pitch - 60f) / 35f) * 28f;
        for (int i = 0; i < Fingers.Length; i++)
        {
            string key = s + "_" + Fingers[i];
            if (!fingerAxis.TryGetValue(key, out var axis)) continue;
            float b = (i == 0 ? 10f : 14f) + grip + 4f * Mathf.Sin(phase * 0.8f + i);
            var q = g["Hand" + s];
            q = q * Quaternion.AngleAxis(b * 0.8f, axis); g[key + "1"] = q;
            q = q * Quaternion.AngleAxis(b * 1.0f, axis); g[key + "2"] = q;
            q = q * Quaternion.AngleAxis(b * 0.7f, axis); g[key + "3"] = q;
        }
    }
}
