using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HighwayLoader : Loader
{

    public static Material DefHighwayMat;

    private readonly List<Highway> highways = new List<Highway>();
    private readonly List<Node> visitedNodes = new List<Node>();
    private GameObject highwayDetails;

    public GameObject HighwayMeshes { get; private set; }

    public HighwayLoaderFields Fields { get; private set; }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        DefHighwayMat = Resources.Load<Material>("Materials/asphalt");
        Fields = GameObject.Find("/Loaders").GetComponent<HighwayLoaderFields>();
        allPath = Main.XmlPath + Fields.elementsFile;
        nodesPath = Main.XmlPath + Fields.nodesFile;
        waysPath = Main.XmlPath + Fields.waysFile;
        relationsPath = Main.XmlPath + Fields.relationsFile;
        tagsPath = Main.XmlPath + Fields.tagsFile;

        LoadXML();  //Loads XML File

        highwayDetails = new GameObject
        {
            name = "Highway Details"
        };
        if (!Main.hideMeshInHierarchy)
        {
            HighwayMeshes = new GameObject
            {
                name = "Highway Meshes",
                hideFlags = HideFlags.NotEditable
            };
        }

        CreateOsmObjs();    //Create an instance of OsmHighway for several nodes, several ways, and each tag element

        foreach (var obj in osmObjs)
        {
            highways.Add(CreateHighway(obj));
        }

        FinishedLoading = true; //tell the program that we’ve finished loading data.
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void CreateOsmObjs()
    {
        if (Fields.reverseOrder)
        {
            for (int i = Ways.Length - 1; i >= (Fields.nHighwayWays < 0 ? 0 : Math.Max(Ways.Length - Fields.nHighwayWays, 0)); i--)
            {
                OsmHighway osmObj = new OsmHighway(Ways[i], this);
                osmObjs.Add(osmObj);
                visitedNodes.AddRange(osmObj.SubNodes);
            }
            for (int i = Nodes.Length - 1; i >= (Fields.nHighwayNodes < 0 ? 0 : Math.Max(Nodes.Length - Fields.nHighwayNodes, 0)); i--)
            {
                var found = from node in visitedNodes
                            where node.Id == Nodes[i].Id
                            select node;
                if (!found.Any())
                    osmObjs.Add(new OsmHighway(Nodes[i], this));
            }
        }
        else
        {
            for (int i = 0; i < (Fields.nHighwayWays < 0 ? Ways.Length : Math.Min(Fields.nHighwayWays, Ways.Length)); i++)
            {
                OsmHighway osmObj = new OsmHighway(Ways[i], this);
                osmObjs.Add(osmObj);
                visitedNodes.AddRange(osmObj.SubNodes);
            }
            for (int i = 0; i < (Fields.nHighwayNodes < 0 ? Nodes.Length : Math.Min(Fields.nHighwayNodes, Nodes.Length)); i++)
            {
                var found = from node in visitedNodes
                            where node.Id == Nodes[i].Id
                            select node;
                if (!found.Any())
                    osmObjs.Add(new OsmHighway(Nodes[i], this));
            }
        }
        foreach (var elem in TagElements)
        {
            osmTagObjs.Add(new OsmHighway(elem, this));
        }
    }

    private Highway CreateHighway(OsmObject obj)
    {
        GameObject go = new GameObject();
        go.transform.SetParent(highwayDetails.transform);
        go.name = "ID = " + obj.Element.Id;
        Highway highway = go.AddComponent<Highway>();
        cityObjs.Add(highway);
        highway.SetOsmObj(obj);
        highway.SetType("highway");
        return highway;
    }

}
