using UnityEngine;

public class HighwayLoaderFields : LoaderFields
{

    [Header("Number of highway nodes to model")]
    [Tooltip("If negative, all highway nodes in the source file will be modeled")]
    public int nHighwayNodes = 0;
    [Header("Number of highway ways to model")]
    [Tooltip("If negative, all highway ways in the source file will be modeled")]
    public int nHighwayWays = -1;
    [Header("Modeling highways in reverse order or not ?")]
    public bool reverseOrder;

    private void Reset()
    {
        SetDefaultValues("liechtenstein", "highways");
    }

}
