using System.Collections.Generic;
using UnityEngine;

public class BoxSpawner : MonoBehaviour
{
    [Header("Prefabs for Non-Ambiguous Colors")]
    public GameObject greenBoxPrefab;
    public GameObject redBoxPrefab;
    public GameObject blueBoxPrefab;

    [Header("Prefabs for Ambiguous Colors")]
    public GameObject ambiguousCyanBoxPrefab;
    public GameObject ambiguousPinkBoxPrefab;
    public GameObject ambiguousPurpleBoxPrefab;

    [Header("References")]
    public Transform spawnLocation;
    public FlowManager flowManager;
    public DataManager dataManager;
    public AgentBehavior agentBehavior;

    private const int totalBoxes = 16;
    private int currentBoxIndex = 0;
    private int boxCounter = 0;
    private bool allBoxesSpawned = false;

    // We'll keep a predefined pattern of box colors
    private static BoxColor[] precomputedBoxColors;

    // Dictionary to map each color to its specific prefab
    private Dictionary<BoxColor, GameObject> prefabDictionary;

    private void Awake()
    {
        // Initialize the dictionary with all six colors:
        prefabDictionary = new Dictionary<BoxColor, GameObject>
        {
            { BoxColor.Green, greenBoxPrefab },
            { BoxColor.Red, redBoxPrefab },
            { BoxColor.Blue, blueBoxPrefab },
            { BoxColor.AmbiguousCyan, ambiguousCyanBoxPrefab },
            { BoxColor.AmbiguousPink, ambiguousPinkBoxPrefab },
            { BoxColor.AmbiguousPurple, ambiguousPurpleBoxPrefab }
        };
    }

    private void Start()
    {
        // Precompute the box pattern if it hasn't been generated yet.
        if (precomputedBoxColors == null)
        {
            precomputedBoxColors = GenerateBoxPattern();
        }

        // Find the active AgentBehavior in the scene.
        AssignActiveAgent();
    }

    private void AssignActiveAgent()
    {
        // If you only have one active AgentBehavior, this should suffice.
        AgentBehavior[] allAgents = FindObjectsOfType<AgentBehavior>(true);
        foreach (var agent in allAgents)
        {
            if (agent.gameObject.activeInHierarchy)
            {
                agentBehavior = agent;
                break;
            }
        }

        if (!agentBehavior)
        {
            Debug.LogError("BoxSpawner: No active AgentBehavior found!");
        }
    }

    public void SpawnNextBox()
    {
        if (currentBoxIndex >= totalBoxes)
        {
            allBoxesSpawned = true;
            flowManager.OnAllBoxesHandled();
            Debug.Log("All boxes spawned, waiting for user action to finalize.");
            return;
        }

        // Get the color from the precomputed pattern.
        BoxColor color = precomputedBoxColors[currentBoxIndex];

        // Get the prefab for the chosen color.
        GameObject prefab = GetPrefabForColor(color);
        if (prefab == null)
        {
            Debug.LogError($"BoxSpawner: No prefab found for color {color}");
            return;
        }

        // Instantiate the box.
        GameObject newBox = Instantiate(prefab, spawnLocation.position, Quaternion.identity);

        // Setup the BoxIdentifier on the spawned object.
        BoxIdentifier identifier = newBox.GetComponent<BoxIdentifier>() ?? newBox.AddComponent<BoxIdentifier>();
        identifier.boxColor = color;
        identifier.boxID = boxCounter++;

        // Create or retrieve the BoxData from the DataManager.
        BoxData data = dataManager.GetOrCreateBoxData(identifier.boxID, color);

        // Record the spawn time relative to the scene start.
        float relativeSpawnTime = Time.time - dataManager.GetSceneStartTime();
        data.spawnTime = relativeSpawnTime;

        // Keep a direct reference to the BoxData on this object.
        identifier.boxData = data;

        currentBoxIndex++;
        Debug.Log("Boxes remaining: " + (totalBoxes - currentBoxIndex));

        // Optionally notify your agent.
        agentBehavior?.OnUserRequestedBox(newBox);
    }

    /// <summary>
    /// Returns true if all boxes have been spawned.
    /// </summary>
    public bool AreAllBoxesSpawned()
    {
        return allBoxesSpawned;
    }

    /// <summary>
    /// Looks up the prefab for a given color via the prefabDictionary.
    /// </summary>
    private GameObject GetPrefabForColor(BoxColor color)
    {
        if (prefabDictionary.TryGetValue(color, out GameObject prefab))
        {
            return prefab;
        }

        Debug.LogWarning($"BoxSpawner: Unknown box color {color}");
        return null;
    }

    /// <summary>
    /// Generates the pattern of colors for the 16 boxes.
    /// </summary>
    private BoxColor[] GenerateBoxPattern()
    {
        BoxColor[] colors = new BoxColor[totalBoxes];

        // First 4 boxes: random non-ambiguous.
        for (int i = 0; i < 4; i++)
        {
            colors[i] = GetRandomNonAmbiguousColor();
        }

        // Next 3 groups of 4 boxes, each group has exactly one ambiguous color.
        int[] groupStarts = { 4, 8, 12 };
        foreach (int startIndex in groupStarts)
        {
            int ambiguousIndex = Random.Range(startIndex, startIndex + 4);
            for (int i = startIndex; i < startIndex + 4; i++)
            {
                if (i == ambiguousIndex)
                {
                    // Randomly pick one of the three ambiguous colors.
                    colors[i] = GetRandomAmbiguousColor();
                }
                else
                {
                    // Otherwise pick a non-ambiguous color.
                    colors[i] = GetRandomNonAmbiguousColor();
                }
            }
        }

        return colors;
    }

    /// <summary>
    /// Randomly returns Blue, Red, or Green.
    /// </summary>
    private BoxColor GetRandomNonAmbiguousColor()
    {
        int rand = Random.Range(0, 3);
        switch (rand)
        {
            case 0: return BoxColor.Blue;
            case 1: return BoxColor.Red;
            default: return BoxColor.Green;
        }
    }

    /// <summary>
    /// Randomly returns one of the three ambiguous colors.
    /// </summary>
    private BoxColor GetRandomAmbiguousColor()
    {
        int rand = Random.Range(0, 3);
        switch (rand)
        {
            case 0: return BoxColor.AmbiguousCyan;
            case 1: return BoxColor.AmbiguousPink;
            default: return BoxColor.AmbiguousPurple;
        }
    }
}

