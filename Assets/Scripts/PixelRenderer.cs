using UnityEngine;

public class PixelRenderer : MonoBehaviour
{
    public PixelSimulation simulation;
    public FilterMode filterMode = FilterMode.Point;
    
    private Texture2D texture;
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        if (simulation == null)
            simulation = GetComponent<PixelSimulation>();

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        // 텍스처 생성
        texture = new Texture2D(simulation.width, simulation.height);
        texture.filterMode = filterMode;
        texture.wrapMode = TextureWrapMode.Clamp;

        // 스프라이트 생성 및 할당
        Rect rect = new Rect(0, 0, simulation.width, simulation.height);
        spriteRenderer.sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
    }

    private Color32[] pixelColors;

    void LateUpdate()
    {
        if (simulation == null) return;

        Pixel[,] grid = simulation.GetGrid();
        if (grid == null) return;

        int width = simulation.width;
        int height = simulation.height;

        // 배열 캐싱 및 초기화
        if (pixelColors == null || pixelColors.Length != width * height)
        {
            pixelColors = new Color32[width * height];
        }

        // 픽셀 데이터 복사 (Color32 사용으로 메모리 대역폭 절약)
        for (int y = 0; y < height; y++)
        {
            int yOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                pixelColors[yOffset + x] = (Color32)grid[x, y].Color;
            }
        }

        texture.SetPixels32(pixelColors);
        texture.Apply();
    }
}
