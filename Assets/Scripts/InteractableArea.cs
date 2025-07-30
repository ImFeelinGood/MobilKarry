using UnityEngine;
using UnityEngine.Events;
using TMPro;
using UnityEngine.InputSystem;

public class InteractableArea : MonoBehaviour
{
    public UnityEvent onInteract;
    public GameObject interactPromptUI;
    public TMP_Text promptText; // Assign this in inspector

    [Tooltip("Displayed action (e.g., 'interact', 'repair', 'open door')")]
    public string actionName = "interact"; // Can be overridden per object

    private void Start()
    {
        if (interactPromptUI != null)
            interactPromptUI.SetActive(false);
    }

    public void ShowPrompt(bool show)
    {
        if (interactPromptUI != null)
            interactPromptUI.SetActive(show);

        if (show && promptText != null)
        {
            string keyString = GetBindingDisplayString("Car", "Interact");
            promptText.text = $"Press <b>{keyString}</b> to {actionName}";
        }
    }

    public void TriggerInteract()
    {
        onInteract.Invoke();
    }

    private string GetBindingDisplayString(string actionMap, string actionName)
    {
        var inputActionAsset = PlayerInput.all[0].actions; // Assumes single player
        var action = inputActionAsset.FindActionMap(actionMap)?.FindAction(actionName);

        if (action != null && action.bindings.Count > 0)
        {
            // Get display name of first non-composite binding (e.g., "E")
            foreach (var binding in action.bindings)
            {
                if (!binding.isComposite && !binding.isPartOfComposite)
                    return InputControlPath.ToHumanReadableString(
                        binding.effectivePath,
                        InputControlPath.HumanReadableStringOptions.OmitDevice);
            }
        }

        return "key";
    }
}
