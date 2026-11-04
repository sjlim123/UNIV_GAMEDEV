using System.Collections;
using UnityEngine;

// 제작대: F로 제작 창을 연다 (화면은 UIManager.Workbench.cs). 조합 규칙은 여기에 모아 둔다 (화로도 함께 쓴다).
// 무언가 만들면 상 위의 망치가 들렸다 내려찍히며 쾅 소리와 먼지가 난다
public class Workbench : Interactable
{
    public static Workbench Instance { get; private set; }

    public override string Prompt => "제작대 - 조합하기";

    public override void Interact(PlayerInteractor player) => GameManager.Instance.UI.OpenWorkbench(transform);

    Transform hammer, hammerHead; // 망치: 손잡이·머리를 쥐는 쪽 끝의 축에 묶은 것
    Vector3 hammerAxis;           // 이 축으로 양의 각도만큼 돌리면 머리가 들린다
    bool hammering;

    void Awake()
    {
        Instance = this;
        BuildHammerPivot();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 벽에 붙은 공구판(Pegboard)을 바라봐도 안내와 윤곽선이 나오게 충돌체를 하나 더 둔다 (씬을 다시 만들지 않아도 되게 실행 중에 붙인다)
    void Start()
    {
        var board = transform.Find("Pegboard");
        if (board == null || GetComponents<BoxCollider>().Length > 1) return;
        var c = gameObject.AddComponent<BoxCollider>();
        c.center = board.localPosition;
        c.size = board.localScale + new Vector3(0, 0, 0.06f);
    }

    // 망치 손잡이·머리를 손잡이의 쥐는 쪽 끝을 축으로 묶어, 들어 올렸다 내려찍을 수 있게 한다
    void BuildHammerPivot()
    {
        var handle = transform.Find("HammerHandle");
        hammerHead = transform.Find("HammerHead");
        if (handle == null || hammerHead == null) return;
        // 손잡이는 로컬 Y가 길이 방향인 원기둥 (길이 = scale.y × 2). 머리 반대쪽 끝이 쥐는 쪽
        var along = handle.up;
        if (Vector3.Dot(hammerHead.position - handle.position, along) < 0) along = -along;
        var grip = handle.position - along * handle.lossyScale.y;
        hammer = new GameObject("Hammer").transform;
        hammer.SetParent(transform, false);
        hammer.position = grip;
        hammer.rotation = Quaternion.identity;
        handle.SetParent(hammer, true);
        hammerHead.SetParent(hammer, true);
        hammerAxis = Vector3.Cross(along, Vector3.up).normalized;
    }

    Vector3 HitPoint => hammerHead != null ? hammerHead.position + Vector3.up * 0.02f : transform.TransformPoint(new Vector3(0, 0.94f, 0));

    // 제작 연출: 망치가 들렸다 내려찍힌다. 이미 휘두르는 중이면(연달아 만들 때) 소리와 먼지만
    public void PlayCraftFx()
    {
        if (hammer == null || hammering) { Hit(); return; }
        StartCoroutine(HammerRoutine());
    }

    IEnumerator HammerRoutine()
    {
        hammering = true;
        const float lift = 48f, raise = 0.22f, drop = 0.09f;
        for (float t = 0; t < raise; t += Time.deltaTime)
        {
            float k = t / raise;
            Swing(lift * (1 - (1 - k) * (1 - k)));
            yield return null;
        }
        for (float t = 0; t < drop; t += Time.deltaTime)
        {
            float k = t / drop;
            Swing(lift * (1 - k * k));
            yield return null;
        }
        Swing(0);
        Hit();
        // 맞고 살짝 튄다
        for (float t = 0; t < 0.14f; t += Time.deltaTime)
        {
            Swing(5f * Mathf.Sin(t / 0.14f * Mathf.PI));
            yield return null;
        }
        Swing(0);
        hammering = false;
    }

    void Swing(float angle) => hammer.rotation = Quaternion.AngleAxis(angle, hammerAxis);

    void Hit()
    {
        var p = HitPoint;
        Vfx.BenchHit(p);
        ProceduralAudio.PlayAt(EffectAudio.HammerHit(), p, 0.9f, Random.Range(0.92f, 1.08f), 1f, 25f);
        if (PlayerController.Instance != null) PlayerController.Instance.Shake(0.25f);
    }

    // 전력이 없으면 작업등과 공구를 쓸 수 없다
    public static bool IsPowered(GameState s) => s.Power > 0;

    // 재료는 창고와 소지품을 합쳐서 센다
    public static bool HasIngredients(GameState s, Recipe recipe)
    {
        foreach (var kv in recipe.Ingredients)
            if (s.Total(kv.Key) < kv.Value) return false;
        return true;
    }

    // 화로(설치)는 아이템이 아니라 방공호에 하나만 놓는다
    public static bool IsInstall(Recipe recipe) => recipe.ResultId == ItemDatabase.FurnaceId;

    public static bool AlreadyInstalled(GameState s, Recipe recipe) => IsInstall(recipe) && s.FurnaceBuilt;

    public static bool CanCraft(GameState s, Recipe recipe) => IsPowered(s) && HasIngredients(s, recipe) && !AlreadyInstalled(s, recipe);

    // 재료를 쓰고 난 뒤 완성품을 몸에 지닐 수 있는지 미리 본다 (Craft와 같은 규칙: 재료는 창고에서 먼저 쓴다)
    public static bool ResultFitsInHand(GameState s, Recipe recipe)
    {
        float weight = s.Carried.Weight;
        foreach (var kv in recipe.Ingredients)
        {
            int fromCarried = Mathf.Clamp(kv.Value - s.Storage.Count(kv.Key), 0, s.Carried.Count(kv.Key));
            weight -= s.Carried.UnitWeight(kv.Key) * fromCarried;
        }
        return weight + s.Carried.UnitWeight(recipe.ResultId) * recipe.ResultCount <= s.CarryCapacity + 0.001f;
    }

    // 완성품을 들 수 있으면 소지품에, 무거우면(또는 toStorage면) 창고에 넣는다. 소지품에 넣었으면 true
    public static bool StoreResult(GameState s, Recipe recipe, bool toStorage = false)
    {
        bool toHand = !toStorage && s.CanCarry(recipe.ResultId, recipe.ResultCount);
        (toHand ? s.Carried : s.Storage).Add(recipe.ResultId, recipe.ResultCount);
        return toHand;
    }

    // 재료를 쓰고, 완성품은 들 수 있으면 소지품에, 무거우면 창고에 넣는다. soundAt: 제작대가 없을 때 소리를 낼 자리 (없으면 2D 소리)
    public static bool Craft(Recipe recipe, Vector3? soundAt = null)
    {
        var gm = GameManager.Instance;
        var s = gm.State;
        if (!CanCraft(s, recipe)) return false;
        foreach (var kv in recipe.Ingredients) s.ConsumeFromAnywhere(kv.Key, kv.Value);
        if (IsInstall(recipe))
        {
            s.FurnaceBuilt = true;
            gm.UI.ShowMessage($"{UIManager.ColoredName(recipe.ResultId)}{PlayerHotbar.Eul(ItemDatabase.Name(recipe.ResultId))} 거실 벽 앞에 설치했다. 부싯돌과 라이터가 있으면 불을 붙일 수 있다.", 4f);
            PlayHammer(soundAt);
            // 망치질 뒤에 화로가 쿵 내려앉는다
            if (Furnace.Instance != null)
            {
                Furnace.Instance.RefreshBuilt();
                Furnace.Instance.PlayInstall();
            }
            return true;
        }
        bool toHand = StoreResult(s, recipe);
        gm.UI.ShowMessage($"{UIManager.ColoredName(recipe.ResultId)} x{recipe.ResultCount} 제작 완료" + (toHand ? "" : " (무거워서 창고에 넣었다)"));
        PlayHammer(soundAt);
        return true;
    }

    // 제작대가 씬에 있으면 망치질 연출, 없으면 소리만
    static void PlayHammer(Vector3? at)
    {
        if (Instance != null) Instance.PlayCraftFx();
        else if (at.HasValue) ProceduralAudio.PlayAt(EffectAudio.HammerHit(), at.Value, 0.8f);
        else ProceduralAudio.Play2D(EffectAudio.HammerHit(), 0.8f);
    }
}
