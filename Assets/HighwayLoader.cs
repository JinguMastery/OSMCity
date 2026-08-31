using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HighwayLoader : Loader
{

    public static Material DefHighwayMat;

    private readonly List<Highway> highways = new List<Highway>();
    //ids only (not full Node objects) since this is only ever used as a membership check, not to look
    //anything up - a HashSet<long> makes that check O(1) instead of a LINQ linear scan per top-level node
    private readonly HashSet<long> visitedNodes = new HashSet<long>();
    //a point-prop node (traffic_signals, bus_stop, ...) placed exactly at a junction is shared by every way
    //that meets there, and each of those ways gets its own Highway component/Start() call - this tracks
    //which node ids have already been spawned so only the first way to reach one actually instantiates it
    private readonly HashSet<long> spawnedPointFeatureIds = new HashSet<long>();
    private GameObject highwayDetails;

    //an isolated point-prop node (one not part of any way's own node list, e.g. most bus_stop nodes in real
    //OSM data) needs to find whichever way segment passes closest to it. Scanning every way's every segment
    //for every such node is O(isolated nodes * total segments) and was measured to make scene load
    //unreasonably slow on a country-sized extract - this grid buckets every way segment by which
    //MaxPostToWayDistance-sized cell(s) it falls in, so that search only has to look at a 3x3 neighborhood
    //of cells around the query point instead of every segment in the loader
    public const float MaxPostToWayDistance = 15f;
    //a road with no explicit OSM "width" tag falls back to this - matches Highway's own default highwayWidth
    //field (3f), halved since callers need the half-width (centerline to edge)
    private const float DefaultRoadHalfWidth = 1.5f;
    private Dictionary<(int x, int z), List<(Vector3 a, Vector3 b, long wayId, float halfWidth)>> waySegmentGrid;

    public GameObject HighwayMeshes { get; private set; }

    public HighwayLoaderFields Fields { get; private set; }

    //returns true (and claims it) the first time this node id is passed in, false on every later call for
    //the same id - lets a point-prop node shared by several ways be spawned exactly once
    public bool TryMarkPointFeatureSpawned(long nodeId)
    {
        return spawnedPointFeatureIds.Add(nodeId);
    }

    //every way segment whose cell (or the cell of its midpoint) falls in the 3x3 neighborhood around pos's
    //own cell - a superset of "every segment within MaxPostToWayDistance of pos", cheap to further filter
    //by exact distance at the call site. wayId/halfWidth let a caller tell "this is my own road" (skip it)
    //from "this is a different, crossing road" (a candidate for intersection-overlap checks) and know how
    //far that other road's own surface extends from its centerline
    public IEnumerable<(Vector3 a, Vector3 b, long wayId, float halfWidth)> GetNearbySegments(Vector3 pos)
    {
        // built lazily, on first query, rather than eagerly at the end of HighwayLoader.Start(): Main and
        // HighwayLoader are components on separate GameObjects, so Unity does not guarantee Main.Start() (which
        // sets terrainCenterCoords) runs before HighwayLoader.Start() - building eagerly there race-lost against
        // that and indexed every segment using a still-zero terrainCenterCoords (raw, uncentered earth
        // coordinates). The first real query only ever happens from inside a dynamically-created Highway's own
        // Start(), which Unity defers to a later pass - by then every other Start() call, Main's included, has
        // already run, so terrainCenterCoords is guaranteed final by this point.
        if (waySegmentGrid == null)
            BuildWaySegmentGrid();
        int cx = Mathf.FloorToInt(pos.x / MaxPostToWayDistance);
        int cz = Mathf.FloorToInt(pos.z / MaxPostToWayDistance);
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (waySegmentGrid.TryGetValue((cx + dx, cz + dz), out var segments))
                {
                    foreach (var segment in segments)
                        yield return segment;
                }
            }
        }
    }

    //built once from every way's own already-resolved SubNodes (no per-node id lookup needed - each way's
    //SubNodes is already in the same order as its own Way.Nodes id list), instead of every isolated node
    //independently rescanning every way
    private void BuildWaySegmentGrid()
    {
        waySegmentGrid = new Dictionary<(int, int), List<(Vector3, Vector3, long, float)>>();
        foreach (var obj in osmObjs)
        {
            if (obj.Element.Type != OsmGeoType.Way)
                continue;
            long wayId = obj.Element.Id ?? -1;
            float halfWidth = GetWayHalfWidth(obj.Element);
            Node[] wayNodes = obj.SubNodes;
            for (int i = 0; i < wayNodes.Length - 1; i++)
            {
                Vector3? a = GetNodeWorldPosition(wayNodes[i]);
                Vector3? b = GetNodeWorldPosition(wayNodes[i + 1]);
                if (a.HasValue && b.HasValue)
                    AddSegmentToGrid(a.Value, b.Value, wayId, halfWidth);
            }
        }
    }

    private static float GetWayHalfWidth(OsmElement way)
    {
        if (way.Tags.TryGetValue("width", out string strWidth) && float.TryParse(strWidth, out float width))
            return width / 2f;
        return DefaultRoadHalfWidth;
    }

    private void AddSegmentToGrid(Vector3 a, Vector3 b, long wayId, float halfWidth)
    {
        HashSet<(int, int)> cells = new HashSet<(int, int)>
        {
            CellOf(a), CellOf(b), CellOf((a + b) / 2f)
        };
        foreach (var cell in cells)
        {
            if (!waySegmentGrid.TryGetValue(cell, out var segments))
            {
                segments = new List<(Vector3, Vector3, long, float)>();
                waySegmentGrid[cell] = segments;
            }
            segments.Add((a, b, wayId, halfWidth));
        }
    }

    private static (int, int) CellOf(Vector3 pos)
    {
        return (Mathf.FloorToInt(pos.x / MaxPostToWayDistance), Mathf.FloorToInt(pos.z / MaxPostToWayDistance));
    }

    private Vector3? GetNodeWorldPosition(Node node)
    {
        if (node.Latitude == null || node.Longitude == null)
            return null;
        return Main.GetTerrainCoords((double)node.Latitude, (double)node.Longitude);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        DefHighwayMat = Resources.Load<Material>("Materials/road1");
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

    private void MarkVisited(Node[] nodes)
    {
        foreach (var node in nodes)
        {
            if (node.Id.HasValue)
                visitedNodes.Add(node.Id.Value);
        }
    }

    private void CreateOsmObjs()
    {
        if (Fields.reverseOrder)
        {
            for (int i = Ways.Length - 1; i >= (Fields.nHighwayWays < 0 ? 0 : Math.Max(Ways.Length - Fields.nHighwayWays, 0)); i--)
            {
                OsmHighway osmObj = new OsmHighway(Ways[i], this);
                osmObjs.Add(osmObj);
                MarkVisited(osmObj.SubNodes);
            }
            for (int i = Nodes.Length - 1; i >= (Fields.nHighwayNodes < 0 ? 0 : Math.Max(Nodes.Length - Fields.nHighwayNodes, 0)); i--)
            {
                if (!Nodes[i].Id.HasValue || !visitedNodes.Contains(Nodes[i].Id.Value))
                    osmObjs.Add(new OsmHighway(Nodes[i], this));
            }
        }
        else
        {
            for (int i = 0; i < (Fields.nHighwayWays < 0 ? Ways.Length : Math.Min(Fields.nHighwayWays, Ways.Length)); i++)
            {
                OsmHighway osmObj = new OsmHighway(Ways[i], this);
                osmObjs.Add(osmObj);
                MarkVisited(osmObj.SubNodes);
            }
            for (int i = 0; i < (Fields.nHighwayNodes < 0 ? Nodes.Length : Math.Min(Fields.nHighwayNodes, Nodes.Length)); i++)
            {
                if (!Nodes[i].Id.HasValue || !visitedNodes.Contains(Nodes[i].Id.Value))
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
