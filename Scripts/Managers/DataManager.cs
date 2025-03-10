using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DataManager : MonoBehaviour
{
    [Header("Data Manager Settings")]
    public int totalBoxes = 16;
    [SerializeField] private DataLogger dataLogger;

    // Dictionary of all boxes encountered
    private readonly Dictionary<int, BoxData> boxesData = new Dictionary<int, BoxData>();
    private int boxesFinalizedCount = 0;

    private string endingAnswer = "";

    // Event that fires when all boxes have been finalized
    public event Action AllBoxesHandled;

    // The time this scene started
    private float sceneStartTime;

    // The time (relative to sceneStartTime) of the last "Next" button press
    public float lastButtonPressTime { get; private set; } = 0f;

    // Keep track of which boxes we have *already* logged/finalized
    private HashSet<int> finalizedBoxIDs = new HashSet<int>();

    private void Awake()
    {
        sceneStartTime = Time.time;

        if (dataLogger == null)
        {
            // Attempt to find any DataLogger in the scene
            dataLogger = FindObjectOfType<DataLogger>();
            if (dataLogger == null)
            {
                Debug.LogWarning("DataManager: No DataLogger assigned or found!");
            }
        }
    }

    public float GetSceneStartTime()
    {
        return sceneStartTime;
    }

    /// <summary>
    /// We store the moment of that button press as a reference for calculating pickupTime on the next box.
    /// </summary>
    public void RecordButtonPress()
    {
        lastButtonPressTime = Time.time - sceneStartTime;
    }

    /// <summary>
    /// Returns the entire dictionary of box data.
    /// </summary>
    public Dictionary<int, BoxData> GetAllBoxData()
    {
        return boxesData;
    }

    /// <summary>
    /// Retrieves or creates a BoxData record by boxID/color.
    /// </summary>
    public BoxData GetOrCreateBoxData(int boxID, BoxColor color)
    {
        if (boxesData.TryGetValue(boxID, out BoxData existing))
        {
            return existing;
        }

        BoxData newData = new BoxData
        {
            boxID = boxID,
            color = color
        };
        boxesData[boxID] = newData;
        return newData;
    }

    /// <summary>
    /// Attempts to get existing BoxData without creating a new one.
    /// </summary>
    public bool TryGetBoxData(int boxID, out BoxData data)
    {
        return boxesData.TryGetValue(boxID, out data);
    }

    /// <summary>
    /// Records that a box was picked up (to track pickup time from the last button press).
    /// </summary>
    public void RecordBoxPickedUp(int boxID, BoxColor color)
    {
        BoxData data = GetOrCreateBoxData(boxID, color);

        float absoluteNow = Time.time - sceneStartTime;
        data.pickupAbsoluteTime = absoluteNow;
        data.pickupTime = absoluteNow - lastButtonPressTime; // time since last "Next" press
    }

    /// <summary>
    /// Records the participant's ending answer, then logs that in its own row if we want.
    /// </summary>
    /// <summary>
    /// Records the participant's ending answer along with the time from the last button press.
    /// </summary>
    public void RecordEndingAnswer(string answer)
    {
        if (answer == "Agree" || answer == "Disagree")
        {
            endingAnswer = answer;

            float answerTime = Time.time - sceneStartTime; // Absolute time when answer was given
            float responseDuration = answerTime - lastButtonPressTime; // Time since last button press

            if (dataLogger != null)
            {
                dataLogger.LogEndPromptRow(
                    dataLogger.participantID,
                    ConditionManager.Instance.currentCondition.ToString(),
                    endingAnswer,
                    responseDuration // Add this new data to the log
                );

                dataLogger.FlushDataToDisk();
            }
        }
        else
        {
            endingAnswer = "";
        }
    }


    /// <summary>
    /// Records that the user requested the next box after picking up boxID.
    /// This sets the "hesitation time" for that box.
    /// </summary>
    public void RecordNextBoxRequested(int boxID)
    {
        float absoluteNow = Time.time - sceneStartTime;
        if (boxesData.TryGetValue(boxID, out BoxData data))
        {
            data.nextRequestTime = absoluteNow;
            data.hesitationTime = data.nextRequestTime - data.pickupAbsoluteTime;
        }
    }

    /// <summary>
    /// Called once the box's final placement/correctness are set.
    /// </summary>
    public void OnBoxFinalized(int boxID)
    {
        // If already finalized/logged, skip
        if (finalizedBoxIDs.Contains(boxID)) return;

        if (!boxesData.TryGetValue(boxID, out BoxData data)) return;

        // Mark as finalized
        finalizedBoxIDs.Add(boxID);

        // Log exactly once
        LogBoxFinalData(data);

        // Increase count, check if all done, etc.
        boxesFinalizedCount++;
        if (boxesFinalizedCount >= totalBoxes)
        {
            AllBoxesHandled?.Invoke();
        }
    }

    /// <summary>
    /// Saves (flushes) data for all boxes so far, *without* forcing them all to be finalized.
    /// We simply flush the in-memory buffer to disk but do NOT call LogBoxFinalData again.
    /// </summary>
    public void SaveAllData()
    {
        if (dataLogger != null)
        {
            // Just flush the buffer. DO NOT re-log or finalize anything.
            dataLogger.FlushDataToDisk();
        }
    }

    /// <summary>
    /// Logs a single box's data to the CSV. This is *only* called from OnBoxFinalized().
    /// </summary>
    private void LogBoxFinalData(BoxData data)
    {
        if (dataLogger == null) return;

        // Build the "CorrectlySorted" column: "Yes" if wasCorrect == true
        string correctlySorted = (data.wasCorrect == true) ? "Yes" : "";

        // Build a single string for the entire placement history
        string placementHistoryStr = "None";
        if (data.placementHistory != null && data.placementHistory.Count > 0)
        {
            var distinctHistory = data.placementHistory.Distinct().ToList();
            List<string> parts = new List<string>();
            foreach (var (t, place) in distinctHistory)
            {
                parts.Add($"[{t:F2}: {place}]");
            }
            placementHistoryStr = string.Join(", ", parts);
        }

        // If there's no end prompt answer yet, default to "No end prompt"
        // Otherwise use the recorded answer.
        string finalEndStatement = string.IsNullOrEmpty(endingAnswer)
                                   ? "No end prompt"
                                   : endingAnswer;

        dataLogger.LogSingleRow(
            participantID: dataLogger.participantID,
            condition: ConditionManager.Instance.currentCondition.ToString(),
            endStatement: finalEndStatement,
            boxID: data.boxID,
            color: data.color.ToString(),
            spawnTime: data.spawnTime,
            pickupTime: data.pickupTime,
            hesitationTime: data.hesitationTime,
            placementHistory: placementHistoryStr,
            correctlySorted: correctlySorted
        );
    }
}
