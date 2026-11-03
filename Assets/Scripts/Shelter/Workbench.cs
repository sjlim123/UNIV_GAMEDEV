using UnityEngine;

// 제작대: F로 제작 창을 연다 (화면은 UIManager.Workbench.cs). 조합 규칙은 여기에 모아 둔다 (화로도 함께 쓴다)
public class Workbench : Interactable
{
    public override string Prompt => "제작대 - 조합하기";

    public override void Interact(PlayerInteractor player) => GameManager.Instance.UI.OpenWorkbench(transform);

    // 벽에 붙은 공구판(Pegboard)을 바라봐도 안내와 윤곽선이 나오게 충돌체를 하나 더 둔다 (씬을 다시 만들지 않아도 되게 실행 중에 붙인다)
    void Start()
    {
        var board = transform.Find("Pegboard");
        if (board == null || GetComponents<BoxCollider>().Length > 1) return;
        var c = gameObject.AddComponent<BoxCollider>();
        c.center = board.localPosition;
        c.size = board.localScale + new Vector3(0, 0, 0.06f);
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

    // 재료를 쓰고, 완성품은 들 수 있으면 소지품에, 무거우면 창고에 넣는다. soundAt이 없으면 2D 소리
    public static bool Craft(Recipe recipe, Vector3? soundAt = null)
    {
        var gm = GameManager.Instance;
        var s = gm.State;
        if (!CanCraft(s, recipe)) return false;
        foreach (var kv in recipe.Ingredients) s.ConsumeFromAnywhere(kv.Key, kv.Value);
        if (IsInstall(recipe))
        {
            s.FurnaceBuilt = true;
            if (Furnace.Instance != null) Furnace.Instance.RefreshBuilt();
            gm.UI.ShowMessage($"{UIManager.ColoredName(recipe.ResultId)}{PlayerHotbar.Eul(ItemDatabase.Name(recipe.ResultId))} 거실 벽 앞에 설치했다. 부싯돌과 라이터가 있으면 불을 붙일 수 있다.", 4f);
            ProceduralAudio.Play2D(ProceduralAudio.Clang(), 0.7f);
            return true;
        }
        bool toHand = StoreResult(s, recipe);
        gm.UI.ShowMessage($"{UIManager.ColoredName(recipe.ResultId)} x{recipe.ResultCount} 제작 완료" + (toHand ? "" : " (무거워서 창고에 넣었다)"));
        if (soundAt.HasValue) ProceduralAudio.PlayAt(ProceduralAudio.Clang(), soundAt.Value, 0.7f);
        else ProceduralAudio.Play2D(ProceduralAudio.Clang(), 0.7f);
        return true;
    }
}
