using UnityEngine;

public class PixelSceneSetup : MonoBehaviour
{
    public PixelSimulation simulation;
    public PixelInput pixelInput;
    private GameObject dynamitePrefab;

    void Start()
    {
        // 런타임 초기화
        if (simulation == null) simulation = FindObjectOfType<PixelSimulation>();
        if (pixelInput == null) pixelInput = FindObjectOfType<PixelInput>();

        SetupScene(); // 씬 기본 설정을 런타임에도 보장
        SetupTestObjects(); // 테스트 오브젝트 설정

        if (simulation != null)
        {
            // Infinite Mode terrain generation logic handles this now
            // simulation.GenerateTerrain();
            
            if (GetComponent<InfiniteModeController>() == null)
            {
                gameObject.AddComponent<InfiniteModeController>();
            }
            
            // Spawn Ball in Air
            int centerX = simulation.width / 2;
            int topY = Mathf.FloorToInt(simulation.height * 0.8f);
            SpawnBallAt(centerX, topY);
        }
    }

    [ContextMenu("Setup Scene")]
    public void SetupScene()
    {
        SetupLogic();
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Pixel Sim/Setup Scene")]
    public static void SetupSceneMenu()
    {
        SetupLogic();
    }
#endif

    private static void SetupLogic()
    {
        // 1. 카메라 설정
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            cam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }
        
        cam.orthographic = true;
        cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
        cam.orthographicSize = 2f;

        // 2. 시뮬레이션 오브젝트 생성
        GameObject simObj = GameObject.Find("PixelSimulation");
        if (simObj == null)
        {
            simObj = new GameObject("PixelSimulation");
        }

        // 3. 컴포넌트 추가
        PixelSimulation sim = simObj.GetComponent<PixelSimulation>();
        if (sim == null) sim = simObj.AddComponent<PixelSimulation>();

        PixelRenderer rend = simObj.GetComponent<PixelRenderer>();
        if (rend == null) rend = simObj.AddComponent<PixelRenderer>();

        PixelInput input = simObj.GetComponent<PixelInput>();
        if (input == null) input = simObj.AddComponent<PixelInput>();
        
        // LevelManager 추가
        LevelManager lvlMgr = simObj.GetComponent<LevelManager>();
        if (lvlMgr == null) lvlMgr = simObj.AddComponent<LevelManager>();

        // 4. 의존성 연결
        rend.simulation = sim;
        input.simulation = sim;
        
        // 5. 드릴 유닛 오브젝트 설정 (사용자 요청으로 비활성화)
        GameObject oldDrillUnit = GameObject.Find("DrillUnit");
        if (oldDrillUnit != null) DestroyImmediate(oldDrillUnit);
        
        // DrillUnit 생성 코드 제거 또는 비활성화 처리
        /*
        GameObject drillObj = new GameObject("DrillUnit");
        // ... (Drill Creation Code Omitted) ...
        drillUnit.visualTransform = visualObj.transform;
        */
        
        if (simObj.GetComponent<PixelSceneSetup>() == null)
        {
            simObj.AddComponent<PixelSceneSetup>();
        }
        
        if (simObj.GetComponent<PerformanceUI>() == null)
        {
            simObj.AddComponent<PerformanceUI>();
        }

        if (simObj.GetComponent<BenchmarkSystem>() == null)
        {
            simObj.AddComponent<BenchmarkSystem>();
        }
        
        Debug.Log("Pixel Simulation Scene Setup Complete!");
    }



    void SetupTestObjects()
    {
        if (pixelInput == null) return;
        pixelInput.throwPrefabs.Clear();

        // 오직 공(Ball)만 생성
        GameObject ball = CreateTestObject("Ball", Color.yellow, 0.24f, 0.24f); 
        ball.AddComponent<CircleCollider2D>().radius = 0.12f;
        
        PixelPhysicsObject ppo = ball.GetComponent<PixelPhysicsObject>();
        if (ppo != null)
        {
            // More dynamic bounce
            ppo.bounceFactor = 0.85f; // 높은 탄성
            ppo.friction = 0.05f;     // 낮은 마찰
            ppo.pointsPerUnit = 24;  
        }
        
        Rigidbody2D rb = ball.GetComponent<Rigidbody2D>();
        if (rb != null) rb.mass = 2f; 

        pixelInput.throwPrefabs.Add(ball);
    }

