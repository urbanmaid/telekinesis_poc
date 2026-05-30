using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 5f;
    public float damage = 20f;
    public float lifeTime = 5f;

    private Vector3 moveDirection;

    void Start()
    {
        Player player = Object.FindAnyObjectByType<Player>();

        if (player != null)
        {
            Vector3 targetDirection = player.transform.position - transform.position;
            moveDirection = targetDirection.normalized;

            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
        }
        else
        {
            moveDirection = transform.right; 
        }

        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        transform.position += moveDirection * speed * Time.deltaTime;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 1. 플레이어 혹은 생명체 피격 체크
        Health targetHealth = collision.GetComponent<Health>();
        if (targetHealth != null)
        {
            targetHealth.TakeDamage(damage);
            Destroy(gameObject); 
            return;
        }

        // 2. 🌟 [새로 추가] 염력 사물(오브젝트)에 부딪혔는지 체크
        TelekinesisTarget targetObj = collision.GetComponent<TelekinesisTarget>();
        if (targetObj != null)
        {
            // 잡혀있는 상태이거나 가만히 있는 상태일 때 총알에 맞으면 사물 파괴
            if (!targetObj.isFlying) 
            {
                targetObj.DestroyObject(); // 사물 파괴 함수 호출
                Destroy(gameObject);       // 총알 자신도 파괴
            }
        }
    }
}