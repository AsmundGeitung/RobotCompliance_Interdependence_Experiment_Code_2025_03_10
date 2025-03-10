using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class BoxPickupHandler : MonoBehaviour
{
    public AgentPromptSystem agentPromptSystem;

    public AgentBehavior agentBehavior;

    public DataManager dataManager;

    private XRGrabInteractable interactable;

    private void OnEnable()
    {
        interactable = GetComponent<XRGrabInteractable>();

        if (agentPromptSystem == null)
        {
            agentPromptSystem = FindFirstObjectByType<AgentPromptSystem>();
            if (agentPromptSystem == null)
            {
                Debug.LogWarning("BoxPickupHandler: No AgentPromptSystem found in scene.");
            }
        }

        if (dataManager == null)
        {
            dataManager = FindFirstObjectByType<DataManager>();
            if (dataManager == null)
            {
                Debug.LogWarning("BoxPickupHandler: No DataManager found in scene.");
            }
        }

        if (interactable)
        {
            interactable.selectEntered.AddListener(OnBoxGrabbed);
        }
    }

    private void OnDisable()
    {
        if (interactable)
        {
            interactable.selectEntered.RemoveListener(OnBoxGrabbed);
        }
    }

    //Logic for when the participant grabs a box
    private void OnBoxGrabbed(SelectEnterEventArgs args)
    {
        // Check if this box has NOT been recorded yet (or pickupTime <= 0)
        // If there's no existing BoxData or the recorded pickupTime is zero, it's the first pickup.
        BoxIdentifier identifier = GetComponent<BoxIdentifier>();
        if (identifier != null && dataManager != null)
        {
            if (!dataManager.TryGetBoxData(identifier.boxID, out BoxData data) || data.pickupTime <= 0f)
            {
                if (agentBehavior != null)
                {
                    agentBehavior.ReleaseBox();
                    agentBehavior.robotAnimator.SetTrigger("BoxGivenAway");
                }
                dataManager.RecordBoxPickedUp(identifier.boxID, identifier.boxColor);
            }
        }

        // If the file color is ambiguous, notify the prompt system
        if (identifier != null && agentPromptSystem != null)
        {
            if (identifier.boxColor == BoxColor.AmbiguousCyan || identifier.boxColor == BoxColor.AmbiguousPink || identifier.boxColor == BoxColor.AmbiguousPurple)
            {
                agentPromptSystem.OnAmbiguousBoxPicked(gameObject);
            }
        }
    }
}

