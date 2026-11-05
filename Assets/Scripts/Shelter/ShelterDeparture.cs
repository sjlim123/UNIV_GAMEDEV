using System.Collections;
using UnityEngine;

// 방공호를 나설 때의 1인칭 연출 (3인칭 컷씬이 아니라 그대로 1인칭). 사람의 눈으로 본 동작 그대로:
// 든 물건을 내려놓고 손전등도 넣은 뒤(두 손이 비어야 한다) 문 앞 팔 닿는 거리까지 두세 걸음 다가가 바퀴 손잡이를 내려다본다.
// 두 손이 화면 아래에서 올라와 테두리의 1시 반(오른손)·10시 반(왼손) 자리를 쥐고, 오른쪽에서 왼쪽으로(반시계) 100°씩 세 번 나눠 돌린다
// (한 번 돌리고 두 손을 떼어 되가져와 다시 잡는다). 돌릴 때 상체가 손을 따라가므로 머리(카메라)가 왼쪽으로 쏠리며 기울고 살짝 내려앉았다가,
// 손을 되가져올 때 오른쪽으로 되돌아온다 (힘줄 때 잔떨림, 걸쇠가 풀릴 때 울림). 잠금이 풀리면 왼손을 놓고 오른손바닥을 문짝에 얹어 밀며
// 한 걸음씩 따라 들어가고, 팔이 다 펴지면 손을 놓는다. 그 너머의 어두운 통로로 들어서며 화면이 어두워지고 도시 씬이 열린다 (넣어 둔 손전등은 나서며 다시 꺼낸다).
// 씬에는 문 뒤에 벽이 있으므로(복도 북쪽 벽) 그 벽 조각을 잠시 감추고 검은 통로를 코드로 세운다.
// GameManager.StartExpedition이 LoadRoutine 전에 Play를 기다린다
public class ShelterDeparture : MonoBehaviour
{
    const float ApproachSeconds = 1.5f, ReachSeconds = 0.6f, RegripSeconds = 0.16f, CrankSeconds = 0.62f, ReturnSeconds = 0.42f, OpenSeconds = 2.1f, StepSeconds = 1.3f;
    const float StandDist = 0.56f;    // 눈에서 문 앞면까지 (m): 팔꿈치를 굽힌 채 바퀴에 손이 닿는 거리
    const float WheelRadius = 0.2f;   // 바퀴 손잡이 반지름 (빌더의 Wheel 지름 0.45: 테두리 조금 안쪽을 쥔다)
    const float GripDegR = 42f, GripDegL = -42f; // 처음 잡는 테두리 각도 (12시 = 0, 사람이 볼 때 시계 방향 +): 오른손 1시 반, 왼손 10시 반
    const float CrankDeg = -100f;     // 한 번에 돌리는 각도 (반시계 = 오른쪽→왼쪽: 잠금을 푸는 방향). 세 번
    const float PalmDepth = 0.075f;   // 손목에서 손바닥 가운데까지
    const float SwayLeft = 0.045f, SwayRoll = 6f, SwayDrop = 0.022f; // 돌릴 때 머리가 왼쪽으로 가는 거리(m)·기우는 각도(도)·내려앉는 높이(m)

    Transform exitGroup, door, wheel, pivot, camAnchor, tunnel;
    GameObject hiddenWall;
    PlayerController pc;
    PlayerHands hands;
    float[] gripFingers;
    Vector3 front, pRight, wheelC, standAt;
    Quaternion wheelRot0, baseRot;
    float spin = 1f;   // 바퀴 각도의 부호 (사람이 볼 때 시계 방향이 +가 되게 맞춘다)
    float wheelAngle;  // 바퀴가 돌아간 각도

    public static IEnumerator Play()
    {
        var go = new GameObject("ShelterDeparture");
        var d = go.AddComponent<ShelterDeparture>();
        yield return d.Run();
        Destroy(go);
    }

