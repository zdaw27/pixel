using UnityEngine;

public class ResolutionManager : MonoBehaviour
{
    private void Awake()
    {
        // 9:16 aspect ratio. 
        // 540x960 is a good base resolution for windowed mode on PC.
        // It fits within most 1080p screens height-wise (leaving room for title bar).
        // If we want exact pixel mapping to 576x1024 simulation, we could use 576x1024, 
        // but 1024 height might be too tall for some layouts. 
        // Let's stick to a standard mobile resolution ratio.
        
        #if UNITY_STANDALONE
        Screen.SetResolution(540, 960, FullScreenMode.Windowed);
        #endif
        
        // Mobile platforms usually handle their own resolution, 
        // but we can force orientation if needed.
        Screen.orientation = ScreenOrientation.Portrait;
    }
}
