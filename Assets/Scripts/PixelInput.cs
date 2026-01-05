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

    void Start()
    {
        if (simulation == null)
            simulation = GetComponent<PixelSimulation>();
        mainCamera = Camera.main;
        
        // 드릴 컨트롤러 찾기 (없으면 씬에서 찾기)
        if (drillController == null)
            drillController = FindObjectOfType<DrillController>();
    }

    void Update()
    {
        HandleInput();
        HandleSelection();
    }

    void HandleInput()
    {
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0; // Z축 고정
        
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        
        float ppu = 100f;
        float worldWidth = simulation.width / ppu;
        float worldHeight = simulation.height / ppu;

        float x = localPos.x + (worldWidth / 2f);
        float y = localPos.y + (worldHeight / 2f);

        int gridX = Mathf.FloorToInt(x * ppu);
        int gridY = Mathf.FloorToInt(y * ppu);

        if (Input.GetMouseButtonDown(0)) // 좌클릭: 그리기 또는 투척
        {
            if (currentType == PixelType.Bomb) // 8번(Bomb)이 투척 모드
            {
                ThrowObject(worldPos);
            }
            else
            {
                DrawBrush(gridX, gridY, currentType);
            }
            
            if (drillController != null) drillController.SetDrilling(false);
        }
        else if (Input.GetMouseButton(0)) // 드래그: 그리기 (투척 제외)
        {
            if (currentType != PixelType.Bomb)
            {
                DrawBrush(gridX, gridY, currentType);
            }
        }
        else if (Input.GetMouseButton(1)) // 우클릭: 드릴
        {
            Drill(gridX, gridY);
            if (drillController != null) drillController.SetDrilling(true);
        }
        else
        {
            if (drillController != null) drillController.SetDrilling(false);
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
            Vector2 throwDir = new Vector2(Random.Range(-0.2f, 0.2f), 0f).normalized; // 거의 제자리 낙하
            rb.linearVelocity = Vector2.zero; // 초기 속도 0
            rb.angularVelocity = Random.Range(-90f, 90f); // 약간의 회전
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
                    // 현재 픽셀 확인
                    if (x >= 0 && x < simulation.width && y >= 0 && y < simulation.height)
                    {
                        Pixel p = simulation.GetGrid()[x, y];
                        // 고체(모래, 돌, 광물)만 파괴, 물은 유지
                        if (p.Type == PixelType.Sand || p.Type == PixelType.Stone || p.Type == PixelType.Mineral)
                        {
                            simulation.SetPixel(x, y, PixelType.Empty);
                        }
                    }
                }
            }
        }
    }

    void HandleSelection()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) currentType = PixelType.Sand;
        if (Input.GetKeyDown(KeyCode.Alpha2)) currentType = PixelType.Water;
        if (Input.GetKeyDown(KeyCode.Alpha3)) currentType = PixelType.Stone;
        if (Input.GetKeyDown(KeyCode.Alpha4)) currentType = PixelType.Mineral;
        if (Input.GetKeyDown(KeyCode.Alpha5)) currentType = PixelType.Empty;
        if (Input.GetKeyDown(KeyCode.Alpha6)) currentType = PixelType.Gas;
        if (Input.GetKeyDown(KeyCode.Alpha7)) currentType = PixelType.Fire;
        if (Input.GetKeyDown(KeyCode.Alpha8)) currentType = PixelType.Bomb;

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            currentPrefabIndex = (currentPrefabIndex + 1) % throwPrefabs.Count;
        }
    }
    
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 250));
        GUILayout.Label("Left Click: Draw / Right Click: Drill");
        GUILayout.Label("Current Element: " + currentType);
        GUILayout.Label("1:Sand 2:Water 3:Stone 4:Mineral 5:Erase");
        GUILayout.Label("6:Gas 7:Fire 8:Throw Object");
        
        if (currentType == PixelType.Bomb && throwPrefabs.Count > 0)
        {
            string prefabName = throwPrefabs[currentPrefabIndex].name;
            GUILayout.Label($"Selected Object (Tab): {prefabName}");
        }
        GUILayout.EndArea();
    }
}
