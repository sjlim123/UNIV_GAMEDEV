using UnityEngine;

// 살펴보면 짧은 글이 뜨는 물체 (쇠사슬 감긴 현관문, 쪽지 등). 열리거나 주워지지 않는다
public class InspectNote : Interactable
{
    public enum NoteSound { Latch, Paper, None }

    public string prompt = "살펴본다";
    [TextArea] public string[] lines = new string[0];
    public NoteSound sound = NoteSound.Latch;
    public bool inOrder; // true: 차례대로 보여 주고 마지막 줄에 머문다. false: 무작위 (바로 앞 것과는 다르게)

    int shown = -1; // 바로 앞에 보여 준 줄 (-1 = 아직 없음)

    public override string Prompt => prompt;

    public override void Interact(PlayerInteractor player)
    {
        if (lines == null || lines.Length == 0) return;
        // 플레이 중이 아니면 GameManager를 건드리지 않는다 (에디터에서는 Instance를 묻기만 해도 빈 물체가 씬에 남는다)
        var gm = Application.isPlaying ? GameManager.Instance : null;
        if (gm == null || gm.UI == null) return;
        if (inOrder) shown = Mathf.Min(shown + 1, lines.Length - 1);
        else
        {
            int i = Random.Range(0, lines.Length);
            if (lines.Length > 1 && i == shown) i = (i + 1 + Random.Range(0, lines.Length - 1)) % lines.Length;
            shown = i;
        }
        if (!string.IsNullOrEmpty(lines[shown])) gm.UI.ShowMessage(lines[shown], 3.5f);
        if (sound == NoteSound.Latch) ProceduralAudio.PlayAt(ProceduralAudio.Latch(), transform.position, 0.35f, Random.Range(0.9f, 1.1f), 1f, 10f);
        else if (sound == NoteSound.Paper) ProceduralAudio.Play2D(ProceduralAudio.PaperUnfold(), 0.4f);
    }
}
