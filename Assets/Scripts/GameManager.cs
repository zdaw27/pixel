using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int Money { get; private set; }
    
    [Header("Skill Prefabs")]
    public GameObject dynamitePrefab;
    public GameObject ballPrefab; 

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        
        Money = 0;
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) UseSkillTNT();
        if (Input.GetKeyDown(KeyCode.Alpha2)) UseSkillEnlarge();
        if (Input.GetKeyDown(KeyCode.Alpha3)) UseSkillCopy();
        if (Input.GetKeyDown(KeyCode.Alpha4)) UseSkillBalls();
    }

    public void AddMoney(int amount)
    {
        Money += amount;
    }

    public void UseSkillTNT()
    {
        // Logic: Throw from Blade position towards Mouse
        BladeController blade = FindObjectOfType<BladeController>();
        Vector3 spawnPos = Vector3.zero;
        if (blade != null) spawnPos = blade.transform.position;
        else spawnPos = Camera.main.ScreenToWorldPoint(Input.mousePosition); 
        spawnPos.z = 0;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        
        if (dynamitePrefab != null)
        {
             GameObject tnt = Instantiate(dynamitePrefab, spawnPos, Quaternion.identity);
             tnt.SetActive(true);
             
             Rigidbody2D rb = tnt.GetComponent<Rigidbody2D>();
             if (rb != null)
             {
                 Vector2 dir = (mousePos - spawnPos).normalized;
                 float force = 10f; // Throw force
                 rb.linearVelocity = dir * force;
                 rb.angularVelocity = Random.Range(-300f, 300f);
             }
        }
    }

    public void UseSkillEnlarge()
    {
        BladeController[] blades = FindObjectsOfType<BladeController>();
        foreach(var blade in blades)
        {
            blade.Enlarge(10f);
        }
    }

    public void UseSkillCopy()
    {
        BladeController blade = FindObjectOfType<BladeController>();
        if (blade != null)
        {
            GameObject clone = Instantiate(blade.gameObject, blade.transform.position, Quaternion.identity);
            clone.name = "Blade_Clone";
             // Give clone some random velocity so they don't stick
            Rigidbody2D rb = clone.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Random.insideUnitCircle * 5f;
        }
    }

    public void UseSkillBalls()
    {
         for(int i=0; i<3; i++)
         {
             Vector3 spawnPos = new Vector3(Random.Range(-2f, 2f), 4f, 0);
             if (ballPrefab != null) 
             {
                 GameObject ball = Instantiate(ballPrefab, spawnPos, Quaternion.identity);
                 ball.SetActive(true);
                 if(ball.GetComponent<Rigidbody2D>() != null)
                 {
                     ball.GetComponent<Rigidbody2D>().linearVelocity = Random.insideUnitCircle * 5f;
                 }
             }
         }
    }
}
