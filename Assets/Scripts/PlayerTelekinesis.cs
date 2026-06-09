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

    [Header("무게별 HP 제어 설정 (🩸)")]
    public float hpHealRate = 0.5f;               // ⏳ 들고 있을 때 초당 소모 배율
    public float grabHpCostMultiplier = 0.5f;    // 💚 [변경] 집기 성공 시 HP 회복 배율 (회복량 = 이 값 * 무게)
    public float launchHpCostMultiplier = 1.0f;  // 🚀 발사 시 소모 배율 (소모량 = 이 값 * 무게)

    [Header("무게별 집기 시간 설정")]
    public float baseGrabTime = 0.2f;            // ⏱️ 기본 최소 집기 시간 (초)
    public float grabTimePerWeight = 0.05f;      // ⏱️ 무게 1당 추가되는 집기 시간 (초)

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

    // ⏳ 집기 캐스팅(홀딩) 관련 내부 변수
    private bool isGrabbingProgress = false; 
    private float currentGrabTimer = 0f;
    private float requiredGrabTime = 0f;
    private TelekinesisTarget targetBeingGrabbed;

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
            CancelCurrentGrab();
            DisableAllUI();
            if (grabbedTarget != null) ReleaseGrabbedObject();
            return;
        }

        UpdateCrosshairPosition();

        // 우클릭 유지 시 부채꼴 레이더 표시
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
            // 💡 우클릭을 떼면 집고 있던 도중이었더라도 그랩 취소
            if (isGrabbingProgress) CancelCurrentGrab();
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
        // 1. 아무것도 안 잡고 있고, 집는 중도 아닐 때 좌클릭을 '누르는 순간' 그랩 시작
        if (Mouse.current.leftButton.wasPressedThisFrame && grabbedTarget == null && !isGrabbingProgress)
        {
            TryStartGrabInSector();
        }

        // 💡 [수정 포인트] 'isGrabbingProgress' 상태라면 마우스 홀딩을 더 확실하게 체크합니다.
        if (isGrabbingProgress && targetBeingGrabbed != null)
        {
            // 플레이어와 물체의 거리가 너무 멀어지면 취소
            if (Vector2.Distance(transform.position, targetBeingGrabbed.transform.position) > grabRange + 1f)
            {
                Debug.Log("물체와 거리가 너무 멀어져 그랩이 취소되었습니다.");
                CancelCurrentGrab();
            }
            // 💡 마우스 좌클릭을 여전히 누르고 있다면 타이머 진행
            else if (Mouse.current.leftButton.isPressed)
            {
                currentGrabTimer += Time.deltaTime;
                
                // 🎯 [성공] 게이지를 다 채우면 물체를 완벽하게 낚아챕니다!
                if (currentGrabTimer >= requiredGrabTime)
                {
                    CompleteGrab(); 
                }
            }
            // 마우스 좌클릭을 도중에 떼버렸다면 취소
            else if (Mouse.current.leftButton.wasReleasedThisFrame || !Mouse.current.leftButton.isPressed)
            {
                Debug.Log("그랩 도중 마우스를 떼서 취소되었습니다.");
                CancelCurrentGrab();
            }
        }

        // 2. 물건을 '이미 완벽히 잡은 상태'에서 다시 좌클릭을 누르면 새총(드래그) 시작
        if (Mouse.current.leftButton.wasPressedThisFrame && grabbedTarget != null && !isDragging)
        {
            dragStartMouseScreenPos = Mouse.current.position.ReadValue(); 
            isDragging = true;
            if (aimLine != null) aimLine.enabled = true;
        }

        if (isDragging && grabbedTarget != null)
        {
            UpdateForwardAimLine();
        }

        // 클릭을 떼는 순간 발사 시도
        if (Mouse.current.leftButton.wasReleasedThisFrame && isDragging && grabbedTarget != null)
        {
            LaunchObjectForward();
        }
    }

    void HandleGrabbedObjectBehavior()
    {
        if (grabbedTarget != null)
        {
            // 들고 있는 동안 HP 회복
            if (playerHealth != null)
            {
                float healAmount =
                    hpHealRate *
                    grabbedTarget.weight *
                    Time.deltaTime;

                playerHealth.currentHp =
                    Mathf.Min(
                        playerHealth.maxHp,
                        playerHealth.currentHp + healAmount
                    );
            }

            Vector3 targetPos = transform.position;

            grabbedTarget.transform.position =
                Vector3.MoveTowards(
                    grabbedTarget.transform.position,
                    targetPos,
                    pullSpeed * Time.deltaTime
                );
        }
    }

    // 부채꼴 안의 물체를 조준하여 집기 '시작'하는 함수
    void TryStartGrabInSector()
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

            // 이미 날아가고 있거나 다른 곳에 잡힌 게 아니라면
            if (!target.isCaught && !target.isFlying)
            {
                // ⏳ 확실하게 타겟을 지정하고 타이머 가동
                targetBeingGrabbed = target;
                requiredGrabTime = baseGrabTime + (target.weight * grabTimePerWeight);
                currentGrabTimer = 0f;
                isGrabbingProgress = true;
                
                Debug.Log($"🎯 {col.gameObject.name} 집기 시작! [필요 시간: {requiredGrabTime:F2}초]");
                return; // 한 번에 하나의 물체만 타겟팅하도록 루프 탈출
            }
        }
    }

    // ⏳ 정해진 시간을 다 채워서 집기에 성공했을 때 호출되는 함수
    void CompleteGrab()
    {
        if (targetBeingGrabbed == null) return;

        grabbedTarget = targetBeingGrabbed;
        grabbedTarget.isCaught = true;

        // 💚 [보상 메커니즘] 무거운 물체일수록 피 회복을 많이 시켜줍니다!
        if (playerHealth != null)
        {
            float grabCost =grabbedTarget.weight *grabHpCostMultiplier;

            playerHealth.TakeDamage(grabCost);

            Debug.Log(
                $"{grabbedTarget.gameObject.name} 그랩 성공! HP {grabCost} 소모."
            );
        }

        Rigidbody2D targetRigid = grabbedTarget.GetComponent<Rigidbody2D>();
        if (targetRigid != null) targetRigid.simulated = false;

        Collider2D targetCollider = grabbedTarget.GetComponent<Collider2D>();
        if (targetCollider != null) targetCollider.enabled = false;

        isGrabbingProgress = false;
        targetBeingGrabbed = null;
    }

    // 집는 도중 취소되었을 때 초기화
    void CancelCurrentGrab()
    {
        isGrabbingProgress = false;
        targetBeingGrabbed = null;
        currentGrabTimer = 0f;
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

    // 🚀 발사 함수 (피 부족 시 떨어뜨리는 제약 조건 추가)
    void LaunchObjectForward()
    {
        isDragging = false;
        if (aimLine != null) aimLine.enabled = false;

        if (grabbedTarget == null) return;

        // 발사할 때 필요한 코스트 계산
        float immediateLaunchCost = grabbedTarget.weight * launchHpCostMultiplier;

        // 🛑 [제약 조건] 만약 현재 피가 발사 비용보다 작거나 같으면 발사 불가능! 그대로 바닥에 떨어뜨립니다.
        if (playerHealth == null || playerHealth.currentHp <= immediateLaunchCost)
        {
            Debug.LogWarning("🚨 HP가 부족하여 물체를 발사하지 못하고 제자리에 떨어뜨렸습니다!");
            ReleaseGrabbedObject(); // 발사하지 않고 그냥 툭 풀어줍니다.
            return;
        }

        // 🩸 피가 충분하므로 정상 발사 및 HP 차감
        playerHealth.TakeDamage(immediateLaunchCost);
        Debug.Log($"{grabbedTarget.gameObject.name} 발사 성공! 즉시 HP {immediateLaunchCost} 소모.");

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
            
            // 💡 제자리에 부드럽게 멈추도록 리지드바디 속도 초기화 후 놔주기
            targetRigid.linearVelocity = Vector2.zero;
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