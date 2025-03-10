using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class StartButton : MonoBehaviour
{
    [Header("Button Animation")]
    public Transform buttonVisual;
    [Tooltip("How far (in local Y) to move the button down when pressed.")]
    public float nudgeDistance = 0.2f;
    [Tooltip("Time (in seconds) it takes to move down or up.")]
    public float nudgeSpeed = 0.2f;

    [Header("Audio")]
    public AudioSource buttonAudioSource;      
    public AudioClip buttonPressClip;

    [Header("Box/Cart References")]
    public Transform[] cartTransforms;         
    public Transform spawnPoint;               
    public GameObject[] boxPrefabs;            

    [Header("Experiment Linking")]
    public ExperimentManager scene;

    private XRSimpleInteractable interactable;

    private void Awake()
    {
        // Grab the XR interaction component
        interactable = GetComponent<XRSimpleInteractable>();

        if (interactable)
        {
            // Subscribe to the select event
            interactable.selectEntered.AddListener(OnButtonPressed);
        }
        else
        {
            Debug.LogWarning("StartButton: No XRSimpleInteractable found on this object.");
        }
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
        // Play the button press sound
        if (buttonAudioSource && buttonPressClip)
        {
            buttonAudioSource.PlayOneShot(buttonPressClip);
        }

        // Nudge the button down/up visually
        StartCoroutine(ButtonNudge());

        // Clear items in carts if there are any
        ClearCarts();

        // Spawn a new box
        SpawnRandomBox();
    }

    /// <summary>
    /// Simple coroutine to visually "press" the button down, then move it back up.
    /// </summary>
    private System.Collections.IEnumerator ButtonNudge()
    {
        if (buttonVisual == null)
            yield break;

        Vector3 originalPos = buttonVisual.localPosition;
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

        // Return back to original
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

    /// <summary>
    /// Destroy child objects in the given cart transforms if any are present.
    /// </summary>
    private void ClearCarts()
    {
        if (cartTransforms == null || cartTransforms.Length == 0) return;

        foreach (Transform cart in cartTransforms)
        {
            if (cart.childCount > 0)
            {
                for (int i = cart.childCount - 1; i >= 0; i--)
                {
                    Destroy(cart.GetChild(i).gameObject);
                }
            }
        }
    }

    /// <summary>
    /// Spawn one of the boxPrefabs at 'spawnPoint' at random.
    /// </summary>
    private void SpawnRandomBox()
    {
        if (boxPrefabs == null || boxPrefabs.Length == 0 || spawnPoint == null) return;

        int randomIndex = Random.Range(0, boxPrefabs.Length);
        GameObject prefabToSpawn = boxPrefabs[randomIndex];
        Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);
    }

    [ContextMenu("Simulate Button Press")]
    public void SimulateButtonPress()
    {
        OnButtonPressed(null);
    }
}
