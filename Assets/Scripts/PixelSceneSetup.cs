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
        SetupScene(); // 씬 기본 설정을 런타임에도 보장
        SetupTestObjects(); // 테스트 오브젝트 설정

        if (simulation != null)
        {
            simulation.GenerateTerrain();
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

        // 4. 의존성 연결
        rend.simulation = sim;
        input.simulation = sim;
        
        // 5. 드릴 유닛 오브젝트 설정
        GameObject oldDrillUnit = GameObject.Find("DrillUnit");
        if (oldDrillUnit != null) DestroyImmediate(oldDrillUnit);
        
        GameObject drillObj = new GameObject("DrillUnit");
        
        GameObject visualObj = new GameObject("Visual");
        visualObj.transform.SetParent(drillObj.transform);
        visualObj.transform.localPosition = Vector3.zero;
            
        int texSize = 32;
        Texture2D tex = new Texture2D(texSize, texSize);
        tex.filterMode = FilterMode.Point;
        Color[] colors = new Color[texSize * texSize];
        Vector2 center = new Vector2(texSize / 2f, texSize / 2f);
        float radius = texSize / 2f - 1;

        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                if (Vector2.Distance(new Vector2(x, y), center) <= radius)
                {
                    colors[y * texSize + x] = new Color(1f, 0.8f, 0.2f);
                }
                else
                {
                    colors[y * texSize + x] = Color.clear;
                }
            }
        }
        tex.SetPixels(colors);
        tex.Apply();

        SpriteRenderer sr = visualObj.AddComponent<SpriteRenderer>();
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), 100f);
        sr.sprite = sprite;
        sr.sortingOrder = 10;
        
        DrillUnit drillUnit = drillObj.AddComponent<DrillUnit>();
        drillUnit.simulation = sim;
        drillUnit.visualTransform = visualObj.transform;
        
        drillObj.transform.position = new Vector3(0, 1f, 0);
        drillObj.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        
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

        // 1. 다이너마이트 (기존)
        SetupDynamite();

        // 2. 박스 (Box) - 시각적으론 박스지만 물리적으론 원형
        GameObject box = CreateTestObject("Box", Color.white, 0.2f, 0.2f);
        box.AddComponent<CircleCollider2D>().radius = 0.1f;
        pixelInput.throwPrefabs.Add(box);

        // 3. 공 (Ball) - 완벽하게 구르는 공
        GameObject ball = CreateTestObject("Ball", Color.yellow, 0.2f, 0.2f);
        ball.AddComponent<CircleCollider2D>().radius = 0.1f;
        PixelPhysicsObject ppo = ball.GetComponent<PixelPhysicsObject>();
        if (ppo != null)
        {
            ppo.bounceFactor = 0.7f; // 더 잘 튀게
            ppo.friction = 0.2f;     // 더 잘 구르게
        }
        pixelInput.throwPrefabs.Add(ball);

        // 4. 캡슐 (Capsule) - 시각적으론 캡슐이지만 물리적으론 원형
        GameObject capsule = CreateTestObject("Capsule", Color.green, 0.15f, 0.3f);
        capsule.AddComponent<CircleCollider2D>().radius = 0.1f;
        pixelInput.throwPrefabs.Add(capsule);
    }

    GameObject CreateTestObject(string name, Color color, float width, float height)
    {
        GameObject obj = new GameObject(name);
        obj.transform.position = new Vector3(-100, -100, 0);
        
        // 텍스처 생성
        int w = Mathf.CeilToInt(width * 100);
        int h = Mathf.CeilToInt(height * 100);
        Texture2D tex = new Texture2D(w, h);
        tex.filterMode = FilterMode.Point;
        Color[] colors = new Color[w * h];
        for (int i = 0; i < colors.Length; i++) colors[i] = color;
        
        // 원형/캡슐 모양 깎기 (간단히)
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
        
        // 커스텀 물리 컴포넌트 추가 (지형 충돌용)
        PixelPhysicsObject ppo = obj.AddComponent<PixelPhysicsObject>();
        ppo.bounceFactor = 0.4f;
        ppo.friction = 0.4f;
        
        obj.SetActive(false);
        return obj;
    }

    void SetupDynamite()
    {
        // 다이너마이트 스프라이트 생성
        Texture2D texture = new Texture2D(8, 20);
        texture.filterMode = FilterMode.Point;
        Color[] colors = new Color[8 * 20];
        for (int i = 0; i < colors.Length; i++) colors[i] = new Color(0.8f, 0.2f, 0.2f); // 빨간색
        // 심지 부분
        for(int y=18; y<20; y++) {
            for(int x=3; x<5; x++) {
                colors[y*8 + x] = Color.white;
            }
        }
        texture.SetPixels(colors);
        texture.Apply();

        Sprite dynamiteSprite = Sprite.Create(texture, new Rect(0, 0, 8, 20), new Vector2(0.5f, 0.5f), 100f);

        // 다이너마이트 프리팹 생성
        dynamitePrefab = new GameObject("Dynamite");
        dynamitePrefab.transform.position = new Vector3(-100, -100, 0);
        
        SpriteRenderer sr = dynamitePrefab.AddComponent<SpriteRenderer>();
        sr.sprite = dynamiteSprite;
        sr.sortingOrder = 10;

        Rigidbody2D rb = dynamitePrefab.AddComponent<Rigidbody2D>();
        rb.mass = 1f;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;

        CircleCollider2D col = dynamitePrefab.AddComponent<CircleCollider2D>();
        col.radius = 0.08f;
        col.sharedMaterial = new PhysicsMaterial2D { bounciness = 0.4f, friction = 0.4f };

        dynamitePrefab.AddComponent<Dynamite>();
        
        // 커스텀 물리 컴포넌트 추가 (지형 충돌용)
        PixelPhysicsObject ppo = dynamitePrefab.AddComponent<PixelPhysicsObject>();
        ppo.bounceFactor = 0.5f;
        ppo.friction = 0.6f;
        
        dynamitePrefab.SetActive(false);
        
        if (pixelInput != null) pixelInput.throwPrefabs.Add(dynamitePrefab);
    }
}
