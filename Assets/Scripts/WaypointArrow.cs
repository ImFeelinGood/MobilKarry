using UnityEngine;

public class WaypointArrow : MonoBehaviour
{
    public Transform target;
    public Transform player;

    [Header("Camera Tracking")]
    public Transform cameraTransform; // Assign this to Camera.main.transform in inspector or at runtime

    [Header("Arrow Model")]
    public GameObject arrowUI; // The visible arrow object (assign in Inspector)

    private void Update()
    {
        if (arrowUI != null)
            arrowUI.SetActive(target != null); // Hide or show arrow model

        if (target == null || player == null || cameraTransform == null) return;

        Vector3 toTarget = target.position - player.position;
        toTarget.y = 0f;

        Vector3 camForward = cameraTransform.forward;
        camForward.y = 0f;

        if (toTarget.sqrMagnitude > 0.01f)
        {
            float angle = Vector3.SignedAngle(camForward.normalized, toTarget.normalized, Vector3.up);
            transform.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void ClearTarget()
    {
        target = null;
    }
}
