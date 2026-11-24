using System.Collections;
using UnityEngine;

// 연료 붓기 (2026-11-24, 사용자 "연료 붓기 동작 만들기"): F를 누르면 1인칭으로 연료통을 들어 발전기 주입구에 붓는다.
//  1) 카메라가 주입구에서 0.48 m 떨어진 자리로 숙여 다가가 내려다본다 (몸은 그대로, ShelterDeparture처럼 카메라만. 배기관이 가리지 않는 쪽으로). 오른손은 연료통으로 바꿔 든다
//  2) 연료통을 주입구 위로 들어 올리고 노란 뚜껑을 딸깍 연다 (왼손 손전등은 그대로: 정전 중 어두운 방이라도 보이게)
//  3) 통을 80°쯤 옆으로 기울여 1.5초 붓는다: 주둥이에서 주입구로 연료 줄기가 흐르고, 꿀렁 소리가 나고, 붓는 중에 전력이 오르며 엔진이 울컥 빨라진다
//  4) 통을 세워 내리고 카메라·손이 돌아온다. 붓는 동안은 움직임·시선·다른 상호작용을 막는다 (PlayerController.ScriptBusy)
// 죽거나 장면이 바뀌어 중간에 끊겨도 연료는 이미 쓴 것이라 전력은 꼭 오르고, 손·카메라·막음은 되돌린다
public partial class Generator
{
    const float PourDistance = 0.48f;   // 카메라와 주입구 사이 (수평)
    const float PourEyeHeight = 0.58f;  // 카메라가 주입구보다 높은 만큼 (몸을 숙여 들여다본다. 서 있는 눈높이보다 높아지지는 않는다)
    const float PourTilt = 80f;         // 부을 때 통을 기울이는 각도
    const float PourSeconds = 1.5f;     // 붓는 시간 (꿀렁 소리 길이)
    static readonly Vector3 SpoutTipLocal = new Vector3(0, 0.458f, 0.10f); // 연료통(ItemVisuals.JerryCan) 주둥이 목 끝(뚜껑 아래), 통 좌표

    bool pouring, pourApplied;
    Transform pourAnchor;
    LineRenderer pourStream;

    // 붓는 몸짓을 시작한다. 1인칭 손이 없으면 false (바로 넣는다)
    bool StartPour()
    {
        var pc = PlayerController.Instance;
        var hands = PlayerHands.Instance;
        if (pc == null || hands == null || hands.RightWrist == null || !isActiveAndEnabled) return false;
        StartCoroutine(PourRoutine(pc, hands));
        return true;
    }

