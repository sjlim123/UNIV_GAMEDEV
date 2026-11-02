using UnityEngine;

// 화로: F로 제련 창을 연다 (화면은 UIManager.Workbench.cs를 화로 모드로 쓴다).
// 처음에는 숨겨져 있고, 제작대에서 화로를 만들면(GameState.FurnaceBuilt) 나타난다.
// 부싯돌과 라이터가 있어야 불을 붙여 고철을 녹이고 고기를 굽는다 (ItemDatabase.SmeltRecipes). 전력이 없어도 쓸 수 있다.
// 재료는 녹이기를 시작할 때 쓰고, SmeltSeconds초 뒤 완성품을 소지품(무거우면 창고)에 넣는다.
// 창을 닫아도 계속 녹는다. 다 녹기 전에 방공호를 떠나면(씬 이동·게임 종료) 완성품은 창고에 넣고 다시 저장한다.
public class Furnace : Interactable
{
    public const float SmeltSeconds = 2f;

    public Light fireLight;       // 화구 앞 불빛 (그림자 없음)
    public Renderer mouthGlow;    // 화구 안의 빛나는 면 (발광 세기를 바꾼다)
    public ParticleSystem embers; // 튀어 오르는 불티
    public AudioSource crackle;   // 늘 나는 작은 탁탁 소리 (반복)
    public AudioSource roar;      // 녹이는 동안 나는 불길 소리 (반복)

    // 방공호의 화로 (하나뿐이다)
    public static Furnace Instance { get; private set; }

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    Recipe smelting;
    GameState smeltState; // 녹이기를 시작한 게임. 그 사이 새 게임이 시작됐으면 완성품을 넣지 않는다
    float smeltTimer, flare, seed, baseIntensity, roarVolume, emberTimer;
    float flickerPhase; // 깜빡임 잡음 위치. 속도가 바뀌어도 튀지 않게 시간 대신 쌓아 간다
    Color baseColor, baseEmission;
    MaterialPropertyBlock block;
    bool quitting, shown = true;

    public bool IsSmelting => smelting != null;
    public Recipe CurrentRecipe => smelting;
    public float Progress => smelting != null ? Mathf.Clamp01(smeltTimer / SmeltSeconds) : 0f;

    public override string Prompt => IsSmelting ? $"화로 - 녹이는 중… {Progress * 100:0}%" : "화로 - 제련하기";

    public override void Interact(PlayerInteractor player) => GameManager.Instance.UI.OpenFurnace(transform);

    // 부싯돌과 라이터가 창고나 소지품에 있어야 불을 붙인다 (닳지 않는다)
    public static bool HasLighter(GameState s) => s.Total(ItemDatabase.LighterId) > 0;

    // 불을 붙일 수 있고, 녹일 재료가 창고·소지품에 다 있고, 화로가 비어 있으면 녹일 수 있다. 전력은 필요 없다
    public bool CanSmelt(GameState s, Recipe recipe) => !IsSmelting && recipe != null && HasLighter(s) && Workbench.HasIngredients(s, recipe);

    // 설치 여부에 맞춰 화로를 보이거나 숨긴다 (숨기면 부딪히지도, F로 열리지도 않는다)
    public void RefreshBuilt()
    {
        bool built = GameManager.Instance.State.FurnaceBuilt;
        if (built == shown) return;
        shown = built;
        foreach (Transform child in transform) child.gameObject.SetActive(built);
        foreach (var c in GetComponents<Collider>()) c.enabled = built;
        if (built) StartAmbience();
    }

    // 늘 나는 작은 탁탁 소리 (화로가 보일 때만)
    void StartAmbience()
    {
        crackle.time = Random.Range(0f, crackle.clip.length * 0.9f); // 매번 같은 곳부터 들리지 않게
        crackle.Play();
    }

    // 재료를 바로 쓰고 녹이기 시작한다
    public bool StartSmelt(Recipe recipe)
    {
        var s = GameManager.Instance.State;
        if (!CanSmelt(s, recipe)) return false;
        foreach (var kv in recipe.Ingredients) s.ConsumeFromAnywhere(kv.Key, kv.Value);
        smelting = recipe;
        smeltState = s;
        smeltTimer = 0;
        ProceduralAudio.PlayAt(FurnaceAudio.Ignite(), MouthPosition, 0.8f, Random.Range(0.92f, 1.06f), 1f, 18f);
        if (embers != null) embers.Emit(18);
        return true;
    }

    Vector3 MouthPosition => fireLight != null ? fireLight.transform.position : transform.position + Vector3.up * 0.6f;

