using UnityEngine;

// 3번 장면 '방공호로': 아파트 로비에서 달려 나와 양손으로 유리 쌍여닫이문을 밀어젖히고(경첩 문짝이 휙 돌아 스토퍼에 튕긴다),
// 계단을 내려 거리로, 방공호 터널까지 한 장면으로 잇는다. 카메라는 길가에서 문을 보다가 뛰쳐나온 사람을 따라 돌아
// 옆에서 따라가고, 끝에 터널 안에서 맞는다.
// 소리(모두 ProceduralAudio로 합성, 3D): 민방위 공습경보 사이렌 셋, 멀리 무너지는 건물(땅울림·먼지 기둥·가라앉는 탑),
// 2차 폭발(섬광·먹먹한 쿵), 머리 위를 지나가는 군 헬기(로터·도플러·땅을 훑는 탐조등)
public partial class CutsceneDirector
{
    [Header("3번 장면: 건물에서 뛰쳐나온다 (GameSceneBuilder가 채운다)")]
    public Transform exitStart, exitDoorL, exitDoorR, exitOut, exitDustSpot, collapseBlock, collapseSpot, farBlastSpot;
    public Light lobbyLight, lobbyRed, farBlastLight;
    public Transform debugCamera;        // 테스트용: 있으면 카메라가 이 자리·방향을 그대로 쓴다
    public static bool LogSounds;        // 테스트용: 소리를 낼 때마다 장면 시각을 콘솔에 적는다

    const float ExitReachDist = 1.3f, ExitContactDist = 0.62f;      // 문까지 남은 거리: 손을 뻗기 시작 / 문에 닿는다
    const float DoorImpulse = 470f, DoorStopAngle = 106f, DoorSpring = 2.2f, DoorDamping = 0.9f, DoorReleaseAngle = 32f;
    const float CollapseAt = 7.4f, FarBlastAt = 6.2f, HeliFrom = 4.6f, HeliTo = 12.5f, TunnelCamAt = 7.0f;
    // 헬기 길 (거리 기준 좌표): 터널 앞 광장 위(z 16쯤)를 t 8.9초에 지난다 → 터널 안에서 내다볼 때 탐조등이 길을 훑는다
    static readonly Vector3 HeliStart = new Vector3(110f, 52f, 104f), HeliEnd = new Vector3(-90f, 60f, -56f);
    // 문짝에서 손이 미는 자리 (경첩 피벗 기준: +x 오른쪽, +z 바깥, 안쪽 면의 밀대 근처)
    static readonly Vector3 PushL = new Vector3(0.86f, 1.07f, -0.06f), PushR = new Vector3(-0.86f, 1.07f, -0.06f);

    readonly float[] doorAngle = new float[2], doorVel = new float[2];
    bool doorPushed, doorReleased, doorCrossed, collapsed, farBlasted;
    float pushWeight, reachAt = -1f, crossedAt = -1f, collapseT = -1f, farBlastT = -1f, audioFade = 1f;
    Vector3[] runPath;
    Vector3 collapseHome, doorForward, heliPrev, releaseLocalL, releaseLocalR;   // 손을 뗀 자리 (사람 기준)
    Quaternion releaseRotL, releaseRotR;
    float contactAt = -1f;
    Transform heliPool, heliBeam;        // 탐조등 웅덩이(땅 위 둥근 빛)와 빛기둥
    Quaternion collapseHomeRot;
    Transform heli;
    AudioSource heliAudio, siren3;
    Light heliSpot;
    ParticleSystem lobbyDust, doorDust, collapsePlume;

