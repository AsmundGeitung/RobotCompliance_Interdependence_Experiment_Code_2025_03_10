using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using static System.Net.Mime.MediaTypeNames;
using static UnityEngine.Rendering.DebugUI.Table;
using static UnityEngine.Rendering.DebugUI;
using UnityEngine.Device;
using UnityEngine.LightTransport;
using UnityEngine.UIElements;
using static Cinemachine.CinemachineFreeLook;
using UnityEditor;

public class AgentPromptSystem : MonoBehaviour
{
    [Header("Data Manager Reference")]
    public DataManager dataManager;

    [Header("Flow Manager Reference")]
    public FlowManager flowManager;

    [Header("UI References")]
    [Tooltip("A single panel GameObject that holds the text and, if needed, yes/no buttons.")]
    public GameObject promptPanel;

    [Tooltip("A TMP text field inside the promptPanel.")]
    public TextMeshProUGUI agentPromptText;

    [Tooltip("Optional Yes button for leftover scenario.")]
    public UnityEngine.UI.Button yesButton;

    [Tooltip("Optional No button for leftover scenario.")]
    public UnityEngine.UI.Button noButton;

    [Header("Prompt Audio Clips")]
    [Tooltip("Clips that play when giving ambiguous instructions. These align with ambiguousPrompts.")]
    public AudioClip[] ambiguousPromptClips;

    [Tooltip("Clips that play when giving positive feedback. These align with positivePrompts.")]
    public AudioClip[] positivePromptClips;

    [Header("End-Shift Audio Clips")]
    [Tooltip("Audio clip for the end of the shift message.")]
    public AudioClip endShiftClip;

    [Tooltip("Audio clip for the end of the shift message.")]
    public AudioClip preEndShiftClip;

    [Tooltip("Audio clip for the leftover box destruction prompt.")]
    public AudioClip destroyBoxesPromptClip;

    [Header("Other Audio Clips")]
    public AudioClip complimentClip;

    [Header("Prompt Settings")]
    public float baseDisplayTime = 1.0f;
    public float perCharacterTime = 0.03f;

    [Header("Ambiguous Prompt Data")]
    public string[] ambiguousPrompts =
    {
        "I would put this box on the table behind you for now.",
        "I would hide this box in that yellow cabin next to the table.",
        "I would throw this box in the green compressor next to the table and push the button to get rid of it."
    };
    private int ambiguousPromptCount = 0;
    private int pendingAmbiguousRequests = 0; 

    // Track which ambiguous boxes have already triggered a prompt.
    private HashSet<GameObject> triggeredAmbiguousBoxes = new HashSet<GameObject>();

    [Header("Positive Prompt Data")]
    public string[] positivePrompts =
    {
        "You’re doing well—keep going.",
        "Nice work so far.",
        "You’ve got this.",
        "Good job, keep it up.",
        "You’re doing a good job.",
        "Looking good so far, keep it up."
    };
    private int lastPromptIndex = -1;

    // Timers/data 
    private float hesitationStartTime;
    private Dictionary<string, float> hesitationTimes = new Dictionary<string, float>();

    // End-shift logic state
    private bool isEndShiftRoutineRunning = false;
    private bool userHasChosen = false;
    private bool userChoseYes = false;

    //Prompt State and Type Enums
    private enum PromptState
    {
        Idle,
        ShowingPrompt,
        WaitingForResponse
    }

    private enum PromptType
    {
        None,
        Normal,
        Ambiguous
    }

    private PromptState currentPromptState = PromptState.Idle;
    private PromptType currentPromptType = PromptType.None;

    // Track the current prompt coroutine
    private Coroutine currentPromptCoroutine;

    [Header("Start Message Audio Clips")]
    [Tooltip("Audio clip for the start message in Cooperation mode.")]
    public AudioClip cooperationStartClip;

    [Tooltip("Audio clip for the start message in Coexistence mode.")]
    public AudioClip coexistenceStartClip;


    [Header("Audio Sources for Each Mode")]
    [Tooltip("AudioSource for Mode One. This should be assigned via the Inspector.")]
    [SerializeField] private AudioSource modeOneAudioSource;

    [Tooltip("AudioSource for Mode Two. This should be assigned via the Inspector.")]
    [SerializeField] private AudioSource modeTwoAudioSource;

