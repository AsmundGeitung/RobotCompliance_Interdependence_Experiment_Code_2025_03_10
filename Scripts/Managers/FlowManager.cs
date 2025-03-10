using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI; 

public class FlowManager : MonoBehaviour
{
    [Header("References")]
    public DataManager dataManager;
    public AgentPromptSystem agentPromptSystem;
    public BoxSpawner boxSpawner;
    public BoxSortingHandler[] sortingAreas;

    [Header("UI Elements")]
    // Assign a UI Text (or TextMeshProUGUI) element from your scene in the Inspector.
    public TextMeshProUGUI cartFinalizedCountText;

    // A counter for boxes finalized in the green, red, or blue bins.
    private int cartFinalizedCount = 0;
    private HashSet<string> countedCartBoxes = new HashSet<string>();

    private void Awake()
    {
        if (dataManager != null)
        {
            dataManager.AllBoxesHandled += OnAllBoxesHandled;
        }
        else
        {
            Debug.LogWarning("FlowManager: No DataManager assigned!");
        }
    }

    private void OnDestroy()
    {
        if (dataManager != null)
        {
            dataManager.AllBoxesHandled -= OnAllBoxesHandled;
        }
    }

    /// <summary>
    /// Called when the user requests the next "batch" or the next box.
    /// </summary>
    public void OnNextBatchRequested()
    {
        // 1) Record time of button press:
        dataManager.RecordButtonPress();

        // 2) Capture a “placement history” entry for every box still in the scene,
        //    but only for non-final placements.
        float localTime = Time.time - dataManager.GetSceneStartTime();
        var allBoxes = GameObject.FindGameObjectsWithTag("Box");
        foreach (var boxObj in allBoxes)
        {
            BoxIdentifier identifier = boxObj.GetComponent<BoxIdentifier>();
            if (!identifier) continue;

            // Skip logging if the box is already in a bin or in a final placement
            if (identifier.currentPlacement == "GreenBin" ||
                identifier.currentPlacement == "BlueBin" ||
                identifier.currentPlacement == "RedBin" ||
                identifier.currentPlacement == "Floor" ||
                identifier.currentPlacement == "Table" ||
                identifier.currentPlacement == "YellowDrawer" ||
                identifier.currentPlacement == "Compressor")
            {
                continue;
            }

            BoxData boxData = dataManager.GetOrCreateBoxData(identifier.boxID, identifier.boxColor);
            boxData.placementHistory.Add((localTime, identifier.currentPlacement));
        }

        // 3) For each box with a pickupTime but no nextRequestTime set, record hesitationTime:
        foreach (var kvp in dataManager.GetAllBoxData())
        {
            BoxData bData = kvp.Value;
            if (bData.pickupTime > 0 && bData.nextRequestTime <= 0)
            {
                dataManager.RecordNextBoxRequested(bData.boxID);
            }
        }

        // 4) Finalize leftover boxes in bins.
        bool anyCorrect = FinalizeLeftoverBoxes();

        // 5) For any boxes whose placement is now final (Floor, Table, etc.) make sure we log it once.
        MarkRemainingBoxesAsFloor();

        // *** NEW: Update the count and UI text for boxes finalized in the green, red, or blue bins.
        UpdateCartFinalizedCount();

        // 6) Save data and possibly show a compliment.
        SaveAllData();
        PossiblyShowCompliment(anyCorrect);

        // 7) Spawn the next box if there are still more to handle.
        if (!CheckIfAllBoxesSpawnedAndHandled())
        {
            SpawnNextBox();
        }
    }

    /// <summary>
    /// Goes through each bin and finalizes boxes that have been sorted.
    /// </summary>
    private bool FinalizeLeftoverBoxes()
    {
        bool anyCorrect = false;
        foreach (var bin in sortingAreas)
        {
            if (bin.FinalizeSortedBoxes())
            {
                anyCorrect = true;
            }
        }
        return anyCorrect;
    }

    /// <summary>
    /// Loops through all boxes and, if their current placement is one of the final ones,
    /// adds a placement record if one has not already been recorded.
    /// </summary>
    private void MarkRemainingBoxesAsFloor()
    {
        var allBoxes = GameObject.FindGameObjectsWithTag("Box");
        foreach (var boxObj in allBoxes)
        {
            BoxIdentifier identifier = boxObj.GetComponent<BoxIdentifier>();
            if (!identifier) continue;

            if (dataManager.TryGetBoxData(identifier.boxID, out var boxData))
            {
                // Process only if the current placement is a final placement.
                if (identifier.currentPlacement == "Floor" ||
                    identifier.currentPlacement == "Table" ||
                    identifier.currentPlacement == "YellowDrawer" ||
                    identifier.currentPlacement == "Compressor")
                {
                    // Avoid duplicate logging by checking the last recorded placement.
                    if (boxData.placementHistory.Count > 0)
                    {
                        var lastEntry = boxData.placementHistory[boxData.placementHistory.Count - 1];
                        // For Compressor, we log as "CompressorWithoutButton"
                        if (lastEntry.Item2 == identifier.currentPlacement ||
                           (identifier.currentPlacement == "Compressor" && lastEntry.Item2 == "CompressorWithoutButton"))
                        {
                            continue;
                        }
                    }

                    // Set the finalPlacement if not already set or if it is still "Other".
                    if (string.IsNullOrEmpty(boxData.finalPlacement) || boxData.finalPlacement == "Other")
                    {
                        boxData.finalPlacement = identifier.currentPlacement;
                    }

                    // Use a special string if the box is in the Compressor.
                    string loggedPlacement = boxData.finalPlacement;
                    if (loggedPlacement == "Compressor")
                    {
                        loggedPlacement = "CompressorWithoutButton";
                    }
                    float localTime = Time.time - dataManager.GetSceneStartTime();
                    boxData.placementHistory.Add((localTime, loggedPlacement));
                    boxData.wasCorrect = false;
                }
            }
        }
    }

