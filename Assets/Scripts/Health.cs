using UnityEngine;
using UnityEngine.Events; // 게임 오버 이벤트를 연동하기 위해 필요합니다.

public class Health : MonoBehaviour
{
    [Header("체력 설정")]
    public float maxHp = 100f;
    public float currentHp;
    public bool isDead = false;

    [Header("이벤트 연계")]
    // 죽었을 때 다른 스크립트(UI 등)에 "나 죽었어!"라고 신호를 보낼 이벤트입니다.
    public UnityEvent onDeath; 

    void Awake()
    {
        currentHp = maxHp;
    }

    // 누군가 나를 때리면 이 함수를 호출하게 만듭니다.
    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHp -= damage;
        Debug.Log($"{gameObject.name}가 맞았습니다! 남은 체력: {currentHp}");

        if (currentHp <= 0)
        {
            currentHp = 0;
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        Debug.Log($"{gameObject.name}가 사망했습니다.");
        
        // 등록된 죽음 이벤트가 있다면 실행합니다 (예: 게임오버 UI 띄우기)
        onDeath?.Invoke(); 
    }
}