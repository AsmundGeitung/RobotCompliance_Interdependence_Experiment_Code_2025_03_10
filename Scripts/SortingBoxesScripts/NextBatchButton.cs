using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class NextBatchButton : MonoBehaviour
{
    [Header("References")]
    public FlowManager gameFlowManager;
    public AgentBehavior agentBehavior;

    [Header("Button Animation")]
    public Transform buttonVisual;
    public float nudgeDistance = 0.2f;
    public float nudgeSpeed = 0.2f;

    [Header("Cooldown Settings")]
    public float cooldownDuration = 10f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip buttonPressClip;

    private XRSimpleInteractable interactable;
    private bool isCooldown = false;

    private void Awake()
    {
        // Grab the XR interaction component
        interactable = GetComponent<XRSimpleInteractable>();
        if (interactable)
        {
            interactable.selectEntered.AddListener(OnButtonPressed);
        }
        else
        {
            Debug.LogWarning("NextBatchButton: No XRSimpleInteractable found on this object.");
        }
    }

    private void Start()
    {
        agentBehavior = FindFirstObjectByType<AgentBehavior>();
        if (agentBehavior == null)
            Debug.LogWarning("NextBatchButton: No AgentBehavior found in scene.");
    }

    private void OnDestroy()
    {
        // Clean up listener
        if (interactable)
        {
            interactable.selectEntered.RemoveListener(OnButtonPressed);
        }
    }

    private void OnButtonPressed(SelectEnterEventArgs args)
    {
        
        if (isCooldown) return;

        agentBehavior.ReleaseBox();

        // 1) Play the button press sound.
        if (audioSource != null && buttonPressClip != null)

        {
            audioSource.PlayOneShot(buttonPressClip, 0.2f);
        }
        
        // 2) Perform the batch logic via GameFlowManager.
        if (gameFlowManager != null)
        {
            gameFlowManager.OnNextBatchRequested();
        }
        else
        {
            Debug.LogWarning("NextBatchButton: No GameFlowManager assigned.");
        }

        // 3) Animate the button press.
        StartCoroutine(ButtonNudge());

        // 4) Start the cooldown period.
        isCooldown = true;
        Invoke(nameof(ResetCooldown), cooldownDuration);
    }

    private void ResetCooldown()
    {
        isCooldown = false;
    }

    /// <summary>
    /// Simple coroutine to visually "press" the button down, then move it back up.
    /// </summary>
    private System.Collections.IEnumerator ButtonNudge()
    {
        if (buttonVisual == null)
            yield break;

        // Save original local position.
        Vector3 originalPos = buttonVisual.localPosition;
        // Calculate pressed position
        Vector3 pressedPos = originalPos - new Vector3(0, nudgeDistance, 0);

        float elapsed = 0f;
        while (elapsed < nudgeSpeed)
        {
            float t = elapsed / nudgeSpeed;
            buttonVisual.localPosition = Vector3.Lerp(originalPos, pressedPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        buttonVisual.localPosition = pressedPos;

        // Return back to the original position.
        elapsed = 0f;
        while (elapsed < nudgeSpeed)
        {
            float t = elapsed / nudgeSpeed;
            buttonVisual.localPosition = Vector3.Lerp(pressedPos, originalPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        buttonVisual.localPosition = originalPos;
    }

    [ContextMenu("Simulate Button Press")]
    public void SimulateButtonPress()
    {
        if (isCooldown)
        {
            Debug.Log("NextBatchButton: Button is on cooldown, can't simulate press.");
            return;
        }
        OnButtonPressed(null);
    }
}
