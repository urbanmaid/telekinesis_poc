using UnityEngine;
using UnityEngine.InputSystem; 
using System.Collections.Generic;

public class PlayerTelekinesis : MonoBehaviour
{
    [Header("염력 기본 설정")]
    public float grabRange = 3.5f;        
    public float grabAngle = 90f;         
    public float pullSpeed = 15f;         // 🔄 움직일 때 물체가 더 빠르고 단단하게 따라붙도록 속도 상향
    public float maxLaunchForce = 50f;    // 🚀 무거운 물체도 시원하게 날리기 위해 한계치 상향

    [Header("새총 조작 및 민감도")]
    public float pullSensitivity = 6.0f;  // 🎯 살짝만 당겨도 힘이 모이게 민감도 소폭 상향
    public float distanceScale = 0.25f;   

    [Header("무게 관련 가중치")]
    public float baseForceMultiplier = 12f; // 🏹 무거운 물체 보정을 위해 기본 탄성 배율 상향
    public float hpDrainRate = 2f;         

    [Header("UI 및 라인 연결")]
    public LineRenderer rangeLine;        
    public LineRenderer aimLine;          
    public Transform crosshairObject;     

    private TelekinesisTarget grabbedTarget; 
    private Health playerHealth;              
    private bool isDragging = false;      
    private const int sectorSegments = 30; 

    // 💡 무빙 샷 좌표 꼬임 방지를 위해, 화면 마우스 좌표 자체를 기억하는 방식으로 변경
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

    // 🖱️ 좌클릭 입력 처리 (무빙 대응 완전 개편)
    void HandleLeftClickInput()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame && grabbedTarget == null)
        {
            TryGrabObjectInSector();
        }
        // 💡 조준을 누르기 시작한 순간의 '마우스 화면 좌표'를 기준으로 박아둡니다.
        else if (Mouse.current.leftButton.wasPressedThisFrame && grabbedTarget != null)
        {
            dragStartMouseScreenPos = Mouse.current.position.ReadValue(); 
            isDragging = true;
            if (aimLine != null) aimLine.enabled = true;
        }

        // 드래그 조준 중일 때
        if (isDragging && grabbedTarget != null)
        {
            UpdateForwardAimLine();
        }

        // 클릭을 떼는 순간 발사
        if (Mouse.current.leftButton.wasReleasedThisFrame && isDragging && grabbedTarget != null)
        {
            LaunchObjectForward();
        }
    }

    void HandleGrabbedObjectBehavior()
    {
        if (grabbedTarget != null)
        {
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

            // 💡 [버그 수정 1] 이제 좌클릭 조준(isDragging) 중이더라도, 
            // 발사하기 직전까지 물체는 무조건 플레이어 본체 위치에 강제로 동기화되어 같이 움직입니다!
            Vector3 targetPos = transform.position; 
            
            // 더 완벽한 자석 효과를 위해 Lerp 대신 MoveTowards를 섞어 거리가 벌어지지 않게 타이트하게 고정
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

            if (!target.isCaught && !target.isFlying && playerHealth != null && playerHealth.currentHp > target.weight)
            {
                grabbedTarget = target;
                grabbedTarget.isCaught = true;
                
                Rigidbody2D targetRigid = grabbedTarget.GetComponent<Rigidbody2D>();
                if (targetRigid != null) targetRigid.simulated = false;

                Collider2D targetCollider = grabbedTarget.GetComponent<Collider2D>();
                if (targetCollider != null) targetCollider.enabled = false;
                break; 
            }
        }
    }

    // 🏹 실시간 무빙 좌표 가중치가 완벽 반영된 정방향 조준선
    void UpdateForwardAimLine()
    {
        if (aimLine == null || grabbedTarget == null) return;

        // 플레이어 캐릭터가 움직이더라도 순수 마우스 드래그 변위만 뽑아냅니다.
        Vector2 currentMouseScreenPos = Mouse.current.position.ReadValue();
        
        // 화면 좌표계 상에서 마우스를 얼마나 뒤로 당겼는지 픽셀 벡터 계산
        Vector2 screenDragVector = dragStartMouseScreenPos - currentMouseScreenPos;
        
        // 이를 월드 단위 스케일로 부드럽게 매핑하기 위해 화면 해상도(스크린 너비) 기준으로 정규화 보정
        Vector2 dragVector = (screenDragVector / Screen.width) * 15f; 

        Vector2 amplifiedDrag = dragVector * pullSensitivity;

        // 무게 페널티 적용
        float finalMultiplier = baseForceMultiplier / Mathf.Max(1f, grabbedTarget.weight);
        Vector2 launchForce = amplifiedDrag * finalMultiplier;

        if (launchForce.magnitude > maxLaunchForce)
        {
            launchForce = launchForce.normalized * maxLaunchForce;
        }

        Vector3 finalAimPath = (Vector3)(launchForce * distanceScale);

        // 💡 [버그 수정 2] 이제 내가 실시간으로 걷고 뛰더라도, 조준선의 시작점과 목적지가 내 몸을 완벽히 정방향으로 쫓아옵니다.
        aimLine.SetPosition(0, transform.position);
        aimLine.SetPosition(1, transform.position + finalAimPath);
    }

    // 🚀 무거운 물체도 정확히 내가 보고 있는 방향으로 무빙 샷 발사
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

        Vector3 finalAimPath = (Vector3)(launchForce * distanceScale);
        
        // 💡 발사 직전 최종적으로 같이 움직여온 플레이어의 '현재 리얼타임 위치' 기준으로 목적지를 계산하여 전달합니다!
        Vector2 targetDestination = (Vector2)transform.position + (Vector2)finalAimPath;

        Collider2D targetCollider = grabbedTarget.GetComponent<Collider2D>();
        if (targetCollider != null) targetCollider.enabled = true;

        // 물체에게 완벽하게 정렬된 목적지 주입
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