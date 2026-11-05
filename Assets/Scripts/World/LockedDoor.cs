using UnityEngine;

// 아파트 현관문 (잠김): 기존 문처럼 조준하면 윤곽선과 안내가 뜨지만 열리지 않는다.
// 상호작용하면 집으로 돌아갈 수 없다는 짧은 글이 뜬다 (여덟 가지 중 하나, 바로 앞 것과는 다르게). 도시 씬 빌더가 삼일아파트 현관 유리문에 붙인다
public class LockedDoor : Interactable
{
    static readonly string[] Lines =
    {
        "잠겨 있다. 집으로는 돌아갈 수 없다...",
        "손잡이가 돌아가지 않는다. 안에서 잠근 모양이다.",
        "두드려 봐도 아무 소리가 없다.",
        "열쇠가 없다. 이 문 뒤에 내 자리는 이제 없다.",
        "문틈으로 찬 바람만 새어 나온다. 아무도 없다.",
        "잠겼다. 누군가 안에서 버티고 있는 걸까, 아니면...",
        "녹슨 자물쇠가 꿈쩍도 하지 않는다. 지금은 방공호가 집이다.",
        "집이었던 곳. 이제는 들어갈 수 없는 곳.",
    };
    static int lastLine = -1;

    public override string Prompt => "현관문 - 열어 본다";

    public override void Interact(PlayerInteractor player)
    {
        int i;
        do i = Random.Range(0, Lines.Length); while (i == lastLine && Lines.Length > 1);
        lastLine = i;
        GameManager.Instance.UI.ShowMessage(Lines[i], 3f);
        ProceduralAudio.PlayAt(ProceduralAudio.Latch(), transform.position, 0.35f, Random.Range(0.9f, 1.1f), 1f, 10f);
    }
}
