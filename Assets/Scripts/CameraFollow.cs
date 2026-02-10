using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 0.125f;
    public Vector3 offset = new Vector3(0, 0, -10f);
    public float minSize = 2f;
    public float maxSize = 8f;
    public float zoomSpeed = 2f;

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (target == null)
        {
            // Auto-find blade
            BladeController blade = FindObjectOfType<BladeController>();
            if (blade != null) target = blade.transform;
        }
    }

    void LateUpdate()
    {
        if (target == null) 
        {
             BladeController blade = FindObjectOfType<BladeController>();
             if (blade != null) target = blade.transform;
             return;
        }

        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        // Lock X/Y to prevent seeing too much void if needed, but for now just follow
        transform.position = smoothedPosition;

        // Simple Wheel Zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            cam.orthographicSize -= scroll * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minSize, maxSize);
        }
    }
}
