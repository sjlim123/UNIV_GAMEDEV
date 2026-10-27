using UnityEngine;

public class Workbench : Interactable
{
    public override string Prompt => "제작대 - 조합하기";

    public override void Interact(PlayerInteractor player)
    {
        var gm = GameManager.Instance;
        var ui = gm.UI;
        var s = gm.State;
        ui.OpenPanel("제작대", c =>
        {
            bool powered = s.Power > 0;
            ui.Line(c, powered ? "재료는 창고와 소지품에서 함께 쓴다." : "전력이 없어 작업등과 공구를 쓸 수 없다. 발전기에 연료를 넣자.", 20, powered ? UIManager.Dim : UIManager.Danger);
            foreach (var recipe in ItemDatabase.Recipes)
            {
                var parts = new System.Collections.Generic.List<string>();
                bool enough = true;
                foreach (var kv in recipe.Ingredients)
                {
                    int have = s.Total(kv.Key);
                    if (have < kv.Value) enough = false;
                    parts.Add($"{ItemDatabase.Name(kv.Key)} {kv.Value} <color=#8a8270>({have})</color>");
                }
                var row = ui.Row(c);
                ui.RowLabel(row, $"{ItemDatabase.Name(recipe.ResultId)} x{recipe.ResultCount}   <size=18>←  {string.Join(", ", parts)}</size>");
                ui.RowButton(row, "제작", () => Craft(recipe), enough && powered);
            }
        }, "craft");
    }

    void Craft(Recipe recipe)
    {
        var gm = GameManager.Instance;
        var s = gm.State;
        foreach (var kv in recipe.Ingredients) s.ConsumeFromAnywhere(kv.Key, kv.Value);
        bool toHand = s.CanCarry(recipe.ResultId, recipe.ResultCount);
        (toHand ? s.Carried : s.Storage).Add(recipe.ResultId, recipe.ResultCount);
        gm.UI.ShowMessage($"{ItemDatabase.Name(recipe.ResultId)} x{recipe.ResultCount} 제작 완료" + (toHand ? "" : " (무거워서 창고에 넣었다)"));
        ProceduralAudio.PlayAt(ProceduralAudio.Clang(), transform.position, 0.7f);
        gm.UI.RefreshPanel();
    }
}
