using UnityEngine;

public enum PixelType
{
    Empty,
    Stone,
    Sand,
    Water,
    // Minerals
    Iron,
    Copper,
    Gold,
    Emerald,
    Ruby,
    Diamond,
    // Others
    Gas,
    Fire,
    Smoke,
    Bomb
}

public struct Pixel
{
    public PixelType Type;
    public Color32 Color;
    public bool Updated; 
    public float Life; 
    public Vector2 Velocity; 
}