    void BuildExitFx()
    {
        if (exitDoorL != null) doorForward = exitDoorL.forward;
        if (exitDustSpot != null && dustParticle != null)
        {
            // 로비 천장에서 떨어지는 회반죽 가루
            lobbyDust = MakeParticles("LobbyDust", dustParticle, 500);
            lobbyDust.transform.SetParent(exitDustSpot, false);
            lobbyDust.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var m = lobbyDust.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.25f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.05f);
            m.gravityModifier = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            m.startColor = new Color(0.8f, 0.78f, 0.72f, 0.5f);
            var sh = lobbyDust.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(6f, 7f, 0.05f);
            var em = lobbyDust.emission;
            em.rateOverTime = 150f;
            SoftFade(lobbyDust, 0.05f, 0.6f);
        }
        if (exitDoorL != null && exitDoorR != null && dustParticle != null)
        {
            // 문이 쾅 닫힐 때 문틀 위에서 떨어지는 먼지
            doorDust = MakeParticles("DoorDust", dustParticle, 120);
            doorDust.transform.position = DoorCenter() + Vector3.up * 2.75f + doorForward * 0.1f;
            doorDust.transform.rotation = Quaternion.Euler(90, 0, 0);
            var m = doorDust.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.2f);
            m.gravityModifier = 0.4f;
            m.startColor = new Color(0.75f, 0.7f, 0.62f, 0.6f);
            var sh = doorDust.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(2.6f, 0.3f, 0.1f);
            SoftFade(doorDust, 0.05f, 0.5f);
        }
        if (collapseSpot != null && smokeParticle != null)
        {
            // 무너지는 건물의 먼지 기둥
            collapsePlume = MakeParticles("CollapsePlume", smokeParticle, 300);
            collapsePlume.transform.position = collapseSpot.position;
            collapsePlume.transform.rotation = Quaternion.Euler(-90, 0, 0);
            var m = collapsePlume.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(9f, 16f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 11f);
            m.startSize = new ParticleSystem.MinMaxCurve(9f, 18f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.42f, 0.35f, 0.3f, 0.6f), new Color(0.28f, 0.22f, 0.19f, 0.55f));
            m.gravityModifier = -0.01f;
            var sh = collapsePlume.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 28f;
            sh.radius = 6f;
            var lim = collapsePlume.limitVelocityOverLifetime;
            lim.enabled = true;
            lim.dampen = 0.3f;
            var sz = collapsePlume.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2.2f));
            var rot = collapsePlume.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            SoftFade(collapsePlume, 0.08f, 0.75f);
            collapsePlume.GetComponent<ParticleSystemRenderer>().maxParticleSize = 3f;
        }
        if (collapseBlock != null)
        {
            collapseHome = collapseBlock.position;
            collapseHomeRot = collapseBlock.rotation;
        }
        // 헬기: 소리(수동 도플러) + 아래를 훑는 탐조등. 저음 필터는 AudioSource 뒤에 붙인다
        heli = new GameObject("Helicopter").transform;
        heli.SetParent(transform, false);
        heliAudio = heli.gameObject.AddComponent<AudioSource>();
        AudioCategory.Tag(heliAudio, SoundCategory.Ambient);
        heliAudio.playOnAwake = false;
        heliAudio.clip = ProceduralAudio.Helicopter();
        heliAudio.loop = true;
        heliAudio.spatialBlend = 1f;
        heliAudio.rolloffMode = AudioRolloffMode.Linear;
        heliAudio.minDistance = 10f;
        heliAudio.maxDistance = 420f;
        heliAudio.dopplerLevel = 0f;
        heliAudio.volume = 0f;
        var lp = heli.gameObject.AddComponent<AudioLowPassFilter>();
        lp.cutoffFrequency = 5000f;
        var spotGo = new GameObject("Searchlight");
        spotGo.transform.SetParent(heli, false);
        heliSpot = spotGo.AddComponent<Light>();
        heliSpot.type = LightType.Spot;
        heliSpot.spotAngle = 11f;
        heliSpot.innerSpotAngle = 5f;
        heliSpot.range = 170f;
        heliSpot.color = new Color(1f, 0.96f, 0.88f);
        heliSpot.intensity = 0f;
        heliSpot.shadows = LightShadows.None;
        // 땅 위의 빛 웅덩이와 빛기둥: 멀리서 비추는 스포트라이트만으로는 땅이 거의 밝아지지 않는다.
        // 충격파 구의 무광 가산 재질을 쓴다 (입자 재질은 땅에 붙은 면을 소프트 파티클로 지워 버린다)
        if (shockRing != null)
        {
            var beamMat = new Material(shockRing);
            beamMat.SetColor("_BaseColor", new Color(1f, 0.95f, 0.82f, 0.055f));
            beamMat.SetFloat("_Cull", 2);   // 앞면만: 양면이면 카메라 가까이서 두 겹이 더해져 화면이 뿌옇게 씻긴다
            var poolMat = new Material(shockRing);
            poolMat.SetColor("_BaseColor", new Color(1f, 0.96f, 0.85f, 0.55f));
            if (emberParticle != null) poolMat.SetTexture("_BaseMap", emberParticle.GetTexture("_BaseMap"));
            var pool = GameObject.CreatePrimitive(PrimitiveType.Quad);
            pool.name = "SearchlightPool";
            Destroy(pool.GetComponent<Collider>());
            pool.transform.SetParent(transform, false);
            pool.transform.rotation = Quaternion.Euler(90, 0, 0);
            var pr = pool.GetComponent<MeshRenderer>();
            pr.sharedMaterial = poolMat;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            heliPool = pool.transform;
            var beam = new GameObject("SearchlightBeam");
            beam.transform.SetParent(transform, false);
            beam.AddComponent<MeshFilter>().sharedMesh = ProcMesh.Tube("cs_helibeam", 2, 14, 0f, 0.7f, 0.7f, 0f, -1f, 5.5f, 5.5f, 0f);
            var br = beam.AddComponent<MeshRenderer>();
            br.sharedMaterial = beamMat;
            br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            heliBeam = beam.transform;
            heliPool.gameObject.SetActive(false);
            heliBeam.gameObject.SetActive(false);
        }
        heli.gameObject.SetActive(false);
    }

    Vector3 DoorCenter() => exitDoorL != null && exitDoorR != null ? (exitDoorL.position + exitDoorR.position) * 0.5f : runDoor.position;

    // 3번 장면 시작: 로비 안에서 문을 향해 선다. 문은 닫혀 있고, 사이렌·숨소리·천장 가루가 시작된다
    void BeginExit()
    {
        var doorC = DoorCenter();
        runPath = new[]
        {
            doorC + doorForward * 0.9f,
            exitOut != null ? exitOut.position : runDoor.position,
            runDoor.position,
            runInside.position,
        };
        var start = exitStart != null ? exitStart.position : runStart.position;
        var look = runPath[0] - start;
        look.y = 0;
        PlaceActor(start, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg);
        actor.Set(CutsceneActor.Mode.Run, 0.3f);
        actor.RunAmount = 0;
        runLeg = 0;
        for (int i = 0; i < 2; i++) { doorAngle[i] = 0; doorVel[i] = 0; }
        doorPushed = doorReleased = doorCrossed = collapsed = farBlasted = false;
        pushWeight = 0;
        reachAt = crossedAt = collapseT = farBlastT = contactAt = -1f;
        ApplyDoors();
        cloudProgress = 0.62f;
        cloud.transform.position = cloudStreet.position;
        cloud.Size = 240f;
        cloud.SetProgress(cloudProgress, 0);
        if (lobbyDust != null) lobbyDust.Play();
        if (!Playing) return;
        if (breath != null) { breath.volume = 0.3f; breath.Play(); }
        if (siren1 != null && !siren1.isPlaying)
        {
            siren1.Play();
            siren2.Play();
            siren2.time = 2.3f;
            if (siren3 != null) { siren3.Play(); siren3.time = 3.7f; }
            Sfx("민방위 공습경보 사이렌 셋 시작");
        }
        if (exitDustSpot != null)
        {
            ProceduralAudio.PlayAt(ProceduralAudio.DebrisFall(), exitDustSpot.position, 0.6f, 0.9f, 1f, 30f);
            Sfx("로비 천장에서 가루가 떨어진다");
        }
    }

    // 장면이 바뀔 때 3번 장면의 것들을 되돌린다
    void ResetExit(int shot)
    {
        if (shot != 3)
        {
            if (lobbyDust != null) lobbyDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (doorDust != null) doorDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (collapsePlume != null) collapsePlume.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (collapseBlock != null) collapseBlock.SetPositionAndRotation(collapseHome, collapseHomeRot);
        }
        if (heli != null)
        {
            if (heliAudio != null) heliAudio.Stop();
            heli.gameObject.SetActive(false);
        }
        if (heliPool != null) heliPool.gameObject.SetActive(false);
        if (heliBeam != null) heliBeam.gameObject.SetActive(false);
        if (farBlastLight != null) farBlastLight.intensity = 0;
    }

    // ---------- 3: 로비 → 문 → 계단 → 거리 → 터널 ----------

    float UpdateShot3(float dt)
    {
        float t = ShotTime;
        float moved = 0;
        var pos = actor.transform.position;
        var doorC = DoorCenter();
        float doorDist = Vector3.Dot(doorC - pos, doorForward);   // 문까지 남은 거리 (지나면 음수)

        // 달리기: 처음 0.5초에 속도가 붙고, 길목마다 몸을 돌린다
        float speed = RunSpeed * Window(t, 0f, 0.5f);
        actor.RunAmount = Mathf.Clamp01(speed / RunSpeed);
        if (speed > 0 && runPath != null && runLeg < runPath.Length)
        {
            var target = runPath[runLeg];
            var to = target - pos;
            to.y = 0;
            float step = speed * dt;
            if (to.magnitude <= step) { pos += to; runLeg++; }
            else pos += to.normalized * step;
            moved = step;
            if (to.sqrMagnitude > 0.001f)
            {
                var face = Quaternion.LookRotation(to.normalized);
                actor.transform.rotation = Quaternion.Slerp(actor.transform.rotation, face, dt * 6f);
                actorYaw = actor.transform.eulerAngles.y;
            }
        }
        // 땅 높이를 따라간다 (로비 바닥 → 계단 → 인도 → 길 → 터널 바닥)
        if (Physics.Raycast(pos + Vector3.up * 1.6f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
            pos.y = Mathf.Lerp(pos.y, hit.point.y, Mathf.Clamp01(dt * 14f));
        actor.transform.position = pos;

        // 문 밀기: 다가가며 양손을 뻗고, 닿으면 문짝에 힘을 주고, 문이 열리면 손을 거둔다
        if (!doorReleased)
        {
            if (reachAt < 0 && doorDist < ExitReachDist) reachAt = t;
            if (reachAt >= 0) pushWeight = Mathf.Min(1f, pushWeight + dt / 0.22f);
            if (!doorPushed && doorDist < ExitContactDist)
            {
                doorPushed = true;
                contactAt = t;
                doorVel[0] = doorVel[1] = DoorImpulse;
                shake = Mathf.Max(shake, 0.12f);
                ProceduralAudio.PlayAt(ProceduralAudio.Swoosh(), doorC + Vector3.up * 1.2f, 0.5f, 0.7f, 1f, 30f);
                ProceduralAudio.PlayAt(ProceduralAudio.Swoosh(), doorC + Vector3.up * 1.2f, 0.45f, 0.62f, 1f, 30f);
                Sfx("양손으로 문을 민다 (문짝이 휙 열린다)");
            }
            if (doorPushed && (doorAngle[0] > DoorReleaseAngle || t - contactAt > 0.09f || doorDist < 0.08f))
            {
                // 손을 뗀다: 그 순간의 손 자리를 사람 기준으로 기억해 두고, 거기서 달리기 팔 동작으로 돌아온다 (문짝을 따라 더 벌어지지 않게)
                doorReleased = true;
                if (exitDoorL != null && exitDoorR != null)
                {
                    releaseLocalL = actor.transform.InverseTransformPoint(exitDoorL.TransformPoint(PushL));
                    releaseLocalR = actor.transform.InverseTransformPoint(exitDoorR.TransformPoint(PushR));
                    releaseRotL = DoorHandRotation(true, exitDoorL);
                    releaseRotR = DoorHandRotation(false, exitDoorR);
                }
            }
        }
        else pushWeight = Mathf.Max(0f, pushWeight - dt / 0.2f);
        if (pushWeight > 0 && exitDoorL != null && exitDoorR != null)
        {
            if (!doorReleased)
            {
                LockHandOnDoor(true, exitDoorL, PushL);
                LockHandOnDoor(false, exitDoorR, PushR);
            }
            else
            {
                actor.LockHand(true, actor.transform.TransformPoint(releaseLocalL), releaseRotL, null, pushWeight);
                actor.LockHand(false, actor.transform.TransformPoint(releaseLocalR), releaseRotR, null, pushWeight);
            }
        }
        else actor.ReleaseHands();
        actor.RunLean = 14f + 12f * pushWeight;
        if (!doorCrossed && doorDist < 0) { doorCrossed = true; crossedAt = t; }

        // 멀리 무너지는 건물, 2차 폭발
        if (!collapsed && t >= CollapseAt)
        {
            collapsed = true;
            collapseT = 0;
            if (collapsePlume != null) { collapsePlume.Play(); collapsePlume.Emit(90); }
            var at = collapseSpot != null ? collapseSpot.position + Vector3.up * 8f : cam.transform.position;
            ProceduralAudio.PlayAt(ProceduralAudio.BuildingCollapse(), at, 1f, 1f, 1f, 380f);
            Sfx("멀리서 건물이 무너진다 (땅울림·먼지 기둥)");
        }
        if (!farBlasted && t >= FarBlastAt)
        {
            farBlasted = true;
            farBlastT = 0;
            if (farBlastSpot != null) ProceduralAudio.PlayAt(ProceduralAudio.FarExplosion(), farBlastSpot.position, 0.9f, 1f, 1f, 300f);
            shake = Mathf.Max(shake, 0.25f);
            Sfx("건물 뒤 2차 폭발 (섬광)");
        }
        tremor = collapseT >= 0 ? 0.07f * Mathf.Clamp01(1 - collapseT / 3.6f) : 0f;

        cloudProgress = Mathf.Min(1f, cloudProgress + dt * 0.012f);
        cloud.SetProgress(cloudProgress, dt);
        SetLightsForShot(3, t, false);
        // 검은 화면에서 열리고, 끝에 다시 어두워진다
        Dark = Mathf.Max(1f - t / 0.6f, Window(t, ShotLength[3] - 1.4f, ShotLength[3] - 0.2f));
        if (breath != null) breath.volume = Mathf.Lerp(0.3f, 0.45f, Window(t, 0f, 6f)) * (1 - Dark);

        var head = actor.Rig.Head.position;
        var fwd = actor.transform.forward;
        var right = actor.transform.right;
        if (t < TunnelCamAt)
        {
            // 길가에서 문을 본다 → 문이 터져 열리면 시선이 사람에게 → 지나가면 길 쪽(사람의 오른쪽)에서 따라가는 자리로 돌아 들어간다
            // (왼쪽 건물 쪽으로 가면 전환 경로가 달리는 사람을 가로지른다)
            var doorR = Vector3.Cross(Vector3.up, doorForward);
            var entrance = doorC + doorForward * 3.9f + doorR * 1.4f + Vector3.up * 0.75f;
            var track = actor.transform.position + right * 2.3f + fwd * 1.1f + Vector3.up * 1.2f;
            float k = crossedAt < 0 ? 0 : Window(t, crossedAt, crossedAt + 1.3f);
            var want = Vector3.Lerp(entrance, track, k);
            camPos = t < 0.05f ? want : Vector3.Lerp(camPos, want, dt * (4.5f + 3f * k));
            float lookK = reachAt < 0 ? 0 : Window(t, reachAt, reachAt + 0.7f);
            var doorLook = doorC + Vector3.up * 0.85f;
            camLook = Vector3.Lerp(doorLook, head + fwd * 0.2f - Vector3.up * 0.25f, lookK);
            camFov = Mathf.Lerp(42f, 46f, k);
            focusDist = Vector3.Distance(camPos, lookK < 0.5f ? doorLook : head);
            aperture = 4f;
            handheld = 0.7f + 0.35f * k;
        }
        else
        {
            // 터널 안에서 입구를 내다본다: 뛰어 들어오는 사람이 카메라 옆을 지나 어둠 속으로
            float u = (t - TunnelCamAt) / Mathf.Max(0.1f, ShotLength[3] - TunnelCamAt);
            camPos = Vector3.Lerp(street.TransformPoint(new Vector3(1.0f, 1.35f, -2.8f)), street.TransformPoint(new Vector3(0.9f, 1.3f, -2.5f)), Smooth(u));
            var far = street.TransformPoint(new Vector3(0.6f, 1.3f, 14f));
            float near = Mathf.Clamp01(1 - Vector3.Distance(camPos, head) / 12f);
            camLook = Vector3.Lerp(far, head, Smooth(near));
            camFov = 52f;
            focusDist = Vector3.Distance(camPos, head);
            aperture = 5.6f;
            handheld = 0.55f;
        }
        return moved;
    }

    // 손바닥을 문짝 안쪽 면의 밀대 자리에 붙인다 (문이 돌면 손도 따라간다). 손가락은 위로, 팔꿈치는 바깥 아래로
    Quaternion DoorHandRotation(bool left, Transform pivot)
    {
        var fingers = (Vector3.up * 0.9f + pivot.right * (left ? -0.25f : 0.25f)).normalized;
        return CutsceneActor.HandRotation(left, pivot.forward, fingers);
    }

    void LockHandOnDoor(bool left, Transform pivot, Vector3 local)
    {
        var palm = pivot.TransformPoint(local);
        var rot = DoorHandRotation(left, pivot);
        var shoulder = (left ? actor.Rig.ArmL : actor.Rig.ArmR).position;
        var pole = shoulder + actor.transform.right * (left ? -0.45f : 0.45f) + Vector3.down * 0.3f + actor.transform.forward * 0.15f;
        actor.LockHand(left, palm, rot, pole, pushWeight);
    }

    void ApplyDoors()
    {
        if (exitDoorL != null) exitDoorL.localRotation = Quaternion.Euler(0, -90f - doorAngle[0], 0);
        if (exitDoorR != null) exitDoorR.localRotation = Quaternion.Euler(0, -90f + doorAngle[1], 0);
    }

    // 매 프레임 (3번 장면): 문짝 물리, 로비 불빛, 무너지는 탑, 2차 폭발 빛, 헬기
    void UpdateExit(float dt)
    {
        if (Shot != 3) return;
        float t = ShotTime;
        // 문짝: 도어 클로저(스프링)와 감쇠, 활짝 열리면 스토퍼에 쾅 튕기고 닫힐 때 틀에 딸깍 부딪힌다
        int sub = Mathf.Clamp(Mathf.CeilToInt(dt / 0.008f), 1, 12);
        float h = dt / sub;
        for (int i = 0; i < 2; i++)
        {
            float th = doorAngle[i], om = doorVel[i];
            for (int k = 0; k < sub; k++)
            {
                om += (-DoorSpring * th - DoorDamping * om) * h;
                th += om * h;
                if (th > DoorStopAngle) { th = DoorStopAngle; if (om > 60f) DoorBang(i, om); om = -om * 0.32f; }
                if (th < 0) { th = 0; if (om < -40f && doorPushed) DoorBang(i, -om * 0.6f); om = -om * 0.2f; }
            }
            doorAngle[i] = th;
            doorVel[i] = om;
        }
        ApplyDoors();
        // 로비 형광등은 떨리고, 비상등은 숨 쉬듯 깜빡인다
        if (lobbyLight != null) lobbyLight.intensity = 0.1f + 1.4f * (Mathf.PerlinNoise(t * 9f, 0.2f) > 0.32f ? 1f : 0.15f);
        if (lobbyRed != null) lobbyRed.intensity = 0.8f + 0.5f * Mathf.Sin(t * 5f);
        // 무너지는 탑: 3.6초에 걸쳐 가라앉으며 기운다
        if (collapseT >= 0 && collapseBlock != null)
        {
            collapseT += dt;
            float s = Mathf.Clamp01(collapseT / 3.6f);
            s *= s;
            collapseBlock.position = collapseHome + Vector3.down * (30f * s);
            collapseBlock.rotation = collapseHomeRot * Quaternion.Euler(0, 0, 14f * s);
        }
        // 2차 폭발 빛: 번쩍 뒤 천천히 식는다
        if (farBlastT >= 0 && farBlastLight != null)
        {
            farBlastT += dt;
            farBlastLight.intensity = 60f * Mathf.Exp(-farBlastT * 2.2f) + 10f * Mathf.Exp(-farBlastT * 0.6f);
            if (farBlastT > 6f) { farBlastLight.intensity = 0; farBlastT = -1; }
        }
        // 헬기: 하늘을 가로지르며(듣는 쪽으로 다가오는 속도로 음높이를 바꾼다) 탐조등으로 땅을 훑는다
        if (heli != null)
        {
            bool on = t >= HeliFrom && t <= HeliTo;
            if (on)
            {
                var path = street.TransformDirection(HeliEnd - HeliStart).normalized;
                if (!heli.gameObject.activeSelf)
                {
                    heli.gameObject.SetActive(true);
                    heliPrev = street.TransformPoint(HeliStart);
                    heli.position = heliPrev;
                    if (heliAudio != null) heliAudio.Play();
                    Sfx("군 헬기가 다가온다 (로터·탐조등)");
                }
                float u = (t - HeliFrom) / (HeliTo - HeliFrom);
                var p = street.TransformPoint(Vector3.Lerp(HeliStart, HeliEnd, u));
                var vel = dt > 0 ? (p - heliPrev) / dt : Vector3.zero;
                heliPrev = p;
                heli.position = p;
                var toListener = cam.transform.position - p;
                float dist = Mathf.Max(0.1f, toListener.magnitude);
                float vr = Vector3.Dot(vel, toListener / dist);
                if (heliAudio != null)
                {
                    heliAudio.pitch = Mathf.Clamp(343f / Mathf.Max(200f, 343f - vr), 0.75f, 1.35f);
                    heliAudio.volume = 0.95f * audioFade;
                }
                if (LogSounds && Mathf.Abs(u - 0.55f) < dt / (HeliTo - HeliFrom) * 0.5f) Sfx($"군 헬기 머리 위 통과 (거리 {dist:0}m, 도플러 피치 {heliAudio.pitch:0.00})");
                // 거의 바로 아래를 비추며 좌우로 훑는다 (앞을 많이 비추면 웅덩이가 터널 앞을 벗어난다)
                var dir = (Vector3.down + path * 0.06f + Vector3.Cross(Vector3.up, path) * (0.22f * Mathf.Sin(t * 1.7f))).normalized;
                heli.rotation = Quaternion.LookRotation(dir);
                heliSpot.intensity = 1400f;
                // 빛이 닿는 땅에 웅덩이를 놓고, 헬기에서 거기까지 빛기둥을 늘인다
                if (heliPool != null && heliBeam != null)
                {
                    if (Physics.Raycast(p, dir, out var ground, 300f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        heliPool.gameObject.SetActive(true);
                        heliBeam.gameObject.SetActive(true);
                        heliPool.position = ground.point + Vector3.up * 0.06f;
                        float spread = ground.distance * 0.19f;
                        heliPool.localScale = new Vector3(spread, spread, 1f);
                        heliBeam.position = p;
                        heliBeam.rotation = Quaternion.FromToRotation(Vector3.down, dir);
                        heliBeam.localScale = new Vector3(spread / 5.5f, ground.distance, spread / 5.5f);
                    }
                    else
                    {
                        heliPool.gameObject.SetActive(false);
                        heliBeam.gameObject.SetActive(false);
                    }
                }
            }
            else if (heli.gameObject.activeSelf)
            {
                heli.gameObject.SetActive(false);
                if (heliAudio != null) heliAudio.Stop();
                if (heliPool != null) heliPool.gameObject.SetActive(false);
                if (heliBeam != null) heliBeam.gameObject.SetActive(false);
                if (t > HeliTo) Sfx("군 헬기 멀어짐");
            }
        }
    }

    // 문짝이 스토퍼(열림)나 틀(닫힘)에 부딪힌다: 쾅 소리, 문틀 먼지, 화면 흔들림
    void DoorBang(int i, float om)
    {
        var pivot = i == 0 ? exitDoorL : exitDoorR;
        if (pivot == null) return;
        var edge = pivot.TransformPoint(new Vector3(i == 0 ? 1.1f : -1.1f, 1.2f, 0));
        float v = Mathf.Clamp01(om / 420f);
        ProceduralAudio.PlayAt(ProceduralAudio.DoorBang(), edge, 0.5f + 0.45f * v, Random.Range(0.94f, 1.06f), 1f, 70f);
        if (doorDust != null) { doorDust.Play(); doorDust.Emit(Mathf.RoundToInt(12 + 24 * v)); }
        shake = Mathf.Max(shake, 0.08f + 0.14f * v);
        Sfx($"{(i == 0 ? "왼쪽" : "오른쪽")} 문짝 쾅 (각속도 {om:0}도/초, 각도 {doorAngle[i]:0}도)");
    }

    void Sfx(string what)
    {
        if (LogSounds) Debug.Log($"[CutsceneSfx] 장면 {Shot + 1} t={ShotTime:0.00}s {what}");
    }
}