    GameObject CreateTestObject(string name, Color color, float width, float height)
    {
        GameObject obj = new GameObject(name);
        obj.transform.position = new Vector3(-100, -100, 0);
        
        int w = Mathf.CeilToInt(width * 100);
        int h = Mathf.CeilToInt(height * 100);
        Texture2D tex = new Texture2D(w, h);
        tex.filterMode = FilterMode.Point;
        Color[] colors = new Color[w * h];
        for (int i = 0; i < colors.Length; i++) colors[i] = color;
        
        if (name == "Circle" || name == "Ball")
        {
            Vector2 center = new Vector2(w/2f, h/2f);
            float radius = w/2f;
            for(int y=0; y<h; y++) {
                for(int x=0; x<w; x++) {
                    if(Vector2.Distance(new Vector2(x,y), center) > radius) colors[y*w+x] = Color.clear;
                }
            }
        }
        
        tex.SetPixels(colors);
        tex.Apply();

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        sr.sortingOrder = 10;

        Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
        rb.mass = 1f;
        
        PixelPhysicsObject ppo = obj.AddComponent<PixelPhysicsObject>();
        ppo.bounceFactor = 0.8f; // Default high bounce
        ppo.friction = 0.1f;
        
        obj.SetActive(false);
        return obj;
    }

    // --- Level Loader Support ---

    public void SpawnBallAt(int gx, int gy)
    {
        // 기존 공 제거
        if (pixelInput != null && pixelInput.throwPrefabs.Count > 0)
        {
            var ballObj = GameObject.Find("Ball_Player");
            if (ballObj != null) Destroy(ballObj);

            // 공 생성
            if (pixelInput.throwPrefabs.Count > 0)
            {
                GameObject prefab = pixelInput.throwPrefabs[0]; 
                GameObject newBall = Instantiate(prefab);
                newBall.name = "Ball_Player";
                newBall.SetActive(true);
                
                Vector3 pos = GridToWorld(gx, gy);
                newBall.transform.position = pos;
                
                // Add Initial Random Velocity for Dynamic Start
                Rigidbody2D rb = newBall.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    float randomX = Random.Range(-5f, 5f);
                    rb.linearVelocity = new Vector2(randomX, 0); 
                }
            }
        }
    }

    public void SpawnGoalAt(int gx, int gy)
    {
        var existingGoal = GameObject.Find("Goal_Object");
        if (existingGoal != null) Destroy(existingGoal);

        GameObject goal = new GameObject("Goal_Object");
        Vector3 pos = GridToWorld(gx, gy);
        goal.transform.position = pos;

        // 깃발 스프라이트 생성
        SpriteRenderer sr = goal.AddComponent<SpriteRenderer>();
        sr.sprite = CreateGoalSprite();
        sr.sortingOrder = 15;
    }

    Vector3 GridToWorld(int gx, int gy)
    {
        float ppu = 100f;
        float worldWidth = simulation.width / ppu;
        float worldHeight = simulation.height / ppu;
        float startX = -worldWidth / 2f;
        float startY = -worldHeight / 2f;

        return new Vector3(startX + (gx / ppu), startY + (gy / ppu), 0);
    }

    Sprite CreateGoalSprite()
    {
        // 간단한 깃발 모양 (Magenta)
        Texture2D tex = new Texture2D(16, 32);
        tex.filterMode = FilterMode.Point;
        Color[] colors = new Color[16 * 32];
        for(int y=0; y<32; y++)
        {
            for(int x=0; x<16; x++)
            {
                if (x < 2) colors[y*16+x] = Color.white; // 깃대
                else if (y > 16 && x < 12) colors[y*16+x] = Color.magenta; // 깃발
                else colors[y*16+x] = Color.clear;
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0,0,16,32), new Vector2(0.5f, 0.0f), 100f);
    }
}
