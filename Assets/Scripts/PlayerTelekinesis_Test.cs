using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerTelekinesis_Test : MonoBehaviour
{
    [Header("염력 기본 설정")]
    public float grabRange = 3.5f;
    public float pullSpeed = 15f;
    public float maxLaunchForce = 50f;

    [Header("새총 조작")]
    public float pullSensitivity = 6f;
    public float distanceScale = 0.25f;

    [Header("HP 설정")]
    public float hpDrainRate = 2f;
    public float grabHpHealMultiplier = 1f;
    public float launchHpCostMultiplier = 1f;

    [Header("집기 시간")]
    public float baseGrabTime = 0.2f;
    public float grabTimePerWeight = 0.05f;

    [Header("발사 힘")]
    public float baseForceMultiplier = 12f;

    private TelekinesisTarget grabbedTarget;
    private TelekinesisTarget targetBeingGrabbed;

    private Health playerHealth;

    private bool isDragging = false;
    private bool isGrabbingProgress = false;

    private float currentGrabTimer;
    private float requiredGrabTime;

    private Vector2 dragStartMouseScreenPos;

    private void Awake()
    {
        playerHealth = GetComponent<Health>();
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.isDead)
            return;

        HandleLeftClickInput();
        HandleGrabbedObjectBehavior();
    }

    private void HandleLeftClickInput()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame &&
            grabbedTarget == null &&
            !isGrabbingProgress)
        {
            TryStartGrab();
        }

        if (isGrabbingProgress && targetBeingGrabbed != null)
        {
            if (Mouse.current.rightButton.isPressed)
            {
                currentGrabTimer += Time.deltaTime;

                if (currentGrabTimer >= requiredGrabTime)
                {
                    CompleteGrab();
                }
            }

            if (Mouse.current.rightButton.wasReleasedThisFrame)
            {
                CancelCurrentGrab();
            }
        }

        if (Mouse.current.leftButton.wasPressedThisFrame &&
            grabbedTarget != null)
        {
            dragStartMouseScreenPos =
                Mouse.current.position.ReadValue();

            isDragging = true;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame &&
            grabbedTarget != null &&
            isDragging)
        {
            LaunchObject();
        }
    }

    private void TryStartGrab()
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                grabRange
            );

        float nearestDistance = float.MaxValue;
        TelekinesisTarget nearestTarget = null;

        foreach (Collider2D hit in hits)
        {
            TelekinesisTarget target =
                hit.GetComponent<TelekinesisTarget>();

            if (target == null)
                continue;

            if (target.isCaught || target.isFlying)
                continue;

            float dist =
                Vector2.Distance(
                    transform.position,
                    target.transform.position
                );

            if (dist < nearestDistance)
            {
                nearestDistance = dist;
                nearestTarget = target;
            }
        }

        if (nearestTarget == null)
            return;

        targetBeingGrabbed = nearestTarget;

        requiredGrabTime =
            baseGrabTime +
            (nearestTarget.weight * grabTimePerWeight);

        currentGrabTimer = 0f;
        isGrabbingProgress = true;

        Debug.Log(
            $"{nearestTarget.name} 집기 시작"
        );
    }

    private void CompleteGrab()
    {
        grabbedTarget = targetBeingGrabbed;

        grabbedTarget.isCaught = true;

        Rigidbody2D rb =
            grabbedTarget.GetComponent<Rigidbody2D>();

        if (rb != null)
            rb.simulated = false;

        Collider2D col =
            grabbedTarget.GetComponent<Collider2D>();

        if (col != null)
            col.enabled = false;

        float healAmount =
            grabbedTarget.weight *
            grabHpHealMultiplier;

        playerHealth.currentHp =
            Mathf.Min(
                playerHealth.maxHp,
                playerHealth.currentHp + healAmount
            );

        targetBeingGrabbed = null;
        isGrabbingProgress = false;

        Debug.Log("집기 성공");
    }

    private void CancelCurrentGrab()
    {
        targetBeingGrabbed = null;
        isGrabbingProgress = false;
        currentGrabTimer = 0f;
    }

    private void HandleGrabbedObjectBehavior()
    {
        if (grabbedTarget == null)
            return;

        float damage =
            hpDrainRate *
            grabbedTarget.weight *
            Time.deltaTime;

        playerHealth.TakeDamage(damage);

        grabbedTarget.transform.position =
            Vector3.MoveTowards(
                grabbedTarget.transform.position,
                transform.position,
                pullSpeed * Time.deltaTime
            );
    }

    private void LaunchObject()
    {
        isDragging = false;

        float cost =
            grabbedTarget.weight *
            launchHpCostMultiplier;

        if (playerHealth.currentHp <= cost)
        {
            ReleaseGrabbedObject();
            return;
        }

        playerHealth.TakeDamage(cost);

        Vector2 currentMousePos =
            Mouse.current.position.ReadValue();

        Vector2 dragVector =
            dragStartMouseScreenPos -
            currentMousePos;

        dragVector =
            (dragVector / Screen.width) * 15f;

        Vector2 launchForce =
            dragVector *
            pullSensitivity;

        float multiplier =
            baseForceMultiplier /
            Mathf.Max(1f, grabbedTarget.weight);

        launchForce *= multiplier;

        if (launchForce.magnitude > maxLaunchForce)
        {
            launchForce =
                launchForce.normalized *
                maxLaunchForce;
        }

        Vector2 targetPos =
            (Vector2)transform.position +
            launchForce * distanceScale;

        Collider2D col =
            grabbedTarget.GetComponent<Collider2D>();

        if (col != null)
            col.enabled = true;

        grabbedTarget.LaunchToTarget(
            targetPos,
            launchForce.magnitude
        );

        grabbedTarget = null;
    }

    private void ReleaseGrabbedObject()
    {
        if (grabbedTarget == null)
            return;

        grabbedTarget.isCaught = false;

        Rigidbody2D rb =
            grabbedTarget.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero;
        }

        Collider2D col =
            grabbedTarget.GetComponent<Collider2D>();

        if (col != null)
            col.enabled = true;

        grabbedTarget = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(
            transform.position,
            grabRange
        );
    }
}
