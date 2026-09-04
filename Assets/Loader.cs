using UnityEngine;
using System.Collections.Generic; //Needed for Lists
using System.Xml; //Needed for XML functionality
using System;
using System.IO;
using System.Linq;
using System.Globalization;

public abstract class Loader : MonoBehaviour
{

    protected List<OsmObject> osmObjs = new List<OsmObject>();
    protected List<OsmObject> osmTagObjs = new List<OsmObject>();
    protected List<CityObject> cityObjs = new List<CityObject>();
    protected string allPath, nodesPath, waysPath, relationsPath, tagsPath;

    private XmlReader allReader, nodesReader, waysReader, relationsReader, tagsReader;
    private readonly List<OsmElement> elements = new List<OsmElement>(); //Initialize list of all elements
    private readonly List<OsmElement> tagElements = new List<OsmElement>(); //Initialize list of all tag elements (for ML)
    private readonly List<Node> nodes = new List<Node> (); //Initialize List of OSM nodes
    private readonly List<Way> ways = new List<Way> (); //Initialize List of OSM ways
    private readonly List<Relation> relations = new List<Relation> (); //Initialize List of OSM relations
    private readonly List<Node> subNodes = new List<Node>(); //Initialize List of OSM subnodes
    private readonly List<Way> subWays = new List<Way>(); //Initialize List of OSM subways
    private readonly List<Relation> subRelations = new List<Relation>(); //Initialize List of OSM subrelations
    private Boundaries? bounds;

    //id -> element indexes over subNodes/subWays/subRelations, built lazily (once) on first lookup instead
    //of OsmObject doing a fresh LINQ linear scan - over a freshly-.ToArray()'d copy, on top of that - for
    //every single node/way/relation reference it needs to resolve. For a way with a dozen node refs this
    //barely matters, but resolving thousands of ways this way is O(refs * total elements) and was measured
    //to dominate scene load time on a real dataset (billions of comparisons for a country-sized extract)
    private Dictionary<long, Node> subNodesById;
    private Dictionary<long, Way> subWaysById;
    private Dictionary<long, Relation> subRelationsById;
    //same idea as the dictionaries above, but for CityObject -> its own index in cityObjs, so GetNeighbors
    //doesn't have to ToList().IndexOf() (an O(n) copy-then-scan) every time it's called
    private Dictionary<CityObject, int> cityObjIndices;

    public Main Main { get; set; }
    public bool FinishedLoading { get; protected set; }

    //List of OSM elements which is related to a list of OSM/game objects of the same type in the scene
    public OsmElement[] Elements => elements.ToArray();
    public OsmElement[] TagElements => tagElements.ToArray();
    public Node[] Nodes => nodes.ToArray();
    public Way[] Ways => ways.ToArray();
    public Relation[] Relations => relations.ToArray();
    public Node[] SubNodes => subNodes.ToArray();
    public Way[] SubWays => subWays.ToArray();
    public Relation[] SubRelations => subRelations.ToArray();
    public OsmObject[] OsmObjects => osmObjs.ToArray();
    public OsmObject[] OsmTagObjects => osmTagObjs.ToArray();
    public IReadOnlyList<CityObject> CityObjects => cityObjs;


    public Boundaries Bounds
    {
        get
        {
            if (bounds != null)
                return (Boundaries)bounds;
            Boundaries b;
            List<double> lats = new List<double>();
            List<double> lons = new List<double>();
            foreach (var node in SubNodes)
            {
                if (node.Latitude != null && node.Longitude != null)
                {
                    lats.Add((double)node.Latitude);
                    lons.Add((double)node.Longitude);
                }
            }
            foreach (var node in Nodes)
            {
                if (node.Latitude != null && node.Longitude != null)
                {
                    lats.Add((double)node.Latitude);
                    lons.Add((double)node.Longitude);
                }
            }
            if (lats.Count == 0)
            {
                // no node with coordinates was ever loaded - most commonly because the configured XML
                // files are missing (see LoadXML's WarnIfPathConfigured for the actual root-cause warning).
                // lats.Min()/Max() would throw InvalidOperationException ("Sequence contains no elements")
                // here, crashing the whole loader over what should just be an empty scene - fall back to a
                // zero-sized region at (0,0) instead
                Debug.LogWarning("Loader.Bounds: no nodes with coordinates were loaded - falling back to a zero-sized region at (0,0). Check that this loader's XML files exist and aren't empty.");
                b = new Boundaries(0, 0, 0, 0);
            }
            else
            {
                b = new Boundaries(lats.Min(), lats.Max(), lons.Min(), lons.Max());
            }
            bounds = b;
            return b;
        }
    }

