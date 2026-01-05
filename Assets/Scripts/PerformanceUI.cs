using UnityEngine;

public class PerformanceUI : MonoBehaviour
{
    private PixelSimulation simulation;
    private float deltaTime = 0.0f;

    void Start()
    {
        simulation = FindObjectOfType<PixelSimulation>();
    }

    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    void OnGUI()
    {
        int w = Screen.width, h = Screen.height;
        
        GUIStyle style = new GUIStyle();
        style.alignment = TextAnchor.UpperRight;
        style.fontSize = h * 2 / 100;
        style.normal.textColor = Color.yellow;

        float msec = deltaTime * 1000.0f;
        float fps = 1.0f / deltaTime;
        
        string text = string.Format("{0:0.0} ms ({1:0.} fps)", msec, fps);
        
        if (simulation != null)
        {
            text += string.Format("\n최적화(수면 모드): {0}", simulation.useChunkOptimization ? "ON" : "OFF");
            text += string.Format("\n활성 청크: {0} / {1}", simulation.ActiveChunkCount, (simulation.width/simulation.chunkSize) * (simulation.height/simulation.chunkSize));
        }

        Rect rect = new Rect(0, 0, w, h);
        GUI.Label(rect, text, style);
    }
}