    // Flag to track which mode is active.
    private bool isCooperation = true;

    private AudioSource activeAudioSource;

    private void Start()
    {
        // Make sure the panel is hidden initially
        if (promptPanel) promptPanel.SetActive(false);

        // Set text field to blank if assigned
        if (agentPromptText) agentPromptText.text = "";

        // Wire up yes/no buttons if they exist
        if (yesButton) yesButton.onClick.AddListener(OnYesButtonPressed);
        if (noButton) noButton.onClick.AddListener(OnNoButtonPressed);

        // Check if both AudioSources have been assigned.
        if (modeOneAudioSource == null || modeTwoAudioSource == null)
        {
            Debug.LogWarning("AgentPromptSystem: Please assign both AudioSources in the Inspector.");
        }

        // Ensure the correct AudioSource is enabled at startup.
        UpdateAudioSources();

        // Trigger the delayed message 2 seconds after the scene starts
        StartCoroutine(DelayedStartMessage());
    }

    /// <summary>
    /// Sends a start message 2 seconds after the experiment begins, changing based on mode.
    /// </summary>
    private IEnumerator DelayedStartMessage()
    {
        yield return new WaitForSeconds(2f);

        string startMessage;
        AudioClip startClip;

        if (isCooperation)
        {
            startMessage = "Hello. I am a warehouse robot. I am here to help you with sorting the boxes.";
            startClip = cooperationStartClip;
        }
        else 
        {
            startMessage = "Hello. I am a warehouse robot. I will be over here working in this area.";
            startClip = coexistenceStartClip;
        }

        // Calculate display time based on text length
        float duration = baseDisplayTime + (startMessage.Length * perCharacterTime) * 2.5f;

        // Show the message and play the corresponding clip
        ShowPrompt(startMessage, startClip, duration);
    }


    public void SwitchMode(bool currentMode)
    {
        isCooperation = currentMode;
        UpdateAudioSources();
    }

    /// <summary>
    /// Enables the AudioSource for the current mode
    /// </summary>
    private void UpdateAudioSources()
    {
        if (modeOneAudioSource != null)
        {
            modeOneAudioSource.enabled = isCooperation;
        }
        if (modeTwoAudioSource != null)
        {
            modeTwoAudioSource.enabled = !isCooperation;
        }

        // Update the active audio source reference.
        activeAudioSource = isCooperation ? modeOneAudioSource : modeTwoAudioSource;
    }

    /// <summary>
    /// Plays the provided AudioClip on the currently active AudioSource.
    /// </summary>
    /// <param name="clip">The AudioClip to play.</param>
    public void PlayClip(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("AgentPromptSystem: No AudioClip provided to play.");
            return;
        }

        if (activeAudioSource == null)
        {
            Debug.LogWarning("AgentPromptSystem: No active AudioSource available to play the clip.");
            return;
        }

