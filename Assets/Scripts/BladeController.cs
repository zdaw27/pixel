using UnityEngine;

[RequireComponent(typeof(PixelPhysicsObject))]
[RequireComponent(typeof(Rigidbody2D))]
public class BladeController : MonoBehaviour
{
    [Header("Blade Stats")]
    public float damage = 50f; 
    public Color normalColor = Color.white;
    public float maxSpeed = 12f; // Increased max speed slightly

    private PixelPhysicsObject ppo;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    
    // Enlarge Skill State
    private float originalScale = 1f;
    private bool isEnlarged = false;
    private float enlargeTimeLeft;

    void Start()
    {
        ppo = GetComponent<PixelPhysicsObject>();
        rb = GetComponent<Rigidbody2D>();
        
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) spriteRenderer.color = normalColor;

        // Balanced Physics
        ppo.bounceFactor = 0.9f; // Bouncy again (was 0.5, originally 1.0)
        ppo.friction = 0.1f;     // Low friction to slide (was 0.4)
        ppo.isSawblade = true;   // Enable sawblade visuals (Rotation + Sparks)
        
        rb.gravityScale = 1.0f;  // Normal gravity
        rb.linearDamping = 0.1f;    // Low air resistance
        
        originalScale = transform.localScale.x;
        
        // Initial Impulse
        if (rb.linearVelocity.sqrMagnitude < 0.1f)
        {
            float randomX = Random.Range(-2f, 2f);
            rb.linearVelocity = new Vector2(randomX, 0);
        }
    }

    void Update()
    {
        // Mining Logic
        MineSurroundings(damage * Time.deltaTime);

        // Cap Speed (soft cap)
        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }

        // Enlarge Logic
        if (isEnlarged)
        {
            enlargeTimeLeft -= Time.deltaTime;
            if (enlargeTimeLeft <= 0)
            {
                transform.localScale = Vector3.one * originalScale;
                isEnlarged = false;
            }
        }
        
        // Keep it moving if it stops
        if (rb.linearVelocity.sqrMagnitude < 0.1f)
        {
             rb.linearVelocity = Random.insideUnitCircle.normalized * 6f;
        }
    }

    public void Enlarge(float duration)
    {
        if (!isEnlarged)
        {
            originalScale = transform.localScale.x;
            transform.localScale = Vector3.one * originalScale * 2f;
            isEnlarged = true;
        }
        enlargeTimeLeft = duration;
    }

    
    // Trajectory Visualization
    private LineRenderer trajectoryLine;
    
    public void ShowTrajectory(Vector2 direction)
    {
        if (trajectoryLine == null)
        {
            GameObject lineObj = new GameObject("TrajectoryLine");
            lineObj.transform.SetParent(transform, false);
            trajectoryLine = lineObj.AddComponent<LineRenderer>();
            trajectoryLine.startWidth = 0.1f;
            trajectoryLine.endWidth = 0.0f; // Arrow shape
            trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));
            trajectoryLine.startColor = Color.yellow;
            trajectoryLine.endColor = Color.red;
            trajectoryLine.positionCount = 2;
            trajectoryLine.sortingOrder = 100;
        }
        
        trajectoryLine.enabled = true;
        trajectoryLine.SetPosition(0, Vector3.zero); // Local space
        trajectoryLine.SetPosition(1, (Vector3)direction * 2f); // Length
    }
    
    public void HideTrajectory()
    {
        if (trajectoryLine != null) trajectoryLine.enabled = false;
    }
    
    public void ApplyExternalForce(Vector2 force)
    {
        if (rb != null)
        {
            rb.linearVelocity = force; // Set velocity directly for "Punch" feel or AddForce?
            // "Give vector certain force" usually means AddForce or SetVelocity.
            // If we want total control, SetVelocity is often cleaner for "Flicking".
            // Let's AddForce for now, or Mix.
            // User said "Give that vector size", implies setting it? "Joystick... releasing... give that vector size".
            // If ball is moving, adding might be chaotic. Setting gives clear control.
            // Let's try Setting Velocity + minimal add if user wants to curve. 
            // Usually "Slingshot" replaces velocity.
            rb.linearVelocity = force;
        }
    }

    void MineSurroundings(float currentDamage)
    {
        int cx, cy;
        PixelSimulation.Instance.WorldToGrid(transform.position, out cx, out cy);
        
        int r = 24; 
        if (isEnlarged) r = 40; 
        
        // Optimization: Don't mine if not moving enough? Or mine always?
        // Mine always is satisfying.

        for (int x = cx - r; x <= cx + r; x++)
        {
            for (int y = cy - r; y <= cy + r; y++)
            {
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r)
                {
                    PixelSimulation.Instance.DamagePixel(x, y, currentDamage);
                }
            }
        }
    }
}
