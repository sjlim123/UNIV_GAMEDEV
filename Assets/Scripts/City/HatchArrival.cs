using System.Collections;
using UnityEngine;

// 방공호로 돌아갈 때의 짧은 3인칭 연출 (인트로 컷씬과 같은 주인공 모델).
// 카메라는 해치 옆 경첩 반대쪽 길가로 물러나고, 주인공이 해치까지 빠른 걸음으로 걸어와 두 손으로 바퀴 손잡이를
// 사람처럼 잡았다 놓았다 하며(손 바꿔 잡기) 돌린 뒤, 오른손으로 문을 당겨 연다 (경첩은 주인공의 오른쪽: 문은 카메라에서 먼 쪽으로 돈다).
// 그리고 문 뒤의 어두운 통로(GameSceneBuilder.Ruins.cs의 HatchShell)로 걸어 들어간다. 문이 쿵 닫히며 어두워진다.
// GameManager.ReturnToShelter가 LoadRoutine 전에 Play를 기다린다
public class HatchArrival : MonoBehaviour
{
    const float WalkSpeed = 2.3f;
    const float StandDist = 0.49f;    // 문 가운데에서 발까지 (문짝 앞면에서 0.45m): 팔꿈치를 굽힌 채 바퀴에 손이 닿는 거리
    const float WheelRadius = 0.2f;   // 바퀴 손잡이 반지름 (빌더의 Wheel 지름 0.4)
    const float CrankDeg = -70f;      // 한 번에 돌리는 각도 (사람이 볼 때 반시계: 잠금을 푸는 방향)
    const float GripR = 60f, GripL = -60f; // 처음 잡는 테두리 각도 (12시 = 0, 시계 방향 +): 오른손 2시, 왼손 10시
    static readonly float ArmReach = CharacterModels.UpperArm + CharacterModels.Forearm;

    public static IEnumerator Play()
    {
        var go = new GameObject("HatchArrival");
        var a = go.AddComponent<HatchArrival>();
        yield return a.Run();
        Destroy(go);
    }

    Camera cam;
    CutsceneActor actor;
    Transform door, wheel, pivot, hatch;
    Vector3 outward, aRight, wheelC, doorBase;
    Quaternion wheelRot0;
    float spin = 1f; // 바퀴 각도의 부호 (사람이 볼 때 시계 방향이 +가 되게 맞춘다)

