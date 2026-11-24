using System.Collections.Generic;
using UnityEngine;

// 플레이어 모델 몸(CharacterModels.Player.cs)의 뼈를 게임 관절에서 매 프레임 따라 움직인다 (CutsceneActor가 관절을 돌린 뒤, LateUpdate).
// 관절의 쉬는 회전은 '팔다리가 -y로 곧게 내려가고 +z가 앞'이고 모델은 T자세라, 팔다리 뼈는 겨누기 짝(관절 회전 × 바인드 차이)으로 놓는다:
//   바인드 때 그 관절이 뼈 방향을 겨눴다면 가졌을 회전(-y = 뼈 방향, +z = 앞 또는 엄지 쪽)과 뼈의 바인드 회전 사이의 차이를 기억한다.
// 몸통: 척추 첫 마디가 몸통 관절 회전을 다 받고 위 마디(가슴·어깨)는 따라간다 (어깨 관절 자리가 모델 어깨와 꼭 맞게).
// 목 40% + 머리 60%, 발은 땅과 나란히(사람 방향), 손가락은 살짝 굽혀 둔다. 팔을 늘이면(LockHand가 멀 때) 아래팔·손 뼈 자리도 늘인다
public class SkinnedPlayerDriver : MonoBehaviour
{
    static readonly string[] Fingers = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
    static readonly string[] Segs = { "Proximal", "Intermediate", "Distal" };
    static readonly string[] Next = { "Intermediate", "Distal", "DistalEnd" };
    static readonly float[] Curl = { 50f, 64f, 40f }; // 느슨한 주먹 (달리기·바퀴 쥐기). 문을 미는 순간은 짧아 그대로 둔다

    class Pair { public Transform Bone, Joint; public Quaternion Offset; }

    CharacterRig rig;
    Transform model, hips;
    Transform hipsB, spineB, neckB, headB;
    Quaternion hipsRest, spineRest, neckRest, headRest; // 모델 기준 바인드 회전
    readonly List<Pair> limbs = new List<Pair>();
    readonly List<Transform> feet = new List<Transform>();
    readonly List<Quaternion> feetRest = new List<Quaternion>();
    readonly List<Transform> fingerB = new List<Transform>();
    readonly List<Quaternion> fingerBind = new List<Quaternion>();  // 손가락을 모은(T자세의 벌림을 뺀) 쉬는 회전
    readonly List<Vector3> fingerAxis = new List<Vector3>();
    readonly List<float> fingerDeg = new List<float>();
    readonly List<int> fingerSide = new List<int>();   // 0 왼손, 1 오른손 (CharacterRig.CurlL/CurlR)
    Transform[] loArm = new Transform[2], hand = new Transform[2];
    Transform[] foreJ = new Transform[2], handJ = new Transform[2];
    Vector3[] loArmPos = new Vector3[2], handPos = new Vector3[2];
    float upperLen, foreLen;

