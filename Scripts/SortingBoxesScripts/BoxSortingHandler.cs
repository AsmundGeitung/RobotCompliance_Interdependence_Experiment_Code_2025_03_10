using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BoxSortingHandler : MonoBehaviour
{
    public DataManager dataManager;
    private HashSet<GameObject> boxesInBin = new HashSet<GameObject>();

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Box")) return;

        var identifier = other.GetComponent<BoxIdentifier>();
        if (identifier != null)
        {
            string placeName = GetPlacementNameFromTag(this.tag);
            identifier.currentPlacement = placeName;
        }

        boxesInBin.Add(other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Box")) return;

        var identifier = other.GetComponent<BoxIdentifier>();
        if (identifier != null)
        {
            identifier.currentPlacement = "Other";
        }

        boxesInBin.Remove(other.gameObject);
    }

    /// <summary>
    /// Finalizes boxes that are currently in this bin.
    /// Returns true if any of them was correct, false otherwise.
    /// </summary>
    public bool FinalizeSortedBoxes()
    {
        bool anyCorrect = false;
        List<GameObject> currentBoxes = new List<GameObject>(boxesInBin);

        foreach (var boxObj in currentBoxes)
        {
            if (boxObj == null) continue;

            BoxIdentifier identifier = boxObj.GetComponent<BoxIdentifier>();
            if (identifier == null) continue;

            // Retrieve the BoxData
            if (!dataManager.TryGetBoxData(identifier.boxID, out BoxData data))
                data = dataManager.GetOrCreateBoxData(identifier.boxID, identifier.boxColor);

            // If the box was picked up but has not yet been marked with a next-request timestamp,
            // record the next box request to calculate hesitation time.
            if (data.pickupTime > 0 && data.nextRequestTime <= 0)
            {
                dataManager.RecordNextBoxRequested(identifier.boxID);
            }

            // Determine correctness based on bin tag
            bool isCorrect = CheckBoxPlacement(identifier);
            data.wasCorrect = isCorrect;

            // Derive final placement from the bin tag
            string finalPlacement = GetPlacementNameFromTag(gameObject.tag);
            data.finalPlacement = finalPlacement;

            // Add to placement history
            float localTime = Time.time - dataManager.GetSceneStartTime();
            data.placementHistory.Add((localTime, finalPlacement));

            // If this bin destroys the box, finalize it
            if (IsBinThatDestroysThisBox())
            {
                dataManager.OnBoxFinalized(identifier.boxID);
                Destroy(boxObj);
                boxesInBin.Remove(boxObj);

                if (isCorrect) anyCorrect = true;
            }
        }

        return anyCorrect;
    }

    private bool CheckBoxPlacement(BoxIdentifier boxID)
    {
        // "Correct" only if color matches bin's tag 
        // (only Red/Blue/Green bins are considered correct).
        return (CompareTag("GreenBin") && boxID.boxColor == BoxColor.Green)
            || (CompareTag("BlueBin") && boxID.boxColor == BoxColor.Blue)
            || (CompareTag("RedBin") && boxID.boxColor == BoxColor.Red);
    }

    private string GetPlacementNameFromTag(string tag)
    {
        return tag switch
        {
            "GreenBin" => "GreenBin",
            "BlueBin" => "BlueBin",
            "RedBin" => "RedBin",
            "Compressor" => "Compressor",
            "YellowDrawer" => "YellowDrawer",
            "TableArea" => "Table",
            _ => "Floor"
        };
    }

    private bool IsBinThatDestroysThisBox()
    {
        return CompareTag("GreenBin")
            || CompareTag("BlueBin")
            || CompareTag("RedBin")
            || CompareTag("Compressor");
    }
}
