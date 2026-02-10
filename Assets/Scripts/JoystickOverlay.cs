using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class JoystickOverlay : MonoBehaviour
{
    private GameObject canvasObj;
    private Image bgImage;
    private Image handleImage;
    
    private RectTransform bgRect;
    private RectTransform handleRect;

    private bool isDragging = false;
    private Vector2 startPos;
    private Vector2 inputVector;
    
    [Header("Settings")]
    public float joySize = 200f; // Background diameter
    public float handleSize = 80f;
    public float joystickRadius; // Populated in Start
    public float slowMotionScale = 0.1f;
    public float forceMultiplier = 20f;
    
    private BladeController targetBlade;
    private float defaultFixedDeltaTime;
    
    
    // UI References
    private Canvas scalerCanvas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        // Only spawn if not already present
        if (FindObjectOfType<JoystickOverlay>() == null)
        {
            GameObject obj = new GameObject("JoystickSystem");
            obj.AddComponent<JoystickOverlay>();
            
            // Ensure EventSystem
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }
    }

    void Start()
    {
        joystickRadius = joySize * 0.4f; // Allow handle to move 40% of size from center
        CreateUI();
        defaultFixedDeltaTime = Time.fixedDeltaTime;
    }

    void CreateUI()
    {
        // 1. Create Canvas
        canvasObj = new GameObject("JoystickCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 99; 
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();
        
        scalerCanvas = canvas;

        // 2. Create Background
        GameObject bgObj = new GameObject("JoystickBG");
        bgObj.transform.SetParent(canvasObj.transform, false);
        bgImage = bgObj.AddComponent<Image>();
        bgImage.sprite = CreateCircleSprite(new Color(0.1f, 0.1f, 0.1f, 0.5f));
        bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(joySize, joySize);
        // Anchor Bottom Center
        bgRect.anchorMin = new Vector2(0.5f, 0f);
        bgRect.anchorMax = new Vector2(0.5f, 0f);
        bgRect.pivot = new Vector2(0.5f, 0f);
        bgRect.anchoredPosition = new Vector2(0, 50); // Margin

        // 3. Create Handle
        GameObject handleObj = new GameObject("JoystickHandle");
        handleObj.transform.SetParent(bgObj.transform, false);
        handleImage = handleObj.AddComponent<Image>();
        handleImage.sprite = CreateCircleSprite(new Color(0.9f, 0.9f, 0.9f, 0.8f));
        handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(handleSize, handleSize);
        // Center of BG (which is pivot (0.5,0) => center is (0, size/2))
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = Vector2.zero; // Center of BG
    }

    Sprite CreateCircleSprite(Color c)
    {
        int res = 128;
        Texture2D tex = new Texture2D(res, res);
        tex.filterMode = FilterMode.Bilinear;
        Color[] colors = new Color[res * res];
        Vector2 center = new Vector2(res/2f, res/2f);
        float r = res/2f - 2;
        
        for(int y=0; y<res; y++)
        {
            for(int x=0; x<res; x++)
            {
                float d = Vector2.Distance(new Vector2(x,y), center);
                if(d < r) colors[y*res+x] = c;
                else colors[y*res+x] = Color.clear;
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0,0,res,res), new Vector2(0.5f, 0.5f));
    }

    void Update()
    {
        HandleInput();
    }
    
    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // Raycast UI or Check Distance
            // Since we know where it is (Screen Space varies with Scale), let's use RectTransformUtility
            if (RectTransformUtility.RectangleContainsScreenPoint(bgRect, Input.mousePosition, null))
            {
                StartControl();
            }
        }
        
        if (isDragging)
        {
            if (Input.GetMouseButtonUp(0))
            {
                EndControl();
            }
            else
            {
                Drag();
            }
        }
    }

    void StartControl()
    {
        isDragging = true;
        Time.timeScale = slowMotionScale;
        Time.fixedDeltaTime = defaultFixedDeltaTime * Time.timeScale;
        
        // Find Blade Logic
        // 1. Try Camera Target
        if (Camera.main != null)
        {
            var follow = Camera.main.GetComponent<CameraFollow>();
            if (follow != null && follow.target != null)
            {
                targetBlade = follow.target.GetComponent<BladeController>();
            }
        }
        
        // 2. Fallback
        if (targetBlade == null) targetBlade = FindObjectOfType<BladeController>();
    }

    void Drag()
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(bgRect, Input.mousePosition, null, out localPoint);
        
        // bgRect pivot is (0.5, 0.5) effectively for children if we treat it as container?
        // No, Handle is child. 
        // We want handle position relative to BG center.
        // ScreenPointToLocalPoint puts (0,0) at Pivot. Pivot is Bottom Center of BG.
        // Center of Joystick is (0, joySize/2).
        
        Vector2 center = new Vector2(0, joySize / 2f);
        Vector2 diff = localPoint - center;
        
        // Clamp
        if (diff.magnitude > joystickRadius)
        {
            diff = diff.normalized * joystickRadius;
        }
        
        // Move Handle
        // Handle anchor is center of BG?
        // Wait, in CreateUI: 
        // handleRect.anchorMin = 0.5, 0.5
        // handleRect.anchorMax = 0.5, 0.5
        // BG pivot is 0.5, 0. 
        // This means Handle (0,0) is at half-width, half-height of BG?
        // Yes, Anchor 0.5,0.5 refers to Parent's rect 0.5,0.5.
        // Parent is BG. So Handle (0,0) IS the center of the Joystick.
        // PERFECT.
        
        handleRect.anchoredPosition = diff;
        
        inputVector = diff / joystickRadius;
        
        if (targetBlade != null)
        {
            targetBlade.ShowTrajectory(inputVector);
        }
    }

    void EndControl()
    {
        isDragging = false;
        handleRect.anchoredPosition = Vector2.zero; // Return to center
        
        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;
        
        if (targetBlade != null)
        {
            if (inputVector.magnitude > 0.1f)
            {
                targetBlade.ApplyExternalForce(inputVector * forceMultiplier);
            }
            targetBlade.HideTrajectory();
        }
        
        inputVector = Vector2.zero;
    }
}
