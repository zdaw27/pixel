using UnityEngine;

public class PixelInput : MonoBehaviour
{
    public PixelSimulation simulation;
    public PixelType currentType = PixelType.Sand;
    public int brushSize = 2;

    public DrillController drillController;

    public System.Collections.Generic.List<GameObject> throwPrefabs = new System.Collections.Generic.List<GameObject>();
    private int currentPrefabIndex = 0;

    private Camera mainCamera;
    
    public bool isStartPointMode = false;
    public bool isGoalPointMode = false;
    public bool hasStartPos = false;
    public bool hasGoalPos = false;
    public Vector2Int startPos;
    public Vector2Int goalPos;

    private GameObject startMarker;
    private GameObject goalMarker;

    public void SetToolType(PixelType type)
    {
        currentType = type;
        isStartPointMode = false;
        isGoalPointMode = false;
    }

    public void SetToolStartPoint()
    {
        isStartPointMode = true;
        isGoalPointMode = false;
    }

    public void SetToolGoalPoint()
    {
        isStartPointMode = false;
        isGoalPointMode = true;
    }

    public void SetStartPos(int x, int y)
    {
        startPos = new Vector2Int(x, y);
        hasStartPos = true;
        UpdateMarker(ref startMarker, x, y, Color.green, "StartMarker");
    }

    public void SetGoalPos(int x, int y)
    {
        goalPos = new Vector2Int(x, y);
        hasGoalPos = true;
        UpdateMarker(ref goalMarker, x, y, Color.magenta, "GoalMarker");
    }

    void UpdateMarker(ref GameObject marker, int gx, int gy, Color color, string name)
    {
        if (marker == null)
        {
            marker = new GameObject(name);
            SpriteRenderer sr = marker.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite(color);
            sr.sortingOrder = 20; 
            marker.transform.localScale = Vector3.one * 0.5f;
        }
        
        float ppu = 100f;
        float worldWidth = simulation.width / ppu;
        float worldHeight = simulation.height / ppu;
        float startX = -worldWidth / 2f;
        float startY = -worldHeight / 2f;

        Vector3 pos = new Vector3(startX + (gx / ppu), startY + (gy / ppu), 0);
        marker.transform.position = pos;
    }

    Sprite CreateCircleSprite(Color c)
    {
        Texture2D tex = new Texture2D(32, 32);
        tex.filterMode = FilterMode.Point;
        Color[] colors = new Color[32 * 32];
        Vector2 center = new Vector2(16, 16);
        for(int y=0; y<32; y++)
        {
            for(int x=0; x<32; x++)
            {
                if(Vector2.Distance(new Vector2(x,y), center) < 14) colors[y*32+x] = c;
                else colors[y*32+x] = Color.clear;
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0,0,32,32), new Vector2(0.5f, 0.5f), 100f);
    }

    void Start()
    {
        if (simulation == null)
            simulation = GetComponent<PixelSimulation>();
        mainCamera = Camera.main;
        
        if (drillController == null)
            drillController = FindObjectOfType<DrillController>();
    }

    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0; 
        
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        
        float ppu = 100f;
        float worldWidth = simulation.width / ppu;
        float worldHeight = simulation.height / ppu;

        float x = localPos.x + (worldWidth / 2f);
        float y = localPos.y + (worldHeight / 2f);

        int gridX = Mathf.FloorToInt(x * ppu);
        int gridY = Mathf.FloorToInt(y * ppu);

        if (Input.GetMouseButton(0))
        {
            if (isStartPointMode)
            {
                SetStartPos(gridX, gridY);
            }
            else if (isGoalPointMode)
            {
                SetGoalPos(gridX, gridY);
            }
            else if (currentType != PixelType.Bomb) 
            {
                DrawBrush(gridX, gridY, currentType);
            }
        }
        else if (Input.GetMouseButtonDown(0)) 
        {
            if (!isStartPointMode && !isGoalPointMode && currentType == PixelType.Bomb)
            {
                ThrowObject(worldPos);
            }
        }
        else if (Input.GetMouseButton(1)) 
        {
            Drill(gridX, gridY);
        }
    }

    void ThrowObject(Vector3 targetPos)
    {
        if (throwPrefabs == null || throwPrefabs.Count == 0) return;

        GameObject prefab = throwPrefabs[currentPrefabIndex];
        if (prefab == null) return;

        GameObject obj = Instantiate(prefab);
        obj.transform.position = targetPos;
        obj.SetActive(true);

        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            Vector2 throwDir = new Vector2(Random.Range(-0.2f, 0.2f), 0f).normalized; 
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = Random.Range(-90f, 90f);
        }
    }

    void DrawBrush(int cx, int cy, PixelType type)
    {
        for (int x = cx - brushSize; x <= cx + brushSize; x++)
        {
            for (int y = cy - brushSize; y <= cy + brushSize; y++)
            {
                if (Vector2.Distance(new Vector2(cx, cy), new Vector2(x, y)) <= brushSize)
                {
                    simulation.SetPixel(x, y, type);
                }
            }
        }
    }

    void Drill(int cx, int cy)
    {
        for (int x = cx - brushSize; x <= cx + brushSize; x++)
        {
            for (int y = cy - brushSize; y <= cy + brushSize; y++)
            {
                if (Vector2.Distance(new Vector2(cx, cy), new Vector2(x, y)) <= brushSize)
                {
                    if (x >= 0 && x < simulation.width && y >= 0 && y < simulation.height)
                    {
                        Pixel p = simulation.GetGrid()[x, y];
                        // Removed Mineral Explicit Check, added generic IsSolid check
                        if (p.Type == PixelType.Sand || p.Type == PixelType.Stone || simulation.IsSolid(p.Type))
                        {
                            simulation.SetPixel(x, y, PixelType.Empty);
                        }
                    }
                }
            }
        }
    }
}
