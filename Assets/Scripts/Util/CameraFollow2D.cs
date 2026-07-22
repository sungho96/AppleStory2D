using UnityEngine;

public class CameraFollow2D : MonoBehaviour

{
    public Transform target;
    public float smoothSpeed = 5f;
    public Vector3 offset;

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPostion = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPostion, smoothSpeed * Time.deltaTime);

        transform.position = new Vector3(smoothedPosition.x, smoothedPosition.y, transform.position.z);
    }
}
