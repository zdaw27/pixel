using UnityEngine;
using System.IO;
using System.Collections;

public class BenchmarkSystem : MonoBehaviour
{
    public bool runOnStart = false; // 자동 시작
    public float duration = 10.0f;
    
    private PixelSimulation simulation;
    private float startTime;
    private int frameCount;
    private float totalTime;
    private float minFps = float.MaxValue;
    private float maxFps = 0f;
    private bool isRunning = false;

    void Start()
    {
        simulation = GetComponent<PixelSimulation>();
        // 인스펙터 설정과 관계없이 자동 시작을 완전히 막음
        // if (runOnStart) StartBenchmark(); 
    }

    void OnGUI()
    {
        if (!isRunning)
        {
            if (GUI.Button(new Rect(10, 100, 150, 40), "벤치마크 실행"))
            {
                StartBenchmark();
            }
        }
        else
        {
            GUI.Label(new Rect(10, 100, 200, 40), $"벤치마크 진행 중... {(int)(Time.time - startTime)}/{duration}초");
        }
    }

    [ContextMenu("Start Benchmark")]
    public void StartBenchmark()
    {
        if (isRunning) return;
        StartCoroutine(BenchmarkRoutine());
    }

    IEnumerator BenchmarkRoutine()
    {
        isRunning = true;
        frameCount = 0;
        totalTime = 0;
        minFps = float.MaxValue;
        maxFps = 0f;

        Debug.Log("벤치마크 시작됨...");
        simulation.ClearGrid();

        // 스트레스 테스트: 10초 동안 랜덤하게 모래와 불을 뿌림
        startTime = Time.time;
        while (Time.time - startTime < duration)
        {
            // 랜덤 위치에 모래/불 투하
            int cx = Random.Range(20, simulation.width - 20);
            int cy = simulation.height - 10;
            
            PixelType type = Random.value > 0.5f ? PixelType.Sand : PixelType.Fire;
            
            // 브러시 크기 5로 그리기
            for (int x = cx - 5; x <= cx + 5; x++)
            {
                for (int y = cy - 5; y <= cy + 5; y++)
                {
                    simulation.SetPixel(x, y, type);
                }
            }

            yield return null;
        }

        string result = GenerateReport();
        Debug.Log(result);
        
        // 파일로 저장 (프로젝트 루트)
        string path = Path.Combine(Application.dataPath, "../benchmark_results.txt");
        File.WriteAllText(path, result);
        Debug.Log($"벤치마크 결과 저장됨: {path}");

        isRunning = false;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void Update()
    {
        if (!isRunning) return;

        float fps = 1.0f / Time.unscaledDeltaTime;
        totalTime += Time.unscaledDeltaTime;
        frameCount++;

        if (totalTime > 0.5f) // 초기 0.5초 제외 (안정화)
        {
            if (fps < minFps) minFps = fps;
            if (fps > maxFps) maxFps = fps;
        }
    }

    string GenerateReport()
    {
        float avgFps = frameCount / totalTime;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("=== 벤치마크 결과 ===");
        sb.AppendLine($"날짜: {System.DateTime.Now}");
        sb.AppendLine($"소요 시간: {duration} 초");
        sb.AppendLine($"평균 FPS: {avgFps:F1}");
        sb.AppendLine($"최소 FPS: {minFps:F1}");
        sb.AppendLine($"최대 FPS: {maxFps:F1}");
        sb.AppendLine($"총 프레임: {frameCount}");
        sb.AppendLine("========================");
        return sb.ToString();
    }
}