        // Play the clip on the activeAudioSource
        activeAudioSource.clip = clip;
        activeAudioSource.Play();
    }

    // ------------------------------------------------
    // END-SHIFT LOGIC
    // ------------------------------------------------

    public void CheckAndShowEndShiftPrompt()
    {
        if (isEndShiftRoutineRunning)
        {
            Debug.Log("AgentPromptSystem: End shift routine already in progress.");
            return;
        }
        StartCoroutine(EndShiftRoutine());
    }

    private IEnumerator EndShiftRoutine()
    {
        isEndShiftRoutineRunning = true;
        yield return StartCoroutine(ShowSimpleMessageForSeconds(
            "That was all the boxes we were supposed to sort today.",
            5f,
            preEndShiftClip
        ));

        bool anyBoxesLeft = AreAnyBoxesLeftInScene();
        if (!anyBoxesLeft)
        {
            // If no leftover, go straight to final message
            yield return StartCoroutine(ShowSimpleMessageForSeconds(
                "The shift is now over. Thank you for your participation.",
                5f,
                endShiftClip
            ));
            EndShiftNow();
        }
        else
        {
            // We do have leftover => show yes/no prompt
            yield return StartCoroutine(ShowLeftoverPromptAndWait(
                "We should throw the remaining boxes in the compressor.",
                destroyBoxesPromptClip
            ));

            // Record choice
            if (userChoseYes)
            {
                dataManager.RecordEndingAnswer("Agree");
            }
            else
            {
                dataManager.RecordEndingAnswer("Disagree");
            }

            // Then final message using the endShiftClip
            yield return StartCoroutine(ShowSimpleMessageForSeconds(
                "The shift is now over. Thank you for your participation.",
                5f,
                endShiftClip
            ));

            EndShiftNow();
        }

        isEndShiftRoutineRunning = false;
    }

    private bool AreAnyBoxesLeftInScene()
    {
        var allBoxes = GameObject.FindGameObjectsWithTag("Box");
        return allBoxes.Length > 0;
    }

    private void EndShiftNow()
    {
        if (flowManager != null)
        {
            flowManager.EndShift();
        }
        else
        {
            Debug.LogWarning("AgentPromptSystem: No FlowManager assigned to end shift.");
        }
    }

    public void OnYesButtonPressed()
    {
        Debug.Log("User pressed YES => userChoseYes = true");
        userChoseYes = true;
        userHasChosen = true;

        dataManager.RecordEndingAnswer("Agree");

        if (currentPromptState == PromptState.WaitingForResponse)
        {
            currentPromptState = PromptState.Idle;
        }
    }

    public void OnNoButtonPressed()
    {
        Debug.Log("User pressed NO => userChoseYes = false");
        userChoseYes = false;
        userHasChosen = true;

        dataManager.RecordEndingAnswer("Disagree");

        if (currentPromptState == PromptState.WaitingForResponse)
        {
            currentPromptState = PromptState.Idle;
        }
    }

    // ------------------------------------------------
    // AMBIGUOUS / COMPLIMENT PROMPTS
    // ------------------------------------------------

    public void OnAmbiguousBoxPicked(GameObject ambiguousBox)
    {
        // Ensure each ambiguous box only triggers once
        if (triggeredAmbiguousBoxes.Contains(ambiguousBox))
        {
            return;
        }
        triggeredAmbiguousBoxes.Add(ambiguousBox);

        if (ambiguousPromptCount < ambiguousPrompts.Length)
        {
            // If we're not already in an ambiguous prompt, show one. Otherwise, queue it.
            if (!(currentPromptType == PromptType.Ambiguous && currentPromptState == PromptState.ShowingPrompt))
            {
                currentPromptType = PromptType.Ambiguous;
                currentPromptState = PromptState.ShowingPrompt;
                ShowAmbiguousPrompt();
            }
            else
            {
                pendingAmbiguousRequests++;
            }
        }
    }

    private void ShowAmbiguousPrompt()
    {
        if (ambiguousPromptCount >= ambiguousPrompts.Length) return;

        string promptText = ambiguousPrompts[ambiguousPromptCount];

        // Safely pick the matching clip if available
        AudioClip clip = null;
        if (ambiguousPromptClips != null && ambiguousPromptCount < ambiguousPromptClips.Length)
        {
            clip = ambiguousPromptClips[ambiguousPromptCount];
        }

        ambiguousPromptCount++;
        hesitationStartTime = Time.time;

        ShowPrompt(promptText, clip, 6f, true);
    }

    public void EndAmbiguousScenario()
    {
        // After an ambiguous prompt finishes, check if we need to show another
        if (pendingAmbiguousRequests > 0 && ambiguousPromptCount < ambiguousPrompts.Length)
        {
            pendingAmbiguousRequests--;
            currentPromptType = PromptType.Ambiguous;
            currentPromptState = PromptState.ShowingPrompt;
            ShowAmbiguousPrompt();
        }
        else
        {
            // Reset to no prompt
            currentPromptType = PromptType.None;
        }
    }
    public void RecordHesitationTime(string action)
    {
        float hesitationTime = Time.time - hesitationStartTime;
        hesitationTimes[action] = hesitationTime;
        Debug.Log($"Hesitation time for '{action}' is {hesitationTime} seconds.");
        EndAmbiguousScenario();
    }

    public void ShowCompliment()
    {
        if (positivePrompts.Length == 0) return;

        int randomIndex;
        do
        {
            randomIndex = UnityEngine.Random.Range(0, positivePrompts.Length);
        }
        while (randomIndex == lastPromptIndex && positivePrompts.Length > 1);

        string prompt = positivePrompts[randomIndex];
        lastPromptIndex = randomIndex;

        // Safely pick a matching audio clip
        AudioClip clip = null;
        if (positivePromptClips != null && randomIndex < positivePromptClips.Length)
        {
            clip = positivePromptClips[randomIndex];
        }

        // Calculate the display duration
        float duration = baseDisplayTime + (prompt.Length * perCharacterTime);

        // Show the prompt
        ShowPrompt(prompt, clip, duration);
    }

    // ------------------------------------------------
    // GENERAL SHORT PROMPT METHOD
    // ------------------------------------------------
    //
    // This is the main entry point for showing a timed prompt.
    // If `isAmbiguous` is true, mark this as an ambiguous prompt.
    public void ShowPrompt(string text, AudioClip clip, float duration, bool isAmbiguous = false)
    {
        // Cancel any existing prompt
        if (currentPromptCoroutine != null)
        {
            StopCoroutine(currentPromptCoroutine);
        }

        // Start a new routine for this prompt
        currentPromptCoroutine = StartCoroutine(PromptRoutine(
            text,

            clip,
            duration,
            showYesNo: false,
            waitForUser: false,
            isAmbiguous ? PromptType.Ambiguous : PromptType.Normal
        ));
    }

    // ------------------------------------------------
    // WRAPPERS FOR EXISTING COROUTINE-BASED PROMPTS
    // ------------------------------------------------

    private IEnumerator ShowSimpleMessageForSeconds(string text, float duration, AudioClip clip)
    {

        yield return StartCoroutine(PromptRoutine(
            text,
            clip,
            duration,
            showYesNo: false,
            waitForUser: false,
            PromptType.Normal
        ));
    }

    private IEnumerator ShowLeftoverPromptAndWait(string text, AudioClip clip)
    {
        // Reset user choice
        userHasChosen = false;
        userChoseYes = false;

        yield return StartCoroutine(PromptRoutine(
            text,
            clip,
            0f,
            showYesNo: true,
            waitForUser: true,
            PromptType.Normal
        ));
    }

    // ------------------------------------------------
    // CORE PROMPT ROUTINE
    // ------------------------------------------------

    private IEnumerator PromptRoutine(
        string text,
        AudioClip clip,
        float duration,
        bool showYesNo,
        bool waitForUser,
        PromptType promptType
    )
    {
        // If waiting for user input, go to "WaitingForResponse" state;
        // otherwise, go to "ShowingPrompt".
        currentPromptType = promptType;
        currentPromptState = waitForUser ? PromptState.WaitingForResponse : PromptState.ShowingPrompt;

        // Show the UI
        ShowPanelAndText(text, showYesNo);

        // Play the audio clip
        if (clip != null)
        {
            PlayClip(clip);
        }

        // Either wait for user input or a timed duration
        if (waitForUser)
        {
            // Wait until user chooses yes/no
            while (!userHasChosen)
            {
                yield return null;
            }

            // Reset for next usage
            userHasChosen = false;
        }
        else
        {
            yield return new WaitForSeconds(duration);
        }

        // Hide the prompt
        HidePanelAndText();

        // Return to Idle state
        currentPromptState = PromptState.Idle;

        // If this was an ambiguous prompt, end that scenario
        if (promptType == PromptType.Ambiguous)
        {
            EndAmbiguousScenario();
        }

        // Clear the reference to the running coroutine
        currentPromptCoroutine = null;
    }

    // ------------------------------------------------
    // HELPER METHODS TO REDUCE REPETITION
    // ------------------------------------------------

    /// <summary>
    /// Shows the prompt panel, sets text, and controls yes/no button visibility.
    /// </summary>
    private void ShowPanelAndText(string text, bool showYesNo)
    {
        if (promptPanel) promptPanel.SetActive(true);
        if (agentPromptText) agentPromptText.text = text;

        if (yesButton) yesButton.gameObject.SetActive(showYesNo);
        if (noButton) noButton.gameObject.SetActive(showYesNo);
    }

    /// <summary>
    /// Hides the prompt panel, clears text, and hides yes/no buttons.
    /// </summary>
    private void HidePanelAndText()
    {
        if (promptPanel) promptPanel.SetActive(false);
        if (agentPromptText) agentPromptText.text = "";

        if (yesButton) yesButton.gameObject.SetActive(false);
        if (noButton) noButton.gameObject.SetActive(false);
    }
}
