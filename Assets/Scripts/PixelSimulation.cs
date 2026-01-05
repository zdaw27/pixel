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
    
    // 청크 시스템
    public int chunkSize = 32;
    private PixelChunk[,] chunks;
    private int chunksX;
    private int chunksY;

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
        
        InitializeChunks();
    }

    void InitializeChunks()
    {
        chunksX = Mathf.CeilToInt((float)width / chunkSize);
        chunksY = Mathf.CeilToInt((float)height / chunkSize);
        chunks = new PixelChunk[chunksX, chunksY];

        GameObject chunkRoot = new GameObject("Chunks");
        chunkRoot.transform.SetParent(transform);

        for (int x = 0; x < chunksX; x++)
        {
            for (int y = 0; y < chunksY; y++)
            {
                GameObject obj = new GameObject($"Chunk_{x}_{y}");
                obj.transform.SetParent(chunkRoot.transform);
                // 위치는 Chunk 내부에서 로컬로 처리하거나 여기서 설정
                // PixelChunk가 transform.position을 설정하므로 여기선 패스
                
                PixelChunk chunk = obj.AddComponent<PixelChunk>();
                chunk.Initialize(x, y, chunkSize);
                chunks[x, y] = chunk;
            }
        }
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
    
    public int LastFrameChunkUpdates { get; private set; }
    public int ActiveChunkCount { get; private set; }

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
        
        float surfaceNoiseScale = 0.05f;
        float caveScale = 0.05f;
        
        for (int x = 0; x < width; x++)
        {
            float noiseVal = Mathf.PerlinNoise(x * surfaceNoiseScale, 0);
            int groundHeight = Mathf.FloorToInt(height * 0.8f + noiseVal * 10f);

            for (int y = 0; y < groundHeight; y++)
            {
                float caveNoise = Mathf.PerlinNoise(x * caveScale, y * caveScale);
                if (caveNoise > 0.65f) continue; 

                float depthRatio = (float)y / groundHeight;

                if (depthRatio > 0.7f)
                {
                    SetPixel(x, y, PixelType.Sand);
                }
                else if (depthRatio > 0.3f)
                {
                    if (Random.value > 0.9f) SetPixel(x, y, PixelType.Sand);
                    else SetPixel(x, y, PixelType.Stone);
                }
                else
                {
                    float mineralNoise = Mathf.PerlinNoise(x * 0.1f + 500, y * 0.1f + 500);
                    
                    if (mineralNoise > 0.6f) SetPixel(x, y, PixelType.Mineral);
                    else SetPixel(x, y, PixelType.Stone);
                }
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
            // 최적화: 고체 상태가 변할 때만 콜라이더 갱신 요청
            PixelType oldType = grid[x, y].Type;
            bool wasSolid = IsSolid(oldType);
            bool isSolid = IsSolid(type);

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
            
            WakeChunk(x, y); // 픽셀 변경 시 무조건 깨움
        }
    }

    public bool useChunkOptimization = true; // 청크 최적화(수면 모드) 사용 여부

    void Simulate()
    {
        if (grid == null) Awake();

        if (useChunkOptimization)
        {
            // 1. 청크 수면 상태 업데이트
            for (int x = 0; x < chunksX; x++)
            {
                for (int y = 0; y < chunksY; y++)
                {
                    PixelChunk chunk = chunks[x, y];
                    if (chunk == null) continue;

                    if (!chunk.DidUpdate) chunk.IsSleeping = true;
                    chunk.DidUpdate = false;
                }
            }
        }

        // 2. 그리드 업데이트 플래그 초기화
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y].Updated = false;
            }
        }

        // 3. 시뮬레이션 루프
        ActiveChunkCount = 0;
        for (int cx = 0; cx < chunksX; cx++)
        {
            for (int cy = 0; cy < chunksY; cy++)
            {
                PixelChunk chunk = chunks[cx, cy];
                if (chunk == null) continue;
                
                // 최적화가 켜져있고 수면 중이면 건너뜀
                if (useChunkOptimization && chunk.IsSleeping) continue;
                
                ActiveChunkCount++;

                int startX = cx * chunkSize;
                int endX = Mathf.Min((cx + 1) * chunkSize, width);
                int startY = cy * chunkSize;
                int endY = Mathf.Min((cy + 1) * chunkSize, height);

                for (int y = startY; y < endY; y++)
                {
                    for (int x = startX; x < endX; x++)
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
        }
    }

    public void WakeChunk(int x, int y)
    {
        if (chunks == null) return;
        int cx = x / chunkSize;
        int cy = y / chunkSize;
        
        if (cx >= 0 && cx < chunksX && cy >= 0 && cy < chunksY)
        {
            PixelChunk chunk = chunks[cx, cy];
            if (chunk != null)
            {
                chunk.IsSleeping = false;
                chunk.DidUpdate = true;
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

        // 상태 변화가 있으므로 청크 깨우기
        WakeChunk(x, y);

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
        WakeChunk(x, y); // 불은 계속 변함

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
        WakeChunk(x, y); // 연기도 계속 변함

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
        WakeChunk(x, y); // 폭탄도 계속 변함

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
        
        WakeChunk(x1, y1);
        WakeChunk(x2, y2);
    }

    void SwapPixel(int x1, int y1, int x2, int y2)
    {
        Pixel p1 = grid[x1, y1];
        Pixel p2 = grid[x2, y2];

        grid[x2, y2] = p1;
        grid[x1, y1] = p2;
        grid[x1, y1].Updated = true; // p2가 x1,y1으로 왔음
        
        WakeChunk(x1, y1);
        WakeChunk(x2, y2);
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
}
