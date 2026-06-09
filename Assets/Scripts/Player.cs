using UnityEngine;
using UnityEngine.InputSystem; // 💡 최신 인풋 시스템 마우스 조작을 위해 필요합니다.

public class Player : MonoBehaviour
{
    [Header("이동 관련")]
    public Vector2 inputVec;
    public float speed;

    [Header("회전 관련")]
    public float rotationSpeed = 15f; // 🔄 몸을 돌리는 속도 (높을수록 빠르게 회전)
    
    Rigidbody2D rigid;
    Health health; 

    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>(); 
    }

    void Update()
    {
        // 죽었다면 회전도 하지 않음
        if (health != null && health.isDead) return;

        // 🔄 1. 매 프레임마다 우클릭을 누르고 있는지 검사하고 부드럽게 회전
        if (Mouse.current.rightButton.isPressed)
        {
            RotateTowardsMouse();
        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (health != null && health.isDead)
        {
            inputVec = Vector2.zero;
            return;
        }

        inputVec = context.ReadValue<Vector2>();
        
    }

    private void FixedUpdate()
    {
        if (health != null && health.isDead)
        {
            rigid.linearVelocity = Vector2.zero;
            return;
        }
        
        Vector2 moveVec = inputVec.normalized * speed * Time.fixedDeltaTime;
        rigid.MovePosition(rigid.position + moveVec);
    }

    // 🔄 마우스 위치를 계산해서 부드럽게 회전시키는 함수
    void RotateTowardsMouse()
    {
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
    
        Vector2 direction = (mouseWorldPos - transform.position).normalized;
    
        // 💡 [수정] 뒤에 -90f를 붙여서 위쪽을 바라보는 캐릭터 기준에 맞게 각도를 꺾어줍니다.
        float targetAngle = (Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg) - 90f;
    
        Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }
}