    //O(1) (amortized): looks up obj's index in cityObjs via a lazily-built cache instead of scanning for it.
    //The cache self-heals if cityObjs grows after it was built (still mid-load, or a stale count) by rebuilding
    //from scratch, so this stays correct to call at any point, not just after loading fully completes.
    public int IndexOf(CityObject obj)
    {
        if (cityObjIndices == null || cityObjIndices.Count != cityObjs.Count)
        {
            cityObjIndices = new Dictionary<CityObject, int>(cityObjs.Count);
            for (int i = 0; i < cityObjs.Count; i++)
                cityObjIndices[cityObjs[i]] = i;
        }
        return cityObjIndices.TryGetValue(obj, out int index) ? index : -1;
    }

    //everything freed here is only ever needed during CreateOsmObjs's resolution pass (BuildingLoader/
    //HighwayLoader call this right after that pass finishes): every OsmHighway/OsmBuilding resolves and
    //caches its own SubNodes/SubWays/SubRelations exactly once, synchronously, in its constructor (see
    //OsmObject.SetSubElements) - nothing reads Loader's own raw lists or subNodesById/subWaysById/
    //subRelationsById again afterward. On a country-sized extract (e.g. Luxembourg's ~123MB of raw highway
    //node XML, ~865MB for buildings) these lists, plus subNodesById's own full duplicate index of every
    //node reference, hold a lot of memory for no further purpose.
    //Nodes/SubNodes are the one exception: Main reads Bounds later, once FinishedLoading is observed, and
    //Bounds' own getter reads Nodes/SubNodes - forcing it to evaluate (and cache) here first means that by
    //the time Main asks for it, the cached value already exists and the underlying lists can be freed too.
    protected void ReleaseIntermediateLoadState()
    {
        _ = Bounds;

        elements.Clear(); elements.TrimExcess();
        tagElements.Clear(); tagElements.TrimExcess();
        nodes.Clear(); nodes.TrimExcess();
        ways.Clear(); ways.TrimExcess();
        relations.Clear(); relations.TrimExcess();
        subNodes.Clear(); subNodes.TrimExcess();
        subWays.Clear(); subWays.TrimExcess();
        subRelations.Clear(); subRelations.TrimExcess();

        subNodesById = null;
        subWaysById = null;
        subRelationsById = null;
    }

    public bool TryGetSubNode(long id, out Node node)
    {
        subNodesById ??= BuildIndex(subNodes);
        return subNodesById.TryGetValue(id, out node);
    }

    public bool TryGetSubWay(long id, out Way way)
    {
        subWaysById ??= BuildIndex(subWays);
        return subWaysById.TryGetValue(id, out way);
    }

    public bool TryGetSubRelation(long id, out Relation relation)
    {
        subRelationsById ??= BuildIndex(subRelations);
        return subRelationsById.TryGetValue(id, out relation);
    }

    private static Dictionary<long, T> BuildIndex<T>(List<T> elements) where T : OsmElement
    {
        Dictionary<long, T> index = new Dictionary<long, T>();
        foreach (T element in elements)
        {
            if (element.Id.HasValue && !index.ContainsKey(element.Id.Value))
                index[element.Id.Value] = element;
        }
        return index;
    }

    protected void LoadXML()
    {
        try
        {
            if (allPath != null && allPath.Length > 0 && File.Exists(allPath))
            {
                allReader = XmlReader.Create(allPath);
                ReadXML(allReader);
            }
            else
                WarnIfPathConfigured(allPath, "elements");
            if (tagsPath != null && tagsPath.Length > 0 && File.Exists(tagsPath))
            {
                tagsReader = XmlReader.Create(tagsPath);
                ReadTagXML(tagsReader);
            }
            else
                WarnIfPathConfigured(tagsPath, "tags");
            if (nodesPath != null && nodesPath.Length > 0 && File.Exists(nodesPath))
            {
                nodesReader = XmlReader.Create(nodesPath);
                nodesReader.MoveToContent();
                nodesReader.Read();
                while (nodesReader.NodeType != XmlNodeType.EndElement && nodesReader.Name != "osm")
                {
                    Node node = AssignNode(nodesReader);
                    if (node != null)
                        subNodes.Add(node);
                }
            }
            else
                WarnIfPathConfigured(nodesPath, "nodes");
            if (waysPath != null && waysPath.Length > 0 && File.Exists(waysPath))
            {
                waysReader = XmlReader.Create(waysPath);
                waysReader.MoveToContent();
                waysReader.Read();
                while (waysReader.NodeType != XmlNodeType.EndElement && waysReader.Name != "osm")
                {
                    Way way = AssignWay(waysReader);
                    if (way != null)
                        subWays.Add(way);
                }
            }
            else
                WarnIfPathConfigured(waysPath, "ways");
            if (relationsPath != null && relationsPath.Length > 0 && File.Exists(relationsPath))
            {
                relationsReader = XmlReader.Create(relationsPath);
                relationsReader.MoveToContent();
                relationsReader.Read();
                while (relationsReader.NodeType != XmlNodeType.EndElement && relationsReader.Name != "osm")
                {
                    Relation relation = AssignRelation(relationsReader);
                    if (relation != null)
                        subRelations.Add(relation);
                }
            }
            else
                WarnIfPathConfigured(relationsPath, "relations");
        }
        catch (Exception exc)
        {
            Debug.LogException(exc);
            return;
        }
        finally
        {
            allReader?.Close();
            nodesReader?.Close();
            waysReader?.Close();
            relationsReader?.Close();
        }
    }

