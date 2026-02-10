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

    void MineSurroundings(float currentDamage)
    {
        int cx, cy;
        PixelSimulation.Instance.WorldToGrid(transform.position, out cx, out cy);
        
        int r = 24; 
        if (isEnlarged) r = 40; 

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
