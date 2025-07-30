using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    private InteractableArea currentInteractable;

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out InteractableArea area))
        {
            currentInteractable = area;
            area.ShowPrompt(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out InteractableArea area) && area == currentInteractable)
        {
            area.ShowPrompt(false);
            currentInteractable = null;
        }
    }

    // This is called automatically by PlayerInput (Send Messages mode)
    private void OnInteract()
    {
        if (currentInteractable != null)
        {
            currentInteractable.TriggerInteract();
        }
    }
}
