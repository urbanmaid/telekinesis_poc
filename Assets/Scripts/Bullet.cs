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
            Vector3 targetDirection =
                player.transform.position - transform.position;

            moveDirection = targetDirection.normalized;

            float angle =
                Mathf.Atan2(moveDirection.y, moveDirection.x)
                * Mathf.Rad2Deg;

            transform.rotation =
                Quaternion.Euler(0, 0, angle - 90f);
        }
        else
        {
            moveDirection = transform.right;
        }

        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        transform.position +=
            moveDirection * speed * Time.deltaTime;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 플레이어만 공격
        if (collision.CompareTag("Player"))
        {
            Health playerHealth =
                collision.GetComponent<Health>();

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
            }

            Destroy(gameObject);
            return;
        }

        // 염력 오브젝트 파괴
        TelekinesisTarget targetObj =
            collision.GetComponent<TelekinesisTarget>();

        if (targetObj != null)
        {
            if (!targetObj.isFlying)
            {
                targetObj.DestroyObject();
                Destroy(gameObject);
            }
        }
    }
}