    IEnumerator Run()
    {
        var pc = PlayerController.Instance;
        var exit = FindAnyObjectByType<CityExit>();
        if (pc == null || exit == null) yield break;
        var gm = GameManager.Instance;
        door = exit.transform;
        hatch = door.parent;
        wheel = hatch != null ? hatch.Find("Wheel") : null;
        outward = door.forward; // 해치 문은 벙커 +Z 면에 있다 (바깥이 +Z)
        doorBase = new Vector3(door.position.x, pc.transform.position.y, door.position.z);
        aRight = Vector3.Cross(Vector3.up, -outward).normalized; // 문을 보고 선 사람의 오른쪽 = 경첩 쪽 (문짝의 -x)
        wheelC = wheel != null ? wheel.position : door.position + Vector3.up * 0.05f;
        wheelRot0 = wheel != null ? wheel.rotation : Quaternion.identity;
        if (Vector3.Dot(Radial(60f), aRight) < 0) spin = -1f;

        // 주인공: 플레이어 자리에서 시작해 문 앞까지 걷는다. 너무 가까우면 조금 물러난 곳에서 시작한다
        var start = pc.transform.position;
        var goal = doorBase + outward * StandDist;
        var toGoal = goal - start;
        toGoal.y = 0;
        if (toGoal.magnitude < 3.5f) start = goal + (toGoal.magnitude > 0.1f ? -toGoal.normalized : outward) * 3.5f;
        start.y = doorBase.y;
        var actorGo = new GameObject("ArrivalActor");
        actor = actorGo.AddComponent<CutsceneActor>();
        actor.Build(null, 1); // 탐험 차림: 1인칭 팔과 같은 야상·반장갑, 작업 바지·군화
        actor.SetBackpack(true);
        actorGo.transform.position = start;
        actorGo.transform.rotation = Quaternion.LookRotation(new Vector3(goal.x - start.x, 0, goal.z - start.z).normalized, Vector3.up);
        actor.Set(CutsceneActor.Mode.Stand, 0.01f);
        actor.Tick(0.02f, 0);

        // 카메라: 플레이어 카메라를 끄고 연출 카메라를 쓴다. 경첩 반대쪽(사람의 왼쪽) 길가에서 문과 사람을 본다.
        // 문은 경첩 쪽(카메라에서 먼 쪽)으로 열리니 열린 문짝 너머로 통로 안이 보인다
        var playerCam = pc.cameraRoot.GetComponent<Camera>();
        var listener = pc.cameraRoot.GetComponent<AudioListener>();
        if (PlayerHands.Instance != null) PlayerHands.Instance.SetHidden(true); // 1인칭 손은 3인칭 장면에 보이지 않게
        var camGo = new GameObject("ArrivalCam");
        cam = camGo.AddComponent<Camera>();
        if (playerCam != null)
        {
            cam.CopyFrom(playerCam);
            playerCam.enabled = false;
        }
        var camData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (camData == null) camData = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        cam.fieldOfView = 44f;
        if (listener != null) listener.enabled = false;
        camGo.AddComponent<AudioListener>();
        cam.transform.position = doorBase + outward * 2.5f - aRight * 2.6f + Vector3.up * 1.5f; // 46° 옆에서: 바퀴를 잡은 두 손이 몸에 가리지 않는다
        cam.transform.rotation = Quaternion.LookRotation((doorBase + Vector3.up * 1.1f) - cam.transform.position, Vector3.up);
        gm.UI.SetLetterbox(true);
        gm.StartCoroutine(gm.UI.Fade(0, 0.5f));

        // 1) 걸어온다 (빠른 걸음), 문 앞에 서서 문을 본다
        actor.Set(CutsceneActor.Mode.Walk, 0.35f);
        actor.WalkCadence = 1.35f;
        actor.OnFootstep = p => ProceduralAudio.PlayAt(ProceduralAudio.Footstep(), p, 0.5f, Random.Range(0.9f, 1.05f));
        yield return WalkTo(actorGo.transform, goal, WalkSpeed, 0.8f, 0.9f);
        var faceDoor = Quaternion.LookRotation(-outward, Vector3.up);
        actor.Set(CutsceneActor.Mode.Stand, 0.4f);
        actor.RunAmount = 0;
        for (float t = 0; t < 0.4f; t += Time.deltaTime)
        {
            actorGo.transform.rotation = Quaternion.Slerp(actorGo.transform.rotation, faceDoor, Time.deltaTime * 8f);
            Step(actorGo.transform, goal, 0);
            yield return null;
        }
        actorGo.transform.rotation = faceDoor;

        // 2) 바퀴를 돌린다: 두 손으로 테두리를 잡고 70°씩 세 번. 한 번 돌리고 나면 손을 떼어 처음 자리로 되가져와 다시 잡는다
        //    (손이 테두리를 따라 빙빙 도는 게 아니라 사람처럼 손을 바꿔 잡는다. 팔은 내내 굽힌 채, 팔꿈치는 바깥 아래로)
        float angle = 0; // 바퀴가 돌아간 각도
        for (int cycle = 0; cycle < 3; cycle++)
        {
            // 잡기: 손이 테두리 바로 앞에서 다가가 붙는다 (처음에는 팔을 뻗는 시간을 더 준다)
            float grabT = cycle == 0 ? 0.45f : 0.24f;
            for (float t = 0; t < grabT; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / grabT);
                float reach = cycle == 0 ? k : 1f;
                Grip(true, GripL, 0.07f * (1 - k), reach);
                Grip(false, GripR, 0.07f * (1 - k), reach);
                Step(actorGo.transform, goal, 0);
                yield return null;
            }
            ProceduralAudio.PlayAt(ProceduralAudio.Latch(), wheelC, 0.25f, cycle == 0 ? 0.9f : 1.05f);
            // 돌리기: 손은 테두리에 붙은 채 바퀴와 함께 돈다. 몸무게가 당기는 쪽으로 살짝 실린다
            var grind = ProceduralAudio.PlayAt(RadioAudio.DoorGrind(), wheelC, 0.35f, 1.15f + cycle * 0.05f, 1f, 14f);
            float crankT = 0.55f, from = angle;
            for (float t = 0; t < crankT; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / crankT);
                angle = from + CrankDeg * k;
                SetWheel(angle);
                Grip(true, GripL + CrankDeg * k, 0, 1f);
                Grip(false, GripR + CrankDeg * k, 0, 1f);
                Step(actorGo.transform, goal - aRight * (0.025f * Mathf.Sin(k * Mathf.PI)), 0);
                yield return null;
            }
            if (grind != null) grind.Stop();
            if (cycle == 2) break;
            // 놓고 되돌아가기: 손을 테두리에서 떼어(7cm) 처음 각도로 가져간다. 바퀴는 멈춰 있다
            float backT = 0.32f;
            for (float t = 0; t < backT; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / backT);
                float lift = 0.07f * Mathf.Sin(k * Mathf.PI) + 0.03f * k;
                Grip(true, GripL + CrankDeg * (1 - k), lift, 1f);
                Grip(false, GripR + CrankDeg * (1 - k), lift, 1f);
                Step(actorGo.transform, goal, 0);
                yield return null;
            }
        }
        ProceduralAudio.PlayAt(ProceduralAudio.Latch(), wheelC, 0.9f, 0.8f);

        // 3) 문을 당겨 연다 (경첩은 사람의 오른쪽, 문짝의 -x 가장자리. 바깥으로 돈다).
        //    오른손은 마지막에 잡은 테두리 자리를 쥔 채 문과 함께 가고, 왼손은 놓는다.
        //    경첩 반대쪽(왼쪽)으로 반 걸음, 뒤로 한 걸음 물러나며 당기다가, 팔이 다 펴지면 손을 놓는다 (문은 제 무게로 마저 열린다)
        MakePivot();
        Vfx.DoorDebris(Vfx.DoorKind.Hatch, door.TransformPoint(new Vector3(-0.55f, 1.05f, 0)), door.TransformPoint(new Vector3(0.55f, 1.05f, 0)), outward, 1.5f);
        var creak = ProceduralAudio.PlayAt(ProceduralAudio.Creak(), door.position, 0.8f, 0.6f);
        var gripLocal = wheel != null ? wheel.InverseTransformPoint(RimPoint(GripR + CrankDeg, 0)) : Vector3.zero;
        var retreat = goal - aRight * 0.5f + outward * 0.72f;
        actor.Set(CutsceneActor.Mode.Walk, 0.3f);
        actor.WalkCadence = 1.0f;
        actor.LockHand(true, Vector3.zero, Quaternion.identity, null, 0f);
        bool stood = false;
        float pullT = 1.8f;
        var prev = actorGo.transform.position;
        for (float t = 0; t < pullT; t += Time.deltaTime)
        {
            float dt = Time.deltaTime;
            float k = Mathf.SmoothStep(0, 1, t / pullT);
            pivot.localRotation = Quaternion.Euler(0, -k * 95f, 0); // 경첩(문짝 -x)에서 바깥으로 돈다
            float back = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - 0.15f) / 1.0f));
            var pos = Vector3.Lerp(goal, retreat, back);
            if (!stood && t >= 1.15f) { actor.Set(CutsceneActor.Mode.Stand, 0.35f); stood = true; }
            actor.RunAmount = stood ? 0f : 0.3f;
            if (wheel != null)
            {
                var gp = wheel.TransformPoint(gripLocal);
                float dist = Vector3.Distance(actor.Rig.ArmR.position, gp);
                float w = Mathf.InverseLerp(ArmReach * 0.97f, ArmReach * 0.86f, dist); // 손이 닿는 동안만 쥔다
                var rot = CutsceneActor.HandRotation(false, (wheel.position - gp).normalized, -door.forward);
                actor.LockHand(false, gp, rot, actor.Rig.ArmR.position + aRight * 0.4f + Vector3.down * 0.25f, w);
            }
            float moved = Vector3.Distance(pos, prev);
            prev = pos;
            // 열리는 문 가운데를 계속 본다
            var look = doorBase - pos;
            look.y = 0;
            actorGo.transform.rotation = Quaternion.Slerp(actorGo.transform.rotation, Quaternion.LookRotation(look.normalized, Vector3.up), dt * 5f);
            Step(actorGo.transform, pos, moved);
            yield return null;
        }
        if (creak != null) creak.Stop();
        actor.ReleaseHands();

        // 4) 통로로 들어간다: 문턱 앞으로 돌아 들어선 뒤 어둠 속으로. 문턱을 넘으면 화면이 어두워진다
        var threshold = doorBase + outward * 0.45f - aRight * 0.08f;
        var inside = doorBase - outward * 1.55f;
        actor.Set(CutsceneActor.Mode.Walk, 0.3f);
        actor.WalkCadence = 1.15f;
        yield return WalkTo(actorGo.transform, threshold, 1.4f, 0.4f, 0f);
        gm.StartCoroutine(gm.UI.Fade(1, 1.4f));
        yield return WalkTo(actorGo.transform, inside, 1.3f, 0f, 0.5f);
        // 문이 쿵 닫힌다
        for (float t = 0; t < 0.6f; t += Time.deltaTime)
        {
            pivot.localRotation = Quaternion.Euler(0, Mathf.Lerp(-95f, 0f, Mathf.SmoothStep(0, 1, t / 0.6f)), 0);
            yield return null;
        }
        ProceduralAudio.PlayAt(RadioAudio.DoorBoom(), door.position, 0.8f, 1.1f, 1f, 30f);
        yield return new WaitForSeconds(0.4f);
        gm.UI.SetLetterbox(false);
    }

    // 바퀴 테두리 위 한 점의 방향 (deg: 12시 = 0, 사람이 볼 때 시계 방향 +)
    Vector3 Radial(float deg) => Quaternion.AngleAxis(-spin * deg, outward) * Vector3.up;
    Vector3 RimPoint(float deg, float lift) => wheelC + Radial(deg) * WheelRadius + outward * (0.015f + lift);

    void SetWheel(float deg)
    {
        if (wheel != null) wheel.rotation = Quaternion.AngleAxis(-spin * deg, outward) * wheelRot0;
    }

    // 손 하나를 테두리의 deg 자리에 붙인다 (lift: 테두리에서 사람 쪽으로 띄운 거리). 손바닥은 바퀴 축을 보고 손가락은 테두리 너머로 감는다.
    // 팔꿈치는 바깥 아래를 향해 팔이 몸에서 떨어져 보이지 않는다
    void Grip(bool left, float deg, float lift, float weight)
    {
        var palm = RimPoint(deg, lift);
        var rot = CutsceneActor.HandRotation(left, -Radial(deg), -outward);
        var shoulder = (left ? actor.Rig.ArmL : actor.Rig.ArmR).position;
        var pole = shoulder + aRight * (left ? -0.45f : 0.45f) + Vector3.down * 0.3f + outward * 0.05f;
        actor.LockHand(left, palm, rot, pole, weight);
    }

    // 한 프레임: 자리를 옮기고 자세를 갱신하고 카메라를 돌린다
    void Step(Transform actorT, Vector3 pos, float moved)
    {
        float dt = Time.deltaTime;
        actorT.position = pos;
        actor.Tick(dt, moved);
        AimCamera(doorBase + Vector3.up * 1.1f, actor.Rig.Head.position, dt);
    }

    // 곧게 걸어간다. rampIn/rampOut: 속도를 올리고 내리는 거리 (0이면 바로)
    IEnumerator WalkTo(Transform actorT, Vector3 target, float speed, float rampIn, float rampOut)
    {
        var from = actorT.position;
        float total = Vector3.Distance(from, target), walked = 0;
        var dir = total > 1e-4f ? (target - from) / total : actorT.forward;
        var face = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z).normalized, Vector3.up);
        while (walked < total - 0.005f)
        {
            float dt = Time.deltaTime;
            float ramp = 1f;
            if (rampIn > 0) ramp *= Mathf.Clamp01(walked / rampIn);
            if (rampOut > 0) ramp *= Mathf.Clamp01((total - walked) / rampOut);
            float v = speed * Mathf.Lerp(0.3f, 1f, ramp);
            float step = Mathf.Min(v * dt, total - walked);
            walked += step;
            actor.RunAmount = Mathf.Clamp01(v / WalkSpeed);
            actorT.rotation = Quaternion.Slerp(actorT.rotation, face, dt * 6f);
            Step(actorT, from + dir * walked, step);
            yield return null;
        }
        actorT.position = target;
    }

    // 카메라가 문과 사람 사이를 본다 (조금 느리게 따라간다)
    void AimCamera(Vector3 doorPoint, Vector3 head, float dt)
    {
        var target = Vector3.Lerp(doorPoint, head, 0.55f);
        var want = Quaternion.LookRotation(target - cam.transform.position, Vector3.up);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, want, 1 - Mathf.Exp(-dt * 4f));
    }

    // 문짝과 바퀴를 경첩 축(문짝 -x 아래 가장자리) 아래로 옮긴다. 축은 해치 그룹의 자식이다
    // (처음 버전은 문구멍용 검은 판을 이 축의 자식으로 잘못 붙여 1.5m 앞 허공에 세웠다. 이제 벙커 자체가 속이 비어 그 판이 필요 없다)
    void MakePivot()
    {
        pivot = new GameObject("HatchPivot").transform;
        pivot.SetParent(hatch, false);
        pivot.position = door.TransformPoint(new Vector3(-0.55f, -1.05f, 0));
        pivot.rotation = door.rotation;
        door.SetParent(pivot, true);
        if (wheel != null) wheel.SetParent(pivot, true);
        var col = door.GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }
}
