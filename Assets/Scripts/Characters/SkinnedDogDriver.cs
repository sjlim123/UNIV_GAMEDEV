using System.Collections.Generic;
using UnityEngine;

// 들개 Tripo 몸의 뼈를 게임 관절(Animal.Animate·Ragdoll.Dog이 돌리는 Body·Neck·Head·Leg·Knee·Tail)에서 매 프레임 따라 움직인다.
// 관절마다 짝 뼈 하나 (DogRigMap). 쉬는 순간의 '관절 회전'과 '뼈 회전'을 모델 기준으로 기억했다가,
// 지금 관절 회전이 쉬는 회전에서 벗어난 만큼(모델 기준 차이)을 뼈의 쉬는 회전에 곱한다:
//   bone = M · (J · J₀⁻¹) · B₀   (M = 모델, J = 관절의 모델 기준 회전)
// 관절의 쉬는 각도가 0이 아니어도(꼬리 -118도, 턱 10도) 차이만 옮기므로 그대로 맞는다.
// 몸 관절은 뿌리뼈의 자리도 옮긴다 (래그돌로 쓰러질 때 몸 전체가 따라 눕는다). 짝 없는 뼈(등뼈 마디·꼬리 끝·귀·가슴의 짧은 뼈)는 부모를 따라간다.
public class SkinnedDogDriver : MonoBehaviour
{
    class Pair { public Transform Joint, Bone; public Quaternion J0, B0; public bool Pos; public Vector3 BoneInJoint; }

    Transform model;
    readonly List<Pair> pairs = new List<Pair>();

    public void Init(Transform modelRoot, DogRigMap map, CharacterRig rig)
    {
        model = modelRoot;
        pairs.Clear();
        // 부모 뼈부터 (자식 뼈의 월드 회전을 정한 뒤 부모를 돌리면 자식이 따라 틀어진다)
        Add(rig.Body, map.Root, true);
        Add(rig.Neck, map.Neck, false);
        Add(rig.Head, map.Head, false);
        for (int i = 0; i < 4; i++)
        {
            if (rig.Legs != null && i < rig.Legs.Length) Add(rig.Legs[i], map.Pivot[i], false);
            if (rig.Knees != null && i < rig.Knees.Length && map.Knee[i] != map.Pivot[i]) Add(rig.Knees[i], map.Knee[i], false);
        }
        Add(rig.Tail, map.TailDrive, false);
    }

    void Add(Transform joint, Transform bone, bool pos)
    {
        if (joint == null || bone == null) return;
        var Mi = Quaternion.Inverse(model.rotation);
        pairs.Add(new Pair
        {
            Joint = joint, Bone = bone, Pos = pos,
            J0 = Mi * joint.rotation, B0 = Mi * bone.rotation,
            BoneInJoint = joint.InverseTransformPoint(bone.position),
        });
    }

    void LateUpdate()
    {
        if (model == null) return;
        var M = model.rotation; var Mi = Quaternion.Inverse(M);
        foreach (var p in pairs)
        {
            if (p.Joint == null || p.Bone == null) continue;
            var delta = (Mi * p.Joint.rotation) * Quaternion.Inverse(p.J0);
            p.Bone.rotation = M * delta * p.B0;
            if (p.Pos) p.Bone.position = p.Joint.TransformPoint(p.BoneInJoint);
        }
    }
}
