using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UI;

public class Highway : CityObject
{

    [Header("Highway attributes")]
    public string highwayType = "road";
    public string crossingType = "uncontrolled";
    public string footwayType = "sidewalk";
    public string markings = "yes";
    public string crossingMarkings = "zebra";
    public string divider = "solid_line";

    [Tooltip("Width must be strictly positive")]
    [Min(0f)]
    [Delayed]
    public float highwayWidth = 3f;
    [Tooltip("Length must be strictly positive")]
    [Min(0f)]
    [Delayed]
    public float highwayLength = 3f;
    [Tooltip("Number of lanes must be greater or equal to 1")]
    [Min(1)]
    [Delayed]
    public int nLanes = 1;
    [Tooltip("Width of lanes must be strictly positive")]
    [Min(0f)]
    [Delayed]
    public float laneWidth = 1f; 

    private float prevHighwayWidth = 3f, prevHighwayLength = 3f, prevLaneWidth = 1f;
    private int prevNLanes = 1;
    private ComputeBuffer pathBuffer, distanceBuffer;
    private static MaterialPropertyBlock block;
    private Vector3[] positions;
    private const float TerrainClearance = 0.02f;
    //vertical separation per OSM 'layer' step (real bridges/tunnels) - large enough to read as a clear level change
    private const float LayerHeightStep = 0.5f;
    //vertical separation per importance TIER below. Tiers are only split where two types have actually been
    //observed to physically overlap rather than just touch at a shared endpoint node
    private const float RankHeightStep = 0.01f;
    private static readonly Dictionary<string, int> HighwayTypeRank = new Dictionary<string, int>
    {
        { "unclassified", 1 }, { "residential", 1 }, { "living_street", 1 }, { "road", 1 },
        { "service", 2 }, { "track", 2 }, { "bus_guideway", 2 }, { "busway", 2 }, { "escape", 2 },
        { "footway", 3 }, { "path", 3 }, { "pedestrian", 3 }, { "crossing", 3 }, { "bus_stop", 3 }, { "platform", 3 }, { "bridleway", 3 },
        { "cycleway", 4 },
        { "steps", 5 },
    };
    //anything not listed above (motorway, trunk, primary, secondary, raceway, ...) is a major arterial/other
    //vehicle road and stays at tier 0
    private const int DefaultHighwayTypeRank = 0;
    private float yOffset;

