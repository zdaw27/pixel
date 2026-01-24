using UnityEngine;
using System.Collections.Generic;

public class InfiniteModeController : MonoBehaviour
{
    public Transform target;
    public float thresholdY = -0.5f; // Trigger scroll when target is below this
    public float targetRestPosY = 0.5f; // Move target to this Y
    
    public int currentDepth = 0;
    
    private PixelSimulation simulation;
    private float ppu = 100f;

    void Start()
    {
        simulation = PixelSimulation.Instance;
        if (target == null)
        {
            // Auto-find player/ball
            var ppo = FindObjectOfType<PixelPhysicsObject>();
            if (ppo != null) target = ppo.transform;
        }

        // Enable destruction on the ball
        if (target != null)
        {
            var ppo = target.GetComponent<PixelPhysicsObject>();
            if (ppo != null)
            {
                ppo.destroyOnImpact = true;
                ppo.destructionRadius = 5;
            }
        }
        
        // Initial terrain setup for infinite mode
        // Fill only the bottom part (e.g., 60%) so the ball starts in air
        if (simulation != null)
        {
            simulation.ClearGrid(); // Ensure clean slate
            GenerateChunk(0, Mathf.FloorToInt(simulation.height * 0.6f));
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1. Camera Follows Ball Smoothly
        if (Camera.main != null)
        {
            Vector3 camPos = Camera.main.transform.position;
            // Smoothly interpolate Y only
            float targetY = target.position.y;
            // Optionally clamp targetY so we don't look at empty space above? 
            // taking simple approach: follow player.
            
            camPos.y = Mathf.Lerp(camPos.y, targetY, Time.deltaTime * 5f);
            Camera.main.transform.position = camPos;
        }
        
        // 2. Continuous Scroll Check (Treadmill)
        // We want to keep the player roughly in the middle of the Simulation Grid (Height/2).
        // The Simulation Grid is fixed in World Space from Y = -Height/2 to +Height/2 (approx).
        // Let's check the player's Grid Y.
        
        int gridX, gridY;
        simulation.WorldToGrid(target.position, out gridX, out gridY);
        
        // If player falls below 40% of the map, shift everything UP.
        // Don't wait for the bottom. Shift early and often?
        // Or Shift when simply below center.
        int safeBuffer = (int)(simulation.height * 0.4f); // Keep 40% buffer at bottom
        
        if (gridY < safeBuffer)
        {
            int scrollAmount = safeBuffer - gridY;
            PerformScroll(scrollAmount);
        }
    }

    void PerformScroll(int pixelsToScroll)
    {
        if (pixelsToScroll <= 0) return;

        float worldShift = pixelsToScroll / ppu;
        Vector3 shiftVector = new Vector3(0, worldShift, 0);

        // 1. Shift Grid Data UP (Logical Scroll)
        simulation.ScrollUp(pixelsToScroll);

        // 2. Generate New Terrain at Bottom (Logical Fill)
        GenerateChunk(0, pixelsToScroll);
        
        // 3. Shift Physics Objects UP (Visual/World Correction)
        // By moving objects UP, we keep them aligned with the specific pixels they were touching
        // (since those pixels just moved UP in the array).
        PixelPhysicsObject[] objects = FindObjectsOfType<PixelPhysicsObject>();
        foreach (var obj in objects)
        {
            obj.transform.position += shiftVector;
        }
        
        // 4. Shift Camera UP (Visual Correction)
        // This cancels out the world shift relative to the screen.
        // Player moves UP, Camera moves UP -> Player stays in same spot on screen.
        if (Camera.main != null)
        {
            Camera.main.transform.position += shiftVector;
        }
        
        // 5. Update Depth Counter
        currentDepth += pixelsToScroll;
    }

    void GenerateChunk(int startY, int height)
    {
        int w = simulation.width;
        
        for (int y = startY; y < startY + height; y++)
        {
            int absoluteY = currentDepth + y; 
            
            for (int x = 0; x < w; x++)
            {
                // Base Noise (Caves)
                float scale = 0.05f;
                float noise = Mathf.PerlinNoise(x * scale, (absoluteY) * scale);
                
                // High Frequency Noise for Jagged Edges (Dynamic Bounce!)
                float jitter = Mathf.PerlinNoise(x * 0.3f, (absoluteY) * 0.3f) * 0.3f;
                noise += jitter;
                
                PixelType type = PixelType.Stone;
                
                // Veins & Caves
                if (noise > 0.6f) type = PixelType.Sand;
                if (noise > 0.8f) type = PixelType.Empty; 
                
                // Ores
                if (type == PixelType.Stone)
                {
                    float r = FastRandom();
                    if (r < 0.03f) type = PixelType.Mineral;
                    else if (r < 0.035f) type = PixelType.Bomb;
                }
                
                simulation.SetPixel(x, y, type);
            }
        }
    }

    float FastRandom()
    {
        return UnityEngine.Random.value;
    }
}
