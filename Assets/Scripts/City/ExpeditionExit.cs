using System.Collections;
using UnityEngine;

// 해치가 없는 맵의 돌아가는 길 (삼일아파트 102동의 대피소 골목 표지판). 창과 귀환 처리는 도시 해치(CityExit)와 같다
public class ExpeditionExit : Interactable
{
    public string label = "대피소 골목";

    public override string Prompt => $"{label} - 방공호로 돌아가기 (하루)";

    public override void Interact(PlayerInteractor player)
    {
        // 플레이 중이 아니면 GameManager를 건드리지 않는다 (에디터에서는 Instance를 묻기만 해도 빈 물체가 씬에 남는다)
        if (!Application.isPlaying || GameManager.Instance == null) return;
        CityExit.OpenReturnPanel();
    }

    // 해치 연출(HatchArrival)이 없는 맵에서 돌아갈 때: 발소리 세 번 뒤 화면이 어두워진다 (문 닫히는 소리가 검은 화면에서 나게).
    // GameManager.ArriveRoutine이 기다린다
    public static IEnumerator Leave()
    {
        for (int i = 0; i < 3; i++)
        {
            if (i > 0) yield return new WaitForSecondsRealtime(0.32f);
            ProceduralAudio.Play2D(Footsteps.Clip(Footsteps.Surface.Asphalt), 0.5f, Random.Range(0.92f, 1.05f));
        }
        var gm = GameManager.Instance;
        if (gm != null && gm.UI != null) yield return gm.UI.Fade(1, 0.9f);
    }
}
