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
    
    [Header("Bounce Speed Settings")]
    public float minBounceSpeed = 4f; // 최소 반동 속도 (멈춤 방지)
    public float maxBounceSpeed = 6f; // 최대 반동 속도 (어지러움 방지) 

    [Header("Advanced Physics")]
    public int subSteps = 8; 
    public float sleepThreshold = 0.05f;
    public float skinWidth = 0.02f; // 보정 범위 약간 증가

    private Rigidbody2D rb;
    private Vector3[] checkPoints; 
    private float colliderRadius = 0.5f; // 기본값
    private bool isSleeping = false;

    [Header("Destruction Settings")]
    public bool destroyOnImpact = true; // Changed default to TRUE to ensure it works immediately
    public int destructionRadius = 8; 

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
            float radius = circle.radius * 0.9f; 
            colliderRadius = radius;
            
            // Auto-calculate destruction radius based on size
            if (destroyOnImpact)
            {
                destructionRadius = Mathf.CeilToInt(circle.radius * 100f * 1.1f); 
            }

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
            Vector2 size = box.size * 0.9f; 
            colliderRadius = Mathf.Min(size.x, size.y) * 0.5f; 
            
            // Auto-calculate for Box as well
            if (destroyOnImpact)
            {
                float maxDim = Mathf.Max(box.size.x, box.size.y);
                destructionRadius = Mathf.CeilToInt((maxDim * 0.5f) * 100f * 1.1f);
            }

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
        
        float dt = Time.fixedDeltaTime / subSteps;

        for (int s = 0; s < subSteps; s++)
        {
            // 1. Predict
            Vector2 currentPos = transform.position;
            Vector2 nextPos = currentPos + rb.linearVelocity * dt;
            
            Vector2 totalNormal = Vector2.zero;
            int hitCount = 0;
            bool inWater = false;

            // 2. Collision Check (Reactive: Check current overlap)
            foreach (Vector3 localPoint in checkPoints)
            {
                // Removed predictive term (+ amt) to prevent air-bouncing
                Vector3 worldPoint = transform.TransformPoint(localPoint); 
                int gx, gy;
                PixelSimulation.Instance.WorldToGrid(worldPoint, out gx, out gy);

                if (gx >= 0 && gx < PixelSimulation.Instance.width && gy >= 0 && gy < PixelSimulation.Instance.height)
                {
                    Pixel p = PixelSimulation.Instance.GetGrid()[gx, gy];
                    if (PixelSimulation.Instance.IsSolid(p.Type))
                    {
                        totalNormal += CalculateNormal(gx, gy, 4);
                        hitCount++;
                    }
                    else if (p.Type == PixelType.Water) inWater = true;
                }
            }

            if (hitCount > 0)
            {
                if (destroyOnImpact)
                {
                    bool excavated = false;
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
                                if (ApplyDestruction(gx, gy, destructionRadius))
                                {
                                    excavated = true;
                                }
                                
                                if (Random.value < 0.3f && !excavated) // Only kick if NOT penetrating
                                {
                                    Vector2 kick = Random.insideUnitCircle.normalized;
                                    kick.y = Mathf.Abs(kick.y) * 0.5f; 
                                    rb.AddForce(kick * 10f, ForceMode2D.Impulse);
                                } 
                            }
                        }
                    }

                    // Removed "Plow through" logic as per user request.
                    // Now it will destroy pixels AND then proceed to Bounce logic below.
                }

                Vector2 normal = (totalNormal / hitCount).normalized;
                if (normal == Vector2.zero) normal = Vector2.up;

                // 3. Bounce Logic
                Vector2 relativeVel = rb.linearVelocity;
                float velAlongNormal = Vector2.Dot(relativeVel, normal);

                if (velAlongNormal < 0)
                {
                    HandleBounce(normal);
                }

                // 4. Depenetration
                int maxIterations = 4;
                float nudgeDistance = 0.02f; 

                for(int k=0; k<maxIterations; k++)
                {
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
                    transform.position += (Vector3)normal * nudgeDistance;
                }
            }

            if (inWater) rb.linearVelocity *= (1.0f - (1.0f - waterDrag) / subSteps);
            
            // --- Wall / Boundary Logic ---
            Vector2 pos = rb.position + rb.linearVelocity * dt;
            float ppu = 100f;
            float halfWidth = (PixelSimulation.Instance.width / ppu) * 0.5f;
            
            // Wall Bounce
            if (pos.x < -halfWidth + colliderRadius)
            {
                pos.x = -halfWidth + colliderRadius;
                if (rb.linearVelocity.x < 0) HandleBounce(Vector2.right);
            }
            else if (pos.x > halfWidth - colliderRadius)
            {
                pos.x = halfWidth - colliderRadius;
                if (rb.linearVelocity.x > 0) HandleBounce(Vector2.left);
            }
        }
    }

    // New infinite bounce logic
    void HandleBounce(Vector2 normal)
    {
        // Reflect velocity
        Vector2 incomingVel = rb.linearVelocity;
        Vector2 reflected = Vector2.Reflect(incomingVel, normal);

        // Enforce Minimum Bounce Speed (Anti-Stop)
        float currentSpeed = reflected.magnitude;
        float targetSpeed = Mathf.Max(currentSpeed, minBounceSpeed); 
        
        // Clamp Maximum Speed (Anti-Dizzy)
        targetSpeed = Mathf.Min(targetSpeed, maxBounceSpeed);

        // Apply
        rb.linearVelocity = reflected.normalized * targetSpeed;
        
        // Removed random torque to keep inertia feeling natural
        // rb.angularVelocity = Random.Range(-180f, 180f);
    }

    
    [Header("Sawblade Settings")]
    public bool isSawblade = false;
    public float rotationSpeed = 1000f; // Degrees per second

    void Update()
    {
        if (isSawblade)
        {
            // Visual Rotation only (Physics rotation might interfere with collision shapes if not circular)
            // But since it's a sawblade, we likely want the RB to rotate if it's a circle collider
            transform.Rotate(0, 0, -rotationSpeed * Time.deltaTime);
        }
    }

    bool ApplyDestruction(int cx, int cy, int r)
    {
        bool destroyedAny = false;
        // Increase effective radius significantly as per user request (2x)
        int effectRadius = Mathf.CeilToInt(r * 2.5f); 
        
        for (int y = cy - effectRadius; y <= cy + effectRadius; y++)
        {
            for (int x = cx - effectRadius; x <= cx + effectRadius; x++)
            {
                 if (x >= 0 && x < PixelSimulation.Instance.width && y >= 0 && y < PixelSimulation.Instance.height)
                 {
                     float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                     if (d <= effectRadius)
                     {
                         if (PixelSimulation.Instance.GetGrid()[x, y].Type != PixelType.Empty)
                         {
                             // Damage Gradient
                             // Center (0 to r) -> INSTANT KILL (10000 damage)
                             // Edge (r to effectRadius) -> Gradient Damage
                             float damage = 0f;

                             if (d <= r) 
                             {
                                 damage = 10000f; // Force destroy
                             }
                             else
                             {
                                 float damageRatio = 1.0f - ((d - r) / (effectRadius - r));
                                 damage = damageRatio * 200f; // Charring outside the hole
                             }

                             PixelSimulation.Instance.DamagePixel(x, y, damage);

                             // Check if it was actually destroyed to count as "excavated"
                             if (PixelSimulation.Instance.GetGrid()[x, y].Type == PixelType.Empty)
                             {
                                 // Only count as excavated if it was within the original collider radius
                                 // This ensures we only penetrate if we clear the path, not just scratch the edges
                                 if (d <= r) 
                                 {
                                     destroyedAny = true;

                                     // SAWBLADE EFFECT: GRINDING PARTICLES
                                     if (isSawblade && Random.value < 0.2f) // 20% chance per pixel
                                     {
                                         // Spawn Fire/Smoke at the destruction point
                                         PixelType particleType = Random.value < 0.5f ? PixelType.Fire : PixelType.Smoke;
                                         PixelSimulation.Instance.SetPixel(x, y, particleType);
                                         
                                         // Shoot separate particles backwards (Spark spray)
                                         // Calculate direction from center to pixel (this is the collision normal roughly)
                                         Vector2 dir = (new Vector2(x, y) - new Vector2(cx, cy)).normalized;
                                         
                                         // Spray direction is roughly tangent or reflected? 
                                         // Let's just spray them out from the contact point + random noise
                                         // Actually, visually "sparks" usually fly opposite to surface velocity.
                                         // Simple approach: Shoot them away from center first
                                         
                                         if (PixelSimulation.Instance.GetGrid()[x, y].Type != PixelType.Empty) // If SetPixel succeeded (it might strict check?)
                                         {
                                              PixelSimulation.Instance.GetGrid()[x, y].Velocity = dir * Random.Range(5f, 15f);
                                              PixelSimulation.Instance.GetGrid()[x, y].Life = Random.Range(10f, 30f); // Short life
                                         }
                                     }
                                 }
                             }
                         }
                     }
                 }
            }
        }
        return destroyedAny;
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
