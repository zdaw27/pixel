using UnityEngine;

public class PixelSceneSetup : MonoBehaviour
{
    public PixelSimulation simulation;
    public PixelInput pixelInput;
    
    void Start()
    {
        if (simulation == null) simulation = FindObjectOfType<PixelSimulation>();
        if (pixelInput == null) pixelInput = FindObjectOfType<PixelInput>();

        SetupScene(); 
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
        // 1. Camera
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            cam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }
        cam.orthographic = true;
        cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
        cam.orthographicSize = 5.12f; // Matches 1024 height at 100 PPU (10.24 / 2)

        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();

        // 2. Simulation Object
        GameObject simObj = GameObject.Find("PixelSimulation");
        if (simObj == null) simObj = new GameObject("PixelSimulation");

        PixelSimulation sim = simObj.GetComponent<PixelSimulation>();
        if (sim == null) sim = simObj.AddComponent<PixelSimulation>();
        
        if (simObj.GetComponent<SkillUI>() == null) simObj.AddComponent<SkillUI>();

        PixelRenderer rend = simObj.GetComponent<PixelRenderer>();
        if (rend == null) rend = simObj.AddComponent<PixelRenderer>();

        PixelInput input = simObj.GetComponent<PixelInput>();
        if (input == null) input = simObj.AddComponent<PixelInput>();
        
        LevelManager lvlMgr = simObj.GetComponent<LevelManager>();
        if (lvlMgr == null) lvlMgr = simObj.AddComponent<LevelManager>();

        rend.simulation = sim;
        input.simulation = sim;
        
        // 3. Map Generator
        MapGenerator mapGen = simObj.GetComponent<MapGenerator>();
        if (mapGen == null) mapGen = simObj.AddComponent<MapGenerator>();
        mapGen.simulation = sim;

        // 4. Game Manager
        GameManager gm = simObj.GetComponent<GameManager>();
        if (gm == null) gm = simObj.AddComponent<GameManager>();

        // 5. Resolution Manager
        ResolutionManager resMgr = simObj.GetComponent<ResolutionManager>();
        if (resMgr == null) resMgr = simObj.AddComponent<ResolutionManager>();

        // 5. Blade
        GameObject bladeObj = GameObject.Find("Blade");
        if (bladeObj == null)
        {
             GameObject oldDrill = GameObject.Find("DrillUnit");
             if (oldDrill != null) DestroyImmediate(oldDrill);

             bladeObj = CreateBladeObject();
             bladeObj.name = "Blade";
        }
        
        if (follow != null) follow.target = bladeObj.transform;

        // 6. Regenerate Skill Prefabs to include Physics
        if (gm.dynamitePrefab != null) DestroyImmediate(gm.dynamitePrefab);
        gm.dynamitePrefab = CreateDynamitePrefab();

        if (gm.ballPrefab != null) DestroyImmediate(gm.ballPrefab);
        gm.ballPrefab = CreateBallPrefab();
        
        // 7. Init Map
        mapGen.GenerateMap();
        
        Debug.Log("Pixel Simulation & Mining Setup Complete!");
    }

    static GameObject CreateBladeObject()
    {
        GameObject obj = new GameObject("Blade");
        obj.transform.position = Vector3.zero;

        // Visual
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(obj.transform);
        visual.transform.localPosition = Vector3.zero;
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite(16, Color.white);
        sr.sortingOrder = 10;

        // Components
        Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
        rb.mass = 1f;
        rb.gravityScale = 1.5f;
        rb.linearDamping = 0.5f;
        
        CircleCollider2D col = obj.AddComponent<CircleCollider2D>();
        col.radius = 0.16f; 

        PixelPhysicsObject ppo = obj.AddComponent<PixelPhysicsObject>();
        ppo.pointsPerUnit = 20;

        obj.AddComponent<BladeController>();
        
        return obj;
    }

    static GameObject CreateDynamitePrefab()
    {
        GameObject obj = new GameObject("Dynamite_Prefab");
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateBoxSprite(12, 12, Color.red); // Visual
        sr.sortingOrder = 9;
        
        // Physics for Throwing
        Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
        rb.mass = 0.5f;
        rb.gravityScale = 1f;

        CircleCollider2D col = obj.AddComponent<CircleCollider2D>();
        col.radius = 0.12f;

        // Interaction with Pixels (optional, but good for resting on ground)
        PixelPhysicsObject ppo = obj.AddComponent<PixelPhysicsObject>();
        ppo.bounceFactor = 0.2f;
        ppo.friction = 0.5f;

        obj.AddComponent<Dynamite>();
        obj.SetActive(false); 
        return obj;
    }

    static GameObject CreateBallPrefab()
    {
        GameObject obj = new GameObject("Ball_Prefab");
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite(12, Color.white);
        
        obj.AddComponent<Rigidbody2D>();
        obj.AddComponent<CircleCollider2D>().radius = 0.12f;
        PixelPhysicsObject ppo = obj.AddComponent<PixelPhysicsObject>();
        ppo.bounceFactor = 0.9f;
        
        obj.SetActive(false);
        return obj;
    }

    static Sprite CreateCircleSprite(int diameter, Color c)
    {
        Texture2D tex = new Texture2D(diameter, diameter);
        tex.filterMode = FilterMode.Point;
        Color[] colors = new Color[diameter * diameter];
        float r = diameter / 2f;
        Vector2 center = new Vector2(r, r);
        
        for(int y=0; y<diameter; y++)
        {
            for(int x=0; x<diameter; x++)
            {
                if(Vector2.Distance(new Vector2(x,y), center) <= r) colors[y*diameter+x] = c;
                else colors[y*diameter+x] = Color.clear;
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0,0,diameter,diameter), new Vector2(0.5f,0.5f), 100f);
    }
    
    static Sprite CreateBoxSprite(int w, int h, Color c)
    {
        Texture2D tex = new Texture2D(w, h);
        tex.filterMode = FilterMode.Point;
        Color[] colors = new Color[w * h];
        for (int i = 0; i < colors.Length; i++) colors[i] = c;
        tex.SetPixels(colors); 
        tex.Apply();
        return Sprite.Create(tex, new Rect(0,0,w,h), new Vector2(0.5f,0.5f), 100f);
    }

    public void SpawnBallAt(int gx, int gy)
    {
        // ... (Existing compatibility logic)
    }

    public void SpawnGoalAt(int gx, int gy)
    {
    }
}
