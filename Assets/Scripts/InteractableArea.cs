using UnityEngine;
using UnityEngine.Events;

public class InteractableArea : MonoBehaviour
{
    public UnityEvent onInteract;
    [Tooltip("Displayed action (e.g., 'interact', 'repair', 'open door')")]
    public string actionName = "interact";

    [Header("Interaction Options")]
    public bool stopTimeOnInteract = false;

    public void TriggerInteract()
    {
        if (stopTimeOnInteract)
        {
            Debug.Log("Time stopped due to interaction.");
            Time.timeScale = 0f;
        }

        onInteract.Invoke();
    }

    public void ResumeTime()
    {
        Time.timeScale = 1f;
    }
}
