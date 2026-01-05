using UnityEngine;

public class Dynamite : MonoBehaviour
{
    public float fuseTime = 3.0f; // 심지 시간
    public int explosionRadius = 10; // 폭발 반경
    
    private float timer;
    private bool exploded = false;
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        timer = fuseTime;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (exploded) return;

        timer -= Time.deltaTime;

        // 깜빡임 효과 (빨간색)
        if (timer < 1.0f)
        {
            float flashSpeed = 10f; // 1초 남았을 때 빠르게 깜빡임
            float t = Mathf.PingPong(Time.time * flashSpeed, 1f);
            spriteRenderer.color = Color.Lerp(Color.white, Color.red, t);
        }
        else if (timer % 0.5f < 0.25f) // 천천히 깜빡임
        {
            spriteRenderer.color = Color.red;
        }
        else
        {
            spriteRenderer.color = Color.white;
        }

        if (timer <= 0)
        {
            Explode();
        }
    }

    void Explode()
    {
        if (exploded) return;
        exploded = true;

        // 픽셀 시뮬레이션에 폭발 요청
        if (PixelSimulation.Instance != null)
        {
            int gridX, gridY;
            PixelSimulation.Instance.WorldToGrid(transform.position, out gridX, out gridY);
            PixelSimulation.Instance.Explode(gridX, gridY, explosionRadius);
        }

        Destroy(gameObject);
    }

    // 불(Fire 픽셀)에 닿으면 즉시 폭발하는 로직은
    // PixelSimulation에서 Dynamite 오브젝트를 감지하기 어려우므로,
    // 여기서는 간단하게 타이머 폭발만 구현하거나, 
    // 추후 Collider를 통해 픽셀과 상호작용하는 복잡한 로직을 추가할 수 있음.
}
