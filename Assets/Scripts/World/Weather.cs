using UnityEngine;

public enum WeatherKind { Clear, Fog, Rain }

// 도시 날씨: 탐험마다 맑음·안개·비 중 하나를 고른다 (CityDirector.Start가 Weather.Begin을 부른다).
// 안개 농도·햇빛·하늘색을 바꾸고, 비와 안개 덩어리 입자를 플레이어 둘레에 띄운다 (카메라를 따라다닌다).
// 붉은 밤은 CityDirector가 이 위에 덮는다 (붉은 밤이 시작될 때의 안개색을 기준으로 삼으므로 날씨가 먼저 적용돼야 한다).
// HUD 왼쪽 위 날씨 명판은 UIManager.Hud가 Weather.Current를 읽어 그린다
public class Weather : MonoBehaviour
{
    public static Weather Instance { get; private set; }
    public static WeatherKind Current { get; private set; } = WeatherKind.Clear;

    public static string Name(WeatherKind k) => k == WeatherKind.Fog ? "안개" : k == WeatherKind.Rain ? "비" : "맑음";

    ParticleSystem rain, fog;
    AudioSource rainSound;
    Transform follow;

    public static void Begin()
    {
        if (Instance != null) return;
        new GameObject("Weather").AddComponent<Weather>();
    }

    // 다음 탐험(또는 지금 도시)의 날씨를 정해 둔다 (시험·연출용). 지금 도시에 날씨가 있으면 바꿔 끼운다
    static WeatherKind? forced;
    public static void Force(WeatherKind kind)
    {
        forced = kind;
        if (Instance == null) return;
        Instance.Undo();
        Destroy(Instance.gameObject);
        Instance = null;
        Begin();
    }

    void Awake()
    {
        Instance = this;
        if (forced.HasValue) { Current = forced.Value; forced = null; }
        else
        {
            float roll = Random.value;
            Current = roll < 0.4f ? WeatherKind.Clear : roll < 0.7f ? WeatherKind.Fog : WeatherKind.Rain;
        }
        Apply();
    }

    // Apply가 바꾼 안개·빛·하늘을 되돌린다 (날씨를 바꿔 끼울 때)
    float baseDensity;
    Color baseFogColor, baseAmbient, baseSky;
    readonly System.Collections.Generic.List<Light> suns = new System.Collections.Generic.List<Light>();
    readonly System.Collections.Generic.List<(float intensity, Color color)> sunBase = new System.Collections.Generic.List<(float, Color)>();

    void Undo()
    {
        RenderSettings.fogDensity = baseDensity;
        RenderSettings.fogColor = baseFogColor;
        RenderSettings.ambientLight = baseAmbient;
        var cam = Camera.main;
        if (cam != null) cam.backgroundColor = baseSky;
        for (int i = 0; i < suns.Count; i++)
            if (suns[i] != null) { suns[i].intensity = sunBase[i].intensity; suns[i].color = sunBase[i].color; }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Apply()
    {
        baseDensity = RenderSettings.fogDensity;
        baseFogColor = RenderSettings.fogColor;
        baseAmbient = RenderSettings.ambientLight;
        var mainCam = Camera.main;
        if (mainCam != null) baseSky = mainCam.backgroundColor;
        suns.Clear();
        sunBase.Clear();
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { suns.Add(l); sunBase.Add((l.intensity, l.color)); }
        float sun = 1f;
        var sunTint = Color.white;
        switch (Current)
        {
            case WeatherKind.Clear:
                // 안개가 옅고 햇빛이 조금 더 따뜻하다
                RenderSettings.fogDensity *= 0.7f;
                sun = 1.1f;
                sunTint = new Color(1f, 0.97f, 0.9f);
                break;
            case WeatherKind.Fog:
                // 짙은 회색 안개: 멀리가 안 보이고 빛이 바랜다. 땅 위로 안개 덩어리가 흘러간다
                RenderSettings.fogDensity = Mathf.Max(RenderSettings.fogDensity * 2.2f, 0.06f);
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, new Color(0.56f, 0.57f, 0.58f), 0.7f);
                RenderSettings.ambientLight *= 0.85f;
                sun = 0.55f;
                sunTint = new Color(0.9f, 0.92f, 0.95f);
                fog = MakeFog();
                break;
            case WeatherKind.Rain:
                // 비: 푸르스름하고 어두운 하늘, 빗줄기, 빗소리
                RenderSettings.fogDensity *= 1.35f;
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, new Color(0.4f, 0.44f, 0.5f), 0.5f);
                RenderSettings.ambientLight *= 0.9f;
                sun = 0.7f;
                sunTint = new Color(0.82f, 0.88f, 1f);
                rain = MakeRain();
                rainSound = gameObject.AddComponent<AudioSource>();
                rainSound.clip = ProceduralAudio.Rain();
                rainSound.loop = true;
                rainSound.spatialBlend = 0;
                rainSound.volume = 0.32f;
                AudioCategory.Tag(rainSound, SoundCategory.Ambient);
                rainSound.Play();
                break;
        }
        foreach (var l in suns)
            if (l != null) { l.intensity *= sun; l.color *= sunTint; }
        if (mainCam != null && Current != WeatherKind.Clear) mainCam.backgroundColor = Color.Lerp(mainCam.backgroundColor, RenderSettings.fogColor, 0.6f);
    }

    // 빗줄기: 카메라 위 넓은 상자에서 떨어지는 늘인 빌보드 (바람에 살짝 비스듬히)
    ParticleSystem MakeRain()
    {
        var go = new GameObject("Rain");
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.Euler(90, 0, 0); // 상자의 +z(뿜는 방향)가 아래를 보게
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop();
        var main = ps.main;
        main.startLifetime = 1.4f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(15f, 18f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.014f, 0.026f);
        main.startColor = new Color(0.72f, 0.78f, 0.9f, 0.55f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 5000;
        main.prewarm = true;
        var emission = ps.emission;
        emission.rateOverTime = 2400f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(34f, 34f, 0.1f);
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.045f;
        r.lengthScale = 1f;
        r.sharedMaterial = Resources.Load<Material>("Effects/M_WeatherRain");
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    // 땅 위를 천천히 흘러가는 안개 덩어리: 크고 부드러운 빌보드가 서서히 나타났다 사라진다
    ParticleSystem MakeFog()
    {
        var go = new GameObject("GroundFog");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 22f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(7f, 12f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startColor = new Color(0.78f, 0.8f, 0.84f, 0.13f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 140;
        main.prewarm = true;
        var emission = ps.emission;
        emission.rateOverTime = 5.5f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(46f, 2.4f, 46f);
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.maxParticleSize = 1.5f;
        r.sharedMaterial = Resources.Load<Material>("Effects/M_WeatherFog");
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    // 입자 뿜는 자리가 카메라를 따라간다 (비는 머리 위 높이, 안개는 땅 가까이)
    void LateUpdate()
    {
        if (follow == null)
        {
            var cam = Camera.main;
            if (cam != null) follow = cam.transform;
            if (follow == null) return;
        }
        var p = follow.position;
        if (rain != null) rain.transform.position = new Vector3(p.x, p.y + 16f, p.z);
        if (fog != null) fog.transform.position = new Vector3(p.x, 1.3f, p.z);
    }
}
