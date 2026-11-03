using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 인트로 컷씬 연출기 (Cutscene 씬). 네 장면을 시간에 따라 실시간으로 연출한다:
//  0 평범한 오후(소파에서 TV) → 1 TV 긴급 속보(굳어 선다) → 2 핵폭발(섬광·버섯구름·충격파에 창이 터진다) → 3 방공호로 달린다.
// 카메라(돌리·팬·핸드헬드·흔들림), 조명, 하늘, 후처리, 소리, 사람(CutsceneActor), 버섯구름(MushroomCloud)과
// 효과(유리·커튼·먼지·재: CutsceneDirector.Fx.cs)를 모두 여기서 움직인다.
// 자막·버튼·섬광 막은 UIManager.Cutscene이 이 연출기의 Shot/ShotTime/Flash/Dark를 읽어 그린다.
// 장면은 시간이 다 되면 저절로 넘어가고, Advance()로 바로 넘길 수 있다. 씬의 물체 참조는 GameSceneBuilder(컷씬 씬 생성)가 채운다
public partial class CutsceneDirector : MonoBehaviour
{
    public static CutsceneDirector Instance { get; private set; }

    public static readonly float[] ShotLength = { 10f, 11.5f, 13f, 11.5f };
    // 1번 장면: 지지직 → 속보 → 일어선다 → 굳는다. 2번 장면: 섬광 → 충격파
    const float NewsAt = 0.9f, RiseAt = 2.4f, FreezeAt = 4.3f;
    const float FlashAt = 0.5f, ShockAt = 3.4f, ShockSpeed = 133f;
    const float RunSpeed = 3.7f, RunDelay = 0.5f;
    // 절차적 하늘(Skybox/Procedural)의 SkyTint는 보색으로 작동한다: 청록을 주면 빨간 하늘, 회색(0.5)이 맑은 파란 하늘
    static readonly Color ClearSkyTint = new Color(0.5f, 0.55f, 0.62f);
    static readonly Color BlastSkyTint = new Color(0.4f, 0.8f, 1f);     // 주황빛 붉은 하늘
    static readonly Color StreetSkyTint = new Color(0.15f, 0.9f, 1f);   // 짙은 핏빛 하늘

    [Header("씬 참조 (GameSceneBuilder가 채운다)")]
    public Camera cam;
    public Volume volume;
    public Light sun, flashLight, tvLight, windowLight, roomFill, beaconLight;
    public Light[] distantLights, fireLights;
    public Transform home, street, sofaSpot, standSpot, windowCenter, tvScreen, ceilingLamp, curtainRod, remote, beaconHead, runStart, runDoor, runInside, cloudHome, cloudStreet;
    public Transform[] looseProps;   // 충격파에 떨어지는 물건들 (액자·컵·과일·책)
    public Renderer tvScreenRenderer;
    public Renderer[] windowPanes;
    public Material tvShow, tvNews, tvStatic, glowTemplate, glassShard, shockRing, skyMaterial, smokeParticle, ashParticle, emberParticle, dustParticle;

    public int Shot { get; private set; } = -1;
    public float ShotTime { get; private set; }
    public float Flash { get; private set; }   // 화면을 덮는 흰빛 (0~1)
    public float Dark { get; private set; }    // 화면을 덮는 검은 막 (0~1)
    public bool Playing { get; private set; }
    public bool Finished { get; private set; }
    public event Action OnFinished;

    CutsceneActor actor;
    MushroomCloud cloud;
    Material tvCur, sky;
    VolumeProfile profile;
    ColorAdjustments grade;
    Bloom bloom;
    Vignette vignette;
    FilmGrain grain;
    DepthOfField dof;
    ChromaticAberration chroma;
    MotionBlur motionBlur;
    float baseExposure;
    AudioSource ambience, tv, boom, breath, heart, sfx, siren1, siren2;
    float shake, tremor, tickTimer, lastStepTime, cloudProgress;
    bool flashed, shocked, risen, stood, frozen, newsOn, tvOff, shielded, stunned, runnerInside;
    float actorYaw, actorYawFrom, actorYawTo, actorTurnT = 1, actorTurnTime = 1;
    Vector3 actorFrom, actorTo; float actorMoveT = 1, actorMoveTime = 1;
    int runLeg;
    Vector3 camPos, camLook;
    float camFov = 45f, focusDist = 3f, aperture = 5.6f, handheld = 0.3f;
    // 색보정 목표 (장면마다 바뀌고, 매 프레임 부드럽게 따라간다)
    Color filterTarget = Color.white, filterCur = Color.white;
    float expTarget, expCur, conTarget, conCur, satTarget, satCur, vigTarget = 0.3f, vigCur = 0.3f, grainTarget = 0.25f, grainCur = 0.25f, bloomTarget = 0.8f, bloomCur = 0.8f, chromaTarget = 0.08f, chromaCur = 0.08f;
    Color skyTintCur, skyTintTarget, skyGroundCur, skyGroundTarget;
    float skyThickCur = 1f, skyThickTarget = 1f, skyExpCur = 1.3f, skyExpTarget = 1.3f;
    Color ambSkyCur, ambSkyTarget, ambEqCur, ambEqTarget, ambGroundCur, ambGroundTarget;
    Color fogCur, fogTarget; float fogDensCur, fogDensTarget;

