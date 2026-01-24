using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class PixelPhysicsObject : MonoBehaviour
{
    [Header("Collision Settings")]
    public int pointsPerUnit = 15; // 충돌 감지 포인트 밀도 최적화
    public float bounceFactor = 0.2f; // 튀는 정도 줄임 (덜 덜덜거리게)
    public float friction = 0.1f; // 마찰 대폭 줄임 (잘 굴러가게)
    public float waterDrag = 0.9f; 

    [Header("Advanced Physics")]
    public int subSteps = 8; 
    public float sleepThreshold = 0.05f;
    public float skinWidth = 0.02f; // 보정 범위 약간 증가

    private Rigidbody2D rb;
    private Vector3[] checkPoints; 
    private float colliderRadius = 0.5f; // 기본값
    private bool isSleeping = false;

    [Header("Destruction Settings")]
    public bool destroyOnImpact = false;
    public int destructionRadius = 3;

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

        List<Vector3> points = new List<Vector3>();

        if (col is CircleCollider2D circle)
        {
            float radius = circle.radius;
            colliderRadius = radius;
            Vector2 offset = circle.offset;
            int count = Mathf.Max(16, Mathf.CeilToInt(2 * Mathf.PI * radius * pointsPerUnit));
            
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
            colliderRadius = Mathf.Min(size.x, size.y) * 0.5f; // 대략적인 반지름
            Vector2 offset = box.offset;
            float halfW = size.x / 2f;
            float halfH = size.y / 2f;
            int countX = Mathf.Max(4, Mathf.CeilToInt(size.x * pointsPerUnit));
            int countY = Mathf.Max(4, Mathf.CeilToInt(size.y * pointsPerUnit));

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
        
        // Removed custom sleep check


        float dt = Time.fixedDeltaTime / subSteps;

        for (int s = 0; s < subSteps; s++)
        {
            // 1. 예측 위치 계산
            Vector2 currentPos = transform.position;
            Vector2 nextPos = currentPos + rb.linearVelocity * dt;
            
            Vector2 totalNormal = Vector2.zero;
            int hitCount = 0;
            bool inWater = false;
            // Vector2 avgContactPoint = Vector2.zero; // Unused

            // 2. 충돌 감지
            foreach (Vector3 localPoint in checkPoints)
            {
                Vector3 worldPoint = transform.TransformPoint(localPoint) + (Vector3)(rb.linearVelocity * dt);
                int gx, gy;
                PixelSimulation.Instance.WorldToGrid(worldPoint, out gx, out gy);

                if (gx >= 0 && gx < PixelSimulation.Instance.width && gy >= 0 && gy < PixelSimulation.Instance.height)
                {
                    Pixel p = PixelSimulation.Instance.GetGrid()[gx, gy];
                    if (PixelSimulation.Instance.IsSolid(p.Type))
                    {
                        // 주변 4x4(반지름) 픽셀을 검사하여 부드러운 노멀 계산
                        totalNormal += CalculateNormal(gx, gy, 4);
                        // avgContactPoint += (Vector2)worldPoint;
                        hitCount++;
                    }
                    else if (p.Type == PixelType.Water) inWater = true;
                }
            }

            if (hitCount > 0)
            {
                if (destroyOnImpact)
                {
                    // 충돌 지점 주변 파괴 (위치 보정 전에 수행해야 함)
                    foreach (Vector3 localPoint in checkPoints)
                    {
                        Vector3 wp = transform.TransformPoint(localPoint) + (Vector3)(rb.linearVelocity * dt);
                        int gx, gy;
                        PixelSimulation.Instance.WorldToGrid(wp, out gx, out gy);
                        
                        if (gx >= 0 && gx < PixelSimulation.Instance.width && gy >= 0 && gy < PixelSimulation.Instance.height)
                        {
                            Pixel p = PixelSimulation.Instance.GetGrid()[gx, gy];
                            if (PixelSimulation.Instance.IsSolid(p.Type))
                            {
                                ApplyDestruction(gx, gy, destructionRadius); 
                                
                                // Digging Kick: Add random upward/sideways force to keep it lively
                                if (Random.value < 0.3f)
                                {
                                    Vector2 kick = Random.insideUnitCircle.normalized;
                                    kick.y = Mathf.Abs(kick.y) * 0.5f; // Bias upwards
                                    rb.AddForce(kick * 10f, ForceMode2D.Impulse);
                                } 
                            }
                        }
                    }
                }

                Vector2 normal = (totalNormal / hitCount).normalized;
                // avgContactPoint /= hitCount;

                if (normal == Vector2.zero) normal = Vector2.up;

                // 3. 임펄스 계산
                Vector2 relativeVel = rb.linearVelocity;
                float velAlongNormal = Vector2.Dot(relativeVel, normal);

                if (velAlongNormal < 0)
                {
                    float e = (relativeVel.magnitude < 1.0f) ? 0 : bounceFactor;
                    float j = -(1 + e) * velAlongNormal;
                    j /= (1.0f / rb.mass);

                    Vector2 impulse = j * normal;
                    rb.linearVelocity += impulse / rb.mass;

                    // 4. 마찰 및 회전
                    Vector2 tangent = relativeVel - (normal * Vector2.Dot(relativeVel, normal));
                    if (tangent.sqrMagnitude > 0.0001f)
                    {
                        tangent.Normalize();
                        float jt = -Vector2.Dot(relativeVel, tangent);
                        jt /= (1.0f / rb.mass);

                        float mu = friction;
                        float maxFriction = j * mu;
                        jt = Mathf.Clamp(jt, -maxFriction, maxFriction);

                        Vector2 frictionImpulse = jt * tangent;
                        rb.linearVelocity += frictionImpulse / rb.mass;
                    }
                }

                // 5. 위치 보정 (Iterative Depenetration) - 확실하게 밀어내기
                int maxIterations = 4;
                float nudgeDistance = 0.02f; // 2 pixels approx

                for(int k=0; k<maxIterations; k++)
                {
                    // 현재 위치에서 다시 충돌 체크
                    bool stillColliding = false;
                    foreach (Vector3 localPoint in checkPoints)
                    {
                        Vector3 wp = transform.TransformPoint(localPoint);
                        int gx, gy;
                        PixelSimulation.Instance.WorldToGrid(wp, out gx, out gy);
                        if (gx >= 0 && gx < PixelSimulation.Instance.width && gy >= 0 && gy < PixelSimulation.Instance.height)
                        {
                            if (PixelSimulation.Instance.IsSolid(PixelSimulation.Instance.GetGrid()[gx, gy].Type))
                            {
                                stillColliding = true;
                                break;
                            }
                        }
                    }

                    if (!stillColliding) break;

                    // 여전히 충돌 중이면 노멀 방향으로 밀어냄
                    transform.position += (Vector3)normal * nudgeDistance;
                }

                // 6. 자연스러운 구르기 (v = r * omega -> omega = v / r)
                // 표면의 접선 벡터
                Vector2 surfaceTangent = new Vector2(normal.y, -normal.x);
                // 접선 방향 속도
                float tangentialSpeed = Vector2.Dot(rb.linearVelocity, surfaceTangent);
                
                // 목표 각속도 (Degree/s)
                // v = r * w(rad) => w(rad) = v / r
                // w(deg) = (v / r) * Rad2Deg
                float targetAngularVel = -(tangentialSpeed / colliderRadius) * Mathf.Rad2Deg;

                // 급격한 변화 방지 (관성 느낌)
                rb.angularVelocity = Mathf.Lerp(rb.angularVelocity, targetAngularVel, 0.1f);

                // 7. 슬립 체크 (제거됨: rb.simulated = false가 중력까지 꺼버려서 공중부양 버그 유발)
                // 대신 속도가 매우 느리면 0으로 수렴하게 유도
                if (rb.linearVelocity.sqrMagnitude < (sleepThreshold * sleepThreshold))
                {
                    rb.linearVelocity *= 0.5f; // 감속
                    rb.angularVelocity *= 0.5f;
                }
            }

            if (inWater) rb.linearVelocity *= (1.0f - (1.0f - waterDrag) / subSteps);
            
            // --- Wall / Boundary Logic (Keep inside screen interactively) ---
            Vector2 pos = rb.position + rb.linearVelocity * dt;
            float ppu = 100f;
            float halfWidth = (PixelSimulation.Instance.width / ppu) * 0.5f;
            
            // Hard Bounce off Walls
            if (pos.x < -halfWidth + colliderRadius)
            {
                pos.x = -halfWidth + colliderRadius;
                if (rb.linearVelocity.x < 0) rb.linearVelocity = new Vector2(-rb.linearVelocity.x * 0.9f, rb.linearVelocity.y);
            }
            else if (pos.x > halfWidth - colliderRadius)
            {
                pos.x = halfWidth - colliderRadius;
                if (rb.linearVelocity.x > 0) rb.linearVelocity = new Vector2(-rb.linearVelocity.x * 0.9f, rb.linearVelocity.y);
            }
        }
    }
    
    void ApplyDestruction(int cx, int cy, int r)
    {
        for (int y = cy - r; y <= cy + r; y++)
        {
            for (int x = cx - r; x <= cx + r; x++)
            {
                 if (x >= 0 && x < PixelSimulation.Instance.width && y >= 0 && y < PixelSimulation.Instance.height)
                 {
                     float d = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                     if (d <= r * r)
                     {
                         PixelSimulation.Instance.SetPixel(x, y, PixelType.Empty);
                     }
                 }
            }
        }
    }

    Vector2 CalculateNormal(int cx, int cy, int radius)
    {
        Vector2 normal = Vector2.zero;
        Pixel[,] grid = PixelSimulation.Instance.GetGrid();
        int width = PixelSimulation.Instance.width;
        int height = PixelSimulation.Instance.height;

        // 가우시안 느낌의 가중치로 가까운 빈 공간 쪽으로 노멀 유도
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x == 0 && y == 0) continue;

                int nx = cx + x;
                int ny = cy + y;

                bool isEmpty = false;
                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    Pixel p = grid[nx, ny];
                    if (!PixelSimulation.Instance.IsSolid(p.Type)) isEmpty = true;
                }
                
                if (isEmpty)
                {
                    float distSq = x * x + y * y;
                    // 가까운 빈 공간일수록 강하게 당김 (Solid -> Empty 방향이 Normal)
                    float weight = 1.0f / (1.0f + distSq); 
                    normal += new Vector2(x, y) * weight;
                }
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
