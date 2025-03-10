using UnityEngine;
using TMPro; 
using UnityEngine.UI; 
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit; 
using static System.Net.Mime.MediaTypeNames;
using UnityEngine.UIElements;
using System.Collections;

public class ExperimentManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI instructionText;    
    public UnityEngine.UI.Button continueButton;
    public TextMeshProUGUI continueButtonText; 

    [Header("Experiment Logic")]
    public StartButton startButton;
    public bool readyToStart = false;

    private int currentStep = 0;

    // Cooldown control
    private bool canPressContinue = true;
    private float continueCooldown = 1f;

    private string[] cooperationInstructions =
    {
        "Hello, and thank you for participating.",
        "Please read the following instructions carefully.",
        "Your task is to sort colored boxes into matching colored carts to ensure they are shipped to the correct location.",
        "It is very important to always sort the current box into a colored cart before requesting a new box.",
        "You must never dispose of a box.",
        "Look around to find the blue colored box.",
        "Point at the box with your controller and hold the trigger to pick it up.",
        "Place the box in the matching blue colored cart, then press the red button to request the next box.",
        "You can move around physically or by pressing the touchpad on top of the controller.",
        "There will be a robot  inside the warehouse helping you by bringing the boxes.",
    };

    private string[] coexistenceInstructions =
    {
        "Hello, and thank you for participating.",
        "Please read the following instructions carefully.",
        "Your task is to sort colored boxes into matching colored carts to ensure they are shipped to the correct location.",
        "It is very important to always sort the current box into a colored cart before requesting a new box.",
        "You must never dispose of a box.",
        "Look around to find the blue colored box.",
        "Point at the box with your controller and hold the trigger to pick it up.",
        "Place the box in the matching blue colored cart, then press the red button to request the next box.",
        "You can move around physically or by pressing the touchpad on top of the controller.",
        "There will be a robot in the warehouse occupied with another task nearby.",
    };

    private string[] currentInstructions;

    private void Start()
    {
        // Pick the instruction set from some global manager
        SetInstructionSet();

        DisplayCurrentInstruction();

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueButtonClicked);
    }

    private void SetInstructionSet()
    {
        var currentCondition = ConditionManager.Instance.currentCondition;

        currentInstructions = currentCondition == AgentCondition.Coexistence
            ? coexistenceInstructions
            : cooperationInstructions;
    }

    public void DisplayCurrentInstruction()
    {
        // Make sure it is within the array bounds
        if (currentStep < currentInstructions.Length)
        {
            instructionText.text = currentInstructions[currentStep];
        }
        else
        {
            // Last instruction before experiment start
            instructionText.text = "Once you are comfortable with the controls, press start to begin the experiment.";
            continueButtonText.text = "Start";
            readyToStart = true;
        }
    }

    private void OnContinueButtonClicked()
    {

        if (!canPressContinue)
            return;

        if (readyToStart)
        {
            LoadExperimentScene();
        }

        // Immediately prevent another press
        canPressContinue = false;
        continueButton.interactable = false;

        // Move to next step and display next instruction
        if (currentStep < currentInstructions.Length)
        {
            currentStep++;
            DisplayCurrentInstruction();
        }

        // Start cooldown timer to re-enable the button
        StartCoroutine(EnableContinueAfterDelay(continueCooldown));
    }

    private IEnumerator EnableContinueAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        canPressContinue = true;
        continueButton.interactable = true;
    }

    public void LoadExperimentScene()
    {
        SceneManager.LoadScene("BasicScene");
    }
}
