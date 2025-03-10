using System.Collections.Generic;

public class BoxData
{
    public int boxID;
    public BoxColor color;
    public float spawnTime;
    public float pickupTime;
    public float pickupAbsoluteTime;
    public float hesitationTime;
    public float nextRequestTime;
    public bool wasCorrect;
    public string finalPlacement = "Other";
    public List<(float, string)> placementHistory = new List<(float, string)>();
}

