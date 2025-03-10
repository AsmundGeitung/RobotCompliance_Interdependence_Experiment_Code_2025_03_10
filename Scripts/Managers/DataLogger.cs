using System.IO;
using System.Text;
using UnityEngine;

public class DataLogger : MonoBehaviour
{
    public string participantID = "P000";

    private string experimentDataPath;
    private StringBuilder dataBuffer = new StringBuilder();

    public static DataLogger Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Setup our single CSV path
        string basePath = Application.persistentDataPath;
        experimentDataPath = Path.Combine(basePath, "ExperimentData.csv");

        Debug.Log("Experiment Data Path: " + experimentDataPath);
        InitializeFile();
    }

    private void InitializeFile()
    {
        // Only create headers if file doesn't exist
        if (!File.Exists(experimentDataPath))
        {
            string header = "ParticipantID;Condition;BoxID;Color;SpawnTime;PickupTime;HesitationTime;PlacementHistory;CorrectlySorted;EndStatement\n";
            File.WriteAllText(experimentDataPath, header);
        }
    }

    // --------------------------------------------------------------------
    //  The normal row logger for boxes
    // --------------------------------------------------------------------
    public void LogSingleRow(
        string participantID,
        string condition,
        string endStatement,
        int boxID,
        string color,
        float spawnTime,
        float pickupTime,
        float hesitationTime,
        string placementHistory,
        string correctlySorted
    )
    {
        string row = string.Format(
            "{0};{1};{2};{3};{4:F2};{5:F2};{6:F2};\"{7}\";{8};{9}\n",
            participantID,
            condition,
            boxID,
            color,
            spawnTime,
            pickupTime,
            hesitationTime,
            placementHistory.Replace("\"", "'"),
            correctlySorted,
            endStatement
        );

        dataBuffer.Append(row);
    }

    // --------------------------------------------------------------------
    //  A special helper to log the user’s end prompt
    //  in its own row
    // --------------------------------------------------------------------
    public void LogEndPromptRow(string participantID, string condition, string endStatement, float responseTime)
    {
        // For instance, we can leave BoxID blank or "END"
        // The key is to keep the CSV columns in order (BoxID,Color, etc.)
        // so we fill them with placeholders.
        //
        // e.g. "ParticipantID; Condition; BoxID; Color; SpawnTime; PickupTime; ...
        // Just put 'NA' for columns not relevant to this row.
        string row = string.Format(
            "{0};{1};END;NA;0.00;0.00;0.00;\"(None)\";{3};{2}\n",
            participantID,
            condition,
            endStatement,
            responseTime
        );
        dataBuffer.Append(row);
    }

    // --------------------------------------------------------------------
    //  Flush buffer to disk
    // --------------------------------------------------------------------
    public void FlushDataToDisk()
    {
        if (dataBuffer.Length > 0)
        {
            File.AppendAllText(experimentDataPath, dataBuffer.ToString());
            dataBuffer.Clear();
        }
    }

    private void OnApplicationQuit()
    {
        FlushDataToDisk();
    }
}
