using UnityEngine;

// 제작대: F로 제작 창을 연다 (화면은 UIManager.Workbench.cs). 조합 규칙은 여기에 모아 둔다
public class Workbench : Interactable
{
    public override string Prompt => "제작대 - 조합하기";

    public override void Interact(PlayerInteractor player) => GameManager.Instance.UI.OpenWorkbench(transform);

    // 전력이 없으면 작업등과 공구를 쓸 수 없다
    public static bool IsPowered(GameState s) => s.Power > 0;

    // 재료는 창고와 소지품을 합쳐서 센다
    public static bool HasIngredients(GameState s, Recipe recipe)
    {
        foreach (var kv in recipe.Ingredients)
            if (s.Total(kv.Key) < kv.Value) return false;
        return true;
    }

    public static bool CanCraft(GameState s, Recipe recipe) => IsPowered(s) && HasIngredients(s, recipe);

    // 재료를 쓰고 난 뒤 완성품을 몸에 지닐 수 있는지 미리 본다 (Craft와 같은 규칙: 재료는 창고에서 먼저 쓴다)
    public static bool ResultFitsInHand(GameState s, Recipe recipe)
    {
        float weight = s.Carried.Weight;
        foreach (var kv in recipe.Ingredients)
        {
            int fromCarried = Mathf.Clamp(kv.Value - s.Storage.Count(kv.Key), 0, s.Carried.Count(kv.Key));
            weight -= ItemDatabase.Get(kv.Key).Weight * fromCarried;
        }
        // 첫 가방은 메는 순간 들 수 있는 무게가 늘어난다 (GameState.CanCarry와 같은 규칙)
        float cap = s.CarryCapacity;
        if (recipe.ResultId == "backpack" && s.Carried.Count("backpack") == 0) cap += GameBalance.Data.backpackBonus;
        return weight + ItemDatabase.Get(recipe.ResultId).Weight * recipe.ResultCount <= cap + 0.001f;
    }

    // 재료를 쓰고, 완성품은 들 수 있으면 소지품에, 무거우면 창고에 넣는다. soundAt이 없으면 2D 소리
    public static bool Craft(Recipe recipe, Vector3? soundAt = null)
    {
        var gm = GameManager.Instance;
        var s = gm.State;
        if (!CanCraft(s, recipe)) return false;
        foreach (var kv in recipe.Ingredients) s.ConsumeFromAnywhere(kv.Key, kv.Value);
        bool toHand = s.CanCarry(recipe.ResultId, recipe.ResultCount);
        (toHand ? s.Carried : s.Storage).Add(recipe.ResultId, recipe.ResultCount);
        gm.UI.ShowMessage($"{UIManager.ColoredName(recipe.ResultId)} x{recipe.ResultCount} 제작 완료" + (toHand ? "" : " (무거워서 창고에 넣었다)"));
        if (soundAt.HasValue) ProceduralAudio.PlayAt(ProceduralAudio.Clang(), soundAt.Value, 0.7f);
        else ProceduralAudio.Play2D(ProceduralAudio.Clang(), 0.7f);
        return true;
    }
}
