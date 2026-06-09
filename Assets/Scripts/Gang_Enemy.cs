using UnityEngine;

public class Gang_Enemy : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private int damage = 10;
    [SerializeField] private float attackInterval = 1f;

    [Header("Bullet")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float shootInterval = 2f;

    public Transform player;
    public GameObject playerObj;

    private float attackTimer;
    private float shootTimer;

    public Vector2[] patrols = new Vector2[5];

    private Vector2 dest;
    private int Count;

    public Vector2? returnP = null;

    private void Start()
    {
        dest = patrols[0];
        Count = 0;
        returnP = null;
    }

    private void Update()
    {
        if (player == null)
        {
            if (returnP != null)
            {
                transform.position = Vector2.MoveTowards(
                    transform.position,
                    returnP.Value,
                    moveSpeed * Time.deltaTime
                );

                if (Vector2.Distance(transform.position, returnP.Value) < 0.1f)
                {
                    returnP = null;
                }
            }
            else
            {
                transform.position = Vector2.MoveTowards(
                    transform.position,
                    dest,
                    moveSpeed * Time.deltaTime
                );

                if (Vector2.Distance(transform.position, dest) < 0.1f)
                {
                    Count = (Count + 1) % patrols.Length;
                    dest = patrols[Count];
                }
            }
        }
        else
        {
            // 플레이어 추적
            transform.position = Vector2.MoveTowards(
                transform.position,
                player.position,
                moveSpeed * Time.deltaTime
            );

            // 총알 발사
            shootTimer += Time.deltaTime;

            if (shootTimer >= shootInterval)
            {
                Shoot();
                shootTimer = 0f;
            }
        }
    }

    private void Shoot()
    {
        if (bulletPrefab == null)
            return;

        Instantiate(
            bulletPrefab,
            transform.position,
            Quaternion.identity
        );
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
            return;

        attackTimer += Time.deltaTime;

        if (attackTimer >= attackInterval)
        {
            Health playerHealth =
                collision.gameObject.GetComponent<Health>();

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
            }

            attackTimer = 0f;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            attackTimer = 0f;
        }
    }
}