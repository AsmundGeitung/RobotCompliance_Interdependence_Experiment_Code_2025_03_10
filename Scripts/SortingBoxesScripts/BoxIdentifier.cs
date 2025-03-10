using UnityEngine;

public enum BoxColor
{
    Green,
    Blue,
    Red,
    AmbiguousCyan,
    AmbiguousPink,
    AmbiguousPurple,
}

public class BoxIdentifier : MonoBehaviour
{
    public int boxID;
    public BoxColor boxColor;
    public BoxData boxData;
    public string currentPlacement = "SomewhereElse";
}

