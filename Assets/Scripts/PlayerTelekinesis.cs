using UnityEngine;
using UnityEngine.InputSystem; 
using System.Collections.Generic;

public class PlayerTelekinesis : MonoBehaviour
{
    [Header("염력 기본 설정")]
    public float grabRange = 3.5f;        
    public float grabAngle = 90f;         
    public float pullSpeed = 15f;         
    public float maxLaunchForce = 50f;    

    [Header("새총 조작 및 민감도")]
    public float pullSensitivity = 6.0f;  
    public float distanceScale = 0.25f;   

    [Header("무게별 HP 소모 설정 (🩸)")]
    public float hpDrainRate = 2f;               // ⏳ 들고 있을 때 초당 소모 배율 (초당 = 이 값 * 무게)
    public float grabHpCostMultiplier = 0.5f;    // 🧲 집는 순간 즉시 소모 배율 (소모량 = 이 값 * 무게)
    public float launchHpCostMultiplier = 1.0f;  // 🚀 발사하는 순간 즉시 소모 배율 (소모량 = 이 값 * 무게)

    [Header("무게 관련 탄성")]
    public float baseForceMultiplier = 12f; 

    [Header("UI 및 라인 연결")]
    public LineRenderer rangeLine;        
    public LineRenderer aimLine;          
    public Transform crosshairObject;     

    private TelekinesisTarget grabbedTarget; 
    private Health playerHealth;              
    private bool isDragging = false;      
    private const int sectorSegments = 30; 

    private Vector2 dragStartMouseScreenPos; 
    private PolygonCollider2D sectorCollider; 

    void Awake()
    {
        playerHealth = GetComponent<Health>();
        if (aimLine != null) 
        {
            aimLine.useWorldSpace = true; 
            aimLine.enabled = false;
        }
        
        if (rangeLine != null)
        {
            SetupSectorLine();
            sectorCollider = rangeLine.GetComponent<PolygonCollider2D>();
            if (sectorCollider != null) sectorCollider.isTrigger = true;
            rangeLine.enabled = false; 
        }
    }

    void Update()
    {
        if (playerHealth != null && playerHealth.isDead)
        {
            DisableAllUI();
            if (grabbedTarget != null) ReleaseGrabbedObject();
            return;
        }

        UpdateCrosshairPosition();

        if (Mouse.current.rightButton.isPressed)
        {
            if (rangeLine != null)
            {
                rangeLine.enabled = true;
                DrawSectorRange(); 
            }
        }
        else
        {
            if (rangeLine != null) rangeLine.enabled = false;
        }

        HandleLeftClickInput();
        HandleGrabbedObjectBehavior();
    }

    void UpdateCrosshairPosition()
    {
        if (crosshairObject != null)
        {
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
            mousePos.z = 0; 
            crosshairObject.position = mousePos;
        }
    }

    void DrawSectorRange()
    {
        rangeLine.positionCount = sectorSegments + 3;
        Vector3 center = Vector3.zero; 
        rangeLine.SetPosition(0, center);

        Vector2[] colliderPoints = new Vector2[sectorSegments + 2];
        colliderPoints[0] = center; 

        float startAngle = (90f - (grabAngle / 2f)) * Mathf.Deg2Rad;
        float endAngle = (90f + (grabAngle / 2f)) * Mathf.Deg2Rad;

        for (int i = 0; i <= sectorSegments; i++)
        {
            float progress = (float)i / sectorSegments;
            float currentAngle = Mathf.Lerp(startAngle, endAngle, progress);

            float x = Mathf.Cos(currentAngle) * grabRange;
            float y = Mathf.Sin(currentAngle) * grabRange;

            Vector3 pointPos = new Vector3(x, y, 0);
            rangeLine.SetPosition(i + 1, pointPos);

            colliderPoints[i + 1] = new Vector2(x, y);
        }

        rangeLine.SetPosition(sectorSegments + 2, center);

        if (sectorCollider != null) sectorCollider.points = colliderPoints;
    }

    void SetupSectorLine()
    {
        rangeLine.loop = true;
        rangeLine.useWorldSpace = false; 
    }

