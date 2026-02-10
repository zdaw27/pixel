using UnityEngine;

public class PixelSimulation : MonoBehaviour
{
    public int width = 576; // 9:16 aspect ratio with height 1024 
    public int height = 1024; // Deeper Map!
    public float updateInterval = 0.02f;

    private Pixel[,] grid;
    private float timer;
    
    // Colors
    public Color32 stoneColor = Color.gray;
    public Color32 sandColor = new Color32(255, 204, 51, 255); // Approximately 1f, 0.8f, 0.2f
    public Color32 waterColor = new Color32(51, 102, 255, 255); // 0.2f, 0.4f, 1f
    public Color32 emptyColor = Color.black;
    public Color32 gasColor = new Color32(153, 255, 153, 128); // 0.6f, 1f, 0.6f, 0.5f
    public Color32 smokeColor = new Color32(128, 128, 128, 204); // 0.5f, 0.5f, 0.5f, 0.8f
    public Color32 bombColor = new Color32(51, 51, 51, 255); // 0.2f, 0.2f, 0.2f

    // Mineral Colors
    public Color32 ironColor = new Color32(204, 179, 153, 255);      
    public Color32 copperColor = new Color32(217, 128, 77, 255);   
    public Color32 goldColor = new Color32(255, 214, 0, 255);         
    public Color32 emeraldColor = new Color32(0, 204, 102, 255);     
    public Color32 rubyColor = new Color32(230, 26, 51, 255);      
    public Color32 diamondColor = new Color32(102, 204, 255, 255);     

    public static PixelSimulation Instance { get; private set; }

    private bool[] isSolidTable;
    private bool[] isLiquidTable;
    private bool[] isGasTable;

    private uint rngState = 123456789;
    private float FastRandom()
    {
            rngState ^= rngState << 13;
            rngState ^= rngState >> 17;
            rngState ^= rngState << 5;
            return (rngState & 0xFFFFFF) / 16777216.0f; 
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

        InitTables();
        
        grid = new Pixel[width, height];
        ClearGrid();
    }

    void InitTables()
    {
        int maxType = (int)System.Enum.GetValues(typeof(PixelType)).Length;
        isSolidTable = new bool[maxType];
        isLiquidTable = new bool[maxType];
        isGasTable = new bool[maxType];

        isSolidTable[(int)PixelType.Sand] = true;
        isSolidTable[(int)PixelType.Stone] = true;
        isSolidTable[(int)PixelType.Bomb] = true;
        
        // Minerals are Solid
        isSolidTable[(int)PixelType.Iron] = true;
        isSolidTable[(int)PixelType.Copper] = true;
        isSolidTable[(int)PixelType.Gold] = true;
        isSolidTable[(int)PixelType.Emerald] = true;
        isSolidTable[(int)PixelType.Ruby] = true;
        isSolidTable[(int)PixelType.Diamond] = true;

        isLiquidTable[(int)PixelType.Water] = true;

        isGasTable[(int)PixelType.Gas] = true;
        isGasTable[(int)PixelType.Smoke] = true;
        isGasTable[(int)PixelType.Fire] = true;
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
            FillRect(0, 0, width, height / 2, PixelType.Stone);
    }

    public int GetHardness(PixelType type)
    {
        switch (type)
        {
            case PixelType.Sand: return 1;
            case PixelType.Stone: return 5; 
            case PixelType.Iron: return 10;
            case PixelType.Copper: return 15;
            case PixelType.Gold: return 20;
            case PixelType.Emerald: return 30;
            case PixelType.Ruby: return 50;
            case PixelType.Diamond: return 100;
            case PixelType.Bomb: return 1;
            default: return 0;
        }
    }

    public void DamagePixel(int x, int y, float damage)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        if (grid[x, y].Type == PixelType.Empty) return;

        grid[x, y].Life -= damage;

        if (grid[x, y].Life > 0)
        {
             Color original = (Color)grid[x, y].Color;
             // Revert to White damage as per user request (better visibility)
             grid[x, y].Color = (Color32)Color.Lerp(original, Color.white, 0.6f); 
             grid[x, y].Updated = true; 
        }

