using UnityEngine;

public enum PixelType
{
    Empty,
    Stone,
    Sand,
    Water,
    Mineral,
    Gas,
    Fire,
    Smoke,
    Bomb
}

public struct Pixel
{
    public PixelType Type;
    public Color Color;
    public bool Updated; 
    public float Life; // 수명 (불, 연기, 폭탄 등)
    public Vector2 Velocity; // 폭발 파편용 속도 (선택적)
}

public class PixelSimulation : MonoBehaviour
{
    public int width = 256;
    public int height = 256;
    public float updateInterval = 0.02f;

    private Pixel[,] grid;
    private float timer;
    
    public Color stoneColor = Color.gray;
    public Color sandColor = new Color(1f, 0.8f, 0.2f);
    public Color waterColor = new Color(0.2f, 0.4f, 1f);
    public Color mineralColor = new Color(0.8f, 0.2f, 0.8f);
    public Color emptyColor = Color.black;
    public Color gasColor = new Color(0.6f, 1f, 0.6f, 0.5f); 
    public Color smokeColor = new Color(0.5f, 0.5f, 0.5f, 0.8f); 
    public Color bombColor = new Color(0.2f, 0.2f, 0.2f); // 진한 회색 (폭탄)

    public static PixelSimulation Instance { get; private set; }

    // 최적화: 속성 조회 테이블
    private bool[] isSolidTable;
    private bool[] isLiquidTable;
    private bool[] isGasTable;

