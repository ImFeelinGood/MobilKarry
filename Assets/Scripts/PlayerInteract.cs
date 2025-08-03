using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class PlayerInteract : MonoBehaviour
{
    [Header("UI")]
    public GameObject interactPromptUI;
    public TMP_Text promptText;

    private InteractableArea currentInteractable;

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out InteractableArea area))
        {
            currentInteractable = area;
            ShowPrompt(true, area.actionName);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out InteractableArea area) && area == currentInteractable)
        {
            ShowPrompt(false, "");
            currentInteractable = null;
        }
    }

    private void OnInteract()
    {
        if (currentInteractable != null)
        {
            currentInteractable.TriggerInteract();
        }
    }

    private void ShowPrompt(bool show, string actionName)
    {
        if (interactPromptUI != null)
            interactPromptUI.SetActive(show);

        if (show && promptText != null)
        {
            string keyString = GetBindingDisplayString("Car", "Interact");
            promptText.text = $"Press <b>{keyString}</b> to {actionName}";
        }
    }

    private string GetBindingDisplayString(string actionMap, string actionName)
    {
        var inputActionAsset = PlayerInput.all[0].actions;
        var action = inputActionAsset.FindActionMap(actionMap)?.FindAction(actionName);

        if (action != null && action.bindings.Count > 0)
        {
            foreach (var binding in action.bindings)
            {
                if (!binding.isComposite && !binding.isPartOfComposite)
                {
                    return InputControlPath.ToHumanReadableString(
                        binding.effectivePath,
                        InputControlPath.HumanReadableStringOptions.OmitDevice);
                }
            }
        }

        return "key";
    }
}
