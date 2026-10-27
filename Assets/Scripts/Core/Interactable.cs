using UnityEngine;

// F 키로 상호작용할 수 있는 모든 물체의 부모 클래스
public abstract class Interactable : MonoBehaviour
{
    public abstract string Prompt { get; }
    public virtual bool CanInteract => true;
    public abstract void Interact(PlayerInteractor player);
}

public interface IDamageable
{
    void TakeDamage(float amount, Vector3 direction);
}