    //a configured-but-missing path was silently skipped before this - the loader just carried on with an
    //empty list for that file, which surfaced much later as a confusing failure somewhere downstream (e.g.
    //Bounds throwing "Sequence contains no elements" if EVERY file ended up missing) instead of pointing at
    //the actual missing file. Only warns when a path was actually configured (non-null/non-empty) - a
    //loader that legitimately has no relationsFile set, for instance, shouldn't spam a warning for it.
    private static void WarnIfPathConfigured(string path, string label)
    {
        if (!string.IsNullOrEmpty(path))
            Debug.LogWarning($"Loader: {label} XML file not found at '{path}' - skipping it.");
    }

    private void ReadXML(XmlReader reader)
    {
        reader.MoveToContent();
        reader.Read();
        while (reader.NodeType != XmlNodeType.EndElement && reader.Name != "osm")   //teste si le type du noeud courant est un élément, sinon la fin du fichier XML a été atteinte
        {
            switch (reader.Name)
            {
                case "node":
                    Node node = AssignNode(reader);
                    if (node != null)
                    {
                        nodes.Add(node);
                        elements.Add(node);
                    }
                    break;
                case "way":
                    Way way = AssignWay(reader);
                    if (way != null)
                    {
                        ways.Add(way);
                        elements.Add(way);
                    }
                    break;
                case "relation":
                    Relation relation = AssignRelation(reader);
                    if (relation != null)
                    {
                        relations.Add(relation);
                        elements.Add(relation);
                    }
                    break;
                default:
                    reader.Read();
                    break;
            }
        }
    }

    private void ReadTagXML(XmlReader reader)
    {
        reader.MoveToContent();
        reader.Read();
        while (reader.NodeType != XmlNodeType.EndElement && reader.Name != "osm")   //teste si le type du noeud courant est un élément, sinon la fin du fichier XML a été atteinte
        {
            switch (reader.Name)
            {
                case "node":
                    Node node = AssignNode(reader);
                    if (node != null)
                    {
                        tagElements.Add(node);
                    }
                    break;
                case "way":
                    Way way = AssignWay(reader);
                    if (way != null)
                    {
                        tagElements.Add(way);
                    }
                    break;
                case "relation":
                    Relation relation = AssignRelation(reader);
                    if (relation != null)
                    {
                        tagElements.Add(relation);
                    }
                    break;
                default:
                    reader.Read();
                    break;
            }
        }
    }