        if (grid[x, y].Life <= 0)
        {
            int value = GetMineralValue(grid[x, y].Type);
            if (value > 0)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.AddMoney(value);
                }
            }

            SetPixel(x, y, PixelType.Empty);
        }
    }

    public int GetMineralValue(PixelType type)
    {
            switch (type)
            {
                case PixelType.Iron: return 10;
                case PixelType.Copper: return 20;
                case PixelType.Gold: return 50;
                case PixelType.Emerald: return 100;
                case PixelType.Ruby: return 200;
                case PixelType.Diamond: return 500;
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
        if (isSolidTable == null) InitTables(); 
        return isSolidTable[(int)type];
    }

    public void SetPixel(int x, int y, PixelType type)
    {
        if (x >= 0 && x < width && y >= 0 && y < height)
        {
            Color32 c = emptyColor;
            float life = 0f;

            switch (type)
            {
                case PixelType.Stone: c = stoneColor; life = 100f; break; // Hardness is damage resistance, Life is HP (Reduced for better destruction)
                case PixelType.Sand: c = sandColor; life = 30f; break;
                case PixelType.Water: c = waterColor; break;
                case PixelType.Gas: c = gasColor; break;
                
                case PixelType.Iron: c = ironColor; life = GetHardness(PixelType.Iron); break;
                case PixelType.Copper: c = copperColor; life = GetHardness(PixelType.Copper); break;
                case PixelType.Gold: c = goldColor; life = GetHardness(PixelType.Gold); break;
                case PixelType.Emerald: c = emeraldColor; life = GetHardness(PixelType.Emerald); break;
                case PixelType.Ruby: c = rubyColor; life = GetHardness(PixelType.Ruby); break;
                case PixelType.Diamond: c = diamondColor; life = GetHardness(PixelType.Diamond); break;

                case PixelType.Fire: 
                    c = (Color32)new Color(1f, FastRandomRange(0.2f, 0.6f), 0f); 
                    life = FastRandomRange(50f, 100f); 
                    break;
                case PixelType.Smoke: 
                    c = smokeColor; 
                    life = FastRandomRange(100f, 200f); 
                    break;
                case PixelType.Bomb:
                    c = bombColor;
                    life = 200f; 
                    break;
            }
            grid[x, y] = new Pixel { Type = type, Color = c, Updated = false, Life = life };
        }
    }

    void Simulate()
    {
        if (grid == null) Awake();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y].Updated = false;
            }
        }

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
        p.Velocity.y -= 0.5f; 
        p.Velocity *= 0.98f;

        int targetX = x + Mathf.RoundToInt(p.Velocity.x);
        int targetY = y + Mathf.RoundToInt(p.Velocity.y);

        if (targetX < 0 || targetX >= width || targetY < 0 || targetY >= height)
        {
            p.Velocity = Vector2.zero;
            grid[x, y] = p;
            return;
        }

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
        if (IsEmpty(x, y - 1) || IsLiquid(x, y - 1) || IsGas(x, y - 1)) MoveOrSwap(x, y, x, y - 1);
        else if (x > 0 && (IsEmpty(x - 1, y - 1) || IsLiquid(x - 1, y - 1) || IsGas(x - 1, y - 1))) MoveOrSwap(x, y, x - 1, y - 1);
        else if (x < width - 1 && (IsEmpty(x + 1, y - 1) || IsLiquid(x + 1, y - 1) || IsGas(x + 1, y - 1))) MoveOrSwap(x, y, x + 1, y - 1);
    }

    void UpdateWater(int x, int y)
    {
        if (y == 0) return;
        if (IsEmpty(x, y - 1) || IsGas(x, y - 1)) MoveOrSwap(x, y, x, y - 1);
        else
        {
            int dir = FastRandom() > 0.5f ? 1 : -1; 
            if (x + dir >= 0 && x + dir < width && (IsEmpty(x + dir, y) || IsGas(x + dir, y))) MoveOrSwap(x, y, x + dir, y);
            else if (x - dir >= 0 && x - dir < width && (IsEmpty(x - dir, y) || IsGas(x - dir, y))) MoveOrSwap(x, y, x - dir, y);
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

        if (IsEmpty(x, y + 1) || IsLiquid(x, y + 1)) MoveOrSwap(x, y, x, y + 1);
        else
        {
            int dir = FastRandom() > 0.5f ? 1 : -1;
            if (x + dir >= 0 && x + dir < width && IsEmpty(x + dir, y)) MovePixel(x, y, x + dir, y);
            else if (x - dir >= 0 && x - dir < width && IsEmpty(x - dir, y)) MovePixel(x, y, x - dir, y);
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

        grid[x, y].Color = (Color32)new Color(1f, FastRandomRange(0.1f, 0.7f), 0f);
        IgniteNeighbors(x, y);

        if (grid[x, y].Life > 10 && y < height - 1 && IsEmpty(x, y + 1) && FastRandom() < 0.1f)
        {
            SetPixel(x, y + 1, PixelType.Fire);
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

        if (IsEmpty(x, y + 1)) MovePixel(x, y, x, y + 1);
        else if (FastRandom() < 0.5f)
        {
            int dir = FastRandom() > 0.5f ? 1 : -1;
            if (x + dir >= 0 && x + dir < width && IsEmpty(x + dir, y)) MovePixel(x, y, x + dir, y);
        }
    }

    void UpdateBomb(int x, int y)
    {
        grid[x, y].Life -= 1f;
        
        if (grid[x, y].Life % 20 < 10) grid[x, y].Color = new Color32(255, 0, 0, 255);
        else grid[x, y].Color = bombColor;

        if (grid[x, y].Life <= 0 || HasNeighborFire(x, y))
        {
            Explode(x, y, 10); 
            return;
        }

        if (y == 0) return;
        if (IsEmpty(x, y - 1) || IsLiquid(x, y - 1) || IsGas(x, y - 1)) MoveOrSwap(x, y, x, y - 1);
    }

    public void Explode(int cx, int cy, int radius)
    {
        int shockRadius = radius + 8; 
        for (int x = cx - shockRadius; x <= cx + shockRadius; x++)
        {
            for (int y = cy - shockRadius; y <= cy + shockRadius; y++)
            {
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    float dist = Vector2.Distance(new Vector2(cx, cy), new Vector2(x, y));
                    if (dist <= radius)
                    {
                        Pixel p = grid[x, y];
                        
                        // 1. Damage existing solids
                        if (p.Type != PixelType.Empty)
                        {
                            if (GetHardness(p.Type) >= 10 && FastRandom() > 0.3f) 
                            {
                                DamagePixel(x, y, 100); 
                                continue;
                            }
                           // Gradient Damage Logic
                        float damageRatio = 1.0f - (dist / radius);
                        // Center = 300 damage (Instant Kill), Edge = ~50 damage
                        float damage = damageRatio * 300f + 50f; 
                        
                        DamagePixel(x, y, damage);
                        }
                        
                        // 2. Create Fire effect (visuals)
                        // If it's now empty (either was empty or just destroyed), add fire
                        if (grid[x, y].Type == PixelType.Empty)
                        {
                            if (FastRandom() < 0.3f) // Don't fill 100%, 30% fill for better look
                            {
                                SetPixel(x, y, PixelType.Fire);
                                grid[x, y].Life = FastRandomRange(10f, 30f); // Short life fire
                                grid[x, y].Velocity = (new Vector2(x, y) - new Vector2(cx, cy)).normalized * FastRandomRange(5f, 15f);
                            }
                        }
                        
                        // 3. Impart velocity to surviving pixels
                        if (grid[x, y].Type != PixelType.Empty)
                        {
                             grid[x, y].Velocity = (new Vector2(x, y) - new Vector2(cx, cy)).normalized * FastRandomRange(5f, 10f); 
                        }
                    }
                    else if (dist <= shockRadius)
                    {
                        Pixel p = grid[x, y];
                        if (p.Type == PixelType.Sand || p.Type == PixelType.Water || p.Type == PixelType.Stone || p.Type == PixelType.Bomb)
                        {
                            Vector2 dir = (new Vector2(x, y) - new Vector2(cx, cy)).normalized;
                            float force = (shockRadius - dist) * 1.5f; 
                            p.Velocity += dir * force;
                            p.Updated = true; 
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
                    if (grid[nx, ny].Type == PixelType.Gas) SetPixel(nx, ny, PixelType.Fire);
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
        grid[x1, y1].Updated = true; 
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
        for (int y = height - 1; y >= dy; y--)
        {
            for (int x = 0; x < width; x++)
            {
                grid[x, y] = grid[x, y - dy];
                grid[x, y].Updated = true; 
            }
        }
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