    public Material HighwayMaterial
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("material", out string strMaterial))
            {
                surface = strMaterial;
                Material mat = Resources.Load<Material>("Materials/" + strMaterial);
                if (mat != null)
                    material = mat;
            }
            return material;
        }
    }

    public float HighwayWidth
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("width", out string strWidth))
            {
                if (float.TryParse(strWidth, out float w))
                {
                    highwayWidth = w;
                }
            }
            return highwayWidth;
        }
    }

    public float HighwayLength
    {
        get
        {
            if (osmObj.Element.Type == OsmGeoType.Node)
            {
                return highwayLength;
            }
            else
            {
                if (positions != null && positions.Length > 1)
                {
                    float length = 0f;
                    for (int i = 0; i < positions.Length - 1; i++)
                    {
                        length += Vector3.Distance(positions[i], positions[i + 1]);
                    }
                    highwayLength = length;
                }
                return highwayLength;
            }
        }
    }

    public string Type
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("highway", out string type))
            {
                highwayType = type;
            }
            return highwayType;
        }
    }

    public string Crossing
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("crossing", out string type))
            {
                crossingType = type;
            }
            return crossingType;
        }
    }

    public string Footway
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("footway", out string type))
            {
                footwayType = type;
            }
            return footwayType;
        }
    }

    public string Markings
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("lane_markings", out string laneMarkings) | osmObj.Element.Tags.TryGetValue("markings", out string markings))
            {
                this.markings = laneMarkings ?? markings;
            }
            return this.markings;
        }
    }

    public string CrossingMarkings
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("crossing:markings", out string markings))
            {
                crossingMarkings = markings;
            }
            return crossingMarkings;
        }
    }

    public int NLanes
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("lanes", out string lanes))
            {
                if (int.TryParse(lanes, out int nLanes))
                {
                    this.nLanes = nLanes;
                }
            }
            return nLanes;
        }
    }

    public string Divider
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("divider", out string divider))
            {
                this.divider = divider;
            }
            return this.divider;
        }
    }

    public float LaneWidth
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("width:lanes", out string width))
            {
                if (float.TryParse(width, out float laneWidth))
                {
                    this.laneWidth = laneWidth;
                }
            }
            return laneWidth;
        }
    }

    //true if this way's node ring bounds a surface (a plaza, parking lot, racetrack...) rather than being a road/path centerline
    public bool IsArea
    {
        get
        {
            return osmObj.Element.Tags.TryGetValue("area", out string area) && area == "yes";
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        trueMiscellaneous = GetMiscellaneous();
        miscellaneous = trueMiscellaneous;
        trueNodePositions = GetNodePositionsText();
        nodePositions = trueNodePositions;
        yOffset = ComputeHeightTierOffset();
        _ = Markings; _ = Type; _ = Surface;

        if (surface != null)
        {
            switch (surface)
            {
                case "unpaved":
                case "ground":
                    material = Resources.Load<Material>("Materials/dirt");
                    break;
                case "paved":
                case "pavers":
                    material = Resources.Load<Material>("Materials/paving_stones");
                    break;
                case "compacted":
                    material = Resources.Load<Material>("Materials/gravel");
                    break;
                case "paving_stones:lanes":
                    surface = "paving_stones";
                    break;
                case "concrete:lanes":
                    surface = "concrete";
                    break;
                case "concrete:plates":
                    surface = "concrete_plates";
                    break;
                case "wood;planks":
                    surface = "wood_plank";
                    break;
                default:
                    break;
            }
            if (material == null)
                material = Resources.Load<Material>("Materials/" + surface);
        }

        if (surface == "asphalt" || HighwayMaterial == null)
        {
            if (surface == "asphalt" && markings == "no") {
                material = Resources.Load<Material>("Materials/asphalt");
            }
            else if (surface == "asphalt" && markings == "yes")
            {
                material = highwayType == "pedestrian" || highwayType == "crossing" ? Resources.Load<Material>("Materials/road2") : HighwayLoader.DefHighwayMat;
            }
            else
            {
                switch (highwayType)
                {
                    case "pedestrian":
                    case "crossing":
                        material = Resources.Load<Material>("Materials/road2");
                        break;
                    case "footway":
                    case "track":
                    case "service":
                    case "bus_guideway":
                    case "busway":
                    case "escape":
                        material ??= Resources.Load<Material>("Materials/asphalt");
                        break;
                    case "path":
                        material ??= Resources.Load<Material>("Materials/dirt");
                        break;
                    case "motorway":
                    case "trunk":
                    case "primary":
                    case "secondary":
                    case "tertiary":
                    case "unclassified":
                    case "residential":
                    case "living_street":
                    case "road":
                    case "raceway":
                        material = HighwayLoader.DefHighwayMat;
                        break;
                    default:
                        if (surface == "asphalt")
                            material = HighwayLoader.DefHighwayMat;
                        else
                            material ??= Resources.Load<Material>("Materials/dirt");
                        break;
                }
            }
        }

        // Add a material
        prevMat = material;
        prevSurface = surface;
        geometry = PrimitiveType.Plane;
        prevGeometry = geometry;

        if (osmObj.Element.Type == OsmGeoType.Relation)
        {
            //CreateMultiPolygon((Relation)osmObj.Element);
        }
        else
        {
            if (osmObj.Element.Type == OsmGeoType.Way)
            {
                Vector3[] pos = GetNodePositions(osmObj.SubNodes);
                if (pos != null)
                {
                    if (pos.Length > 2 && IsArea)
                        CreateAreaPolygon(pos);
                    else if (pos.Length > 1)
                        CreatePolygon(pos);
                    else if (pos.Length == 1)
                        AddPrimitive(pos[0]);
                }
            }
            else
            {
                AddPrimitive((Node)osmObj.Element);
            }
        }

        IsMeshCreated = true;
        prevNLanes = NLanes;
        prevLaneWidth = LaneWidth;
        prevHighwayWidth = HighwayWidth;
        prevHighwayLength = HighwayLength;
        // initialisation des champs
        _ = Length; _ = Width; _ = Amenity; _ = Source; _ = Elevation;
        // attributs relatifs aux routes
        _ = Crossing; _ = Footway; _ = CrossingMarkings; _ = Divider;
    }

    // Update is called once per frame
    void Update()
    {
        //teste la validité des entrées
        if (highwayWidth <= 0)
            highwayWidth = prevHighwayWidth;
        if (highwayLength <= 0)
            highwayLength = prevHighwayLength;
        if (nLanes < 1)
            nLanes = prevNLanes;
        if (laneWidth <= 0)
            laneWidth = prevLaneWidth;
        if (width <= 0)
            width = prevWidth;
        if (length <= 0)
            length = prevLength;
        if (nodePositions != trueNodePositions)
            nodePositions = trueNodePositions;
        if (miscellaneous != trueMiscellaneous)
            miscellaneous = trueMiscellaneous;

        //met à jour la position
        transform.position = Barycenter ?? Vector3.zero;

        //met à jour la texture si besoin
        if (material != null && prevMat != null && material.name != prevMat.name)
        {
            UpdateMaterial();
            prevMat = material;
            surface = material.name;
        }
        if (surface != prevSurface)
        {
            Material mat = Resources.Load<Material>("Materials/" + surface);
            if (mat != null)
            {
                material = mat;
                UpdateMaterial();
            }
            prevSurface = surface;
        }

        if (osmObj.Element.Type == OsmGeoType.Node)
        {
            if (highwayWidth != prevHighwayWidth || highwayLength != prevHighwayLength)
            {
                UpdatePrimitive((Node)osmObj.Element);
                prevHighwayWidth = highwayWidth;
                prevHighwayLength = highwayLength;
            }
        }
    }

    void OnDestroy()
    {
        pathBuffer?.Release();
        distanceBuffer?.Release();
        pathBuffer = null;
        distanceBuffer = null;
    }

    //deterministic vertical tier for this road, computed once from its own tags
    private float ComputeHeightTierOffset()
    {
        if (IsArea)
            return 0f;
        int layer = 0;
        if (osmObj.Element.Tags.TryGetValue("layer", out string layerStr))
            int.TryParse(layerStr, out layer);
        int rank = HighwayTypeRank.TryGetValue(Type, out int r) ? r : DefaultHighwayTypeRank;
        return layer * LayerHeightStep + rank * RankHeightStep;
    }

    private void AddPrimitive(Node node)
    {
        Vector3? pos = GetNodePosition(node);
        if (pos.HasValue)
            AddPrimitive(pos.Value);
    }

    private void AddPrimitive(Vector3 pos)
    {
        cityObj = GameObject.CreatePrimitive(geometry);
        cityObj.name = "ID = " + osmObj.Element.Id;
        cityObj.hideFlags = HideFlags.NotEditable;
        cityObj.transform.position = new Vector3(pos.x, pos.y + yOffset, pos.z);
        cityObj.transform.localScale = new Vector3(HighwayLength, 0.01f, HighwayWidth);
        IsVisible = true;
        // Add infos
        AddObjInfos();
    }

    private void UpdatePrimitive(Node node)
    {
        Vector3? pos = GetNodePosition(node);
        if (pos.HasValue)
        {
            cityObj.transform.position = new Vector3(pos.Value.x, pos.Value.y + yOffset, pos.Value.z);
            cityObj.transform.localScale = new Vector3(HighwayLength, 0.01f, HighwayWidth);
        }
    }

    private void CreatePolygon(Vector3[] pos)
    {
        positions = pos;

        List<Vector3[]> segments = SplitAtRepeatedPoints(pos);
        if (segments.Count <= 1)
        {
            // A plain closed loop (a roundabout, a racetrack outline...) with no self-touch never enters
            // the multi-segment branch below, but CreateShapeFromPolygon's ear-clipping still triangulates
            // it as one big polygon
            bool closed = pos.Length > 2 && pos[0] == pos[pos.Length - 1];
            if (closed)
            {
                mesh = BuildRibbonMesh(pos, HighwayWidth, true);
                cityObj = mesh.gameObject;
                IsVisible = true;
            }
            else
            {
                cityObj = new GameObject
                {
                    hideFlags = HideFlags.NotEditable
                };
                // Add a ProBuilderMesh component (ProBuilder mesh data is stored here)
                mesh = cityObj.AddComponent<ProBuilderMesh>();
                // Compute the node positions corresponding to the highway bounds
                Vector3[] bounds = ComputeHighwayBounds(pos, HighwayWidth);
                // Au lieu d'un seul long segment, découpe en sous-segments
                Vector3[] subDivisions = ComputeSubDivisions(bounds);
                // Create a mesh from the polygon shape
                ActionResult act = mesh.CreateShapeFromPolygon(subDivisions.ToList(), 0.01f, false);
                mesh.DuplicateAndFlip(mesh.faces.ToArray());
                IsVisible = act.ToBool();
            }
            cityObj.name = "ID = " + osmObj.Element.Id;
            cityObj.hideFlags = HideFlags.NotEditable;
        }
        else
        {
            // The path touches itself (e.g. a driveway/track that loops back to its own junction node). A
            // shared junction has no single "correct" offset direction - that concept only applies to a
            // smooth 2-segment bend - so forcing independently-built pieces to meet there exactly produces
            // either a gap (each computes its own direction) or an overlap (both forced to agree on one,
            // which is wrong for at least one of them if 3+ edges genuinely converge). Instead, every piece
            // touching a junction is trimmed back from it, and a small filled disc caps the seam - the disc
            // doesn't care how many ribbons converge there or at what angle, it just covers them all.
            HashSet<Vector3> junctions = FindJunctionPoints(pos);
            float capRadius = HighwayWidth / 2f;
            List<ProBuilderMesh> parts = new List<ProBuilderMesh>();
            bool isVisible = false;
            foreach (var segment in segments)
            {
                if (segment.Length < 2)
                    continue;
                Vector3[] trimmed = TrimAtJunctions(segment, junctions, capRadius);
                parts.Add(BuildRibbonMesh(trimmed, HighwayWidth, false));
                isVisible = true;
            }
            foreach (var junction in junctions)
            {
                parts.Add(BuildJunctionCapMesh(junction, capRadius));
                isVisible = true;
            }
            // Combine(meshes, meshTarget) requires meshTarget to be one of the meshes being merged, and
            // merges everything into it in place (it calls ToMesh()/Refresh() on the target itself) -
            // it returns null for fewer than 2 meshes, so only call it once there's actually something to merge.
            if (parts.Count == 0)
                mesh = new GameObject().AddComponent<ProBuilderMesh>();
            else if (parts.Count == 1)
                mesh = parts[0];
            else
            {
                mesh = parts[0];
                CombineMeshes.Combine(parts, mesh);
                for (int i = 1; i < parts.Count; i++)
                    Destroy(parts[i].gameObject);
            }
            cityObj = mesh.gameObject;
            cityObj.name = "ID = " + osmObj.Element.Id;
            cityObj.hideFlags = HideFlags.NotEditable;
            IsVisible = isVisible;
        }
        // cityObj is its own GameObject (not this component's), so the height tier has to be applied here
        cityObj.transform.position += new Vector3(0f, yOffset, 0f);
        // Add a mesh collider
        AddMeshCollider();
        // Add infos
        AddObjInfos();

        if (material != null && material.shader == HighwayLoader.DefHighwayMat.shader)
        {
            MeshRenderer renderer = cityObj.GetComponent<MeshRenderer>();
            // Create path buffer
            pathBuffer = new ComputeBuffer(pos.Length, sizeof(float) * 3);
            pathBuffer.SetData(pos);
            // Create cumulative distances buffer
            distanceBuffer = new ComputeBuffer(pos.Length, sizeof(float));
            float[] cumDist = new float[pos.Length];
            cumDist[0] = 0f;
            for (int i = 1; i < pos.Length; i++)
            {
                cumDist[i] = cumDist[i - 1] + Vector3.Distance(pos[i - 1], pos[i]);
            }
            distanceBuffer.SetData(cumDist);
            block ??= new MaterialPropertyBlock();
            block.Clear();
            block.SetBuffer("_PathPoints", pathBuffer);
            block.SetBuffer("_CumulativeDistances", distanceBuffer);
            block.SetInt("_PointCount", pos.Length);
            block.SetFloat("_RoadWidth", HighwayWidth);
            block.SetFloat("_RoadLength", cumDist[pos.Length - 1]);
            renderer.SetPropertyBlock(block);
        }
    }

    //builds a flat filled mesh from the way's own node ring, for ways that bound a surface (area=yes) rather than a road centerline
    private void CreateAreaPolygon(Vector3[] pos)
    {
        positions = pos;
        cityObj = new GameObject
        {
            name = "ID = " + osmObj.Element.Id,
            hideFlags = HideFlags.NotEditable
        };
        if (material != null && material.shader == HighwayLoader.DefHighwayMat.shader)
            material = Resources.Load<Material>("Materials/asphalt");
        mesh = cityObj.AddComponent<ProBuilderMesh>();
        ActionResult act = mesh.CreateShapeFromPolygon(pos.ToList(), 0.01f, false);
        mesh.DuplicateAndFlip(mesh.faces.ToArray());
        IsVisible = act.ToBool();
        // yOffset is always 0 here (ComputeHeightTierOffset returns 0 for IsArea)
        cityObj.transform.position += new Vector3(0f, yOffset, 0f);
        AddMeshCollider();
        AddObjInfos();
    }

    private void AddObjInfos()
    {
        // Update the texture and the color
        UpdateMaterial();
        // Add a canvas and text fields for node positions and miscellanous infos
        Text t = cityObj.AddComponent<Text>();
        t.text = GetNodePositionsText();
        if (osmObj.Loader.Main.hideMeshInHierarchy)
            cityObj.hideFlags = HideFlags.HideInHierarchy;
        else
            cityObj.transform.SetParent(((HighwayLoader)osmObj.Loader).HighwayMeshes.transform);
    }

    private Vector3[] ComputeHighwayBounds(Vector3[] pos, float width)
    {
        bool isClosed = pos.Length > 2 && pos[0] == pos[pos.Length - 1];
        Vector3[] bounds = new Vector3[pos.Length * 2];
        for (int i = 0; i < pos.Length; i++)
        {
            Vector3 dir;
            if (isClosed && (i == 0 || i == pos.Length - 1))
                dir = ((pos[1] - pos[i]).normalized + (pos[i] - pos[pos.Length - 2]).normalized).normalized;
            else if (i == 0)
                dir = (pos[i + 1] - pos[i]).normalized;
            else if (i == pos.Length - 1)
                dir = (pos[i] - pos[i - 1]).normalized;
            else
                dir = ((pos[i + 1] - pos[i]).normalized + (pos[i] - pos[i - 1]).normalized).normalized;
            Vector3 normal = new Vector3(-dir.z, 0, dir.x);
            bounds[i] = pos[i] + normal * width / 2f;
            bounds[bounds.Length - 1 - i] = pos[i] - normal * width / 2f;
        }
        return bounds;
    }

    private Vector3[] ComputeSubDivisions(Vector3[] bounds, float maxSegmentLength = 50f)
    {
        List<Vector3> subDivisions = new List<Vector3>();
        for (int i = 0; i < bounds.Length - 1; i++)
        {
            Vector3 start = bounds[i];
            Vector3 end = bounds[i + 1];
            float segmentLength = Vector3.Distance(start, end);
            int numSubSegments = Mathf.CeilToInt(segmentLength / maxSegmentLength);
            for (int j = 0; j < numSubSegments; j++)
            {
                float t = (float)j / numSubSegments;
                subDivisions.Add(Vector3.Lerp(start, end, t));
            }
        }
        subDivisions.Add(bounds[bounds.Length - 1]);
        return subDivisions.ToArray();
    }

    //splits a path at every point it revisits (e.g. a driveway/track that loops back to its own junction
    //node), so each returned sub-path is simple and safe to offset into a ribbon without self-intersecting
    private List<Vector3[]> SplitAtRepeatedPoints(Vector3[] pos)
    {
        List<Vector3[]> segments = new List<Vector3[]>();
        Dictionary<Vector3, int> firstIndex = new Dictionary<Vector3, int>();
        int segmentStart = 0;
        for (int i = 0; i < pos.Length; i++)
        {
            if (firstIndex.TryGetValue(pos[i], out int prevIndex))
            {
                if (prevIndex > segmentStart)
                    segments.Add(pos.Skip(segmentStart).Take(prevIndex - segmentStart + 1).ToArray());
                if (i > prevIndex)
                    segments.Add(pos.Skip(prevIndex).Take(i - prevIndex + 1).ToArray());
                segmentStart = i;
                firstIndex.Clear();
            }
            firstIndex[pos[i]] = i;
        }
        if (segmentStart < pos.Length - 1)
            segments.Add(pos.Skip(segmentStart).Take(pos.Length - segmentStart).ToArray());
        return segments;
    }

    //points the path visits more than once - a self-touching path's own junction(s), where a tail meets a
    //loop or similar. Ribbons touching one of these get trimmed back and covered by a cap mesh instead of
    //trying to make their edges meet exactly (see CreatePolygon's multi-segment branch for why).
    private HashSet<Vector3> FindJunctionPoints(Vector3[] pos)
    {
        Dictionary<Vector3, int> counts = new Dictionary<Vector3, int>();
        foreach (var p in pos)
            counts[p] = counts.TryGetValue(p, out int c) ? c + 1 : 1;
        HashSet<Vector3> junctions = new HashSet<Vector3>();
        foreach (var kv in counts)
            if (kv.Value > 1)
                junctions.Add(kv.Key);
        return junctions;
    }

    //pulls each endpoint of segment that is a junction point back toward its own neighbor by distance,
    //so the ribbon built from the result stops short of the junction instead of reaching exactly into it
    //(leaving room for a cap mesh to cover the seam). A segment that was closed (its single junction point
    //is both segment[0] and segment[last]) ends up with two distinct, separated endpoints - it's opened up,
    //which is intentional: BuildRibbonMesh(..., closed: false) is used for every trimmed segment, closed or not.
    private Vector3[] TrimAtJunctions(Vector3[] segment, HashSet<Vector3> junctions, float distance)
    {
        Vector3[] trimmed = (Vector3[])segment.Clone();
        int last = segment.Length - 1;
        if (segment.Length >= 2 && junctions.Contains(segment[0]))
            trimmed[0] = Vector3.MoveTowards(segment[0], segment[1], distance);
        if (segment.Length >= 2 && junctions.Contains(segment[last]))
            trimmed[last] = Vector3.MoveTowards(segment[last], segment[last - 1], distance);
        return trimmed;
    }

    //inserts intermediate points along any segment longer than maxSegmentLength, so BuildRibbonMesh never
    //produces a single quad edge long enough to trip PhysX's large-triangle mesh collider warning. closed
    //also subdivides the wraparound segment back from the last point to pos[0], and re-appends the closing
    //duplicate point, matching pos's own closed-ring convention (pos[0] == pos[pos.Length-1]).
    private Vector3[] SubdivideLongSegments(Vector3[] pos, bool closed, float maxSegmentLength = 50f)
    {
        int n = closed ? pos.Length - 1 : pos.Length;
        int segCount = closed ? n : n - 1;
        List<Vector3> result = new List<Vector3>();
        for (int i = 0; i < segCount; i++)
        {
            Vector3 start = pos[i];
            Vector3 end = pos[(i + 1) % n];
            result.Add(start);
            int extra = Mathf.FloorToInt(Vector3.Distance(start, end) / maxSegmentLength);
            for (int j = 1; j <= extra; j++)
                result.Add(Vector3.Lerp(start, end, (float)j / (extra + 1)));
        }
        result.Add(closed ? result[0] : pos[n - 1]);
        return result.ToArray();
    }

    //builds a ribbon as a strip of independent convex quads instead of one big polygon, avoiding
    //CreateShapeFromPolygon's ear-clipping (which fills concave/annulus shapes solid, and can locally
    //self-intersect at sharp turns - visible as tonal/gradient banding - on a long or complex path).
    //closed wraps the last quad back to the first point (pos[0] == pos[pos.Length-1] is expected, and its
    //duplicate final point is dropped); open builds one fewer quad than points, with one-sided end directions.
    private ProBuilderMesh BuildRibbonMesh(Vector3[] pos, float width, bool closed)
    {
        // A long straight stretch between two widely-spaced OSM nodes would otherwise become a single quad
        // edge long enough to trip PhysX's large-triangle mesh collider warning
        pos = SubdivideLongSegments(pos, closed);
        int n = closed ? pos.Length - 1 : pos.Length;
        Vector3[] outer = new Vector3[n];
        Vector3[] inner = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 dir;
            if (closed)
            {
                int prevIdx = (i - 1 + n) % n;
                int nextIdx = (i + 1) % n;
                dir = ((pos[nextIdx] - pos[i]).normalized + (pos[i] - pos[prevIdx]).normalized).normalized;
            }
            else if (i == 0)
                dir = (pos[i + 1] - pos[i]).normalized;
            else if (i == n - 1)
                dir = (pos[i] - pos[i - 1]).normalized;
            else
                dir = ((pos[i + 1] - pos[i]).normalized + (pos[i] - pos[i - 1]).normalized).normalized;
            Vector3 normal = new Vector3(-dir.z, 0, dir.x);
            Vector3 lifted = pos[i] + Vector3.up * TerrainClearance;
            outer[i] = lifted + normal * width / 2f;
            inner[i] = lifted - normal * width / 2f;
        }

        List<Vector3> vertices = new List<Vector3>();
        List<Face> faces = new List<Face>();
        int numQuads = closed ? n : n - 1;
        for (int i = 0; i < numQuads; i++)
        {
            int next = closed ? (i + 1) % n : i + 1;
            int b = vertices.Count;
            vertices.Add(outer[i]);
            vertices.Add(outer[next]);
            vertices.Add(inner[next]);
            vertices.Add(inner[i]);
            faces.Add(new Face(new int[] { b, b + 1, b + 2, b, b + 2, b + 3 }));
        }

        ProBuilderMesh ribbon = new GameObject().AddComponent<ProBuilderMesh>();
        ribbon.RebuildWithPositionsAndFaces(vertices, faces);
        ribbon.DuplicateAndFlip(ribbon.faces.ToArray());
        ribbon.ToMesh();
        ribbon.Refresh();
        return ribbon;
    }

    //fills the gap left by TrimAtJunctions with a simple disc centered on the junction - rotationally
    //symmetric so it covers any number of converging ribbons at any angle without needing per-junction
    //blending logic, unlike trying to make the ribbons' own edges meet exactly
    private ProBuilderMesh BuildJunctionCapMesh(Vector3 center, float radius, int segments = 16)
    {
        Vector3 liftedCenter = center + Vector3.up * TerrainClearance;
        List<Vector3> vertices = new List<Vector3> { liftedCenter };
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            vertices.Add(liftedCenter + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius));
        }

        List<Face> faces = new List<Face>();
        for (int i = 0; i < segments; i++)
        {
            int a = 1 + i;
            int b = 1 + (i + 1) % segments;
            faces.Add(new Face(new int[] { 0, a, b }));
        }

        ProBuilderMesh cap = new GameObject().AddComponent<ProBuilderMesh>();
        cap.RebuildWithPositionsAndFaces(vertices, faces);
        cap.DuplicateAndFlip(cap.faces.ToArray());
        cap.ToMesh();
        cap.Refresh();
        return cap;
    }

}