    /// <summary>
    /// Checks if all boxes have been spawned (and presumably handled).
    /// If so, calls OnAllBoxesHandled().
    /// </summary>
    private bool CheckIfAllBoxesSpawnedAndHandled()
    {
        if (boxSpawner != null && boxSpawner.AreAllBoxesSpawned())
        {
            OnAllBoxesHandled();
            return true;
        }
        return false;
    }

    private void SpawnNextBox()
    {
        if (boxSpawner != null)
        {
            boxSpawner.SpawnNextBox();
        }
        else
        {
            Debug.LogError("FlowManager: No BoxSpawner assigned!");
        }
    }

    private void PossiblyShowCompliment(bool anyCorrect)
    {
        if (anyCorrect && agentPromptSystem != null)
        {
            agentPromptSystem.ShowCompliment();
        }
    }

    /// <summary>
    /// Called when all boxes are handled.
    /// </summary>
    public void OnAllBoxesHandled()
    {
        FinalizeAllRemainingBoxes();
        // You could also update the count here if needed.
        UpdateCartFinalizedCount();

        Debug.Log("FlowManager: All boxes handled.");
        if (agentPromptSystem != null)
        {
            agentPromptSystem.CheckAndShowEndShiftPrompt();
        }
    }

    /// <summary>
    /// Called when the user ends the shift.
    /// Finalizes any remaining boxes and then quits.
    /// </summary>
    public void EndShift()
    {
        SaveAllData();
        Debug.Log("FlowManager: Ending shift now...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Finalizes all boxes still in the scene by ensuring that any box without a final placement 
    /// (or still marked as "Other") gets assigned one and logged only once.
    /// </summary>
    private void FinalizeAllRemainingBoxes()
    {
        FinalizeLeftoverBoxes();

        GameObject[] remainingBoxes = GameObject.FindGameObjectsWithTag("Box");
        foreach (GameObject boxObj in remainingBoxes)
        {
            if (boxObj == null) continue;
            BoxIdentifier identifier = boxObj.GetComponent<BoxIdentifier>();
            if (identifier == null) continue;

            BoxData boxData = dataManager.GetOrCreateBoxData(identifier.boxID, identifier.boxColor);

            if (string.IsNullOrEmpty(boxData.finalPlacement) || boxData.finalPlacement == "Other")
            {
                string finalPlacement = (!string.IsNullOrEmpty(identifier.currentPlacement) && identifier.currentPlacement != "Other")
                                          ? identifier.currentPlacement
                                          : "Floor";
                // Only log if we haven’t already recorded this final placement.
                bool alreadyLogged = boxData.placementHistory.Count > 0 &&
                                     (boxData.placementHistory[boxData.placementHistory.Count - 1].Item2 == finalPlacement ||
                                      (finalPlacement == "Compressor" && boxData.placementHistory[boxData.placementHistory.Count - 1].Item2 == "CompressorWithoutButton"));
                if (!alreadyLogged)
                {
                    float localTime = Time.time - dataManager.GetSceneStartTime();
                    boxData.placementHistory.Add((localTime, finalPlacement));
                }
                boxData.finalPlacement = finalPlacement;
            }

            // Finalize the box (DataManager ignores if already finalized).
            dataManager.OnBoxFinalized(identifier.boxID);
        }
    }

    private void SaveAllData()
    {
        if (dataManager != null)
        {
            dataManager.SaveAllData();
        }
    }

    /// <summary>
    /// Updates the counter and UI text for boxes that have been finalized in the green, red, or blue bins.
    /// </summary>
    private void UpdateCartFinalizedCount()
    {
        // Loop through all box data in the DataManager.
        foreach (var kvp in dataManager.GetAllBoxData())
        {
            BoxData bData = kvp.Value;
         
            if (bData.finalPlacement == "GreenBin" || bData.finalPlacement == "RedBin" || bData.finalPlacement == "BlueBin")
               if (!countedCartBoxes.Contains(bData.boxID.ToString()))
                {
                    countedCartBoxes.Add(bData.boxID.ToString());
                    cartFinalizedCount++;
                }
        }

        if (cartFinalizedCountText != null)
        {
            cartFinalizedCountText.text = "" + cartFinalizedCount;
        }
    }
}
