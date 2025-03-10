using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class AutoSortingTest : MonoBehaviour
{
    [Header("References")]
    public FlowManager flowManager;
    public BoxSortingHandler greenBin, blueBin, redBin, tableArea, cabinetArea;
    public DataLogger dataLogger;
    public DataManager dataManager;

    [Header("Auto Sort Settings")]
    public int targetBoxesSorted = 16;   // Total number of boxes to process.
    public float nextBatchInterval = 3f;   // Time between batches (box spawns).
    public float teleportDelay = 1f;       // Delay after spawning before teleporting.

    // Internal bookkeeping: for each processed box we record our expected result.
    private Dictionary<int, ExpectedResult> expectedResults = new Dictionary<int, ExpectedResult>();
    // To avoid processing the same box twice:
    private HashSet<int> processedBoxIDs = new HashSet<int>();
    // A counter to know which box in order we are processing.
    private int processedCount = 0;

    // A simple container for the expected result of each box.
    private class ExpectedResult
    {
        public int boxID;
        public string expectedFinalPlacement;  
        public bool expectedCorrect;           
    }

    private void Start()
    {
        if (dataLogger == null || dataManager == null)
        {
            Debug.LogError("AutoSortingTest: DataLogger or DataManager reference is missing!");
            return;
        }

        StartCoroutine(AutoSortRoutine());
    }

    private IEnumerator AutoSortRoutine()
    {
        Debug.Log("=== AutoSortingTest: Beginning automated sorting routine ===");

        // Process a total of targetBoxesSorted boxes.
        for (int i = 0; i < targetBoxesSorted; i++)
        {
            yield return new WaitForSeconds(nextBatchInterval);
            flowManager.OnNextBatchRequested();

            yield return new WaitForSeconds(teleportDelay);
            TeleportNewBoxes();
        }

        Debug.Log($"=== AutoSortingTest: Completed processing {targetBoxesSorted} boxes. Validating Logs... ===");
        ValidateLogging();
    }

    /// <summary>
    /// Finds all new (unprocessed) boxes and moves each one based on a predetermined pattern.
    /// </summary>
    private void TeleportNewBoxes()
    {
        GameObject[] allFiles = GameObject.FindGameObjectsWithTag("File");
        List<GameObject> newFiles = new List<GameObject>();

        foreach (GameObject file in allFiles)
        {
            BoxIdentifier identifier = file.GetComponent<BoxIdentifier>();
            if (identifier == null)
                continue;

            if (processedBoxIDs.Contains(identifier.boxID))
                continue;

            newFiles.Add(file);
        }

        newFiles = newFiles.OrderBy(file => file.GetComponent<BoxIdentifier>().boxID).ToList();

        foreach (GameObject file in newFiles)
        {
            BoxIdentifier identifier = file.GetComponent<BoxIdentifier>();
            if (identifier == null)
                continue;


            (BoxSortingHandler target, bool isCorrect) = GetSortInstruction(processedCount, identifier);

           
            ExpectedResult exp = new ExpectedResult()
            {
                boxID = identifier.boxID,
                expectedFinalPlacement = target != null ? target.name : "None",
                expectedCorrect = isCorrect
            };
            expectedResults[identifier.boxID] = exp;

           
            dataManager.RecordBoxPickedUp(identifier.boxID, identifier.boxColor);
            Debug.Log($"AutoSortingTest: Picked up {file.name} (ID: {identifier.boxID}, Color: {identifier.boxColor})");

            // Move the file based on whether the instruction is “correct” or “incorrect.”
            if (isCorrect)
            {
                // For a correct move, we place the file into the bin matching its color.
                PlaceInCorrectBin(file, identifier);
            }
            else
            {
                // For an incorrect move, we move the file to our predetermined (and wrong) destination.
                MoveFileToArea(file, target);
                Debug.Log($"AutoSortingTest: (Incorrect Move) Placed {file.name} in {target.name}");
            }

            // Mark this box as processed.
            processedBoxIDs.Add(identifier.boxID);
            processedCount++;
        }
    }

    /// <summary>
    /// Returns the predetermined instruction for a given box.
    /// Even-numbered boxes are sorted correctly, odd-numbered are sorted incorrectly.
    /// For incorrect moves, the target destination cycles between tableArea and cabinetArea.
    /// </summary>
    /// <param name="index">The order index of the box (0-based).</param>
    /// <param name="identifier">The FileIdentifier to check the file’s color.</param>
    /// <returns>A tuple (target destination, shouldBeCorrect)</returns>
    private (BoxSortingHandler target, bool isCorrect) GetSortInstruction(int index, BoxIdentifier identifier)
    {
        bool isCorrect = (index % 2 == 0); // Even index: correct; odd: incorrect.
        BoxSortingHandler target = null;

        if (isCorrect)
        {
            // For a correct move, pick the bin corresponding to the file’s color.
            switch (identifier.boxColor)
            {
                case BoxColor.Red:
                    target = redBin;
                    break;
                case BoxColor.Green:
                    target = greenBin;
                    break;
                case BoxColor.Blue:
                    target = blueBin;
                    break;
                default:
                    target = redBin;
                    break;
            }
        }
        else
        {
            // For an incorrect move, choose a wrong area.
            // Here we alternate between tableArea and cabinetArea.
            if (index % 4 == 1)
            {
                target = tableArea;
            }
            else // index % 4 == 3
            {
                target = cabinetArea;
            }
        }

        return (target, isCorrect);
    }

    /// <summary>
    /// Moves the file into the correct bin based on its color.
    /// </summary>
    private void PlaceInCorrectBin(GameObject file, BoxIdentifier identifier)
    {
        BoxSortingHandler targetBin = null;
        switch (identifier.boxColor)
        {
            case BoxColor.Red:
                targetBin = redBin;
                break;
            case BoxColor.Green:
                targetBin = greenBin;
                break;
            case BoxColor.Blue:
                targetBin = blueBin;
                break;
        }

        if (targetBin != null)
        {
            MoveFileToArea(file, targetBin);
            Debug.Log($"AutoSortingTest: (Correct Move) Placed {file.name} in {targetBin.name}");
        }
    }

    /// <summary>
    /// Moves the file to the specified area and logs the finalization.
    /// </summary>
    private void MoveFileToArea(GameObject file, BoxSortingHandler destination)
    {
        if (destination == null)
            return;

        // Position the file at the destination.
        file.transform.position = destination.transform.position + Vector3.up * 0.3f;
        Debug.Log($"AutoSortingTest: Moved {file.name} to {destination.name}");

        // Signal that this box has been finalized.
        BoxIdentifier identifier = file.GetComponent<BoxIdentifier>();
        if (identifier != null)
        {
            dataManager.OnBoxFinalized(identifier.boxID);
        }
    }

    /// <summary>
    /// Validates that the expected results match what was recorded in the DataManager.
    /// </summary>
    private void ValidateLogging()
    {
        Dictionary<int, BoxData> loggedData = dataManager.GetAllBoxData();

        Debug.Log("=== VALIDATION RESULTS ===");
        foreach (var expEntry in expectedResults)
        {
            int boxID = expEntry.Key;
            ExpectedResult expected = expEntry.Value;

            if (!loggedData.ContainsKey(boxID))
            {
                Debug.LogError($"Validation Error: Box ID {boxID} was not found in the logs.");
                continue;
            }

            BoxData actual = loggedData[boxID];
            bool match = (actual.finalPlacement == expected.expectedFinalPlacement) &&
                         (actual.wasCorrect == expected.expectedCorrect);

            if (match)
            {
                Debug.Log($"Box ID {boxID} PASSED: Expected Placement = {expected.expectedFinalPlacement}, " +
                          $"Expected Correct = {expected.expectedCorrect}");
            }
            else
            {
                Debug.LogError($"Box ID {boxID} FAILED: Expected Placement = {expected.expectedFinalPlacement}, " +
                               $"Logged Placement = {actual.finalPlacement}; Expected Correct = {expected.expectedCorrect}, " +
                               $"Logged Correct = {actual.wasCorrect}");
            }
        }
    }
}
