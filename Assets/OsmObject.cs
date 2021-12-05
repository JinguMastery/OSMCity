using System.Collections.Generic;
using System.Linq;

public abstract class OsmObject
{

    private Boundaries? bounds;
    private readonly List<Node> directSubNodes = new List<Node>();      //les sous-noeuds directs d'une relation ou sous-relation, faisant partie de leurs membres

    public Loader Loader { get; protected set; }
    public OsmElement Element { get; protected set; }          //the OSM element associated with a given OSM object of any type
    public OsmElement[] SubElements { get; private set; }
    public Node[] SubNodes { get; private set; }
    public Way[] SubWays { get; private set; }
    public Relation[] SubRelations { get; private set; }
    public Node[] DirectSubNodes => directSubNodes.ToArray();

    public Boundaries Bounds
    {
        get
        {
            if (bounds != null)
                return (Boundaries)bounds;
            Boundaries b;
            if (Element.Type == OsmGeoType.Node)
            {
                Node node = (Node)Element;
                b = new Boundaries(node.Latitude ?? 0, node.Latitude ?? 0, node.Longitude ?? 0, node.Longitude ?? 0);
            }
            else
            {
                var lats = from node in SubNodes
                           where node.Latitude != null
                           select node.Latitude;
                var lons = from node in SubNodes
                           where node.Longitude != null
                           select node.Longitude;
                b = new Boundaries(lats.Min() ?? 0, lats.Max() ?? 0, lons.Min() ?? 0, lons.Max() ?? 0);
            }
            bounds = b;
            return b;
        }
    }

    protected void SetSubElements()
    {
        if (Element.Type == OsmGeoType.Node)
        {
            SubElements = null;
            SubNodes = null;
            SubWays = null;
            SubRelations = null;
        }
        else
        {
            if (Element.Type == OsmGeoType.Way)
            {
                SubNodes = GetSubNodes((Way)Element).ToArray();
                SubElements = SubNodes;
                SubWays = null;
                SubRelations = null;
            }
            else
            {
                List<OsmElement> elems = new List<OsmElement>();
                List<Node> nodes = GetSubNodes((Relation)Element);
                List<Way> ways = GetSubWays((Relation)Element);
                List<Relation> relations = GetSubRelations((Relation)Element);
                SubNodes = nodes.ToArray();
                SubWays = ways.ToArray();
                SubRelations = relations.ToArray();
                elems.AddRange(nodes);
                elems.AddRange(ways);
                elems.AddRange(relations);
                SubElements = elems.ToArray();
            }
        }
    }

    private List<Node> GetSubNodes(Way way)
    {
        List<Node> nodes = new List<Node>();
        foreach (var nodeId in way.Nodes) {
            var filtered = from node in Loader.SubNodes
                           where node.Id == nodeId
                           select node;
            if (filtered.Any())
            {
                nodes.Add(filtered.First());
            }
        }
        return nodes;
    }

    private List<Node> GetSubNodes(Relation relation)
    {
        List<Node> nodes = new List<Node>();
        foreach (var member in relation.Members)
        {
            if (member.Type == OsmGeoType.Node)
            {
                var filtered = from node in Loader.SubNodes
                               where node.Id == member.Id
                               select node;
                if (filtered.Any())
                {
                    nodes.Add(filtered.First());
                    directSubNodes.Add(filtered.First());
                }
            }
            else
            {
                if (member.Type == OsmGeoType.Way)
                {
                    var filtered = from way in Loader.SubWays
                                   where way.Id == member.Id
                                   select way;
                    if (filtered.Any())
                    {
                        List<Node> subNodes = GetSubNodes(filtered.First());
                        nodes.AddRange(subNodes);
                    }
                }
                else
                {
                    var filtered = from subRelation in Loader.SubRelations
                                    where subRelation.Id == member.Id
                                    select subRelation;
                    if (filtered.Any())
                    {
                        List<Node> subNodes = GetSubNodes(filtered.First());
                        nodes.AddRange(subNodes);
                    }
                }
            }
        }
        return nodes;
    }

    private List<Way> GetSubWays(Relation relation)
    {
        List<Way> ways = new List<Way>();
        foreach (var member in relation.Members)
        {
            if (member.Type == OsmGeoType.Way)
            {
                var filtered = from way in Loader.SubWays
                               where way.Id == member.Id
                               select way;
                if (filtered.Any())
                {
                    ways.Add(filtered.First());
                }
            }
            else
            {
                if (member.Type == OsmGeoType.Relation)
                {
                    var filtered = from subRelation in Loader.SubRelations
                                   where subRelation.Id == member.Id
                                   select subRelation;
                    if (filtered.Any())
                    {
                        List<Way> subWays = GetSubWays(filtered.First());
                        ways.AddRange(subWays);
                    }
                }
            }
        }
        return ways;
    }

    private List<Relation> GetSubRelations(Relation relation)
    {
        List<Relation> relations = new List<Relation>();
        foreach (var member in relation.Members)
        {
            if (member.Type == OsmGeoType.Relation)
            {
                var filtered = from subRelation in Loader.SubRelations
                               where subRelation.Id == member.Id
                               select subRelation;
                if (filtered.Any())
                {
                    relations.Add(filtered.First());
                    List<Relation> subRelations = GetSubRelations(filtered.First());
                    relations.AddRange(subRelations);
                }
            }
        }
        return relations;
    }

}