    void HandleLeftClickInput()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame && grabbedTarget == null)
        {
            TryGrabObjectInSector();
        }
        else if (Mouse.current.leftButton.wasPressedThisFrame && grabbedTarget != null)
        {
            dragStartMouseScreenPos = Mouse.current.position.ReadValue(); 
            isDragging = true;
            if (aimLine != null) aimLine.enabled = true;
        }

        if (isDragging && grabbedTarget != null)
        {
            UpdateForwardAimLine();
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame && isDragging && grabbedTarget != null)
        {
            LaunchObjectForward();
        }
    }

    void HandleGrabbedObjectBehavior()
    {
        if (grabbedTarget != null)
        {
            // ⏳ [지속 소모] 물체를 들고 있는 동안 시간에 비례해 실시간 피 깎임
            if (playerHealth != null)
            {
                float damageOverTime = hpDrainRate * grabbedTarget.weight * Time.deltaTime;
                playerHealth.TakeDamage(damageOverTime);

                if (playerHealth.currentHp <= 0)
                {
                    ReleaseGrabbedObject();
                    return;
                }
            }

            Vector3 targetPos = transform.position; 
            grabbedTarget.transform.position = Vector3.MoveTowards(grabbedTarget.transform.position, targetPos, pullSpeed * Time.deltaTime);
        }
    }

    void TryGrabObjectInSector()
    {
        if (sectorCollider == null) return;

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true; 
        filter.useLayerMask = false; 
        
        List<Collider2D> overlappedColliders = new List<Collider2D>();
        Physics2D.OverlapCollider(sectorCollider, filter, overlappedColliders);

        foreach (var col in overlappedColliders)
        {
            TelekinesisTarget target = col.GetComponent<TelekinesisTarget>();
            if (target == null) target = col.GetComponentInParent<TelekinesisTarget>();
            if (target == null) continue;

            // 최소한 집을 때 필요한 즉시 소모 HP보다는 현재 피가 많아야 그랩 가능
            float immediateGrabCost = target.weight * grabHpCostMultiplier;

            if (!target.isCaught && !target.isFlying && playerHealth != null && playerHealth.currentHp > immediateGrabCost)
            {
                grabbedTarget = target;
                grabbedTarget.isCaught = true;
                
                // 🩸 [버그 수정 1] 집는 순간 즉시 HP 소모!
                playerHealth.TakeDamage(immediateGrabCost);

                Rigidbody2D targetRigid = grabbedTarget.GetComponent<Rigidbody2D>();
                if (targetRigid != null) targetRigid.simulated = false;

                Collider2D targetCollider = grabbedTarget.GetComponent<Collider2D>();
                if (targetCollider != null) targetCollider.enabled = false;
                
                Debug.Log($"{col.gameObject.name} 그랩 성공! 즉시 HP {immediateGrabCost} 소모.");
                break; 
            }
        }
    }

    void UpdateForwardAimLine()
    {
        if (aimLine == null || grabbedTarget == null) return;

        Vector2 currentMouseScreenPos = Mouse.current.position.ReadValue();
        Vector2 screenDragVector = dragStartMouseScreenPos - currentMouseScreenPos;
        Vector2 dragVector = (screenDragVector / Screen.width) * 15f; 

        Vector2 amplifiedDrag = dragVector * pullSensitivity;

        float finalMultiplier = baseForceMultiplier / Mathf.Max(1f, grabbedTarget.weight);
        Vector2 launchForce = amplifiedDrag * finalMultiplier;

        if (launchForce.magnitude > maxLaunchForce)
        {
            launchForce = launchForce.normalized * maxLaunchForce;
        }

        Vector3 finalAimPath = (Vector3)(launchForce * distanceScale);

        aimLine.SetPosition(0, transform.position);
        aimLine.SetPosition(1, transform.position + finalAimPath);
    }

    void LaunchObjectForward()
    {
        isDragging = false;
        if (aimLine != null) aimLine.enabled = false;

        Vector2 currentMouseScreenPos = Mouse.current.position.ReadValue();
        Vector2 screenDragVector = dragStartMouseScreenPos - currentMouseScreenPos;
        Vector2 dragVector = (screenDragVector / Screen.width) * 15f; 

        Vector2 amplifiedDrag = dragVector * pullSensitivity;

        float finalMultiplier = baseForceMultiplier / Mathf.Max(1f, grabbedTarget.weight);
        Vector2 launchForce = amplifiedDrag * finalMultiplier;

        if (launchForce.magnitude > maxLaunchForce)
        {
            launchForce = launchForce.normalized * maxLaunchForce;
        }

        // 🩸 [버그 수정 2] 발사하는 순간 즉시 HP 추가 소모!
        if (playerHealth != null)
        {
            float immediateLaunchCost = grabbedTarget.weight * launchHpCostMultiplier;
            playerHealth.TakeDamage(immediateLaunchCost);
            Debug.Log($"{grabbedTarget.gameObject.name} 발사 성공! 즉시 HP {immediateLaunchCost} 소모.");
        }

        Vector3 finalAimPath = (Vector3)(launchForce * distanceScale);
        Vector2 targetDestination = (Vector2)transform.position + (Vector2)finalAimPath;

        Collider2D targetCollider = grabbedTarget.GetComponent<Collider2D>();
        if (targetCollider != null) targetCollider.enabled = true;

        grabbedTarget.LaunchToTarget(targetDestination, launchForce.magnitude);
        grabbedTarget = null; 
    }

    void ReleaseGrabbedObject()
    {
        if (grabbedTarget != null)
        {
            grabbedTarget.isCaught = false;
            Rigidbody2D targetRigid = grabbedTarget.GetComponent<Rigidbody2D>();
            if (targetRigid != null) targetRigid.simulated = true;

            Collider2D targetCollider = grabbedTarget.GetComponent<Collider2D>();
            if (targetCollider != null) targetCollider.enabled = true;
            grabbedTarget = null;
        }
        isDragging = false;
        if (aimLine != null) aimLine.enabled = false;
    }

    void DisableAllUI()
    {
        if (rangeLine != null) rangeLine.enabled = false;
        if (aimLine != null) aimLine.enabled = false;
        if (crosshairObject != null) crosshairObject.gameObject.SetActive(false);
    }
}