    void Awake()
    {
        Instance = this;
        seed = Random.value * 50f;
        block = new MaterialPropertyBlock();
        if (fireLight != null)
        {
            baseIntensity = fireLight.intensity;
            baseColor = fireLight.color;
        }
        if (mouthGlow != null && mouthGlow.sharedMaterial != null && mouthGlow.sharedMaterial.HasProperty(EmissionId))
            baseEmission = mouthGlow.sharedMaterial.GetColor(EmissionId);
        if (crackle == null) crackle = MakeSource(0.3f, 9f);
        if (roar == null) roar = MakeSource(0.6f, 16f);
        roarVolume = roar.volume;
    }

    AudioSource MakeSource(float volume, float maxDistance)
    {
        var src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1;
        src.minDistance = 1f;
        src.maxDistance = maxDistance;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.volume = volume;
        return src;
    }

    void Start()
    {
        crackle.clip = FurnaceAudio.Crackle();
        crackle.loop = true;
        roar.clip = FurnaceAudio.Roar();
        roar.loop = true;
        roar.volume = 0;
        FurnaceAudio.Ignite(); // 처음 녹일 때 끊기지 않게 미리 만든다
        if (GameManager.Instance.State.FurnaceBuilt) StartAmbience();
        else RefreshBuilt();
    }

    void Update()
    {
        if (!shown) return;
        if (smelting != null)
        {
            smeltTimer += Time.deltaTime;
            if (smeltTimer >= SmeltSeconds) Finish(false);
        }

        // 녹이는 동안 불길이 확 커졌다가, 끝나면 천천히 가라앉는다
        flare = Mathf.MoveTowards(flare, IsSmelting ? 1f : 0f, Time.deltaTime * (IsSmelting ? 2.5f : 0.8f));
        flickerPhase += Time.deltaTime * (5f + 7f * flare);
        if (flickerPhase > 10000f) flickerPhase -= 10000f; // 오래 켜 두어도 float 정밀도가 떨어지지 않게
        float n = Mathf.PerlinNoise(flickerPhase, seed);
        float k = (0.78f + 0.44f * n) * (1f + 1.7f * flare);
        if (fireLight != null)
        {
            fireLight.intensity = baseIntensity * k;
            fireLight.color = Color.Lerp(baseColor, new Color(1f, 0.66f, 0.32f), flare * 0.6f);
        }
        if (mouthGlow != null)
        {
            // 화구 면은 블룸이 번지지 않게 불빛보다 덜 세게 바꾼다
            block.SetColor(EmissionId, baseEmission * ((0.85f + 0.3f * n) * (1f + 0.8f * flare)));
            mouthGlow.SetPropertyBlock(block);
        }

        roar.volume = roarVolume * flare;
        if (flare > 0.01f && !roar.isPlaying) roar.Play();
        else if (flare <= 0.01f && roar.isPlaying) roar.Stop();

        // 녹이는 동안 불티가 계속 튄다 (평소 불티는 파티클 설정대로 조금씩)
        if (IsSmelting && embers != null)
        {
            emberTimer -= Time.deltaTime;
            if (emberTimer <= 0)
            {
                emberTimer = 0.07f;
                embers.Emit(Random.Range(1, 3));
            }
        }
    }

    // 녹이기를 끝내고 완성품을 넣는다. destroying: 화로가 사라지는 중 (씬 이동·게임 종료)
    void Finish(bool destroying)
    {
        var recipe = smelting;
        var s = smeltState;
        smelting = null;
        smeltState = null;
        smeltTimer = 0;

        var gm = GameManager.Instance;
        if (recipe == null || gm == null || gm.State != s || gm.IsGameOver) return;
        // 씬 이동·잠자기 도중이면 저장이 이미 끝났을 수 있으므로, 완성품은 창고에 넣고 다시 저장한다
        // (탐험 출발 직후 게임을 꺼도 완성품이 남는다. 죽어서 지운 저장은 되살리지 않는다)
        bool toStorage = destroying || gm.IsTransitioning;
        bool toHand = Workbench.StoreResult(s, recipe, toStorage);
        if (toStorage && SaveSystem.Exists(SaveSystem.CurrentSlot)) SaveSystem.Save(s);
        if (destroying) return;
        string where = toHand ? "" : toStorage ? " (창고에 넣었다)" : " (무거워서 창고에 넣었다)";
        gm.UI.ShowMessage($"{UIManager.ColoredName(recipe.ResultId)} x{recipe.ResultCount} 녹이기 완료" + where);
        ProceduralAudio.PlayAt(ProceduralAudio.Clang(), MouthPosition, 0.6f, 0.75f);
    }

    void OnApplicationQuit()
    {
        if (IsSmelting) Finish(true);
        quitting = true;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (IsSmelting && !quitting) Finish(true);
    }
}