    public void Init(CharacterRig r, Transform hipsJoint, Dictionary<string, Transform> bones)
    {
        rig = r;
        model = r.Model;
        hips = hipsJoint;
        upperLen = r.UpperArmLen; foreLen = r.ForearmLen;
        foreJ[0] = r.ForeL; foreJ[1] = r.ForeR; handJ[0] = r.HandL; handJ[1] = r.HandR;
        var inv = Quaternion.Inverse(model.rotation);
        hipsB = bones["Hips"]; spineB = bones["Spine"]; neckB = bones["Neck"]; headB = bones["Head"];
        hipsRest = inv * hipsB.rotation; spineRest = inv * spineB.rotation; neckRest = inv * neckB.rotation; headRest = inv * headB.rotation;
        Vector3 fwd = model.forward;
        for (int i = 0; i < 2; i++)
        {
            string p = i == 0 ? "Left_" : "Right_";
            Transform B(string n) => bones[p + n];
            var joints = i == 0 ? new[] { r.ArmL, r.ForeL, r.HandL, r.LegL, r.ShinL } : new[] { r.ArmR, r.ForeR, r.HandR, r.LegR, r.ShinR };
            // 엄지 쪽: 손가락 뿌리 줄(새끼 → 검지)
            Vector3 fingers = (B("MiddleProximal").position - B("Hand").position).normalized;
            Vector3 thumbSide = bones.ContainsKey(p + "IndexProximal") && bones.ContainsKey(p + "PinkyProximal")
                ? Vector3.ProjectOnPlane(B("IndexProximal").position - B("PinkyProximal").position, fingers).normalized : fwd;
            Add(B("UpperArm"), joints[0], B("LowerArm").position - B("UpperArm").position, fwd);
            // 아래팔은 손과 같은 엄지 쪽 기준 (CutsceneActor 손 규칙: 손 회전이 0이면 엄지가 아래팔의 +z). 앞쪽 기준이면 손목에서 90° 비틀린다 (검토 11-24)
            Add(B("LowerArm"), joints[1], B("Hand").position - B("LowerArm").position, thumbSide);
            Add(B("Hand"), joints[2], fingers, thumbSide);
            Add(B("UpperLeg"), joints[3], B("LowerLeg").position - B("UpperLeg").position, fwd);
            Add(B("LowerLeg"), joints[4], B("Foot").position - B("LowerLeg").position, fwd);
            feet.Add(B("Foot")); feetRest.Add(inv * B("Foot").rotation);
            loArm[i] = B("LowerArm"); hand[i] = B("Hand");
            loArmPos[i] = loArm[i].localPosition; handPos[i] = hand[i].localPosition;
            // 손가락: 손바닥 쪽으로 굽는 축 (손가락 방향 × 손바닥). 손바닥 = 왼손 엄지쪽×손가락, 오른손 손가락×엄지쪽
            Vector3 palm = (i == 0 ? Vector3.Cross(thumbSide, fingers) : Vector3.Cross(fingers, thumbSide)).normalized;
            foreach (var f in Fingers)
                for (int k = 0; k < 3; k++)
                {
                    if (!bones.TryGetValue(p + f + Segs[k], out var fb)) continue;
                    var local = fb.localRotation;
                    // T자세 손가락은 부채처럼 벌어져 있다: 뿌리 마디를 손바닥 축으로 돌려 가운데 손가락과 나란히 모은다 (엄지는 그대로)
                    if (k == 0 && f != "Thumb" && f != "Middle" && bones.TryGetValue(p + f + Next[0], out var nb0))
                    {
                        var d0 = Vector3.ProjectOnPlane(nb0.position - fb.position, palm);
                        var q = Quaternion.FromToRotation(d0, Vector3.ProjectOnPlane(fingers, palm));
                        q = Quaternion.Slerp(Quaternion.identity, q, 0.85f);
                        local = Quaternion.Inverse(fb.parent.rotation) * q * fb.rotation;
                    }
                    // 굽힘 축: 이 마디 방향 × 손바닥 (마디마다 자기 방향 기준이라 엄지도 손바닥 쪽으로 접힌다)
                    Vector3 dir = bones.TryGetValue(p + f + Next[k], out var nb) ? (nb.position - fb.position).normalized : fingers;
                    var axisW = Vector3.Cross(dir, palm);
                    if (axisW.sqrMagnitude < 1e-6f) axisW = Vector3.Cross(fingers, palm);
                    fingerB.Add(fb); fingerBind.Add(local);
                    fingerAxis.Add(fb.InverseTransformDirection(axisW.normalized));
                    fingerDeg.Add(Curl[k] * (f == "Thumb" ? 0.55f : 1f));
                    fingerSide.Add(i);
                }
        }
    }

    // 바인드 때 관절이 dir(뼈 방향)을 겨누고 +z가 zHint 쪽이었다면 가졌을 회전 → 뼈 바인드 회전과의 차이
    void Add(Transform bone, Transform joint, Vector3 dir, Vector3 zHint)
    {
        var down = dir.normalized;
        var z = Vector3.ProjectOnPlane(zHint, down);
        if (z.sqrMagnitude < 1e-6f) z = Vector3.ProjectOnPlane(model.forward, down);
        var frame = Quaternion.LookRotation(z.normalized, -down);
        limbs.Add(new Pair { Bone = bone, Joint = joint, Offset = Quaternion.Inverse(frame) * bone.rotation });
    }

    void LateUpdate()
    {
        if (rig == null || rig.Body == null || model == null) return;
        var M = model.rotation;
        hipsB.position = hips.position;
        hipsB.rotation = M * hipsRest;
        // 몸통 관절 회전을 척추 첫 마디에 (자식인 가슴·어깨·목은 바인드 자리대로 따라간다)
        var bodyRel = Quaternion.Inverse(M) * rig.Body.rotation;
        spineB.rotation = M * bodyRel * spineRest;
        var hl = rig.Head.localRotation;
        neckB.rotation = M * bodyRel * Quaternion.Slerp(Quaternion.identity, hl, 0.4f) * neckRest;
        headB.rotation = M * bodyRel * hl * headRest;
        // 팔 늘이기: 관절 사이 거리대로
        for (int i = 0; i < 2; i++)
        {
            float su = foreJ[i] != null ? foreJ[i].localPosition.magnitude / Mathf.Max(1e-4f, upperLen) : 1f;
            float sf = handJ[i] != null ? handJ[i].localPosition.magnitude / Mathf.Max(1e-4f, foreLen) : 1f;
            loArm[i].localPosition = loArmPos[i] * su;
            hand[i].localPosition = handPos[i] * sf;
        }
        foreach (var l in limbs)
            if (l.Joint != null) l.Bone.rotation = l.Joint.rotation * l.Offset;
        // 발: 땅과 나란히 (사람이 보는 쪽)
        for (int i = 0; i < feet.Count; i++) feet[i].rotation = M * feetRest[i];
        for (int i = 0; i < fingerB.Count; i++)
            fingerB[i].localRotation = fingerBind[i] * Quaternion.AngleAxis(fingerDeg[i] * (fingerSide[i] == 0 ? rig.CurlL : rig.CurlR), fingerAxis[i]);
    }
}
