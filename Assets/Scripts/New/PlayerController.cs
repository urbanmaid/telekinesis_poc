using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float moveAcceleration = 20f;

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;

    [Header("Inhale")]
    [SerializeField] private float inhaleRadius = 5f;
    [SerializeField] private float inhaleForce = 20f;
    [SerializeField] private float inhaleHealPerMass = 1f;

    [Header("Exhale")]
    [SerializeField] private float exhaleForce = 30f;
    [SerializeField] private float exhaleCostPerMass = 2f;
    [SerializeField] private float minimumHealthForExhale = 50f;

    private Rigidbody2D rb;

    private Vector2 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference inhaleAction;
    [SerializeField] private InputActionReference exhaleAction;

    private void OnEnable()
    {
        moveAction.action.Enable();
        inhaleAction.action.Enable();
        exhaleAction.action.Enable();

        exhaleAction.action.performed += OnExhalePerformed;
    }

    private void OnDisable()
    {
        exhaleAction.action.performed -= OnExhalePerformed;

        moveAction.action.Disable();
        inhaleAction.action.Disable();
        exhaleAction.action.Disable();
    }

    private void Update()
    {
        moveInput = moveAction.action.ReadValue<Vector2>();

        if (inhaleAction.action.IsPressed())
        {
            Inhale();
        }
    }

    private void OnExhalePerformed(InputAction.CallbackContext context)
    {
        Exhale();
    }

    private void FixedUpdate()
    {
        Move();
    }

    private void Move()
    {
        Vector2 desiredVelocity =
            moveInput.normalized * moveSpeed;

        Vector2 velocityDelta =
            desiredVelocity - rb.linearVelocity;

        rb.AddForce(
            velocityDelta * moveAcceleration,
            ForceMode2D.Force);
    }

    private void Inhale()
    {
        Collider2D[] colliders =
            Physics2D.OverlapCircleAll(
                transform.position,
                inhaleRadius);

        foreach (Collider2D col in colliders)
        {
            Rigidbody2D targetRb = col.attachedRigidbody;

            if (targetRb == null)
                continue;

            if (targetRb == rb)
                continue;

            Vector2 dir =
                ((Vector2)transform.position - targetRb.position)
                .normalized;

            targetRb.AddForce(
                dir * inhaleForce,
                ForceMode2D.Force);

            float healAmount =
                targetRb.mass * inhaleHealPerMass;

            Heal(healAmount);
        }
    }

    private void Exhale()
    {
        if (currentHealth <= minimumHealthForExhale)
            return;

        Collider2D[] colliders =
            Physics2D.OverlapCircleAll(
                transform.position,
                inhaleRadius);

        foreach (Collider2D col in colliders)
        {
            Rigidbody2D targetRb = col.attachedRigidbody;

            if (targetRb == null)
                continue;

            if (targetRb == rb)
                continue;

            float cost =
                targetRb.mass * exhaleCostPerMass;

            if (currentHealth - cost < minimumHealthForExhale)
                continue;

            currentHealth -= cost;

            Vector2 dir =
                (targetRb.position - (Vector2)transform.position)
                .normalized;

            targetRb.AddForce(
                dir * exhaleForce,
                ForceMode2D.Force);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.attachedRigidbody == null)
            return;

        Heal(other.attachedRigidbody.mass * inhaleHealPerMass);

        Destroy(other.gameObject);
    }

    private void Heal(float amount)
    {
        currentHealth =
            Mathf.Min(maxHealth, currentHealth + amount);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(
            transform.position,
            inhaleRadius);
    }
}