    IEnumerator Run()
    {
        pc = PlayerController.Instance;
        hands = PlayerHands.Instance;
        var exit = FindAnyObjectByType<ShelterExit>();
        if (pc == null || exit == null) yield break;
        exitGroup = exit.transform.parent;
        door = exit.transform;
        wheel = exitGroup != null ? exitGroup.Find("Wheel") : null;
        var gm = GameManager.Instance;
        gm.UI.SetLetterbox(true);
        if (hands != null) hands.SetStowed(true); // 든 물건을 내려놓는다 (손이 내려갔다 빈손으로 올라온다)
        var fl = PlayerFlashlight.Instance;
        bool lightWasOut = fl != null && fl.IsOut;
        if (lightWasOut) fl.SetOut(false); // 두 손으로 돌려야 하므로 손전등도 넣는다 (나서며 다시 꺼낸다)
        if (fl != null) fl.Dim = 0.3f;   // 다시 꺼냈을 때 50cm 앞 철문이 손전등에 하얗게 날아가지 않게

        front = -door.forward;                      // 문의 앞면은 -Z (복도 쪽)
        pRight = Vector3.Cross(Vector3.up, -front); // 문을 보고 선 사람의 오른쪽
        float eyeHeight = pc.cameraRoot.position.y - pc.transform.position.y;
        wheelC = wheel != null ? wheel.position : door.position + front * 0.1f + Vector3.up * 0.05f;
        wheelRot0 = wheel != null ? wheel.rotation : Quaternion.identity;
        if (Vector3.Dot(Radial(60f), pRight) < 0) spin = -1f;
        var face = door.position + front * 0.06f;
        standAt = new Vector3(face.x, pc.transform.position.y + eyeHeight, face.z) + front * StandDist;
        var lookAt = wheelC + Vector3.up * 0.24f;   // 바퀴가 화면 아래쪽 가운데에 오게 조금 내려다본다
        baseRot = Quaternion.LookRotation(lookAt - standAt, Vector3.up);
        camAnchor = new GameObject("DepartureCam").transform;
        camAnchor.position = pc.cameraRoot.position;
        camAnchor.rotation = pc.cameraRoot.rotation;
        // 복도 램프 빛이 손과 바퀴에 닿는 느낌의 약한 보조광 (머리 위 왼쪽, 그림자 없음): 팔·손이 어둠에 묻히지 않는다
        var fill = new GameObject("DepartureFill").AddComponent<Light>();
        fill.transform.SetParent(camAnchor, false);
        fill.transform.localPosition = new Vector3(-0.25f, 0.3f, -0.1f);
        fill.type = LightType.Point;
        fill.color = new Color(1f, 0.82f, 0.66f);
        fill.intensity = 0.32f;
        fill.range = 2.6f;
        fill.shadows = LightShadows.None;
        BuildTunnel(front);

        // 1) 문 앞으로 다가가 선다: 거리에 맞춰 한두세 걸음 (걸음마다 머리가 오르내리고 살짝 좌우로 흔들리며 발소리). 그동안 손전등이 내려간다
        var fromPos = camAnchor.position;
        var fromRot = camAnchor.rotation;
        float dist = Vector3.Distance(new Vector3(fromPos.x, 0, fromPos.z), new Vector3(standAt.x, 0, standAt.z));
        int steps = Mathf.Clamp(Mathf.RoundToInt(dist / 0.65f), 1, 3);
        float approach = ApproachSeconds * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(dist / 2f));
        int stepsDone = 0;
        for (float t = 0; t < approach; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0, 1, t / approach);
            float gait = Mathf.Sin(k * Mathf.PI * steps);
            camAnchor.position = Vector3.Lerp(fromPos, standAt, k) + Vector3.up * (Mathf.Abs(gait) * 0.022f * (1 - k * 0.5f)) + pRight * (gait * 0.008f);
            camAnchor.rotation = Quaternion.Slerp(fromRot, baseRot, k);
            pc.SetCinematic(camAnchor, Mathf.Clamp01(t / 0.35f));
            int stepNow = Mathf.FloorToInt(k * steps + 0.02f);
            if (stepNow > stepsDone) { stepsDone = stepNow; ProceduralAudio.Play2D(Footsteps.Clip(Footsteps.Surface.Stone), 0.5f, Random.Range(0.92f, 1.04f)); }
            yield return null;
        }
        Head(0, 0, 0, 0, 0);
        pc.SetCinematic(camAnchor, 1f);

        // 2) 바퀴를 세 번 나눠 돌린다: 두 손이 테두리의 1시 반·10시 반 자리를 잡고 반시계로 돌리고, 떼어 되가져와 다시 잡는다
        gripFingers = GripPose();
        float lastDegR = GripDegR, lastDegL = GripDegL;
        for (int cycle = 0; cycle < 3; cycle++)
        {
            // 잡기: 처음에는 화면 아래의 빈손 둘이 올라와 붙고(왼손이 반 박자 늦다), 다음부터는 되가져온 자리에서 바로 붙는다
            float grabT = cycle == 0 ? ReachSeconds : RegripSeconds;
            Vector3 fromR = hands != null && hands.RightWrist != null ? hands.RightWrist.position : RimWrist(GripDegR, 0.3f, false);
            Vector3 fromL = hands != null && hands.LeftWrist != null ? hands.LeftWrist.position : RimWrist(GripDegL, 0.3f, true);
            for (float t = 0; t < grabT; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / grabT);
                if (cycle == 0)
                {
                    float kl = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - 0.1f) / (grabT - 0.1f)));
                    Lock(false, Vector3.Lerp(fromR, RimWrist(GripDegR, 0f, false), k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.02f), RimRot(GripDegR, false), k);
                    Lock(true, Vector3.Lerp(fromL, RimWrist(GripDegL, 0f, true), kl) + Vector3.up * (Mathf.Sin(kl * Mathf.PI) * 0.02f), RimRot(GripDegL, true), kl);
                }
                else
                {
                    Lock(false, RimWrist(GripDegR, 0.06f * (1 - k), false), RimRot(GripDegR, false), 1f);
                    Lock(true, RimWrist(GripDegL, 0.06f * (1 - k), true), RimRot(GripDegL, true), 1f);
                }
                Head(1.5f * k, 0f, 0.012f * k, 0f, 0f); // 손을 뻗으며 아주 조금 숙이고 다가선다
                yield return null;
            }
            ProceduralAudio.PlayAt(cycle == 0 ? ProceduralAudio.Clang() : ProceduralAudio.Latch(), wheelC, cycle == 0 ? 0.45f : 0.3f, cycle == 0 ? 0.9f : 1.05f);
            pc.Shake(0.03f);
            // 돌리기: 두 손은 테두리에 붙은 채 바퀴와 함께 오른쪽에서 왼쪽으로 돈다. 상체가 따라가 머리가 왼쪽으로 쏠리며 기울고, 힘주는 동안 내려앉아 다가선다 (잔떨림). 쇠가 긁히는 소리
            var grind = ProceduralAudio.PlayAt(RadioAudio.DoorGrind(), wheelC, 0.35f, 1.2f + cycle * 0.05f, 1f, 12f);
            float from = wheelAngle;
            for (float t = 0; t < CrankSeconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / CrankSeconds);
                float e = Mathf.Sin(k * Mathf.PI);
                wheelAngle = from + CrankDeg * k;
                SetWheel(wheelAngle);
                lastDegR = GripDegR + CrankDeg * k;
                lastDegL = GripDegL + CrankDeg * k;
                Lock(false, RimWrist(lastDegR, 0f, false), RimRot(lastDegR, false), 1f);
                Lock(true, RimWrist(lastDegL, 0f, true), RimRot(lastDegL, true), 1f);
                float tremor = e * 0.0025f;
                float nx = (Mathf.PerlinNoise(Time.time * 19f, 0.7f) - 0.5f) * 2f * tremor, nz = (Mathf.PerlinNoise(Time.time * 23f, 0.3f) - 0.5f) * 2f * tremor;
                Head(1.5f + 2.5f * e, SwayRoll * k, 0.012f + 0.025f * e + nz, -SwayLeft * k + nx, -SwayDrop * e);
                yield return null;
            }
            if (grind != null) grind.Stop();
            ProceduralAudio.PlayAt(ProceduralAudio.Click(), wheelC, 0.3f, 0.7f);
            pc.Shake(0.05f);
            if (cycle == 2) break;
            // 놓고 되돌아가기: 두 손을 테두리에서 떼어(6cm) 처음 각도로 가져가고, 상체도 오른쪽으로 되돌아온다. 바퀴는 멈춰 있다
            for (float t = 0; t < ReturnSeconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / ReturnSeconds);
                float lift = 0.05f * Mathf.Sin(k * Mathf.PI) + 0.06f * k;
                float degR = GripDegR + CrankDeg * (1 - k), degL = GripDegL + CrankDeg * (1 - k);
                Lock(false, RimWrist(degR, lift, false), RimRot(degR, false), 1f);
                Lock(true, RimWrist(degL, lift, true), RimRot(degL, true), 1f);
                Head(1.5f, SwayRoll * (1 - k), 0.012f, -SwayLeft * (1 - k), -0.008f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
        }
        ProceduralAudio.PlayAt(ProceduralAudio.Latch(), wheelC, 0.9f, 0.8f);
        pc.Shake(0.12f);

        // 3) 문짝을 밀어 연다: 왼쪽 가장자리 경첩에서 안쪽(통로 쪽)으로 돌아 열린다. 왼손은 바퀴를 놓고, 오른손바닥을 문짝 오른쪽에 얹고 밀며 두 걸음 따라 들어간다.
        //    문이 팔 길이 너머로 멀어지면 손을 놓는다. 시선은 열리는 문틈 너머 통로로 옮겨 가고, 쏠렸던 상체가 바로 선다
        MakePivot();
        Vfx.DoorDebris(Vfx.DoorKind.Steel, door.TransformPoint(new Vector3(-0.6f, 1.1f, 0)), door.TransformPoint(new Vector3(0.6f, 1.1f, 0)), front, 1.4f);
        var creak = ProceduralAudio.PlayAt(ProceduralAudio.Creak(), door.position, 0.7f, 0.65f);
        var shoulderOffset = pRight * 0.19f + Vector3.down * 0.27f + front * 0.08f; // 눈 기준 오른쪽 어깨
        var tunnelLook = new Vector3(door.position.x, standAt.y - 0.15f, door.position.z) - front * 2.2f;
        var pushEnd = standAt - front * 0.8f;
        var leftWrist = RimWrist(lastDegL, 0.02f, true);
        var leftRot = RimRot(lastDegL, true);
        bool released = false;
        int pushSteps = 0;
        for (float t = 0; t < OpenSeconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0, 1, t / OpenSeconds);
            pivot.localRotation = Quaternion.Euler(0, -k * 96f, 0);
            // 문이 열리는 만큼 한 걸음씩 따라 들어간다 (두 걸음). 쏠렸던 머리는 반 초 동안 제자리로
            float walk = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - 0.2f) / (OpenSeconds - 0.4f)));
            float gait = Mathf.Sin(walk * Mathf.PI * 2f);
            float sway = 1f - Mathf.SmoothStep(0, 1, t / 0.5f);
            var pos = Vector3.Lerp(standAt, pushEnd, walk) + Vector3.up * (Mathf.Abs(gait) * 0.02f) + pRight * (gait * 0.006f - SwayLeft * sway);
            int stepNow = Mathf.FloorToInt(walk * 2f + 0.02f);
            if (stepNow > pushSteps) { pushSteps = stepNow; ProceduralAudio.Play2D(Footsteps.Clip(Footsteps.Surface.Stone), 0.55f, Random.Range(0.9f, 1.0f)); }
            camAnchor.position = pos;
            camAnchor.rotation = Quaternion.Slerp(baseRot, Quaternion.LookRotation(tunnelLook - pos, Vector3.up), Mathf.SmoothStep(0, 1, Mathf.Clamp01((k - 0.12f) / 0.7f))) * Quaternion.Euler(1.5f * sway, 0, SwayRoll * sway);
            // 왼손은 바퀴에서 떨어져 내려온다
            Lock(true, leftWrist, leftRot, Mathf.Clamp01(1f - t / 0.3f));
            // 오른손바닥은 문짝 앞면 오른쪽(경첩 반대쪽)에 얹힌 채 문과 함께 간다. 어깨에서 팔 길이를 넘어가면 놓는다
            var palm = door.TransformPoint(new Vector3(0.32f, 0.06f, -0.105f)); // 한 단 올라온 문짝 판 위
            var rot = FpHandRot(door.forward, Vector3.up - pRight * 0.35f, false);
            float reach = Vector3.Distance(pos + shoulderOffset, palm);
            if (!released && reach > 0.66f) released = true;
            float hold = released ? 0f : Mathf.InverseLerp(0.66f, 0.58f, reach);
            if (hands != null) hands.LockRight(palm - rot * new Vector3(0, 0, PalmDepth + 0.01f), rot, hold, HandRig.Open);
            yield return null;
        }
        if (creak != null) creak.Stop();
        if (hands != null)
        {
            hands.LockRight(Vector3.zero, Quaternion.identity, 0f);
            hands.LockLeft(Vector3.zero, Quaternion.identity, 0f);
        }
        ProceduralAudio.PlayAt(ProceduralAudio.Bang(), door.position, 0.5f, 0.7f);

        // 4) 열린 문으로 들어서며 어두워진다 (두 걸음). 넣어 둔 손전등을 다시 꺼낸다 (도시에서 꺼낸 채 시작)
        if (lightWasOut && fl != null && !fl.IsOut) fl.SetOut(true);
        var stepFrom = camAnchor.position;
        var stepRotFrom = camAnchor.rotation;
        var stepTo = stepFrom - front * 1.6f;
        var stepRot = Quaternion.LookRotation(-front, Vector3.up);
        gm.StartCoroutine(gm.UI.Fade(1, StepSeconds * 0.9f));
        int walkSteps = 0;
        for (float t = 0; t < StepSeconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0, 1, t / StepSeconds);
            camAnchor.position = Vector3.Lerp(stepFrom, stepTo, k) + Vector3.up * (Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f)) * 0.025f);
            camAnchor.rotation = Quaternion.Slerp(stepRotFrom, stepRot, k);
            int stepNow = Mathf.FloorToInt(k * 2f + 0.02f);
            if (stepNow > walkSteps) { walkSteps = stepNow; ProceduralAudio.Play2D(Footsteps.Clip(Footsteps.Surface.Stone), 0.6f, Random.Range(0.9f, 1.0f)); }
            yield return null;
        }
        gm.UI.SetLetterbox(false);
        // 카메라·손·문은 씬이 바뀌며 사라진다 (복구하지 않는다)
    }

    // 바퀴 테두리 위 한 점의 방향 (deg: 12시 = 0, 사람이 볼 때 시계 방향 +)
    Vector3 Radial(float deg) => Quaternion.AngleAxis(-spin * deg, front) * Vector3.up;

    // 테두리의 deg 자리를 쥔 손: 손바닥이 바퀴 축을 보고 손가락은 테두리 너머(문 쪽)로 감는다 (왼손은 거울)
    Quaternion RimRot(float deg, bool left) => FpHandRot(-Radial(deg), -front, left);

    // 그 손의 손목 자리 (lift: 테두리에서 사람 쪽으로 띄운 거리)
    Vector3 RimWrist(float deg, float lift, bool left) => wheelC + Radial(deg) * WheelRadius + front * (0.025f + lift) - RimRot(deg, left) * new Vector3(0, 0, PalmDepth);

    void SetWheel(float deg)
    {
        if (wheel != null) wheel.rotation = Quaternion.AngleAxis(-spin * deg, front) * wheelRot0;
    }

    void Lock(bool left, Vector3 wrist, Quaternion rot, float weight)
    {
        if (hands == null) return;
        if (left) hands.LockLeft(wrist, rot, weight, gripFingers);
        else hands.LockRight(wrist, rot, weight, gripFingers);
    }

    // 머리(카메라): 선 자리에서 pitch(아래 +)·roll(왼쪽으로 기움 +, 도)만큼 돌리고 lean(문 쪽)·side(오른쪽)·drop(위) 만큼 옮긴다
    void Head(float pitch, float roll, float lean, float side, float drop)
    {
        camAnchor.position = standAt - front * lean + pRight * side + Vector3.up * drop;
        camAnchor.rotation = baseRot * Quaternion.Euler(pitch, 0, roll);
    }

    // 1인칭 손의 회전: 손바닥이 palmNormal 쪽을 보고 손가락이 fingers 쪽을 가리킨다 (HandRig 손 좌표: +z 손가락, +y 엄지, 손바닥은 오른손 -x·왼손 +x)
    static Quaternion FpHandRot(Vector3 palmNormal, Vector3 fingers, bool left)
    {
        var x = (left ? palmNormal : -palmNormal).normalized;
        var z = Vector3.ProjectOnPlane(fingers, x).normalized;
        if (z.sqrMagnitude < 1e-6f) z = Vector3.ProjectOnPlane(Vector3.forward, x).normalized;
        return Quaternion.LookRotation(z, Vector3.Cross(z, x));
    }

    // 바퀴 테를 쥔 손가락 모양: 주먹에 가깝게 감는다
    static float[] GripPose()
    {
        var p = new float[HandRig.PoseLength];
        HandRig.Blend(HandRig.Fist, HandRig.Relaxed, 0.25f, p);
        return p;
    }

    // 문짝과 바퀴를 왼쪽 가장자리의 경첩 피벗 아래로 옮긴다
    void MakePivot()
    {
        pivot = new GameObject("DoorPivot").transform;
        pivot.SetParent(door.parent, false);
        pivot.position = door.TransformPoint(new Vector3(-0.6f, -1.1f, 0));
        pivot.rotation = door.rotation;
        door.SetParent(pivot, true);
        if (wheel != null) wheel.SetParent(pivot, true);
        var col = door.GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }

    // 문 너머의 어두운 통로: 북쪽 벽 조각을 감추고 검은 바닥·벽·천장과 끝의 희미한 빛을 둔다
    void BuildTunnel(Vector3 front)
    {
        // 문 바로 뒤의 벽 조각을 찾는다 (문 뒤 0.4m 안)
        var behind = door.position - front * 0.35f + Vector3.up * 0.2f;
        foreach (var h in Physics.OverlapSphere(behind, 0.25f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.transform == door || h.transform.IsChildOf(door) || (exitGroup != null && h.transform.IsChildOf(exitGroup))) continue;
            if (h.GetComponent<MeshRenderer>() == null) continue;
            hiddenWall = h.gameObject;
            hiddenWall.SetActive(false);
            break;
        }
        tunnel = new GameObject("DepartureTunnel").transform;
        tunnel.position = door.position - front * 0.2f;
        tunnel.position = new Vector3(tunnel.position.x, pc.transform.position.y, tunnel.position.z);
        tunnel.rotation = Quaternion.LookRotation(-front, Vector3.up);
        var black = RuntimeVisuals.Mat(new Color(0.012f, 0.012f, 0.013f), 0.05f);
        var wet = RuntimeVisuals.Mat(new Color(0.05f, 0.05f, 0.055f), 0.6f);
        const float L = 6f, W = 1.9f, H = 2.6f;
        Part(tunnel, new Vector3(0, -0.05f, L / 2), new Vector3(W + 0.6f, 0.1f, L), wet);
        Part(tunnel, new Vector3(0, H + 0.05f, L / 2), new Vector3(W + 0.6f, 0.1f, L), black);
        Part(tunnel, new Vector3(-W / 2 - 0.1f, H / 2, L / 2), new Vector3(0.2f, H, L), black);
        Part(tunnel, new Vector3(W / 2 + 0.1f, H / 2, L / 2), new Vector3(0.2f, H, L), black);
        Part(tunnel, new Vector3(0, H / 2, L + 0.1f), new Vector3(W + 0.6f, H, 0.2f), black);
        // 문틀 옆·위의 틈을 막는 검은 판 (복도 폭 2m, 문 1.2m)
        Part(tunnel, new Vector3(-0.72f, 1.3f, 0.12f), new Vector3(0.6f, 2.6f, 0.06f), black);
        Part(tunnel, new Vector3(0.72f, 1.3f, 0.12f), new Vector3(0.6f, 2.6f, 0.06f), black);
        Part(tunnel, new Vector3(0, 2.45f, 0.12f), new Vector3(1.4f, 0.4f, 0.06f), black);
        // 끝의 희미한 푸른 빛과 천장에서 떨어지는 먼지
        var lg = new GameObject("FarLight").AddComponent<Light>();
        lg.transform.SetParent(tunnel, false);
        lg.transform.localPosition = new Vector3(0.3f, 1.6f, L - 0.6f);
        lg.type = LightType.Point;
        lg.color = new Color(0.45f, 0.55f, 0.7f);
        lg.intensity = 1.1f;
        lg.range = 5f;
        lg.shadows = LightShadows.None;
        var fl = lg.gameObject.AddComponent<FlickerLight>();
        fl.baseIntensity = 1.1f;
        fl.flickerAmount = 0.3f;
        fl.speed = 7f;
    }

    static void Part(Transform parent, Vector3 pos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
