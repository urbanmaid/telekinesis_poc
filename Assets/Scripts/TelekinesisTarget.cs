using UnityEngine;

public class TelekinesisTarget : MonoBehaviour
{
    [Header("사물 설정")]
    public float weight = 20f;           // 물체의 무게 (조종할 때 소모될 플레이어 HP)
    public float damageMultiplier = 1.5f; // 날아갈 때 적에게 줄 데미지 배율 (속도 * 배율)

    [HideInInspector] public bool isCaught = false; // 현재 염력에 잡혔는지 여부
    [HideInInspector] public bool isFlying = false; // 플레이어가 날려서 날아가는 중인지 여부

    private Rigidbody2D rigid;
    private Collider2D col;

    // 🎯 목적지 및 부드러운 감속 제어 변수
    private Vector2 targetDestination;
    private float initialSpeed = 0f;
    private bool isBraking = false; 

    // 💡 [버그 해결 핵심 변수] 플레이어 품을 탈출할 때까지 시간을 벌어주는 타이머
    private float escapeTimer = 0f;
    private const float minEscapeTime = 0.15f; // ⏱️ 발사 후 최소 0.15초 동안은 플레이어와 절대 충돌하지 않음

    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    public void LaunchToTarget(Vector2 destination, float forceMagnitude)
    {
        isCaught = false;
        isFlying = true;
        isBraking = false; 
        escapeTimer = 0f; // 타이머 초기화

        if (rigid != null)
        {
            rigid.simulated = true; 
            rigid.angularVelocity = Random.Range(-100f, 100f); 
            rigid.linearDamping = 0f; 
            
            // 무게에 따른 초기 속도 계산
            initialSpeed = forceMagnitude / Mathf.Max(1f, weight);
            if (initialSpeed < 5f) initialSpeed = 5f; 
            
            Vector2 dir = (destination - (Vector2)transform.position).normalized;
            rigid.linearVelocity = dir * initialSpeed;
        }

        // 💡 발사 시점에 콜라이더를 트리거로 만들어 플레이어 몸을 스르륵 통과하게 만듭니다.
        if (col != null) col.isTrigger = true; 
        
        targetDestination = destination;
    }

    void Update()
    {
        if (isFlying && col != null && col.isTrigger)
        {
            // 💡 실시간으로 타이머를 증가시킵니다.
            escapeTimer += Time.deltaTime;

            GameObject player = GameObject.FindWithTag("Player");
            float distToPlayer = player != null ? Vector2.Distance(transform.position, player.transform.position) : 2f;
            
            // 💡 [버그 수정 핵심 조건]
            // 1. 최소 탈출 시간(0.15초)이 지났고,
            // 2. 플레이어 몸뚱아리(무게가 무거울수록 안전거리를 더 넓게 설정)에서 완벽히 벗어났을 때만 실체화(isTrigger = false)합니다!
            float safeDistance = Mathf.Max(1.2f, weight * 0.05f); // 무게가 50이면 2.5유닛 거리 확보 후 단단해짐
            
            if (escapeTimer >= minEscapeTime && distToPlayer > safeDistance) 
            {
                col.isTrigger = false; // 이제 완전히 플레이어 몸을 빠져나왔으니 단단한 물리 상태로 복구!
            }
        }
    }

    void FixedUpdate()
    {
        if (isFlying)
        {
            Vector2 currentPos = transform.position;
            float distanceToTarget = Vector2.Distance(currentPos, targetDestination);

            if (distanceToTarget <= 1.5f || isBraking)
            {
                isBraking = true;
                
                if (rigid != null)
                {
                    rigid.linearVelocity = Vector2.Lerp(rigid.linearVelocity, Vector2.zero, 5f * Time.fixedDeltaTime);
                    rigid.angularVelocity = Mathf.Lerp(rigid.angularVelocity, 0f, 5f * Time.fixedDeltaTime);

                    if (rigid.linearVelocity.magnitude < 0.1f || distanceToTarget < 0.1f)
                    {
                        StopObject();
                    }
                }
            }
        }
    }

    private void StopObject()
    {
        isFlying = false;
        isBraking = false;
        isCaught = false; 

        if (col != null) 
        {
            col.isTrigger = false; 
            col.enabled = true; 
        }

        if (rigid != null)
        {
            rigid.linearVelocity = Vector2.zero; 
            rigid.angularVelocity = 0f;
            rigid.linearDamping = 1f; 
        }
    }

    public void DestroyObject()
    {
        Debug.Log($"{gameObject.name} 물체가 산산조각 났습니다!");
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 유령(Trigger) 상태로 날아가는 도중 적과 부딪히면 쾅!
        if (isFlying && collision.CompareTag("Enemy"))
        {
            ApplyDamage(collision.gameObject);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 실체화(isTrigger=false)된 후 적 몸체와 충돌해도 쾅!
        if (isFlying && collision.collider.CompareTag("Enemy"))
        {
            ApplyDamage(collision.gameObject);
        }
        // 플레이어가 아닌 벽이나 장애물에 부딪히면 브레이크 밟기
        else if (isFlying && !collision.collider.CompareTag("Player"))
        {
            isBraking = true; 
        }
    }

    private void ApplyDamage(GameObject enemy)
    {
        if (rigid != null)
        {
            float finalDamage = rigid.linearVelocity.magnitude * damageMultiplier;
            if (finalDamage < 5f) finalDamage = weight * damageMultiplier;

            Health enemyHealth = enemy.GetComponent<Health>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(finalDamage);
            }
        }
        DestroyObject(); 
    }
}