    void Awake()
    {
        Instance = this;
        // 사람
        var actorGo = new GameObject("Actor");
        actorGo.transform.SetParent(transform, false);
        actor = actorGo.AddComponent<CutsceneActor>();
        actor.Build(glowTemplate);
        actor.OnFootstep = OnActorFootstep;
        // 버섯구름 (2·3번 장면)
        cloud = MushroomCloud.Create(transform, glowTemplate, smokeParticle, 170f);
        cloud.gameObject.SetActive(false);
        // 하늘·후처리는 실행 중 복사본을 고친다 (에셋은 그대로)
        if (skyMaterial != null)
        {
            sky = new Material(skyMaterial);
            RenderSettings.skybox = sky;
        }
        if (volume != null)
        {
            profile = volume.profile;
            profile.TryGet(out grade);
            profile.TryGet(out bloom);
            profile.TryGet(out vignette);
            profile.TryGet(out grain);
            profile.TryGet(out dof);
            profile.TryGet(out chroma);
            profile.TryGet(out motionBlur);
            if (grade != null) baseExposure = grade.postExposure.value;
            if (dof != null) { dof.active = true; dof.mode.overrideState = true; dof.mode.value = DepthOfFieldMode.Bokeh; dof.focusDistance.overrideState = dof.aperture.overrideState = dof.focalLength.overrideState = true; dof.focalLength.value = 50f; }
        }
        tvCur = new Material(tvShow);
        if (tvScreenRenderer != null) tvScreenRenderer.sharedMaterial = tvCur;
        BuildAudio();
        BuildFx();
        BeginShot(0);
        ApplyLook(true);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // UIManager.PlayCutscene이 부른다: 시간이 가기 시작한다
    public void Begin()
    {
        if (Playing) return;
        Playing = true;
        if (ambience != null) ambience.Play();
        if (Shot == 0 && tv != null) { tv.clip = ProceduralAudio.TvMurmur(); tv.loop = true; tv.Play(); }
    }

    // 다음 장면으로 (마지막 장면이면 끝낸다)
    public void Advance()
    {
        if (!Playing || Finished) return;
        if (Shot + 1 < ShotLength.Length) BeginShot(Shot + 1);
        else Finish();
    }

    public void Skip() => Finish();

    void Finish()
    {
        if (Finished) return;
        Finished = true;
        var done = OnFinished;
        OnFinished = null;
        done?.Invoke();
    }

    // ================= 장면 시작: 그 장면의 첫 상태를 통째로 맞춘다 (건너뛰어 와도 어긋나지 않게) =================

    void BeginShot(int shot)
    {
        Shot = shot;
        ShotTime = 0;
        shake = tremor = 0;
        Flash = 0;
        flashed = shocked = risen = stood = frozen = newsOn = tvOff = shielded = stunned = runnerInside = false;
        bool atHome = shot < 3;
        if (home != null) home.gameObject.SetActive(atHome);
        if (street != null) street.gameObject.SetActive(!atHome);
        cloud.gameObject.SetActive(shot >= 2);
        ResetFx(shot);
        actor.Tremble = 0;
        actor.RunAmount = 0;
        actor.SetBackpack(shot == 3);
        if (tv != null) tv.Stop();
        if (heart != null) heart.Stop();
        if (breath != null) breath.Stop();

        if (shot <= 1)
        {
            PlaceActor(sofaSpot.position, sofaSpot.eulerAngles.y);
            actor.Set(CutsceneActor.Mode.Sit, 0.01f);
            HoldRemote();
            SetTv(shot == 1 ? tvStatic : tvShow, 1f);
            if (Playing && tv != null) { tv.clip = ProceduralAudio.TvMurmur(); tv.loop = true; tv.Play(); }
            if (shot == 1 && Playing) sfx.PlayOneShot(ProceduralAudio.Static(), 0.5f);
        }
        else if (shot == 2)
        {
            PlaceActor(standSpot.position, standSpot.eulerAngles.y);
            actor.Set(CutsceneActor.Mode.Freeze, 0.01f);
            actor.Tremble = 0.5f;
            DropRemoteOnFloor();
            SetTv(tvNews, 1.4f);
            cloudProgress = 0;
            cloud.transform.position = cloudHome.position;
            cloud.Size = 170f;
            cloud.SetProgress(0, 0);
            cloud.gameObject.SetActive(false); // 섬광과 함께 켠다
            if (Playing && tv != null) { tv.clip = ProceduralAudio.NewsMurmur(); tv.loop = true; tv.Play(); }
        }
        else
        {
            PlaceActor(runStart.position, Quaternion.LookRotation(runDoor.position - runStart.position).eulerAngles.y);
            actor.Set(CutsceneActor.Mode.Run, 0.3f);
            actor.RunAmount = 0;
            runLeg = 0;
            cloudProgress = 0.62f;
            cloud.transform.position = cloudStreet.position;
            cloud.Size = 240f;
            cloud.SetProgress(cloudProgress, 0);
            if (Playing && breath != null) { breath.volume = 0.3f; breath.Play(); }
            if (siren1 != null && !siren1.isPlaying) { siren1.Play(); siren2.Play(); }
        }
        SetLightsForShot(shot, 0f, true);
        ApplyLook(true);
    }

    void PlaceActor(Vector3 pos, float yaw)
    {
        actor.transform.position = pos;
        actorYaw = actorYawFrom = actorYawTo = yaw;
        actorTurnT = 1;
        actorFrom = actorTo = pos;
        actorMoveT = 1;
        actor.transform.rotation = Quaternion.Euler(0, yaw, 0);
    }

    void TurnActor(float yaw, float seconds)
    {
        actorYawFrom = actorYaw;
        actorYawTo = yaw;
        actorTurnTime = Mathf.Max(0.01f, seconds);
        actorTurnT = 0;
    }

    void MoveActor(Vector3 to, float seconds)
    {
        actorFrom = actor.transform.position;
        actorTo = to;
        actorMoveTime = Mathf.Max(0.01f, seconds);
        actorMoveT = 0;
    }

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3 - 2 * t);
    }

    static float Window(float t, float a, float b) => Smooth((t - a) / Mathf.Max(0.0001f, b - a));

    // ================= 매 프레임 =================

    void Update()
    {
        float dt = Time.deltaTime;
        if (Playing) ShotTime += dt;
        float moved = 0;
        switch (Shot)
        {
            case 0: UpdateShot0(dt); break;
            case 1: UpdateShot1(dt); break;
            case 2: UpdateShot2(dt); break;
            case 3: moved = UpdateShot3(dt); break;
        }
        // 사람의 몸 돌리기·옮기기 (연출에서 예약한 것)
        if (actorTurnT < 1)
        {
            actorTurnT = Mathf.Min(1, actorTurnT + dt / actorTurnTime);
            actorYaw = Mathf.LerpAngle(actorYawFrom, actorYawTo, Smooth(actorTurnT));
            actor.transform.rotation = Quaternion.Euler(0, actorYaw, 0);
        }
        if (actorMoveT < 1)
        {
            actorMoveT = Mathf.Min(1, actorMoveT + dt / actorMoveTime);
            actor.transform.position = Vector3.Lerp(actorFrom, actorTo, Smooth(actorMoveT));
        }
        actor.Tick(dt, moved);
        UpdateFx(dt);
        UpdateLook(dt);
        UpdateAudio(dt);
        if (Playing && !Finished && ShotTime >= ShotLength[Shot]) Advance();
    }

    // ---------- 0: 평범한 오후 ----------

    void UpdateShot0(float dt)
    {
        float t = ShotTime;
        // TV 화면이 프로그램처럼 조금씩 밝기가 바뀐다
        float tvFlicker = 0.92f + 0.08f * Mathf.PerlinNoise(t * 3f, 0.5f);
        tvCur.SetColor("_BaseColor", Color.white * (1.05f * tvFlicker));
        tvLight.intensity = 1.3f * tvFlicker;
        // 4초쯤 리모컨을 누른다 (채널이 바뀌듯 화면이 한 번 어두워진다)
        if (t > 4.2f && t < 4.35f) { tvCur.SetColor("_BaseColor", Color.white * 0.25f); tvLight.intensity = 0.3f; }
        SetLightsForShot(0, t, false);
        Dark = 0;
        if (t < 5.5f)
        {
            // 소파 뒤에서 TV와 창을 향해 천천히 민다 (앉은 사람이 왼쪽 아래에 보인다)
            camPos = Vector3.Lerp(new Vector3(1.6f, 1.5f, -3.1f), new Vector3(1.0f, 1.45f, -2.4f), Smooth(t / 5.5f));
            camLook = Vector3.Lerp(new Vector3(-1.1f, 1.05f, 2.0f), new Vector3(-0.85f, 1.1f, 2.4f), Smooth(t / 5.5f));
            camFov = Mathf.Lerp(46f, 42f, t / 5.5f);
            focusDist = Vector3.Distance(camPos, actor.Rig.Head.position);
            aperture = 8f;
            handheld = 0.25f;
        }
        else
        {
            // 앞 오른쪽에서 TV 빛을 받는 옆얼굴을 가까이 (창이 뒤에 보인다)
            float u = (t - 5.5f) / 4.5f;
            camPos = Vector3.Lerp(new Vector3(0.42f, 1.14f, 0.42f), new Vector3(0.25f, 1.13f, 0.22f), Smooth(u));
            camLook = actor.Rig.Head.position + new Vector3(0, 0.06f, 0);
            camFov = 34f;
            focusDist = Vector3.Distance(camPos, actor.Rig.Head.position);
            aperture = 2.2f;
            handheld = 0.35f;
        }
    }

    // ---------- 1: 긴급 속보 ----------

    void UpdateShot1(float dt)
    {
        float t = ShotTime;
        if (!newsOn && t >= NewsAt)
        {
            newsOn = true;
            SetTv(tvNews, 1.5f);
            sfx.PlayOneShot(ProceduralAudio.AlertTone(), 0.55f);
            if (tv != null) { tv.clip = ProceduralAudio.NewsMurmur(); tv.loop = true; tv.PlayDelayed(3.2f); }
        }
        if (!newsOn)
        {
            // 지지직: 화면이 마구 떨리고 TV 빛이 하얗게 깜빡인다
            tvCur.mainTextureOffset = new Vector2(Mathf.Repeat(t * 37f, 1f), Mathf.Repeat(t * 23f, 1f));
            float f = Mathf.PerlinNoise(t * 40f, 0.3f);
            tvCur.SetColor("_BaseColor", Color.white * (0.6f + f));
            tvLight.color = Color.white;
            tvLight.intensity = 1.5f + 3f * f;
        }
        else
        {
            // 속보: 차가운 푸른 빛이 방을 채우고, 빨간 속보 띠가 1.1초마다 번쩍인다
            float pulse = Mathf.Max(0, Mathf.Sin((t - NewsAt) * Mathf.PI * 2 / 1.1f));
            tvCur.SetColor("_BaseColor", Color.white * (1.3f + 0.15f * pulse));
            tvLight.color = Color.Lerp(new Color(0.7f, 0.82f, 1f), new Color(1f, 0.45f, 0.35f), pulse * 0.35f);
            tvLight.intensity = Mathf.Lerp(tvLight.intensity, 5.5f + pulse * 1.2f, dt * 6);
        }
        if (!risen && t >= RiseAt)
        {
            risen = true;
            actor.Set(CutsceneActor.Mode.Stand, 1.1f);
            DropRemote();
            // 소파에서 일어나 한 걸음 앞으로 나가 TV 앞에 선다
            MoveActor(standSpot.position, 1.6f);
            TurnActor(standSpot.eulerAngles.y, 1.6f);
        }
        if (!frozen && t >= FreezeAt)
        {
            frozen = true;
            actor.Set(CutsceneActor.Mode.Freeze, 0.9f);
            if (heart != null) { heart.volume = 0; heart.Play(); }
        }
        if (frozen) actor.Tremble = Mathf.Min(0.7f, actor.Tremble + dt * 0.25f);
        if (heart != null && heart.isPlaying) heart.volume = Mathf.Min(0.35f, heart.volume + dt * 0.08f);
        SetLightsForShot(1, t, false);
        Dark = 0;
        if (t < 3.0f)
        {
            // TV 전체가 틀째 보이게 (속보 띠까지), 천천히 민다
            float u = t / 3.0f;
            camPos = Vector3.Lerp(new Vector3(-1.62f, 1.3f, 0.4f), new Vector3(-1.6f, 1.3f, 0.85f), Smooth(u));
            camLook = new Vector3(-1.6f, 1.28f, 3.1f);
            camFov = Mathf.Lerp(36f, 33f, u);
            focusDist = Vector3.Distance(camPos, tvScreen.position);
            aperture = 4f;
            handheld = 0.3f;
        }
        else
        {
            float u = (t - 3.0f) / 8.5f;
            camPos = Vector3.Lerp(new Vector3(-2.15f, 1.38f, 2.55f), new Vector3(-1.8f, 1.42f, 2.25f), Smooth(u));
            var head = actor.Rig.Head.position;
            camLook = Vector3.Lerp(head + new Vector3(0, -0.2f, 0), head + new Vector3(0, 0.02f, 0), Smooth(u * 2));
            camFov = Mathf.Lerp(40f, 33f, Smooth(u));
            focusDist = Vector3.Distance(camPos, head);
            aperture = 2.6f;
            handheld = 0.4f + 0.25f * Smooth(u);
        }
    }

    // ---------- 2: 핵폭발 ----------

    void UpdateShot2(float dt)
    {
        float t = ShotTime;
        if (!flashed && t >= FlashAt)
        {
            flashed = true;
            cloud.gameObject.SetActive(true);
            StartShockwave(cloud.transform.position);
            // 전자기파에 TV가 꺼지고, 사람은 창 쪽으로 몸을 돌리며 팔로 눈을 가린다
            SetTv(null, 0f);
            tvOff = true;
            if (tv != null) tv.Stop();
            if (heart != null) heart.Stop();
            var toWindow = windowCenter.position - actor.transform.position;
            TurnActor(Mathf.Atan2(toWindow.x, toWindow.z) * Mathf.Rad2Deg, 0.7f);
            actor.Set(CutsceneActor.Mode.Shield, 0.35f);
            actor.Tremble = 0.2f;
            shielded = true;
            boom.PlayOneShot(ProceduralAudio.Rumble(), 0.9f);
        }
        if (flashed)
        {
            float ft = t - FlashAt;
            Flash = Mathf.Exp(-ft * 1.7f);
            cloudProgress = Mathf.Clamp01(ft / 15f);
            cloud.SetProgress(cloudProgress, dt);
            // 폭발 쪽에서 쏟아지는 빛: 처음엔 하얗게, 곧 주황으로 식는다
            float heat = Mathf.Exp(-ft * 0.35f);
            flashLight.transform.rotation = Quaternion.LookRotation(windowCenter.position - cloud.transform.position - Vector3.up * cloud.Size * 0.5f);
            flashLight.intensity = 14f * Mathf.Exp(-ft * 2.2f) + 2.6f * heat;
            flashLight.color = Color.Lerp(new Color(1f, 0.55f, 0.3f), new Color(1f, 0.95f, 0.85f), Mathf.Exp(-ft * 1.5f));
            windowLight.color = Color.Lerp(new Color(1f, 0.6f, 0.35f), new Color(1f, 0.9f, 0.75f), Mathf.Exp(-ft * 1.5f));
            windowLight.intensity = 6f * Mathf.Exp(-ft * 1.8f) + 2.5f * heat + 0.6f;
            // 땅울림: 충격파가 오기 전부터 조금씩 떨린다
            tremor = ft < 1.6f ? 0 : Mathf.Lerp(0.03f, 0.12f, Window(ft, 1.6f, ShockAt - FlashAt));
        }
        if (!shocked && t >= ShockAt)
        {
            shocked = true;
            Shock();
            shake = 1f;
            // 밀려나며 비틀거린다
            var back = -(windowCenter.position - actor.transform.position).normalized;
            back.y = 0;
            MoveActor(actor.transform.position + back * 0.4f, 0.45f);
            boom.PlayOneShot(ProceduralAudio.Blast(), 1f);
            sfx.PlayOneShot(ProceduralAudio.GlassBreak(), 0.9f);
            sfx.PlayOneShot(ProceduralAudio.DebrisFall(), 0.7f);
        }
        if (shocked)
        {
            float st = t - ShockAt;
            tremor = Mathf.Lerp(0.1f, 0.02f, Window(st, 0f, 7f));
            if (!stunned && st > 1.3f)
            {
                stunned = true;
                actor.Set(CutsceneActor.Mode.Freeze, 1.0f);
            }
            if (stunned) actor.Tremble = Mathf.Min(0.8f, actor.Tremble + dt * 0.3f);
        }
        SetLightsForShot(2, t, false);
        Dark = 0;
        // 어깨 너머로 창을 보는 긴 샷: 아주 천천히 앞으로 민다
        float u = t / ShotLength[2];
        camPos = Vector3.Lerp(new Vector3(-1.78f, 1.52f, -1.0f), new Vector3(-1.45f, 1.5f, -0.55f), Smooth(u));
        camLook = Vector3.Lerp(new Vector3(1.25f, 1.55f, 3.2f), new Vector3(1.45f, 1.7f, 3.2f), Smooth(Window(t, FlashAt, FlashAt + 4f)));
        camFov = 50f;
        focusDist = Vector3.Distance(camPos, windowCenter.position);
        aperture = 11f;
        handheld = 0.3f;
    }

    // ---------- 3: 방공호로 ----------

    float UpdateShot3(float dt)
    {
        float t = ShotTime;
        float moved = 0;
        // 달리기: 출발 → 문 앞 → 터널 안
        float speed = RunSpeed * Window(t, RunDelay, RunDelay + 0.7f);
        actor.RunAmount = Mathf.Clamp01(speed / RunSpeed);
        if (speed > 0)
        {
            var target = runLeg == 0 ? runDoor.position : runInside.position;
            var pos = actor.transform.position;
            var to = target - pos;
            to.y = 0;
            float step = speed * dt;
            if (to.magnitude <= step)
            {
                pos = target;
                if (runLeg == 0) runLeg = 1;
                else runnerInside = true;
            }
            else pos += to.normalized * step;
            moved = step;
            actor.transform.position = pos;
            if (to.sqrMagnitude > 0.001f)
            {
                var face = Quaternion.LookRotation(to.normalized);
                actor.transform.rotation = Quaternion.Slerp(actor.transform.rotation, face, dt * 6f);
                actorYaw = actor.transform.eulerAngles.y;
            }
        }
        cloudProgress = Mathf.Min(1f, cloudProgress + dt * 0.012f);
        cloud.SetProgress(cloudProgress, dt);
        SetLightsForShot(3, t, false);
        // 검은 화면에서 열리고, 끝에 다시 어두워진다
        Dark = Mathf.Max(1f - t / 0.6f, Window(t, ShotLength[3] - 1.4f, ShotLength[3] - 0.2f));
        if (breath != null) breath.volume = Mathf.Lerp(0.3f, 0.45f, Window(t, 0f, 6f)) * (1 - Dark);

        var head = actor.Rig.Head.position;
        var fwd = actor.transform.forward;
        var right = actor.transform.right;
        if (t < 5.8f)
        {
            // 달리는 옆에서 따라가는 카메라 (조금 앞에서 얼굴을 본다)
            var want = actor.transform.position + right * -2.3f + fwd * 1.1f + Vector3.up * 1.2f;
            camPos = t < 0.05f ? want : Vector3.Lerp(camPos, want, dt * 5f);
            camLook = head + fwd * 0.2f - Vector3.up * 0.25f;
            camFov = 46f;
            focusDist = Vector3.Distance(camPos, head);
            aperture = 4f;
            handheld = 1.0f;
        }
        else
        {
            // 터널 안에서 입구를 내다본다: 뛰어 들어오는 사람이 카메라 옆을 지나 어둠 속으로
            float u = (t - 5.8f) / 5.7f;
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

    // ================= 조명·하늘·색보정 =================

    // 장면별 빛·하늘·안개·색보정 목표. immediate면 바로 맞춘다 (장면이 시작될 때)
    void SetLightsForShot(int shot, float t, bool immediate)
    {
        Color filter; float exp, con, sat, vig, grn, blm, chr;
        Color skyTint, ground; float thick, skyExp;
        Color aSky, aEq, aGround, fog; float fogDens;
        switch (shot)
        {
            case 0:
                sun.color = new Color(1f, 0.9f, 0.76f); sun.intensity = 2.4f; sun.transform.rotation = Quaternion.Euler(26, 200, 0);
                windowLight.color = new Color(1f, 0.9f, 0.76f); windowLight.intensity = 2.2f;
                roomFill.color = new Color(1f, 0.94f, 0.86f); roomFill.intensity = 1.1f;
                tvLight.color = new Color(0.85f, 0.9f, 1f);
                flashLight.intensity = 0;
                filter = new Color(1f, 0.97f, 0.9f); exp = 0.15f; con = 8f; sat = 6f; vig = 0.3f; grn = 0.22f; blm = 0.8f; chr = 0.06f;
                skyTint = ClearSkyTint; ground = new Color(0.4f, 0.37f, 0.34f); thick = 0.95f; skyExp = 1.3f;
                aSky = new Color(0.55f, 0.62f, 0.72f); aEq = new Color(0.5f, 0.48f, 0.45f); aGround = new Color(0.3f, 0.27f, 0.24f);
                fog = new Color(0.72f, 0.78f, 0.88f); fogDens = 0.0022f;
                break;
            case 1:
                sun.color = new Color(1f, 0.88f, 0.72f); sun.intensity = 1.5f; sun.transform.rotation = Quaternion.Euler(24, 200, 0);
                windowLight.color = new Color(1f, 0.9f, 0.76f); windowLight.intensity = 1.4f;
                roomFill.color = new Color(0.6f, 0.7f, 0.95f); roomFill.intensity = 0.45f;
                flashLight.intensity = 0;
                filter = new Color(0.92f, 0.96f, 1.05f); exp = -0.1f; con = 18f; sat = -12f; vig = 0.4f; grn = 0.3f; blm = 1.0f; chr = 0.1f;
                skyTint = ClearSkyTint; ground = new Color(0.4f, 0.37f, 0.34f); thick = 0.95f; skyExp = 1.2f;
                aSky = new Color(0.35f, 0.4f, 0.5f); aEq = new Color(0.3f, 0.3f, 0.33f); aGround = new Color(0.18f, 0.17f, 0.17f);
                fog = new Color(0.72f, 0.78f, 0.88f); fogDens = 0.0022f;
                break;
            case 2:
            {
                float ft = Mathf.Max(0, t - FlashAt);
                float k = flashed ? Window(ft, 0f, 9f) : 0;      // 섬광 뒤 하늘이 주황으로 물들고 어두워진다
                float heat = flashed ? Mathf.Exp(-ft * 0.35f) : 0;
                sun.color = Color.Lerp(new Color(1f, 0.88f, 0.72f), new Color(1f, 0.55f, 0.3f), k); sun.intensity = Mathf.Lerp(1.5f, 0.5f, k); sun.transform.rotation = Quaternion.Euler(24, 200, 0);
                roomFill.color = Color.Lerp(new Color(0.6f, 0.7f, 0.95f), new Color(1f, 0.6f, 0.4f), k); roomFill.intensity = Mathf.Lerp(0.45f, 0.3f, k);
                if (!flashed) { windowLight.color = new Color(1f, 0.9f, 0.76f); windowLight.intensity = 1.4f; flashLight.intensity = 0; }
                filter = flashed ? new Color(1f, 0.92f, 0.82f) : new Color(0.92f, 0.96f, 1.05f);
                exp = flashed ? 0.05f + 3.2f * Flash : -0.1f; con = flashed ? 24f : 18f; sat = flashed ? 10f : -12f; vig = 0.4f; grn = 0.3f; blm = flashed ? 1.7f : 1.0f; chr = flashed ? 0.18f : 0.1f;
                skyTint = Color.Lerp(ClearSkyTint, BlastSkyTint, k); ground = Color.Lerp(new Color(0.4f, 0.37f, 0.34f), new Color(0.35f, 0.18f, 0.12f), k);
                thick = Mathf.Lerp(0.95f, 2.6f, k); skyExp = Mathf.Lerp(1.2f, 0.9f, k) + 7f * Flash + heat * 0.6f;
                aSky = Color.Lerp(new Color(0.35f, 0.4f, 0.5f), new Color(0.6f, 0.32f, 0.2f), k) + Color.white * (Flash * 1.2f);
                aEq = Color.Lerp(new Color(0.3f, 0.3f, 0.33f), new Color(0.45f, 0.26f, 0.18f), k) + Color.white * (Flash * 0.8f);
                aGround = Color.Lerp(new Color(0.18f, 0.17f, 0.17f), new Color(0.22f, 0.13f, 0.1f), k);
                fog = Color.Lerp(new Color(0.72f, 0.78f, 0.88f), new Color(0.9f, 0.5f, 0.3f), k); fogDens = Mathf.Lerp(0.0022f, 0.003f, k);
                break;
            }
            default:
                // 거리: 연기로 덮인 붉은 하늘 아래 낮게 비치는 주황 햇빛, 먼지 안개
                // 해는 건물 사이 길까지 닿게 높이 둔다 (낮으면 거리 전체가 건물 그늘에 잠겨 새카맣다)
                sun.color = new Color(1f, 0.55f, 0.32f); sun.intensity = 1.7f; sun.transform.rotation = Quaternion.Euler(38, 150, 0);
                flashLight.intensity = 0;
                filter = new Color(1f, 0.88f, 0.82f); exp = 0.1f; con = 18f; sat = -10f; vig = 0.42f; grn = 0.38f; blm = 1.1f; chr = 0.18f;
                skyTint = StreetSkyTint; ground = new Color(0.25f, 0.12f, 0.08f); thick = 3.2f; skyExp = 0.6f;
                aSky = new Color(0.62f, 0.36f, 0.24f); aEq = new Color(0.52f, 0.32f, 0.24f); aGround = new Color(0.22f, 0.15f, 0.12f);
                fog = new Color(0.52f, 0.32f, 0.24f); fogDens = 0.008f;
                break;
        }
        filterTarget = filter; expTarget = exp; conTarget = con; satTarget = sat; vigTarget = vig; grainTarget = grn; bloomTarget = blm; chromaTarget = chr;
        skyTintTarget = skyTint; skyGroundTarget = ground; skyThickTarget = thick; skyExpTarget = skyExp;
        ambSkyTarget = aSky; ambEqTarget = aEq; ambGroundTarget = aGround; fogTarget = fog; fogDensTarget = fogDens;
        if (immediate)
        {
            filterCur = filter; expCur = exp; conCur = con; satCur = sat; vigCur = vig; grainCur = grn; bloomCur = blm; chromaCur = chr;
            skyTintCur = skyTint; skyGroundCur = ground; skyThickCur = thick; skyExpCur = skyExp;
            ambSkyCur = aSky; ambEqCur = aEq; ambGroundCur = aGround; fogCur = fog; fogDensCur = fogDens;
        }
    }

    // 목표를 향해 부드럽게 (섬광처럼 급한 값은 바로)
    void UpdateLook(float dt)
    {
        float k = Mathf.Clamp01(dt * 2.5f);
        filterCur = Color.Lerp(filterCur, filterTarget, k);
        expCur = Shot == 2 && flashed ? expTarget : Mathf.Lerp(expCur, expTarget, k);
        conCur = Mathf.Lerp(conCur, conTarget, k); satCur = Mathf.Lerp(satCur, satTarget, k); vigCur = Mathf.Lerp(vigCur, vigTarget, k);
        grainCur = Mathf.Lerp(grainCur, grainTarget, k); bloomCur = Mathf.Lerp(bloomCur, bloomTarget, k); chromaCur = Mathf.Lerp(chromaCur, chromaTarget, k);
        skyTintCur = Color.Lerp(skyTintCur, skyTintTarget, k); skyGroundCur = Color.Lerp(skyGroundCur, skyGroundTarget, k);
        skyThickCur = Mathf.Lerp(skyThickCur, skyThickTarget, k);
        skyExpCur = Shot == 2 && flashed ? skyExpTarget : Mathf.Lerp(skyExpCur, skyExpTarget, k);
        ambSkyCur = Shot == 2 && flashed ? ambSkyTarget : Color.Lerp(ambSkyCur, ambSkyTarget, k);
        ambEqCur = Shot == 2 && flashed ? ambEqTarget : Color.Lerp(ambEqCur, ambEqTarget, k);
        ambGroundCur = Color.Lerp(ambGroundCur, ambGroundTarget, k);
        fogCur = Color.Lerp(fogCur, fogTarget, k); fogDensCur = Mathf.Lerp(fogDensCur, fogDensTarget, k);
        ApplyLook(false);
    }

    void ApplyLook(bool force)
    {
        float bright = GameSettings.Brightness > 0 ? GameSettings.Brightness : 1f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambSkyCur * bright;
        RenderSettings.ambientEquatorColor = ambEqCur * bright;
        RenderSettings.ambientGroundColor = ambGroundCur * bright;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = fogCur;
        RenderSettings.fogDensity = fogDensCur;
        if (sky != null)
        {
            sky.SetColor("_SkyTint", skyTintCur);
            sky.SetColor("_GroundColor", skyGroundCur);
            sky.SetFloat("_AtmosphereThickness", skyThickCur);
            sky.SetFloat("_Exposure", skyExpCur);
        }
        if (grade != null)
        {
            grade.colorFilter.value = filterCur;
            grade.postExposure.value = baseExposure + expCur;
            grade.contrast.value = conCur;
            grade.saturation.value = satCur;
        }
        if (vignette != null) vignette.intensity.value = vigCur;
        if (grain != null) grain.intensity.value = grainCur;
        if (bloom != null) bloom.intensity.value = bloomCur;
        if (chroma != null) chroma.intensity.value = chromaCur + shake * 0.5f;
        if (dof != null)
        {
            dof.focusDistance.value = Mathf.Max(0.3f, focusDist);
            dof.aperture.value = aperture;
        }
        if (motionBlur != null) motionBlur.intensity.value = 0.2f + shake * 0.5f;
    }

    // 카메라: 연출이 정한 자리·시선에 핸드헬드 흔들림과 충격 흔들림을 더한다 (사람이 움직인 뒤에)
    void LateUpdate()
    {
        float dt = Time.deltaTime;
        shake = Mathf.Max(tremor, shake - dt * 0.5f);
        float t = Time.time;
        var jitterPos = new Vector3(Mathf.PerlinNoise(t * 0.9f, 1.3f) - 0.5f, Mathf.PerlinNoise(2.1f, t * 1.1f) - 0.5f, Mathf.PerlinNoise(t * 0.7f, t * 0.6f) - 0.5f) * (0.025f * handheld);
        var jitterRot = new Vector3((Mathf.PerlinNoise(t * 0.8f, 5.3f) - 0.5f) * 1.6f, (Mathf.PerlinNoise(7.7f, t * 0.6f) - 0.5f) * 2.2f, (Mathf.PerlinNoise(t * 0.5f, 9.1f) - 0.5f) * 0.8f) * handheld;
        if (shake > 0.001f)
        {
            float s = shake * shake;
            jitterPos += new Vector3(Mathf.PerlinNoise(t * 24f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t * 27f) - 0.5f, Mathf.PerlinNoise(t * 21f, t * 19f) - 0.5f) * (0.14f * s);
            jitterRot += new Vector3((Mathf.PerlinNoise(t * 25f, 3.3f) - 0.5f) * 9f, (Mathf.PerlinNoise(4.4f, t * 23f) - 0.5f) * 9f, (Mathf.PerlinNoise(t * 20f, 6.6f) - 0.5f) * 5f) * s;
        }
        var pos = camPos + jitterPos;
        var look = Quaternion.LookRotation(camLook - camPos) * Quaternion.Euler(jitterRot);
        cam.transform.SetPositionAndRotation(pos, look);
        cam.fieldOfView = camFov;
    }

    // ================= TV·리모컨 =================

    void SetTv(Material source, float brightness)
    {
        if (source == null)
        {
            tvCur.SetTexture("_BaseMap", null);
            tvCur.SetColor("_BaseColor", new Color(0.01f, 0.01f, 0.012f));
            tvLight.intensity = 0;
            return;
        }
        tvCur.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
        tvCur.mainTextureOffset = Vector2.zero;
        tvCur.SetColor("_BaseColor", Color.white * brightness);
    }

    void HoldRemote()
    {
        if (remote == null) return;
        var rb = remote.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);
        remote.SetParent(actor.Rig.HandR, false);
        remote.localPosition = new Vector3(0.01f, -0.085f, 0.03f);
        remote.localRotation = Quaternion.Euler(-70, 0, 0);
    }

    void DropRemote()
    {
        if (remote == null || remote.parent == home) return;
        remote.SetParent(home, true);
        var rb = remote.gameObject.AddComponent<Rigidbody>();
        rb.mass = 0.15f;
        rb.linearVelocity = actor.transform.forward * 0.4f + Vector3.up * 0.3f;
        rb.angularVelocity = new Vector3(3f, 1f, 5f);
    }

    void DropRemoteOnFloor()
    {
        if (remote == null) return;
        var rb = remote.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);
        remote.SetParent(home, true);
        remote.position = sofaSpot.position + sofaSpot.right * 0.35f + sofaSpot.forward * 0.45f + Vector3.up * 0.015f;
        remote.rotation = Quaternion.Euler(0, sofaSpot.eulerAngles.y + 40f, 0);
    }

    // ================= 소리 =================

    void BuildAudio()
    {
        AudioSource Make2D(SoundCategory cat, bool loop)
        {
            var a = gameObject.AddComponent<AudioSource>();
            AudioCategory.Tag(a, cat);
            a.playOnAwake = false;
            a.loop = loop;
            a.spatialBlend = 0;
            a.volume = 0;
            return a;
        }
        ambience = Make2D(SoundCategory.Ambient, true);
        ambience.clip = ProceduralAudio.Wind();
        ambience.volume = 0.06f;
        boom = Make2D(SoundCategory.Effects, false);
        boom.volume = 1f;
        sfx = Make2D(SoundCategory.Effects, false);
        sfx.volume = 1f;
        breath = Make2D(SoundCategory.Effects, true);
        breath.clip = ProceduralAudio.RunBreath();
        heart = Make2D(SoundCategory.Effects, true);
        heart.clip = ProceduralAudio.Heartbeat();
        heart.pitch = 1.15f;
        // TV 소리는 TV 자리에서 난다
        if (tvScreen != null)
        {
            var tvGo = new GameObject("TvAudio");
            tvGo.transform.SetParent(tvScreen, false);
            tv = tvGo.AddComponent<AudioSource>();
            AudioCategory.Tag(tv, SoundCategory.Ambient);
            tv.playOnAwake = false;
            tv.spatialBlend = 0.7f;
            tv.rolloffMode = AudioRolloffMode.Linear;
            tv.minDistance = 1.5f;
            tv.maxDistance = 14f;
            tv.volume = 0.4f;
        }
        // 거리의 먼 사이렌 둘 (높낮이가 다르다)
        if (street != null)
        {
            siren1 = MakeSiren(street.TransformPoint(new Vector3(-34f, 4f, 62f)), 0.82f);
            siren2 = MakeSiren(street.TransformPoint(new Vector3(38f, 4f, 96f)), 1.07f);
        }
    }

    AudioSource MakeSiren(Vector3 pos, float pitch)
    {
        var go = new GameObject("Siren");
        go.transform.SetParent(transform, true);
        go.transform.position = pos;
        var a = go.AddComponent<AudioSource>();
        AudioCategory.Tag(a, SoundCategory.Ambient);
        a.playOnAwake = false;
        a.clip = ProceduralAudio.Siren();
        a.loop = true;
        a.pitch = pitch;
        a.spatialBlend = 1f;
        a.rolloffMode = AudioRolloffMode.Linear;
        a.minDistance = 6f;
        a.maxDistance = 190f;
        a.volume = 0.55f;
        var lp = go.AddComponent<AudioLowPassFilter>();
        lp.cutoffFrequency = 2200f;
        return a;
    }

    void UpdateAudio(float dt)
    {
        if (Finished && finishedAt == 0) finishedAt = Time.time;
        float fade = Finished ? Mathf.Max(0, 1 - (Time.time - finishedAt) * 1.5f) : 1f;
        float amb = Shot == 0 ? 0.06f : Shot == 1 ? 0.07f : Shot == 2 ? (shocked ? 0.3f : 0.08f) : 0.38f;
        float pitch = Shot == 3 ? 0.78f : Shot == 2 && shocked ? 0.62f : 1f;
        ambience.volume = Mathf.MoveTowards(ambience.volume, amb * fade, dt * 0.3f);
        ambience.pitch = Mathf.MoveTowards(ambience.pitch, pitch, dt * 0.4f);
        if (tv != null) tv.volume = 0.4f * fade;
        if (siren1 != null)
        {
            siren1.volume = 0.55f * fade;
            siren2.volume = 0.5f * fade;
        }
        if (breath != null && Finished) breath.volume *= fade;
        if (heart != null && Finished) heart.volume *= fade;
        // 거실의 벽시계
        if (Shot <= 1 && !Finished)
        {
            tickTimer -= dt;
            if (tickTimer <= 0)
            {
                tickTimer = 1f;
                sfx.pitch = 0.75f;
                sfx.PlayOneShot(ProceduralAudio.Click(), 0.12f);
                sfx.pitch = 1f;
            }
        }
    }

    float finishedAt;

    void OnActorFootstep(Vector3 pos)
    {
        if (Shot != 3 || Finished || Time.time - lastStepTime < 0.12f) return;
        lastStepTime = Time.time;
        ProceduralAudio.PlayAt(ProceduralAudio.Footstep(), pos, 0.7f, UnityEngine.Random.Range(0.9f, 1.05f), 1f, 20f);
    }
}
