using UnityEngine;

public class DrillUnit : MonoBehaviour
{
    public PixelSimulation simulation;
    
    [Header("Movement")]
    public float maxSpeed = 5f;
    public float acceleration = 10f;
    public float deceleration = 5f; 

    [Header("Mining")]
    public int drillPower = 3; 
    public int drillSize = 18; 
    public int drillSpeed = 100;

    public Transform visualTransform; 
    private bool isDrilling = false;

    private Vector3 velocity;
    private float ppu = 100f;
    private float unitRadius = 0.16f; 

    void Start()
    {
        if (simulation == null)
            simulation = FindObjectOfType<PixelSimulation>();
        
        if (visualTransform == null)
        {
            Transform child = transform.Find("Visual");
            if (child != null) visualTransform = child;
        }
    }

    void Update()
    {
        if (simulation == null) return;

        isDrilling = false; 

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
        
        if (CheckAreaCollision(nextPos, unitRadius))
        {
            velocity = Vector3.zero;
            ProcessDrill(nextPos);
            isDrilling = true;
        }
        else
        {
            transform.position = nextPos;
        }

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
                visualTransform.localPosition = (Vector3)Random.insideUnitCircle * 0.05f;
            }
            else
            {
                visualTransform.localPosition = Vector3.zero;
            }
        }
    }

    bool CheckAreaCollision(Vector3 centerPos, float radius)
    {
        int cx, cy;
        WorldToGrid(centerPos, out cx, out cy);
        int r = Mathf.CeilToInt(radius * ppu); 

        for (int x = cx - r; x <= cx + r; x++)
        {
            for (int y = cy - r; y <= cy + r; y++)
            {
                if (x >= 0 && x < simulation.width && y >= 0 && y < simulation.height)
                {
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r)
                    {
                        Pixel p = simulation.GetGrid()[x, y];
                        // Removed Mineral Reference
                        if (p.Type == PixelType.Sand || p.Type == PixelType.Stone || simulation.IsSolid(p.Type))
                        {
                            return true; 
                        }
                    }
                }
            }
        }
        return false;
    }

    void ProcessDrill(Vector3 worldPos)
    {
        int cx, cy;
        WorldToGrid(worldPos, out cx, out cy);

        for (int i = 0; i < drillSpeed; i++)
        {
            Vector2 randomPoint = Random.insideUnitCircle * drillSize;
            int tx = cx + Mathf.RoundToInt(randomPoint.x);
            int ty = cy + Mathf.RoundToInt(randomPoint.y);

            if (tx >= 0 && tx < simulation.width && ty >= 0 && ty < simulation.height)
            {
                Pixel p = simulation.GetGrid()[tx, ty];
                if (p.Type != PixelType.Empty && p.Type != PixelType.Water)
                {
                    int hardness = simulation.GetHardness(p.Type);
                    if (drillPower < hardness) continue;

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