    private Node AssignNode(XmlReader reader)
    {
        try
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "node")
            {
                long id = long.Parse(reader.GetAttribute("id"), CultureInfo.InvariantCulture);
                long uid = long.Parse(reader.GetAttribute("uid"), CultureInfo.InvariantCulture);
                long changeset = long.Parse(reader.GetAttribute("changeset"), CultureInfo.InvariantCulture);
                bool visible = bool.Parse(reader.GetAttribute("visible"));
                DateTime timestamp = DateTime.Parse(reader.GetAttribute("timestamp"), CultureInfo.InvariantCulture);
                int version = int.Parse(reader.GetAttribute("version"), CultureInfo.InvariantCulture);
                string user = reader.GetAttribute("user");
                double lat = double.Parse(reader.GetAttribute("lat"), CultureInfo.InvariantCulture);
                double lon = double.Parse(reader.GetAttribute("lon"), CultureInfo.InvariantCulture);
                reader.Read();
                
                Dictionary<string, string> tags = new Dictionary<string, string>();
                while (reader.NodeType == XmlNodeType.Element && reader.Name == "tag")
                {
                    //get the key and the value attributes of the tag
                    tags.Add(reader.GetAttribute("k"), reader.GetAttribute("v"));
                    reader.Read();
                }
                //test if the current node is an end node
                if (reader.NodeType == XmlNodeType.EndElement && reader.Name != "osm")
                    reader.Read();
                if (tags.Count == 0)
                    tags = OsmElement.EmptyTags;

                //returns a node with the given attributes
                return new Node(id, changeset, visible, timestamp, version, uid, user, tags, lat, lon);
            }
            else
            {
                reader.Skip();
                return null;
            }
        }
        catch (Exception exc)
        {
            Debug.LogWarning(exc.Message);
            reader.Skip();
            return null;
        }
    }

    private Way AssignWay(XmlReader reader)
    {
        try
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "way")
            {
                long id = long.Parse(reader.GetAttribute("id"), CultureInfo.InvariantCulture);
                long uid = long.Parse(reader.GetAttribute("uid"), CultureInfo.InvariantCulture);
                long changeset = long.Parse(reader.GetAttribute("changeset"), CultureInfo.InvariantCulture);
                bool visible = bool.Parse(reader.GetAttribute("visible"));
                DateTime timestamp = DateTime.Parse(reader.GetAttribute("timestamp"), CultureInfo.InvariantCulture);
                int version = int.Parse(reader.GetAttribute("version"), CultureInfo.InvariantCulture);
                string user = reader.GetAttribute("user");
                reader.Read();

                List<long> nodeIds = new List<long>();
                while (reader.NodeType == XmlNodeType.Element && reader.Name == "nd")
                {
                    nodeIds.Add(long.Parse(reader.GetAttribute("ref"), CultureInfo.InvariantCulture));
                    reader.Read();
                }

                Dictionary<string, string> tags = new Dictionary<string, string>();
                while (reader.NodeType == XmlNodeType.Element && reader.Name == "tag")
                {
                    //get the key and the value attributes of the tag
                    tags.Add(reader.GetAttribute("k"), reader.GetAttribute("v"));
                    reader.Read();
                }
                //test if the current node is an end node
                if (reader.NodeType == XmlNodeType.EndElement && reader.Name != "osm")
                    reader.Read();
                if (tags.Count == 0)
                    tags = OsmElement.EmptyTags;

                //returns a way with the given attributes
                return new Way(id, changeset, visible, timestamp, version, uid, user, tags, nodeIds.ToArray());
            }
            else
            {
                reader.Skip();
                return null;
            }
        }
        catch (Exception exc)
        {
            Debug.LogWarning(exc.Message);
            reader.Skip();
            return null;
        }
    }

    private Relation AssignRelation(XmlReader reader)
    {
        try
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "relation")
            {
                long id = long.Parse(reader.GetAttribute("id"), CultureInfo.InvariantCulture);
                long uid = long.Parse(reader.GetAttribute("uid"), CultureInfo.InvariantCulture);
                long changeset = long.Parse(reader.GetAttribute("changeset"), CultureInfo.InvariantCulture);
                bool visible = bool.Parse(reader.GetAttribute("visible"));
                DateTime timestamp = DateTime.Parse(reader.GetAttribute("timestamp"), CultureInfo.InvariantCulture);
                int version = int.Parse(reader.GetAttribute("version"), CultureInfo.InvariantCulture);
                string user = reader.GetAttribute("user");
                reader.Read();

                List<RelationMember> members = new List<RelationMember>();
                while (reader.NodeType == XmlNodeType.Element && reader.Name == "member")
                {
                    OsmGeoType geoType = (OsmGeoType)Convert(reader.GetAttribute("type"));
                    members.Add(new RelationMember(long.Parse(reader.GetAttribute("ref"), CultureInfo.InvariantCulture), reader.GetAttribute("role"), geoType));
                    reader.Read();
                }

                Dictionary<string, string> tags = new Dictionary<string, string>();
                while (reader.NodeType == XmlNodeType.Element && reader.Name == "tag")
                {
                    //get the key and the value attributes of the tag
                    tags.Add(reader.GetAttribute("k"), reader.GetAttribute("v"));
                    reader.Read();
                }
                //test if the current node is an end node
                if (reader.NodeType == XmlNodeType.EndElement && reader.Name != "osm")
                    reader.Read();
                if (tags.Count == 0)
                    tags = OsmElement.EmptyTags;

                //returns a relation with the given attributes
                return new Relation(id, changeset, visible, timestamp, version, uid, user, tags, members.ToArray());
            }
            else
            {
                reader.Skip();
                return null;
            }
        }
        catch (Exception exc)
        {
            Debug.LogWarning(exc.Message);
            reader.Skip();
            return null;
        }
    }

    private static OsmGeoType? Convert(string type)
    {
        return type switch
        {
            "node" => OsmGeoType.Node,
            "way" => OsmGeoType.Way,
            "relation" => OsmGeoType.Relation,
            _ => null,
        };
    }

}