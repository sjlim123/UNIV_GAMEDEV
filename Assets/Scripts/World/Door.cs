using System.Collections.Generic;
using UnityEngine;

// 경첩 문. 이 오브젝트가 경첩 위치이고, 자식에 문짝이 있다. 닫힌 문은 좀비 길을 막는다.
public class Door : Interactable
{
    public static readonly List<Door> All = new List<Door>();

    public float openAngle = 100f;
    public float speed = 4f;

    Quaternion closedRotation;
    float angle, targetAngle;

    public bool IsOpen => Mathf.Abs(targetAngle) > 1f;

    public override string Prompt => IsOpen ? "문 닫기" : "문 열기";

    void Awake() => closedRotation = transform.localRotation;
    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public override void Interact(PlayerInteractor player)
    {
        if (IsOpen) targetAngle = 0;
        else OpenAwayFrom(player.transform.position);
        ProceduralAudio.PlayAt(ProceduralAudio.Creak(), transform.position, 0.6f, Random.Range(0.85f, 1.15f));
        PlayerController.Instance.MakeNoise(0.6f);
    }

    // 문짝은 +X 방향으로 뻗어 있다. 상대가 있는 쪽의 반대로 연다
    public void OpenAwayFrom(Vector3 position)
    {
        float side = Vector3.Dot(position - transform.position, transform.parent != null ? transform.parent.forward : Vector3.forward);
        targetAngle = side > 0 ? openAngle : -openAngle;
    }

    // 좀비가 문을 부수고 들어올 때
    public void ForceOpen(Vector3 from)
    {
        OpenAwayFrom(from);
        speed = 8f;
        ProceduralAudio.PlayAt(ProceduralAudio.Bang(), transform.position, 1f, 0.8f);
    }

    void Update()
    {
        angle = Mathf.Lerp(angle, targetAngle, Time.deltaTime * speed);
        transform.localRotation = closedRotation * Quaternion.Euler(0, angle, 0);
    }
}
