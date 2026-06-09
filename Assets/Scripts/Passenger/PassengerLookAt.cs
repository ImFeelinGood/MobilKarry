using UnityEngine;

public class LookAtPlayer3D_Assigned : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private bool lockVerticalRotation = true;

    private Transform player;

    public void SetPlayer(Transform playerTransform)
    {
        player = playerTransform;
    }

    private void Update()
    {
        if (player == null)
            return;

        Vector3 direction = player.position - transform.position;

        if (lockVerticalRotation)
            direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}