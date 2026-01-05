using UnityEngine;

public class DrillUnit : MonoBehaviour
{
    public PixelSimulation simulation;
    
    [Header("Movement")]
    public float maxSpeed = 5f;
    public float acceleration = 10f;
    public float deceleration = 5f; // 감속 (Drag)

    [Header("Mining")]
    public int drillPower = 3; 
    public int drillSize = 18; // 채굴 범위를 충돌 범위(16)보다 약간 크게 설정하여 여유 공간 확보
    public int drillSpeed = 100; // 채굴 속도 상향 (잔존물 빠른 제거)

    public Transform visualTransform; // 시각적 효과를 담당할 자식 오브젝트
    private bool isDrilling = false;

    private Vector3 velocity;
    private float ppu = 100f;
    private float unitRadius = 0.16f; // 충돌 반지름 (16픽셀)

    void Start()
    {
        if (simulation == null)
            simulation = FindObjectOfType<PixelSimulation>();
        
        // Visual 트랜스폼이 할당되지 않았으면 자식에서 찾기
        if (visualTransform == null)
        {
            Transform child = transform.Find("Visual");
            if (child != null) visualTransform = child;
        }
    }

    void Update()
    {
        if (simulation == null) return;

        isDrilling = false; // 매 프레임 초기화

        HandleMovement();
        MoveAndCollide();
        UpdateVisuals();
    }

    void HandleMovement()
    {
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputY = Input.GetAxisRaw("Vertical");
        Vector3 inputDir = new Vector3(inputX, inputY, 0).normalized;

        if (inputDir.magnitude > 0)
        {
            velocity += inputDir * acceleration * Time.deltaTime;
            velocity = Vector3.ClampMagnitude(velocity, maxSpeed);
        }
        else
        {
            if (velocity.magnitude > 0)
            {
                float drop = deceleration * Time.deltaTime;
                if (velocity.magnitude <= drop) velocity = Vector3.zero;
                else velocity -= velocity.normalized * drop;
            }
        }
    }

    void MoveAndCollide()
    {
        if (velocity == Vector3.zero) return;

        float dt = Time.deltaTime;
        Vector3 nextPos = transform.position + velocity * dt;
        
        // 엄격한 영역 충돌 체크
        if (CheckAreaCollision(nextPos, unitRadius))
        {
            // 충돌 발생!
            velocity = Vector3.zero;
            
            // 장애물 제거 시도
            ProcessDrill(nextPos);
            
            // 진동 플래그 설정
            isDrilling = true;
        }
        else
        {
            // 장애물 없음 -> 이동
            transform.position = nextPos;
        }

        // 회전
        if (velocity.sqrMagnitude > 0.1f)
        {
            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    void UpdateVisuals()
    {
        if (visualTransform != null)
        {
            if (isDrilling)
            {
                // 진동 효과 (로컬 위치만 변경)
                visualTransform.localPosition = (Vector3)Random.insideUnitCircle * 0.05f;
            }
            else
            {
                // 원위치 복귀
                visualTransform.localPosition = Vector3.zero;
            }
        }
    }

    // 특정 위치의 반경 내에 고체가 있는지 전수 조사 (엄격한 체크)
    bool CheckAreaCollision(Vector3 centerPos, float radius)
    {
        int cx, cy;
        WorldToGrid(centerPos, out cx, out cy);
        int r = Mathf.CeilToInt(radius * ppu); // 월드 반지름 -> 픽셀 반지름

        // 바운딩 박스 내 픽셀 순회
        for (int x = cx - r; x <= cx + r; x++)
        {
            for (int y = cy - r; y <= cy + r; y++)
            {
                if (x >= 0 && x < simulation.width && y >= 0 && y < simulation.height)
                {
                    // 원형 범위 체크
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r)
                    {
                        Pixel p = simulation.GetGrid()[x, y];
                        if (p.Type == PixelType.Sand || p.Type == PixelType.Stone || p.Type == PixelType.Mineral)
                        {
                            return true; // 하나라도 걸리면 true
                        }
                    }
                }
            }
        }
        return false;
    }

    // 침식(Grinding) 채굴 로직
    void ProcessDrill(Vector3 worldPos)
    {
        int cx, cy;
        WorldToGrid(worldPos, out cx, out cy);

        // 드릴 범위 내에서 무작위 픽셀 선택하여 파괴 시도
        for (int i = 0; i < drillSpeed; i++)
        {
            // 원형 범위 내 무작위 좌표
            Vector2 randomPoint = Random.insideUnitCircle * drillSize;
            int tx = cx + Mathf.RoundToInt(randomPoint.x);
            int ty = cy + Mathf.RoundToInt(randomPoint.y);

            if (tx >= 0 && tx < simulation.width && ty >= 0 && ty < simulation.height)
            {
                Pixel p = simulation.GetGrid()[tx, ty];
                if (p.Type != PixelType.Empty && p.Type != PixelType.Water)
                {
                    int hardness = simulation.GetHardness(p.Type);
                    
                    // 파워가 부족하면 못 깸
                    if (drillPower < hardness) continue;

                    // 확률적 파괴 (경도가 높을수록 확률 낮음)
                    // 예: 경도 1 -> 100%, 경도 2 -> 50%, 경도 3 -> 33%
                    float chance = 1f / hardness; 
                    
                    if (Random.value <= chance)
                    {
                        simulation.SetPixel(tx, ty, PixelType.Empty);
                    }
                }
            }
        }
    }

    void WorldToGrid(Vector3 worldPos, out int x, out int y)
    {
        float worldWidth = simulation.width / ppu;
        float worldHeight = simulation.height / ppu;
        
        float localX = worldPos.x + (worldWidth / 2f);
        float localY = worldPos.y + (worldHeight / 2f);

        x = Mathf.FloorToInt(localX * ppu);
        y = Mathf.FloorToInt(localY * ppu);
    }
}
