using UnityEngine;
using System.Collections.Generic;

public class InfiniteModeController : MonoBehaviour
{
    public Transform target;
    public float thresholdY = -0.5f; 
    public float targetRestPosY = 0.5f; 
    
    public int currentDepth = 0;
    
    private PixelSimulation simulation;
    private float ppu = 100f;

    void Start()
    {
        simulation = PixelSimulation.Instance;
        if (target == null)
        {
            var ppo = FindObjectOfType<PixelPhysicsObject>();
            if (ppo != null) target = ppo.transform;
        }

        if (target != null)
        {
            var ppo = target.GetComponent<PixelPhysicsObject>();
            if (ppo != null)
            {
                ppo.destroyOnImpact = true;
                ppo.destructionRadius = 5;
            }
        }
        
        if (simulation != null)
        {
            simulation.ClearGrid(); 
            GenerateChunk(0, Mathf.FloorToInt(simulation.height * 0.6f));
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (Camera.main != null)
        {
            Vector3 camPos = Camera.main.transform.position;
            float targetY = target.position.y;
            camPos.y = Mathf.Lerp(camPos.y, targetY, Time.deltaTime * 5f);
            Camera.main.transform.position = camPos;
        }
        
        int gridX, gridY;
        simulation.WorldToGrid(target.position, out gridX, out gridY);
        
        int safeBuffer = (int)(simulation.height * 0.4f); 
        
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

        simulation.ScrollUp(pixelsToScroll);
        GenerateChunk(0, pixelsToScroll);
        
        PixelPhysicsObject[] objects = FindObjectsOfType<PixelPhysicsObject>();
        foreach (var obj in objects)
        {
            obj.transform.position += shiftVector;
        }
        
        if (Camera.main != null)
        {
            Camera.main.transform.position += shiftVector;
        }
        
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
                float scale = 0.05f;
                float noise = Mathf.PerlinNoise(x * scale, (absoluteY) * scale);
                
                float jitter = Mathf.PerlinNoise(x * 0.3f, (absoluteY) * 0.3f) * 0.3f;
                noise += jitter;
                
                PixelType type = PixelType.Stone;
                
                if (noise > 0.6f) type = PixelType.Sand;
                if (noise > 0.8f) type = PixelType.Empty; 
                
                if (type == PixelType.Stone)
                {
                    float r = FastRandom();
                    // Replaced Mineral with generic mineral logic or random specific mineral
                    if (r < 0.03f) type = PixelType.Iron; // Default to Iron for infinite mode
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