    // 최적화: 고속 난수 생성기 (Xorshift)
    private uint rngState = 123456789;
    private float FastRandom()
    {
        rngState ^= rngState << 13;
        rngState ^= rngState >> 17;
        rngState ^= rngState << 5;
        return (rngState & 0xFFFFFF) / 16777216.0f; // 0..1
    }
    private int FastRandomRange(int min, int max)
    {
        if (min >= max) return min;
        return min + (int)(FastRandom() * (max - min));
    }
    private float FastRandomRange(float min, float max)
    {
        return min + FastRandom() * (max - min);
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 속성 테이블 초기화
        int maxType = (int)System.Enum.GetValues(typeof(PixelType)).Length;
        isSolidTable = new bool[maxType];
        isLiquidTable = new bool[maxType];
        isGasTable = new bool[maxType];

        isSolidTable[(int)PixelType.Sand] = true;
        isSolidTable[(int)PixelType.Stone] = true;
        isSolidTable[(int)PixelType.Mineral] = true;
        isSolidTable[(int)PixelType.Bomb] = true;

        isLiquidTable[(int)PixelType.Water] = true;

        isGasTable[(int)PixelType.Gas] = true;
        isGasTable[(int)PixelType.Smoke] = true;
        isGasTable[(int)PixelType.Fire] = true; // 불도 기체 취급

        grid = new Pixel[width, height];
        ClearGrid();
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            Simulate();
            timer = 0f;
        }
    }

    public void ClearGrid()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = new Pixel { Type = PixelType.Empty, Color = emptyColor, Updated = false };
            }
        }
    }

    public void GenerateTerrain()
    {
        ClearGrid();
        
        // 0: 부드러운 언덕, 1: 슬로프, 2: 그릇(Bowl), 3: 하프 파이프
        int mode = Random.Range(0, 4); 
        Debug.Log($"Generating Terrain Mode: {mode}");

        for (int x = 0; x < width; x++)
        {
            int groundHeight = 0;
            float normalizedX = (float)x / width; // 0 to 1

            switch (mode)
            {
                case 0: // Smooth Hills (부드러운 언덕)
                    float noise = Mathf.PerlinNoise(x * 0.02f, 0);
                    groundHeight = Mathf.FloorToInt(height * 0.3f + noise * height * 0.4f);
                    break;
                case 1: // Slope (슬로프)
                    groundHeight = Mathf.FloorToInt(Mathf.Lerp(height * 0.8f, height * 0.2f, normalizedX));
                    break;
                case 2: // Bowl (그릇 모양)
                    float parabola = 4f * (normalizedX - 0.5f) * (normalizedX - 0.5f); // 0.5에서 0, 양끝에서 1
                    groundHeight = Mathf.FloorToInt(height * 0.2f + parabola * height * 0.6f);
                    break;
                case 3: // Half-Pipe (하프 파이프 - Sine Wave)
                     float sinWave = Mathf.Sin(normalizedX * Mathf.PI); // 0에서 0, 0.5에서 1, 1에서 0 (위로 볼록)
                     // 아래로 볼록하게 뒤집기
                     sinWave = 1f - sinWave;
                     groundHeight = Mathf.FloorToInt(height * 0.3f + sinWave * height * 0.5f);
                    break;
            }

            // 지형 채우기 (돌)
            for (int y = 0; y < groundHeight; y++)
            {
                 SetPixel(x, y, PixelType.Stone);
            }
        }
    }

    public int GetHardness(PixelType type)
    {
        switch (type)
        {
            case PixelType.Sand: return 1;
            case PixelType.Stone: return 5; 
            case PixelType.Mineral: return 10; 
            default: return 0;
        }
    }

    public Pixel[,] GetGrid()
    {
        if (grid == null) Awake();
        return grid;
    }

    public bool IsSolid(PixelType type)
    {
        return isSolidTable[(int)type];
    }

    public void SetPixel(int x, int y, PixelType type)
    {
        if (x >= 0 && x < width && y >= 0 && y < height)
        {
            Color c = emptyColor;
            float life = 0f;

            switch (type)
            {
                case PixelType.Stone: c = stoneColor; break;
                case PixelType.Sand: c = sandColor; break;
                case PixelType.Water: c = waterColor; break;
                case PixelType.Mineral: c = mineralColor; break;
                case PixelType.Gas: c = gasColor; break;
                case PixelType.Fire: 
                    c = new Color(1f, FastRandomRange(0.2f, 0.6f), 0f); 
                    life = FastRandomRange(50f, 100f); 
                    break;
                case PixelType.Smoke: 
                    c = smokeColor; 
                    life = FastRandomRange(100f, 200f); 
                    break;
                case PixelType.Bomb:
                    c = bombColor;
                    life = 150f; // 약 3초 (50fps 기준)
                    break;
            }
            grid[x, y] = new Pixel { Type = type, Color = c, Updated = false, Life = life };
        }
    }

    void Simulate()
    {
        if (grid == null) Awake();

        // 1. 그리드 업데이트 플래그 초기화
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y].Updated = false;
            }
        }

        // 2. 시뮬레이션 루프
        // 전체 그리드를 순회 (Bottom-up: y=0 to height)
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Pixel p = grid[x, y];
                if (p.Updated || p.Type == PixelType.Empty) continue;

                if (p.Velocity.sqrMagnitude > 0.01f) UpdatePhysics(x, y);
                else if (p.Type == PixelType.Sand) UpdateSand(x, y);
                else if (p.Type == PixelType.Water) UpdateWater(x, y);
                else if (p.Type == PixelType.Gas) UpdateGas(x, y);
                else if (p.Type == PixelType.Fire) UpdateFire(x, y);
                else if (p.Type == PixelType.Smoke) UpdateSmoke(x, y);
                else if (p.Type == PixelType.Bomb) UpdateBomb(x, y);
            }
        }
    }

    void UpdatePhysics(int x, int y)
    {
        Pixel p = grid[x, y];
        
        // 중력 적용
        p.Velocity.y -= 0.5f; 
        // 공기 저항
        p.Velocity *= 0.98f;

        // 예상 이동 위치
        int targetX = x + Mathf.RoundToInt(p.Velocity.x);
        int targetY = y + Mathf.RoundToInt(p.Velocity.y);

        // 경계 체크
        if (targetX < 0 || targetX >= width || targetY < 0 || targetY >= height)
        {
            p.Velocity = Vector2.zero;
            grid[x, y] = p; // 속도 0으로 업데이트
            return;
        }

        // 충돌 체크
        if (IsEmpty(targetX, targetY) || IsGas(targetX, targetY) || IsLiquid(targetX, targetY))
        {
            if (IsLiquid(targetX, targetY)) p.Velocity *= 0.5f;
            
            MovePixel(x, y, targetX, targetY);
            
            grid[targetX, targetY].Velocity = p.Velocity;
        }
        else
        {
            p.Velocity *= -0.5f; 
            if (p.Velocity.sqrMagnitude < 1f) p.Velocity = Vector2.zero;
            grid[x, y] = p; 
        }
    }

    void UpdateSand(int x, int y)
    {
        if (y == 0) return; 

        if (IsEmpty(x, y - 1) || IsLiquid(x, y - 1) || IsGas(x, y - 1))
        {
            MoveOrSwap(x, y, x, y - 1);
        }
        else if (x > 0 && (IsEmpty(x - 1, y - 1) || IsLiquid(x - 1, y - 1) || IsGas(x - 1, y - 1)))
        {
            MoveOrSwap(x, y, x - 1, y - 1);
        }
        else if (x < width - 1 && (IsEmpty(x + 1, y - 1) || IsLiquid(x + 1, y - 1) || IsGas(x + 1, y - 1)))
        {
            MoveOrSwap(x, y, x + 1, y - 1);
        }
    }

    void UpdateWater(int x, int y)
    {
        if (y == 0) return;

        if (IsEmpty(x, y - 1) || IsGas(x, y - 1))
        {
            MoveOrSwap(x, y, x, y - 1);
        }
        else
        {
            int dir = FastRandom() > 0.5f ? 1 : -1; 
            if (x + dir >= 0 && x + dir < width && (IsEmpty(x + dir, y) || IsGas(x + dir, y)))
            {
                MoveOrSwap(x, y, x + dir, y);
            }
            else if (x - dir >= 0 && x - dir < width && (IsEmpty(x - dir, y) || IsGas(x - dir, y)))
            {
                MoveOrSwap(x, y, x - dir, y);
            }
        }
    }

    void UpdateGas(int x, int y)
    {
        if (HasNeighborFire(x, y))
        {
            if (FastRandom() < 0.1f) Explode(x, y, 5);
            else SetPixel(x, y, PixelType.Fire);
            return;
        }

        if (y >= height - 1) return;

        if (IsEmpty(x, y + 1) || IsLiquid(x, y + 1))
        {
            MoveOrSwap(x, y, x, y + 1);
        }
        else
        {
            int dir = FastRandom() > 0.5f ? 1 : -1;
            if (x + dir >= 0 && x + dir < width && IsEmpty(x + dir, y))
            {
                MovePixel(x, y, x + dir, y);
            }
            else if (x - dir >= 0 && x - dir < width && IsEmpty(x - dir, y))
            {
                MovePixel(x, y, x - dir, y);
            }
        }
    }

    void UpdateFire(int x, int y)
    {
        grid[x, y].Life -= 1f;
        if (grid[x, y].Life <= 0)
        {
            if (FastRandom() > 0.5f) SetPixel(x, y, PixelType.Smoke);
            else SetPixel(x, y, PixelType.Empty);
            return;
        }

        grid[x, y].Color = new Color(1f, FastRandomRange(0.1f, 0.7f), 0f);

        IgniteNeighbors(x, y);

        // 불은 위로 번지려는 성질 (수명이 충분할 때만)
        if (grid[x, y].Life > 10 && y < height - 1 && IsEmpty(x, y + 1) && FastRandom() < 0.1f)
        {
            SetPixel(x, y + 1, PixelType.Fire);
            // 새로 번진 불은 수명을 약간 줄임
            grid[x, y + 1].Life = FastRandomRange(40f, 80f);
        }
    }

    void UpdateSmoke(int x, int y)
    {
        grid[x, y].Life -= 1f;
        if (grid[x, y].Life <= 0)
        {
            SetPixel(x, y, PixelType.Empty);
            return;
        }

        if (y >= height - 1) return;

        if (IsEmpty(x, y + 1))
        {
            MovePixel(x, y, x, y + 1);
        }
        else if (FastRandom() < 0.5f)
        {
            int dir = FastRandom() > 0.5f ? 1 : -1;
            if (x + dir >= 0 && x + dir < width && IsEmpty(x + dir, y))
            {
                MovePixel(x, y, x + dir, y);
            }
        }
    }

    void UpdateBomb(int x, int y)
    {
        // 1. 수명 감소 (심지)
        grid[x, y].Life -= 1f;
        
        // 깜빡임 효과 (빨간색)
        if (grid[x, y].Life % 20 < 10) grid[x, y].Color = Color.red;
        else grid[x, y].Color = bombColor;

        // 2. 폭발 조건: 수명 다함 또는 불 접촉
        if (grid[x, y].Life <= 0 || HasNeighborFire(x, y))
        {
            Explode(x, y, 10); // 큰 폭발
            return;
        }

        // 3. 중력 낙하 (모래와 동일)
        if (y == 0) return;
        if (IsEmpty(x, y - 1) || IsLiquid(x, y - 1) || IsGas(x, y - 1))
        {
            MoveOrSwap(x, y, x, y - 1);
        }
    }

    public void Explode(int cx, int cy, int radius)
    {
        // 1. 충격파 (Shockwave): 주변 픽셀에 속도 부여
        int shockRadius = radius + 8; // 범위 약간 증가
        
        for (int x = cx - shockRadius; x <= cx + shockRadius; x++)
        {
            for (int y = cy - shockRadius; y <= cy + shockRadius; y++)
            {
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    float dist = Vector2.Distance(new Vector2(cx, cy), new Vector2(x, y));
                    
                    // 폭발 중심부는 파괴 (불 생성)
                    if (dist <= radius)
                    {
                        Pixel p = grid[x, y];
                        if (p.Type == PixelType.Stone && FastRandom() > 0.5f) continue;
                        if (p.Type == PixelType.Mineral && FastRandom() > 0.2f) continue;
                        
                        SetPixel(x, y, PixelType.Fire);
                        grid[x, y].Life = FastRandomRange(3f, 6f); // 폭발 불꽃은 아주 짧게 (0.06~0.12초)
                        // 불에도 속도를 주어 퍼지게 함
                        grid[x, y].Velocity = (new Vector2(x, y) - new Vector2(cx, cy)).normalized * FastRandomRange(5f, 10f); // 속도 증가
                    }
                    // 외곽부는 속도 부여 (파편 효과)
                    else if (dist <= shockRadius)
                    {
                        Pixel p = grid[x, y];
                        // 이동 가능한 픽셀만
                        if (p.Type == PixelType.Sand || p.Type == PixelType.Water || p.Type == PixelType.Stone || p.Type == PixelType.Bomb)
                        {
                            // 중심에서 바깥으로 향하는 벡터
                            Vector2 dir = (new Vector2(x, y) - new Vector2(cx, cy)).normalized;
                            float force = (shockRadius - dist) * 1.5f; // 힘 조절
                            
                            // 속도 부여
                            p.Velocity += dir * force;
                            p.Updated = true; // 이번 프레임 처리 완료
                            grid[x, y] = p;
                        }
                    }
                }
            }
        }
    }

    bool HasNeighborFire(int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    if (grid[nx, ny].Type == PixelType.Fire) return true;
                }
            }
        }
        return false;
    }

    void IgniteNeighbors(int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    if (grid[nx, ny].Type == PixelType.Gas)
                    {
                        SetPixel(nx, ny, PixelType.Fire); // 가스 점화
                    }
                }
            }
        }
    }

    bool IsEmpty(int x, int y)
    {
        return grid[x, y].Type == PixelType.Empty;
    }

    bool IsLiquid(int x, int y)
    {
        return isLiquidTable[(int)grid[x, y].Type];
    }

    bool IsGas(int x, int y)
    {
        return isGasTable[(int)grid[x, y].Type];
    }

    void MovePixel(int x1, int y1, int x2, int y2)
    {
        Pixel p = grid[x1, y1];

        grid[x2, y2] = p;
        grid[x2, y2].Updated = true;
        grid[x1, y1] = new Pixel { Type = PixelType.Empty, Color = emptyColor, Updated = true };
    }

    void SwapPixel(int x1, int y1, int x2, int y2)
    {
        Pixel p1 = grid[x1, y1];
        Pixel p2 = grid[x2, y2];

        grid[x2, y2] = p1;
        grid[x1, y1] = p2;
        grid[x1, y1].Updated = true; // p2가 x1,y1으로 왔음
    }

    void MoveOrSwap(int x1, int y1, int x2, int y2)
    {
        if (IsEmpty(x2, y2)) MovePixel(x1, y1, x2, y2);
        else SwapPixel(x1, y1, x2, y2);
    }

    public void WorldToGrid(Vector3 worldPos, out int x, out int y)
    {
        float ppu = 100f;
        float worldWidth = width / ppu;
        float worldHeight = height / ppu;

        float localX = worldPos.x + (worldWidth / 2f);
        float localY = worldPos.y + (worldHeight / 2f);

        x = Mathf.FloorToInt(localX * ppu);
        y = Mathf.FloorToInt(localY * ppu);
    }

    public void ScrollUp(int dy)
    {
        if (dy <= 0) return;

        // 1. Shift existing pixels up
        for (int y = height - 1; y >= dy; y--)
        {
            for (int x = 0; x < width; x++)
            {
                grid[x, y] = grid[x, y - dy];
                grid[x, y].Updated = true; // Mark as updated to ensure rendering/physics update if needed
            }
        }

        // 2. clear bottom pixels (they will be filled by generator immediately after, but good practice)
        for (int y = 0; y < dy; y++)
        {
            for (int x = 0; x < width; x++)
            {
                grid[x, y] = new Pixel { Type = PixelType.Empty, Color = emptyColor, Updated = true };
            }
        }
    }

    public void FillRect(int x, int y, int w, int h, PixelType type)
    {
        for (int j = y; j < y + h; j++)
        {
            for (int i = x; i < x + w; i++)
            {
                SetPixel(i, j, type);
            }
        }
    }
}
