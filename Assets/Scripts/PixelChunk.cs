using UnityEngine;
using System.Collections.Generic;

public class PixelChunk : MonoBehaviour
{
    public int chunkX;
    public int chunkY;
    public int size = 32;
    
    // 최적화: 청크 수면 모드
    public bool IsSleeping = false;
    public bool DidUpdate = false;

    private PixelSimulation simulation;

    void Awake()
    {
        simulation = FindObjectOfType<PixelSimulation>();
    }

    public void Initialize(int x, int y, int chunkSize)
    {
        chunkX = x;
        chunkY = y;
        size = chunkSize;
        
        float ppu = 100f;
        float worldWidth = simulation.width / ppu;
        float worldHeight = simulation.height / ppu;
        float startX = -worldWidth / 2f;
        float startY = -worldHeight / 2f;

        transform.position = new Vector3(startX + (x * size) / ppu, startY + (y * size) / ppu, 0);
    }
}
