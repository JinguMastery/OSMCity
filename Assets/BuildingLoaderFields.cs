using UnityEngine;

public class BuildingLoaderFields: LoaderFields
{

    // champs relatifs aux bâtiments pouvant être modifiés par l'utilisateur à l'initialisation
    [Header("Building file paths/names")]
    [Tooltip("File path of the attributes of buildings in the training set")]
    public string trainingPath = @"F:\Buildings\liechtenstein_training.txt";
    [Tooltip("File path of the attributes of buildings in the test set")]
    public string testPath = @"F:\Buildings\liechtenstein_test.txt";
    [Tooltip("File name of the building heights")]
    public string heightsFile = "liechtenstein_heights.txt";
    [Header("Number of building nodes in the test set to model")]
    [Tooltip("If negative, all building nodes in the source file will be modeled")]
    public int nBuildingNodes = -1;
    [Header("Number of building ways in the test set to model")]
    [Tooltip("If negative, all building ways in the source file will be modeled")]
    public int nBuildingWays = -1;
    [Header("Modeling buildings in reverse order or not ?")]
    public bool reverseOrder;
    [Header("Method used for the prediction of building heights")]
    public PredictionMethod predMethod = PredictionMethod.None;
    [Header("Writing building features or not ?")]
    public bool writeFeatures;

    private void Reset()
    {
        string cityName = "luxembourg";
        SetDefaultValues(cityName, "buildings", true);
        trainingPath = @"F:\Buildings\" + cityName + "_training_extract.txt";
        testPath = @"F:\Buildings\" + cityName + "_test_extract.txt";
        heightsFile = cityName + "_heights_extract.txt";
    }

}
