using UnityEngine;

public class DrillController : MonoBehaviour
{
    public float shakeAmount = 0.1f;
    public float followSpeed = 20f;
    public Vector3 offset = new Vector3(0.5f, -0.5f, 0); // 마우스 커서에 드릴 팁을 맞추기 위한 오프셋

    private bool isDrilling = false;
    private Camera mainCamera;
    private Vector3 targetPosition;

    void Start()
    {
        mainCamera = Camera.main;
        // 커서 숨기기
        Cursor.visible = false;
    }

    void Update()
    {
        FollowMouse();
        if (isDrilling)
        {
            Shake();
        }
        else
        {
            transform.rotation = Quaternion.identity;
        }
    }

    void FollowMouse()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = 10f; // 카메라 앞
        targetPosition = mainCamera.ScreenToWorldPoint(mousePos) + offset;
        
        // 부드럽게 따라가기
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSpeed);
    }

    void Shake()
    {
        // 무작위 진동
        Vector3 randomOffset = Random.insideUnitCircle * shakeAmount;
        transform.position = targetPosition + randomOffset;
        
        // 살짝 회전 진동
        float randomAngle = Random.Range(-5f, 5f);
        transform.rotation = Quaternion.Euler(0, 0, randomAngle);
    }

    public void SetDrilling(bool drilling)
    {
        isDrilling = drilling;
    }

    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
        Cursor.visible = !active; // 드릴이 활성화되면 커서 숨김
    }
}
