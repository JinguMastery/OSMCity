using System;
using System.Collections.Generic;

public abstract class OsmElement
{

    protected OsmElement(long id, OsmGeoType type, long changeSetId, bool visible, DateTime timeStamp, int version, long userId, string userName, Dictionary<string, string> tags)
    {
        Id = id; Type = type; ChangeSetId = changeSetId; Visible = visible; TimeStamp = timeStamp; Version = version; UserId = userId; UserName = userName; Tags = tags;
    }

    public long? Id { get; set; }
    public OsmGeoType Type { get; protected set; }
    public Dictionary<string, string> Tags { get; set; }
    public long? ChangeSetId { get; set; }
    public bool? Visible { get; set; }
    public DateTime? TimeStamp { get; set; }
    public int? Version { get; set; }
    public long? UserId { get; set; }
    public string UserName { get; set; }

    public override string ToString()
    {
        return $"ID = {Id}, Timestamp = {TimeStamp}, Type = {Type}, User ID = {UserId}, Username = {UserName}, Version = {Version}, Visible = {Visible}, Changeset ID = {ChangeSetId}";
    }

}

public enum OsmGeoType
{
    Node = 0,
    Way = 1,
    Relation = 2
}

public class Node : OsmElement
{
    public Node(long id, long changeSetId, bool visible, DateTime timeStamp, int version, long userId, string userName, Dictionary<string, string> tags, double latitude, double longitude): 
        base(id, OsmGeoType.Node, changeSetId, visible, timeStamp, version, userId, userName, tags)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public override string ToString()
    {
        return base.ToString() + $", Latitude = {Latitude}, Longitude = {Longitude}";
    }

}

public class Way : OsmElement
{
    public Way(long id, long changeSetId, bool visible, DateTime timeStamp, int version, long userId, string userName, Dictionary<string, string> tags, long[] nodes) : 
        base(id, OsmGeoType.Way, changeSetId, visible, timeStamp, version, userId, userName, tags)
    {
        Nodes = nodes;
    }

    public long[] Nodes { get; set; }

    public override string ToString()
    {
        string infos = base.ToString() + "\n";
        foreach (var nodeId in Nodes)
        {
            infos += $"Node ID = {nodeId}\n";
        }
        return infos;
    }

}

public class Relation : OsmElement
{
    public Relation(long id, long changeSetId, bool visible, DateTime timeStamp, int version, long userId, string userName, Dictionary<string, string> tags, RelationMember[] members) :
        base(id, OsmGeoType.Relation, changeSetId, visible, timeStamp, version, userId, userName, tags)
    {
        Members = members;
    }

    public RelationMember[] Members { get; set; }

    public override string ToString()
    {
        string infos = base.ToString() + "\n";
        foreach (var member in Members)
        {
            infos += $"Member ID = {member.Id}, Member type = {member.Type}, Member role = {member.Role}\n";
        }
        return infos;
    }

}

public class RelationMember
{
    public RelationMember(long id, string role, OsmGeoType memberType)
    {
        Id = id;
        Role = role;
        Type = memberType;
    }

    public OsmGeoType Type { get; set; }
    public long Id { get; set; }
    public string Role { get; set; }
}


