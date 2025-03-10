using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class TrashCompactorButton : MonoBehaviour
{
    public BoxSortingHandler boxSortingHandler;
    public DataManager dataManager;

    [Header("Button Visual (Optional)")]
    public Transform buttonVisual;

    [Header("Move Object Settings")]
    public Transform movingObject;
    public AudioSource audioSource;
    public AudioClip moveSoundClip;

    [Header("Cooldown Settings")]
    public float cooldownDuration = 10.0f;
    public float nudgeDistance = 0.2f;
    public float nudgeSpeed = 0.2f;

    private XRSimpleInteractable interactable;
    private bool isCooldown = false;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        interactable.selectEntered.AddListener(OnButtonPressed);
    }

    private void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnButtonPressed);
        }
    }

    private void OnButtonPressed(SelectEnterEventArgs args)
    {
        if (isCooldown) return;

        StartCoroutine(DelayedFinalizeSortBoxes());
        StartCoroutine(ButtonNudge());
        StartCoroutine(MoveAndCooldownRoutine());
    }

    private IEnumerator DelayedFinalizeSortBoxes()
    {
        yield return new WaitForSeconds(1f);
        if (boxSortingHandler != null)
        {
            bool anyCorrect = boxSortingHandler.FinalizeSortedBoxes();
        }
        else
        {
            Debug.LogWarning("No BoxSortingHandler reference assigned.");
        }
    }

    private IEnumerator MoveAndCooldownRoutine()
    {
        isCooldown = true;
        float cooldownEndTime = Time.time + cooldownDuration;

        if (movingObject == null)
        {
            Debug.LogWarning("CompressorButton: No movingObject assigned.");
        }
        else if (audioSource == null)
        {
            Debug.LogWarning("CompressorButton: No audioSource assigned.");
        }
        else if (moveSoundClip == null)
        {
            Debug.LogWarning("CompressorButton: No moveSoundClip assigned.");
        }
        else
        {
            Vector3 originalPos = movingObject.localPosition;
            Vector3 targetPos = originalPos + Vector3.up * 1f;
            float moveDuration = 2f;
            float elapsed = 0f;

            audioSource.clip = moveSoundClip;
            audioSource.loop = true;
            audioSource.Play();

            while (elapsed < moveDuration)
            {
                movingObject.localPosition = Vector3.Lerp(originalPos, targetPos, elapsed / moveDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            movingObject.localPosition = targetPos;

            yield return new WaitForSeconds(5f);

            elapsed = 0f;
            while (elapsed < moveDuration)
            {
                movingObject.localPosition = Vector3.Lerp(targetPos, originalPos, elapsed / moveDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            movingObject.localPosition = originalPos;

            audioSource.Stop();
            audioSource.loop = false;
        }

        float remainingCooldown = cooldownEndTime - Time.time;
        if (remainingCooldown > 0f)
        {
            yield return new WaitForSeconds(remainingCooldown);
        }
        isCooldown = false;
    }

    private IEnumerator ButtonNudge()
    {
        if (buttonVisual == null) yield break;

        Vector3 originalPosition = buttonVisual.localPosition;
        Vector3 pressedPosition = originalPosition - new Vector3(0, 0, nudgeDistance);

        float time = 0f;

        while (time < nudgeSpeed)
        {
            buttonVisual.localPosition = Vector3.Lerp(originalPosition, pressedPosition, time / nudgeSpeed);
            time += Time.deltaTime;
            yield return null;
        }
        buttonVisual.localPosition = pressedPosition;

        time = 0f;
        while (time < nudgeSpeed)
        {
            buttonVisual.localPosition = Vector3.Lerp(pressedPosition, originalPosition, time / nudgeSpeed);
            time += Time.deltaTime;
            yield return null;
        }
        buttonVisual.localPosition = originalPosition;
    }

    [ContextMenu("Simulate Button Press")]
    public void SimulateButtonPress()
    {
        OnButtonPressed(null);
    }
}
