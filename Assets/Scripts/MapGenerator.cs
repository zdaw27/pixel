using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public PixelSimulation simulation;
    
    [Header("Generation Settings")]
    public int chunkWidth = 128;
    public int startY = 240; // Surface Level
    
    [System.Serializable]
    public struct MineralSpawnData
    {
        public PixelType type;
        public float spawnChance; // 0.0 to 1.0 (per column)
        public int minHeight;     // Map coordinates (0 is bottom)
        public int maxHeight;
        public int radiusMin;
        public int radiusMax;
    }

    public MineralSpawnData[] minerals;

    void Start()
    {
        if (simulation == null) simulation = GetComponent<PixelSimulation>();
        
        // Define Default Distribution if empty
        if (minerals == null || minerals.Length == 0)
        {
            minerals = new MineralSpawnData[]
            {
                new MineralSpawnData { type = PixelType.Iron, spawnChance = 0.05f, minHeight = 100, maxHeight = 220, radiusMin = 2, radiusMax = 4 },
                new MineralSpawnData { type = PixelType.Copper, spawnChance = 0.04f, minHeight = 80, maxHeight = 200, radiusMin = 2, radiusMax = 4 },
                new MineralSpawnData { type = PixelType.Gold, spawnChance = 0.02f, minHeight = 50, maxHeight = 150, radiusMin = 2, radiusMax = 3 },
                new MineralSpawnData { type = PixelType.Emerald, spawnChance = 0.01f, minHeight = 30, maxHeight = 100, radiusMin = 1, radiusMax = 3 },
                new MineralSpawnData { type = PixelType.Ruby, spawnChance = 0.008f, minHeight = 10, maxHeight = 80, radiusMin = 1, radiusMax = 2 },
                new MineralSpawnData { type = PixelType.Diamond, spawnChance = 0.005f, minHeight = 0, maxHeight = 50, radiusMin = 1, radiusMax = 2 }
            };
        }

        GenerateMap();
    }

    [ContextMenu("Generate Map")]
    public void GenerateMap()
    {
        if (simulation == null) return;
        simulation.ClearGrid();

        int width = simulation.width;
        int height = simulation.height;

        // 1. Fill basic ground (Dirt/Stone mix?) - Just Stone for now per request "Stone/Mineral"
        // Fill from bottom to startY
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < startY; y++)
            {
                 // Surface noise
                 if (y == startY - 1 && Random.value > 0.5f) continue;
                 
                 simulation.SetPixel(x, y, PixelType.Stone);
            }
        }

        // 2. Generate Veins
        // Iterate columns
        for (int x = 0; x < width; x++)
        {
            // Check each mineral type
            foreach (var mineral in minerals)
            {
                // Roll chance
                if (Random.value < mineral.spawnChance)
                {
                    // Determine Height
                    int cy = Random.Range(mineral.minHeight, mineral.maxHeight);
                    if (cy >= startY) cy = startY - 5; // Clamp below surface

                    // Determine Radius
                    int r = Random.Range(mineral.radiusMin, mineral.radiusMax + 1);

                    // Draw Vein
                    DrawVein(x, cy, r, mineral.type);
                }
            }
        }
    }

    void DrawVein(int cx, int cy, int r, PixelType type)
    {
        for (int x = cx - r; x <= cx + r; x++)
        {
            for (int y = cy - r; y <= cy + r; y++)
            {
                if (x >= 0 && x < simulation.width && y >= 0 && y < simulation.height)
                {
                    // Circle check
                    if (Vector2.Distance(new Vector2(cx, cy), new Vector2(x, y)) <= r)
                    {
                        Pixel current = simulation.GetGrid()[x, y];
                        
                        // Overwrite logic:
                        // Only overwrite Stone, Empty, or Lower Value Minerals?
                        // Simple rule: Always overwrite Stone.
                        // If hitting another mineral, only overwrite if new one is "Higher Tier" (Hardness check?)
                        
                        bool canPlace = false;
                        if (current.Type == PixelType.Stone || current.Type == PixelType.Empty) canPlace = true;
                        else if (simulation.GetHardness(type) > simulation.GetHardness(current.Type)) canPlace = true;

                        if (canPlace)
                        {
                            simulation.SetPixel(x, y, type);
                        }
                    }
                }
            }
        }
    }
}
