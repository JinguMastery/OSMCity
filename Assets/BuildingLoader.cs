using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

public class BuildingLoader : Loader
{

    public static Material DefBuildingMat, DefRoofMat;      //The default building and roof materials

    private readonly List<Building> buildingsTraining = new List<Building>();
    private readonly List<Building> buildingsTest = new List<Building>();
    private readonly List<Node> visitedNodes = new List<Node>();
    private bool isTrainingDone, isTestDone;
    private string heightsPath;
    private GameObject buildingDetails;

    public Dictionary<long, float> Heights { get; } = new Dictionary<long, float>();
    public GameObject BuildingMeshes { get; private set; }
    public GameObject RoofMeshes { get; private set; }
    public BuildingLoaderFields Fields { get; private set; }

    void Start()
    {
        DefBuildingMat = Resources.Load<Material>("Materials/building2");
        DefRoofMat = Resources.Load<Material>("Materials/roof2");
        Fields = GameObject.Find("/Loaders").GetComponent<BuildingLoaderFields>();
        allPath = Main.XmlPath + Fields.elementsFile;
        nodesPath = Main.XmlPath + Fields.nodesFile;
        waysPath = Main.XmlPath + Fields.waysFile;
        relationsPath = Main.XmlPath + Fields.relationsFile;
        tagsPath = Main.XmlPath + Fields.tagsFile;
        heightsPath = Main.TxtPath + Fields.heightsFile;

        LoadXML();  //Loads XML File

        buildingDetails = new GameObject
        {
            name = "Building Details"
        };
        if (!Main.hideMeshInHierarchy)
        {
            BuildingMeshes = new GameObject
            {
                name = "Building Meshes",
                hideFlags = HideFlags.NotEditable
            };
            RoofMeshes = new GameObject
            {
                name = "Roof Meshes",
                hideFlags = HideFlags.NotEditable
            };
        }
        if (Fields.predMethod == PredictionMethod.Text)       //importe les données sur les hauteurs des bâtiments
        {
            LoadHeights();
        }

        CreateOsmObjs();    //Create an instance of OsmBuilding for several nodes, several ways, and each tag element

        //obtient les bâtiments dont on connaît la hauteur et le nombre d'étages pour le "training set"
        var trainObjs = from obj in osmTagObjs
                        where obj.Element.Tags.ContainsKey("height")
                        select obj;

        foreach (var obj in trainObjs)
        {
            buildingsTraining.Add(CreateBuilding(obj));
        }

        //obtient le reste des bâtiments pour le "test set"
        var testObjs = from obj in osmObjs
                       where !obj.Element.Tags.ContainsKey("height")
                       select obj;

        foreach (var obj in testObjs)
        {
            buildingsTest.Add(CreateBuilding(obj));
        }

        FinishedLoading = true; //tell the program that we’ve finished loading data.
    }

    void Update()
    {
        if (Fields.writeFeatures && Main.FinishedAllLoading)
        {
            if (!isTrainingDone)
            {
                var meshBuildings = from building in buildingsTraining
                                    where !building.IsMeshCreated
                                    select building;
                if (!meshBuildings.Any())
                {
                    SaveBuildings(buildingsTraining.ToArray(), Fields.trainingPath);
                    isTrainingDone = true;
                }
            }
            if (!isTestDone)
            {
                var meshBuildings = from building in buildingsTest
                                    where !building.IsMeshCreated
                                    select building;
                if (!meshBuildings.Any())
                {
                    SaveBuildings(buildingsTest.ToArray(), Fields.testPath);
                    isTestDone = true;
                }
            }
        }
    }

    public void LoadHeights()
    {
        StreamReader reader = null;
        try
        {
            reader = new StreamReader(heightsPath);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                int ind = line.IndexOf(':');
                long id = long.Parse(line.Substring(0, ind), CultureInfo.InvariantCulture);
                float h = float.Parse(line.Substring(ind + 2).Replace(',', '.'), CultureInfo.InvariantCulture);
                Heights.Add(id, h);
            }
        }
        catch (Exception exc)
        {
            Debug.LogWarning(exc.Message);
        }
        finally
        {
            reader?.Close();
        }
    }

    private void CreateOsmObjs()
    {
        if (Fields.reverseOrder)
        {
            for (int i = Ways.Length - 1; i >= (Fields.nBuildingWays < 0 ? 0 : Math.Max(Ways.Length - Fields.nBuildingWays, 0)); i--)
            {
                OsmBuilding osmObj = new OsmBuilding(Ways[i], this);
                osmObjs.Add(osmObj);
                visitedNodes.AddRange(osmObj.SubNodes);
            }
            for (int i = Nodes.Length - 1; i >= (Fields.nBuildingNodes < 0 ? 0 : Math.Max(Nodes.Length - Fields.nBuildingNodes, 0)); i--)
            {
                var found = from node in visitedNodes
                            where node.Id == Nodes[i].Id
                            select node;
                if (!found.Any())
                    osmObjs.Add(new OsmBuilding(Nodes[i], this));
            }
        }
        else
        {
            for (int i = 0; i < (Fields.nBuildingWays < 0 ? Ways.Length : Math.Min(Fields.nBuildingWays, Ways.Length)); i++)
            {
                OsmBuilding osmObj = new OsmBuilding(Ways[i], this);
                osmObjs.Add(osmObj);
                visitedNodes.AddRange(osmObj.SubNodes);
            }
            for (int i = 0; i < (Fields.nBuildingNodes < 0 ? Nodes.Length : Math.Min(Fields.nBuildingNodes, Nodes.Length)); i++)
            {
                var found = from node in visitedNodes
                            where node.Id == Nodes[i].Id
                            select node;
                if (!found.Any())
                    osmObjs.Add(new OsmBuilding(Nodes[i], this));
            }
        }
        foreach (var elem in TagElements)
        {
            osmTagObjs.Add(new OsmBuilding(elem, this));
        }
    }

    private Building CreateBuilding(OsmObject obj)
    {
        GameObject go = new GameObject();
        go.transform.SetParent(buildingDetails.transform);
        go.name = "ID = " + obj.Element.Id;
        Building building = go.AddComponent<Building>();
        cityObjs.Add(building);
        building.SetOsmObj(obj);
        building.SetType("building");
        return building;
    }

    private void SaveBuildings(Building[] buildings, string path)
    {
        StreamWriter writer = null;
        try
        {
            writer = new StreamWriter(path);
            foreach (var building in buildings)
            {
                writer.WriteLine($"{building.OsmObject.Element.Id},{building.GroundArea},{building.Perimeter},{building.NormPerimeterIndex},{building.NFloors},{building.NetInternalSurface},{building.GetNeighbors(Main.radius).Length}," +
                    $"{building.Length},{building.Width},{building.Type},{building.Height}");
            }
        }
        catch (Exception exc)
        {
            Debug.LogException(exc);
        }
        finally
        {
            writer?.Close();
        }
    }

}