    IEnumerator PourRoutine(PlayerController pc, PlayerHands hands)
    {
        pouring = true;
        pourApplied = false;
        pc.ScriptBusy = true;
        hands.HoldForScript("fuel");

        // 카메라 자리: 주입구에서 0.48 m, 주입구보다 0.58 m 위(숙여 들여다보는 높이, 지금 눈높이보다 높지 않게, 0.35 m는 위), 주입구 바로 위를 내려다본다
        Vector3 hole = FillerPos;
        Vector3 eye = pc.cameraRoot.position;
        Vector3 flat = eye - hole;
        flat.y = 0;
        if (flat.sqrMagnitude < 0.01f) flat = -transform.forward;
        flat = AvoidExhaust(flat.normalized, hole);
        float eyeY = Mathf.Max(Mathf.Min(eye.y, hole.y + PourEyeHeight), hole.y + 0.35f);
        pourAnchor = new GameObject("PourCamera").transform;
        pourAnchor.position = hole + flat * PourDistance + Vector3.up * (eyeY - hole.y);
        // 통 주둥이가 향할 쪽: 화면 왼쪽으로 조금 안쪽 (통 몸통은 주입구 오른쪽 조금 앞에 와서 옆모습으로 기운다)
        Vector3 rightFlat = Quaternion.AngleAxis(90f, Vector3.up) * -flat;
        Vector3 toward = (-rightFlat - flat * 0.3f).normalized;
        Vector3 lookAt = hole + Vector3.up * 0.1f - flat * 0.04f + rightFlat * 0.07f; // 주입구와 오른쪽의 통이 함께 들어오게
        pourAnchor.rotation = Quaternion.LookRotation(lookAt - pourAnchor.position, Vector3.up);

        float t = 0f, lockW = 0f, tilt = 12f;
        bool capOff = false, poured = false, ready = false;
        float pourStart = -1f, waitHand = 0f;
        Transform can = null;
        while (true)
        {
            if (pc == null || hands == null || pc.IsDead) break;
            float dt = Time.deltaTime;
            if (dt <= 0f) { yield return null; continue; } // 일시정지
            t += dt;
            // 1) 카메라가 다가간다 (0.45초). 손이 연료통을 들고 올라오면 다음으로
            pc.SetCinematic(pourAnchor, Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 0.45f)) * (1f - OutBlend(t, pourStart)));
            if (!ready)
            {
                waitHand += dt;
                // 카메라가 다 다가간 뒤(0.45초)에 든다: 이미 연료통을 들고 있었어도 카메라가 한 번에 튀지 않게. 손이 늦으면 1.2초까지 기다린다
                ready = (hands.ScriptItemReady || waitHand > 1.2f) && t >= 0.45f;
                yield return null;
                continue;
            }
            can = hands.HeldModel;
            if (can == null) break;
            // 2) 들어 올리기 (0.45~1.0초): 손을 주입구 위로 붙인다. 0.75초에 뚜껑을 연다
            float lift = Mathf.Clamp01((t - 0.45f) / 0.55f);
            lockW = Mathf.SmoothStep(0, 1, lift);
            if (!capOff && t > 0.75f)
            {
                capOff = true;
                SetCapVisible(can, false);
                ProceduralAudio.PlayAt(ProceduralAudio.Clang(), hole, 0.25f, 1.4f);
            }
            // 3) 기울이기 (1.0~1.35초) → 붓기 (1.5초) → 세우기 (0.35초)
            if (pourStart < 0f && t >= 1.35f)
            {
                pourStart = t;
                ProceduralAudio.PlayAt(GeneratorAudio.Pour(), hole, 0.8f, Random.Range(0.95f, 1.05f), 1f, 14f);
            }
            float pourT = pourStart < 0f ? -1f : t - pourStart;
            if (pourStart < 0f) tilt = Mathf.Lerp(12f, PourTilt, Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - 1.0f) / 0.35f)));
            else if (pourT <= PourSeconds) tilt = PourTilt + Mathf.Sin(pourT * 9f) * 2.5f; // 꿀렁일 때마다 조금씩 흔들린다
            else tilt = Mathf.Lerp(PourTilt, 12f, Mathf.SmoothStep(0, 1, Mathf.Clamp01((pourT - PourSeconds) / 0.35f)));
            if (!poured && pourT >= 0.55f) { poured = true; ApplyOnce(); } // 붓는 중에 엔진이 받아 문다
            // 4) 내리기: 세운 뒤 0.45초 동안 손·카메라가 돌아온다
            float outK = OutBlend(t, pourStart);
            float w = lockW * (1f - outK);
            PlaceCan(hands, can, hole, toward, tilt, w);
            UpdateStream(can, hole, pourT >= 0f && pourT <= PourSeconds + 0.1f);
            if (pourStart >= 0f && pourT > PourSeconds + 0.35f + 0.45f) break;
            yield return null;
        }
        if (can != null) SetCapVisible(can, true);
        EndPour();
    }

    // 배기관(주입구 옆에 위로 솟은 관)이 화면을 가리지 않게 카메라 방향을 고른다.
    // 통은 주입구 오른쪽에서 왼쪽으로 기울여 붓고(옆모습이라 주둥이·줄기·주입구가 다 보인다), 관은 주입구의 왼쪽이나 뒤쪽에 오게 한다.
    // 관이 보는 쪽에서 오른쪽(통 자리)이나 앞(카메라와 주입구 사이)에 있으면, 플레이어가 보던 방향에서 가장 덜 돌아가는 자리로 옮긴다
    Vector3 AvoidExhaust(Vector3 flat, Vector3 hole)
    {
        const float MinA = -110f, MaxA = 15f; // 관의 방향 (보는 쪽 기준, + 오른쪽): 왼쪽 옆 ~ 뒤쪽 조금 오른쪽까지
        var pipe = transform.Find("Vibe/ExhaustPipe");
        if (pipe == null) return flat;
        Vector3 pd = pipe.position - hole;
        pd.y = 0;
        if (pd.sqrMagnitude < 1e-4f) return flat;
        pd.Normalize();
        float a = Vector3.SignedAngle(-flat, pd, Vector3.up);
        if (a >= MinA && a <= MaxA) return flat;
        float want = Mathf.Abs(Mathf.DeltaAngle(a, MinA)) < Mathf.Abs(Mathf.DeltaAngle(a, MaxA)) ? MinA : MaxA;
        return -(Quaternion.AngleAxis(-want, Vector3.up) * pd); // 새로 보는 쪽에서 관이 want 각도에 온다
    }

    // 붓기를 마치고 0.35초 세운 뒤부터 0.45초에 걸쳐 1 (돌아옴)
    static float OutBlend(float t, float pourStart)
    {
        if (pourStart < 0f) return 0f;
        return Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - pourStart - PourSeconds - 0.35f) / 0.45f));
    }

    // 통 주둥이 끝이 주입구 바로 위(기울일수록 가까이)에 오게 통 자리를 정하고, 그 통을 쥔 손목 자리로 오른손을 붙인다
    void PlaceCan(PlayerHands hands, Transform can, Vector3 hole, Vector3 toward, float tilt, float weight)
    {
        var canRot = Quaternion.LookRotation(toward, Vector3.up) * Quaternion.Euler(tilt, 0, 0); // +x로 돌리면 주둥이(+z)가 내려간다
        float k = Mathf.InverseLerp(12f, PourTilt, tilt);
        // 주둥이는 주입구 17 cm 위, 13 cm 뒤: 줄기가 주둥이에서 앞으로 나가 휘어 떨어지며 주입구에 들어간다 (손·소매가 주입구와 줄기를 가리지 않게)
        Vector3 tipTarget = hole + Vector3.up * Mathf.Lerp(0.3f, 0.17f, k) - toward * 0.13f;
        float scale = can.localScale.x;
        Vector3 canPos = tipTarget - canRot * (SpoutTipLocal * scale);
        // 손목 → 통 (Attach가 놓은 자리)의 거꾸로
        var wristRot = canRot * Quaternion.Inverse(can.localRotation);
        var wristPos = canPos - wristRot * can.localPosition;
        hands.LockRight(wristPos, wristRot, weight);
    }

    // 주둥이 끝에서 주입구로 떨어지는 연료 줄기 (살짝 휘는 띠, 굵기가 조금씩 일렁인다)
    void UpdateStream(Transform can, Vector3 hole, bool on)
    {
        if (!on)
        {
            if (pourStream != null) pourStream.enabled = false;
            return;
        }
        if (pourStream == null)
        {
            var go = new GameObject("FuelStream");
            pourStream = go.AddComponent<LineRenderer>();
            pourStream.positionCount = 10;
            pourStream.useWorldSpace = true;
            pourStream.sharedMaterial = StreamMat();
            pourStream.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pourStream.receiveShadows = false;
            pourStream.numCapVertices = 2;
            pourStream.alignment = LineAlignment.View;
        }
        pourStream.enabled = true;
        Vector3 tip = can.TransformPoint(SpoutTipLocal);
        Vector3 dir = can.TransformDirection(Vector3.up).normalized; // 기울인 주둥이 쪽
        Vector3 end = hole + Vector3.up * 0.01f;
        Vector3 ctrl = tip + dir * 0.12f;
        for (int i = 0; i < 10; i++)
        {
            float u = i / 9f;
            var p = (1 - u) * (1 - u) * tip + 2 * (1 - u) * u * ctrl + u * u * end; // 2차 베지어
            pourStream.SetPosition(i, p);
        }
        float wob = 1f + Mathf.Sin(Time.time * 31f) * 0.12f + Mathf.Sin(Time.time * 17f) * 0.08f;
        pourStream.startWidth = 0.02f * wob;
        pourStream.endWidth = 0.012f * wob;
    }

    // 연료 줄기 재질: 밝은 호박색에 매끈하고, 어두운 방에서도 줄기가 보이게 아주 조금 스스로 빛난다
    static Material streamMat;
    static Material StreamMat()
    {
        if (streamMat != null) return streamMat;
        streamMat = new Material(RuntimeVisuals.Mat(new Color(0.78f, 0.56f, 0.2f), 0.95f)) { name = "FuelStream" };
        streamMat.EnableKeyword("_EMISSION");
        streamMat.SetColor("_EmissionColor", new Color(0.16f, 0.1f, 0.03f));
        return streamMat;
    }

    static void SetCapVisible(Transform can, bool on)
    {
        foreach (var n in new[] { "Spout/Cap", "Spout/CapBand" })
        {
            var c = can.Find(n);
            if (c != null) c.gameObject.SetActive(on);
        }
    }

    // quiet: 장면을 닫는 중(OnDisable)이거나 죽는 중이면 전력만 올린다 (불티·연기·알림·흔들림을 부르지 않는다).
    // 장면을 닫는 중이면 저장(메인 메뉴로·게임 종료)이 연료만 빠진 채로 이미 끝났을 수 있으므로 다시 저장한다
    // (화로 Furnace.Finish와 같은 식. 죽어서 지운 저장은 되살리지 않는다)
    void ApplyOnce(bool quiet = false)
    {
        if (pourApplied) return;
        pourApplied = true;
        if (!quiet) { ApplyFuel(); return; }
        var gm = GameManager.Instance;
        var s = gm.State;
        s.Power = Mathf.Min(100, s.Power + GameBalance.Data.fuelPower);
        if (!gm.IsGameOver && SaveSystem.Exists(SaveSystem.CurrentSlot)) SaveSystem.Save(s);
    }

    // 끝내기 (중간에 끊겨도): 연료는 이미 썼으니 전력은 오르게 하고, 손·카메라·막음을 되돌린다.
    // 죽어서 끊기면 카메라는 붓던 자리에 둔다 (쓰러지는 연출이 그 자리에서 이어진다. 자리 표시는 장면과 함께 사라진다)
    void EndPour(bool quiet = false)
    {
        if (!pouring) return;
        pouring = false;
        var pc = PlayerController.Instance;
        var gm = GameManager.Instance;
        bool dead = (pc != null && pc.IsDead) || (gm != null && gm.IsGameOver);
        if (!pourApplied && gm != null) ApplyOnce(quiet || dead);
        if (pc != null)
        {
            if (!dead) pc.SetCinematic(null, 0f);
            pc.ScriptBusy = false;
        }
        var hands = PlayerHands.Instance;
        if (hands != null)
        {
            hands.LockRight(Vector3.zero, Quaternion.identity, 0f);
            hands.HoldForScript(null);
        }
        if (pourStream != null) Destroy(pourStream.gameObject);
        pourStream = null;
        if (pourAnchor != null && !dead) Destroy(pourAnchor.gameObject);
        pourAnchor = null;
    }

    void OnDisable() => EndPour(true);
}
