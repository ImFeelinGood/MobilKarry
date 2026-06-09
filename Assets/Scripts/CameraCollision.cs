using UnityEngine;

public class CameraCollision : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Camera Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 3f, -6f);
    [SerializeField] private float smoothSpeed = 10f;

    [Header("Collision Settings")]
    [SerializeField] private LayerMask collisionLayer;
    [SerializeField] private float collisionRadius = 0.3f;
    [SerializeField] private float collisionOffset = 0.2f;

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 targetPosition = target.position;
        Vector3 desiredCameraPosition = targetPosition + offset;

        Vector3 direction = desiredCameraPosition - targetPosition;
        float distance = direction.magnitude;

        Vector3 finalCameraPosition = desiredCameraPosition;

        if (Physics.SphereCast(
            targetPosition,
            collisionRadius,
            direction.normalized,
            out RaycastHit hit,
            distance,
            collisionLayer
        ))
        {
            finalCameraPosition = hit.point - direction.normalized * collisionOffset;
        }

        transform.position = Vector3.Lerp(
            transform.position,
            finalCameraPosition,
            smoothSpeed * Time.deltaTime
        );

        transform.LookAt(targetPosition);
    }
}