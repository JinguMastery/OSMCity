using UnityEngine;
using System.Collections.Generic; //Needed for Lists
using System.Xml; //Needed for XML functionality
using System;
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
    public CityObject[] CityObjects => cityObjs.ToArray();


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
            b = new Boundaries(lats.Min(), lats.Max(), lons.Min(), lons.Max());
            bounds = b;
            return b;
        }
    }

    protected void LoadXML()
    {
        try
        {
            if (allPath != null && allPath.Length > 0)
            {
                allReader = XmlReader.Create(allPath);
                ReadXML(allReader);
            }
            if (tagsPath != null && tagsPath.Length > 0)
            {
                tagsReader = XmlReader.Create(tagsPath);
                ReadTagXML(tagsReader);
            }
            if (nodesPath != null && nodesPath.Length > 0)
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
            if (waysPath != null && waysPath.Length > 0)
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
            if (relationsPath != null && relationsPath.Length > 0)
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