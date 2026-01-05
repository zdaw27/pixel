using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PixelPhysicsObject : MonoBehaviour
{
    [Header("Collision Settings")]
    public int pointsPerUnit = 10; // 유닛당 생성할 포인트 수 (정밀도)
    public float bounceFactor = 0.5f; // 반발 계수
    public float friction = 0.5f; // 마찰 계수 (회전 감쇠 등)
    public float waterDrag = 0.9f; // 물속 저항

    [Header("Advanced Physics")]
    public int subSteps = 8; // 정밀도 대폭 상향
    public float sleepThreshold = 0.05f;
    public float skinWidth = 0.01f; // 예측 충돌을 위한 여유 공간

    private Rigidbody2D rb;
    private Vector3[] checkPoints; 
    private bool isSleeping = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        GenerateCollisionPoints();
    }

    void GenerateCollisionPoints()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            checkPoints = new Vector3[] { Vector3.zero };
            return;
        }

        System.Collections.Generic.List<Vector3> points = new System.Collections.Generic.List<Vector3>();

        if (col is CircleCollider2D circle)
        {
            float radius = circle.radius;
            Vector2 offset = circle.offset;
            int count = Mathf.Max(12, Mathf.CeilToInt(2 * Mathf.PI * radius * pointsPerUnit));
            
            for (int i = 0; i < count; i++)
            {
                float angle = i * (2 * Mathf.PI / count);
                points.Add(offset + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            points.Add(offset);
        }
        else if (col is BoxCollider2D box)
        {
            Vector2 size = box.size;
            Vector2 offset = box.offset;
            float halfW = size.x / 2f;
            float halfH = size.y / 2f;
            int countX = Mathf.Max(3, Mathf.CeilToInt(size.x * pointsPerUnit));
            int countY = Mathf.Max(3, Mathf.CeilToInt(size.y * pointsPerUnit));

            for (int i = 0; i <= countX; i++)
            {
                float t = (float)i / countX;
                points.Add(offset + new Vector2(Mathf.Lerp(-halfW, halfW, t), halfH));
                points.Add(offset + new Vector2(Mathf.Lerp(-halfW, halfW, t), -halfH));
            }
            for (int i = 1; i < countY; i++)
            {
                float t = (float)i / countY;
                points.Add(offset + new Vector2(-halfW, Mathf.Lerp(-halfH, halfH, t)));
                points.Add(offset + new Vector2(halfW, Mathf.Lerp(-halfH, halfH, t)));
            }
            points.Add(offset);
        }

        checkPoints = points.ToArray();
    }

    void FixedUpdate()
    {
        if (PixelSimulation.Instance == null) return;
        
        if (isSleeping)
        {
            if (rb.linearVelocity.sqrMagnitude > 0.1f || rb.angularVelocity > 10f)
            {
                isSleeping = false;
                rb.simulated = true;
            }
            else return;
        }

        float dt = Time.fixedDeltaTime / subSteps;

        for (int s = 0; s < subSteps; s++)
        {
            // 1. 예측 위치 계산 (Predictive)
            Vector2 currentPos = transform.position;
            Vector2 nextPos = currentPos + rb.linearVelocity * dt;
            
            Vector2 totalNormal = Vector2.zero;
            int hitCount = 0;
            bool inWater = false;

            // 2. 충돌 감지 (예측 위치 기준)
            foreach (Vector3 localPoint in checkPoints)
            {
                Vector3 worldPoint = transform.TransformPoint(localPoint) + (Vector3)(rb.linearVelocity * dt);
                int gx, gy;
                PixelSimulation.Instance.WorldToGrid(worldPoint, out gx, out gy);

                if (gx >= 0 && gx < PixelSimulation.Instance.width && gy >= 0 && gy < PixelSimulation.Instance.height)
                {
                    Pixel p = PixelSimulation.Instance.GetGrid()[gx, gy];
                    if (p.Type == PixelType.Sand || p.Type == PixelType.Stone || p.Type == PixelType.Mineral || p.Type == PixelType.Bomb)
                    {
                        totalNormal += CalculateNormal(gx, gy, 5);
                        hitCount++;
                    }
                    else if (p.Type == PixelType.Water) inWater = true;
                }
            }

            if (hitCount > 0)
            {
                Vector2 normal = (totalNormal / hitCount).normalized;
                if (normal == Vector2.zero) normal = Vector2.up;

                // 3. 임펄스 솔버 (Impulse Solver)
                Vector2 relativeVel = rb.linearVelocity;
                float velAlongNormal = Vector2.Dot(relativeVel, normal);

                // 이미 멀어지고 있다면 무시
                if (velAlongNormal < 0)
                {
                    // 반발 임펄스 계산 (J = -(1+e) * v_n / (1/m))
                    float e = (relativeVel.magnitude < 1.0f) ? 0 : bounceFactor;
                    float j = -(1 + e) * velAlongNormal;
                    j /= (1.0f / rb.mass); // m1만 고려 (지형은 무한 질량)

                    Vector2 impulse = j * normal;
                    rb.linearVelocity += impulse / rb.mass;

                    // 4. 마찰 임펄스 (Coulomb Friction)
                    Vector2 tangent = relativeVel - (normal * Vector2.Dot(relativeVel, normal));
                    if (tangent.sqrMagnitude > 0.0001f)
                    {
                        tangent.Normalize();
                        float jt = -Vector2.Dot(relativeVel, tangent);
                        jt /= (1.0f / rb.mass);

                        // 마찰 계수 제한 (jt <= j * friction)
                        float mu = friction;
                        float maxFriction = j * mu;
                        jt = Mathf.Clamp(jt, -maxFriction, maxFriction);

                        Vector2 frictionImpulse = jt * tangent;
                        rb.linearVelocity += frictionImpulse / rb.mass;
                    }
                }

                // 5. 위치 보정 (파묻힘 방지 - Penetration Correction)
                // 예측 위치가 파묻혔으므로 현재 위치에서 살짝 밀어냄
                transform.position += (Vector3)normal * skinWidth;

                // 6. 구르기 회전 (마찰에 의한 토크)
                Vector2 surfaceTangent = new Vector2(normal.y, -normal.x);
                float rollVel = Vector2.Dot(rb.linearVelocity, surfaceTangent);
                rb.angularVelocity = Mathf.Lerp(rb.angularVelocity, -rollVel * 1200f, 0.2f);

                // 7. 수면 체크
                if (rb.linearVelocity.magnitude < sleepThreshold && Mathf.Abs(rb.angularVelocity) < 5f)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0;
                    isSleeping = true;
                    rb.simulated = false;
                    break;
                }
            }

            if (inWater) rb.linearVelocity *= (1.0f - (1.0f - waterDrag) / subSteps);
        }
    }

    Vector2 CalculateNormal(int cx, int cy, int radius)
    {
        Vector2 normal = Vector2.zero;
        Pixel[,] grid = PixelSimulation.Instance.GetGrid();
        int width = PixelSimulation.Instance.width;
        int height = PixelSimulation.Instance.height;

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                int nx = cx + x;
                int ny = cy + y;

                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    Pixel p = grid[nx, ny];
                    if (p.Type == PixelType.Empty || p.Type == PixelType.Water || p.Type == PixelType.Gas || p.Type == PixelType.Fire || p.Type == PixelType.Smoke)
                    {
                        float distSq = x * x + y * y;
                        float weight = Mathf.Exp(-distSq / (radius * radius));
                        normal += new Vector2(x, y) * weight;
                    }
                }
                else normal += new Vector2(x, y);
            }
        }
        return normal.normalized;
    }

    void OnDrawGizmos()
    {
        if (checkPoints == null) return;
        Gizmos.color = Color.yellow;
        foreach (Vector3 pt in checkPoints)
        {
            Gizmos.DrawSphere(transform.TransformPoint(pt), 0.01f);
        }
    }
}
