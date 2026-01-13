using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class LevelManager : MonoBehaviour
{
    public string levelDirectory = "Assets/Levels";
    
    private PixelSimulation simulation;
    private PixelInput pixelInput;
    private PixelSceneSetup sceneSetup;

    // Type to Color Mapping
    private Dictionary<PixelType, Color> typeToColor = new Dictionary<PixelType, Color>()
    {
        { PixelType.Empty, Color.clear },
        { PixelType.Sand, new Color(1f, 0.8f, 0.2f) }, // Yellow
        { PixelType.Stone, Color.gray },               // Gray
        { PixelType.Water, new Color(0.2f, 0.4f, 1f) },// Blue
        { PixelType.Mineral, new Color(0.8f, 0.2f, 0.8f) }, // Purple
        { PixelType.Gas, new Color(0.6f, 1f, 0.6f) },  // Light Green
        { PixelType.Fire, Color.red },                 // Red
        { PixelType.Smoke, new Color(0.5f, 0.5f, 0.5f) }, // Dark Gray
        { PixelType.Bomb, new Color(0.2f, 0.2f, 0.2f) }   // Black-ish
    };

    // Special Colors for Logic
    public static readonly Color COLOR_START = Color.green;
    public static readonly Color COLOR_GOAL = Color.magenta;

    void Awake()
    {
        simulation = GetComponent<PixelSimulation>();
        pixelInput = GetComponent<PixelInput>();
        sceneSetup = GetComponent<PixelSceneSetup>();
        
        if (!Directory.Exists(levelDirectory))
        {
            Directory.CreateDirectory(levelDirectory);
        }
    }

    public void SaveLevel(string levelName)
    {
        if (simulation == null) return;

        int width = simulation.width;
        int height = simulation.height;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] colors = new Color[width * height];

        Pixel[,] grid = simulation.GetGrid();

        // 1. Grid Data
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                PixelType type = grid[x, y].Type;
                if (typeToColor.ContainsKey(type))
                {
                    colors[y * width + x] = typeToColor[type];
                }
                else
                {
                    colors[y * width + x] = Color.clear;
                }
            }
        }

        // 2. Start Point (Green)
        if (pixelInput != null && pixelInput.hasStartPos)
        {
            int sx = pixelInput.startPos.x;
            int sy = pixelInput.startPos.y;
            DrawMarker(colors, width, height, sx, sy, COLOR_START, 3);
        }

        // 3. Goal Point (Magenta)
        if (pixelInput != null && pixelInput.hasGoalPos)
        {
            int gx = pixelInput.goalPos.x;
            int gy = pixelInput.goalPos.y;
            DrawMarker(colors, width, height, gx, gy, COLOR_GOAL, 3);
        }

        texture.SetPixels(colors);
        texture.Apply();

        byte[] bytes = texture.EncodeToPNG();
        string path = Path.Combine(levelDirectory, levelName + ".png");
        File.WriteAllBytes(path, bytes);
        
        Debug.Log($"Level Saved to: {path}");
        
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    public void TestLevel()
    {
        // Snapshot current state to temp level and reload it to reset physics state
        SaveLevel("_temp_test_level");
        LoadLevel("_temp_test_level");
        Debug.Log("Test Level Started: Resetting simulation state...");
    }

    void DrawMarker(Color[] colors, int w, int h, int cx, int cy, Color c, int size)
    {
        for (int y = cy - size; y <= cy + size; y++)
        {
            for (int x = cx - size; x <= cx + size; x++)
            {
                if (x >= 0 && x < w && y >= 0 && y < h)
                {
                    colors[y * w + x] = c;
                }
            }
        }
    }

    public void LoadLevel(string levelName)
    {
        string path = Path.Combine(levelDirectory, levelName + ".png");
        if (!File.Exists(path))
        {
            Debug.LogError($"Level file not found: {path}");
            return;
        }

        byte[] bytes = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(bytes);

        if (texture.width != simulation.width || texture.height != simulation.height)
        {
            Debug.LogWarning("Level size mismatch! Resizing might occur or logic might break.");
        }

        simulation.ClearGrid();
        Color[] colors = texture.GetPixels();
        int width = texture.width;
        int height = texture.height;

        Vector2Int? startPos = null;
        Vector2Int? goalPos = null;

        List<Vector2Int> startPixels = new List<Vector2Int>();
        List<Vector2Int> goalPixels = new List<Vector2Int>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color c = colors[y * width + x];
                
                // Check for Markers first (approximate check for compression artifacts)
                if (IsColorSimilar(c, COLOR_START))
                {
                    startPixels.Add(new Vector2Int(x, y));
                    continue; 
                }
                if (IsColorSimilar(c, COLOR_GOAL))
                {
                    goalPixels.Add(new Vector2Int(x, y));
                    continue;
                }

                // Map Color to Type
                PixelType type = GetTypeFromColor(c);
                if (type != PixelType.Empty)
                {
                    simulation.SetPixel(x, y, type);
                }
            }
        }

        // Calculate Centroids
        if (startPixels.Count > 0)
        {
            Vector2Int center = GetCentroid(startPixels);
            startPos = center;
        }

        if (goalPixels.Count > 0)
        {
            Vector2Int center = GetCentroid(goalPixels);
            goalPos = center;
        }

        // Setup Start/Goal
        if (pixelInput != null)
        {
            if (startPos.HasValue)
            {
                pixelInput.SetStartPos(startPos.Value.x, startPos.Value.y);
                sceneSetup.SpawnBallAt(startPos.Value.x, startPos.Value.y);
            }
            if (goalPos.HasValue)
            {
                pixelInput.SetGoalPos(goalPos.Value.x, goalPos.Value.y);
                sceneSetup.SpawnGoalAt(goalPos.Value.x, goalPos.Value.y);
            }
        }

        Debug.Log($"Level Loaded: {levelName}");
    }

    bool IsColorSimilar(Color a, Color b, float tolerance = 0.1f)
    {
        return Mathf.Abs(a.r - b.r) < tolerance &&
               Mathf.Abs(a.g - b.g) < tolerance &&
               Mathf.Abs(a.b - b.b) < tolerance &&
               a.a > 0.5f;
    }

    PixelType GetTypeFromColor(Color c)
    {
        if (c.a < 0.1f) return PixelType.Empty;

        float minDiff = float.MaxValue;
        PixelType bestMatch = PixelType.Empty;

        foreach (var pair in typeToColor)
        {
            if (pair.Key == PixelType.Empty) continue;

            float diff = Mathf.Abs(c.r - pair.Value.r) + Mathf.Abs(c.g - pair.Value.g) + Mathf.Abs(c.b - pair.Value.b);
            if (diff < minDiff && diff < 0.2f) // Threshold
            {
                minDiff = diff;
                bestMatch = pair.Key;
            }
        }

        return bestMatch;
    }

    Vector2Int GetCentroid(List<Vector2Int> pixels)
    {
        if (pixels.Count == 0) return Vector2Int.zero;

        long sumX = 0;
        long sumY = 0;

        foreach (var p in pixels)
        {
            sumX += p.x;
            sumY += p.y;
        }

        return new Vector2Int((int)(sumX / pixels.Count), (int)(sumY / pixels.Count));
    }
}
