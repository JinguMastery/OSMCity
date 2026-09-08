using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UI;
using UnityEngine.UIElements;
using static UnityEditor.FilePathAttribute;

public class Highway : CityObject
{

    [Header("Highway attributes")]
    public string highwayType = "road";
    public string crossingType = "uncontrolled";
    public string footwayType = "sidewalk";
    public string markings = "yes";
    public string crossingMarkings = "zebra";
    public string divider = "solid_line";
    public string trafficSignalsDirection = "forward";

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
    //every "post" prop from this asset pack (LampPost_A, StreetSign_G, ...) shares the same source FBX and
    //is authored with its pole running along local Z; each one's root is baked with this exact rotation so
    //the pole stands up along world Y (confirmed identical in both prefabs' own m_LocalRotation). Instantiate()
    //overrides that baked root rotation, so it has to be re-applied explicitly and composed with the yaw
    //below, or the post ends up lying on its side. If a future prop from a different pack needs a different
    //fix, give it its own constant rather than assuming this one still applies.
    private static readonly Quaternion PostUprightCorrection = Quaternion.Euler(-90f, 0f, 0f);
    //every OSM highway=X value that gets its own prefab instead of a generated road-mesh primitive, along
    //with the Hierarchy-search-friendly name prefix used for it (see SpawnEmbeddedPointProps and the
    //Hierarchy-window search box). Add a new point prop type here - not by adding another switch case -
    //and it's automatically picked up by both the isolated-node path below and the embedded-in-way scan.
    private static readonly Dictionary<string, (string prefabPath, string namePrefix)> PointPropByType = new Dictionary<string, (string, string)>
    {
        { "traffic_signals", ("Prefabs/LampPost_A", "TrafficSignal ") },
        { "bus_stop", ("Prefabs/StreetSign_G", "BusStop ") },
        { "give_way", ("Prefabs/StreetSign_F", "GiveWay ") },
        { "stop", ("Prefabs/StreetSign_C", "Stop ") },
        { "street_lamp", ("Prefabs/LampPost_J", "StreetLamp ") },
        { "crossing", ("Prefabs/Road_Crosswalk", "Crossing ") }
    };

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

    //orientation of a traffic_signals node relative to its way's own digitisation order ("forward"/"backward").
    //traffic_signals:direction is the specific OSM key for this; the generic direction key is used as a fallback
    //when it isn't set
    public string TrafficSignalsDirection
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("traffic_signals:direction", out string dir) || osmObj.Element.Tags.TryGetValue("direction", out dir))
            {
                trafficSignalsDirection = dir;
            }
            return trafficSignalsDirection;
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
        // set once, not touched again: children of this GameObject (the road mesh, point props)
        // are parented with worldPositionStays, so this must stay fixed after they're attached
        transform.position = Barycenter ?? Vector3.zero;

        trueMiscellaneous = GetMiscellaneous();
        miscellaneous = trueMiscellaneous;
        trueNodePositions = GetNodePositionsText();
        nodePositions = trueNodePositions;
        yOffset = ComputeHeightTierOffset();
        _ = Markings; _ = Type; _ = Surface;

        CheckSurface();

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
                        AddPrimitive(pos[0], osmObj.SubNodes[0]);
                }
                // a point-prop node (traffic_signals, bus_stop, ...) shared with this way's own node list
                // never gets its own Highway/OsmObject (HighwayLoader only turns unvisited top-level nodes
                // into one) - it has to be found and spawned here instead, alongside the way's own road mesh
                SpawnEmbeddedPointProps();
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
        _ = Crossing; _ = Footway; _ = CrossingMarkings; _ = Divider; _ = TrafficSignalsDirection;
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

    private void CheckSurface()
    {
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
            if (surface == "asphalt" && markings == "no")
                material = Resources.Load<Material>("Materials/asphalt");
            else if (surface == "asphalt" && markings == "yes")
                material = highwayType == "pedestrian" || highwayType == "crossing" ? Resources.Load<Material>("Materials/crosswalk") : HighwayLoader.DefHighwayMat;
            else
            {
                switch (highwayType)
                {
                    case "pedestrian":
                    case "crossing":
                        material = Resources.Load<Material>("Materials/crosswalk");
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
            AddPrimitive(pos.Value, node);
    }

    private void AddPrimitive(Vector3 pos, Node node = null)
    {
        bool isPointProp = PointPropByType.TryGetValue(highwayType, out var propInfo);
        if (isPointProp)
        {
            GameObject prefab = Resources.Load<GameObject>(propInfo.prefabPath);
            if (highwayType == "crossing")
            {
                // a standalone crossing node has no "own way" to exclude - it isn't shared with any way's
                // node list at all - so this always searches every nearby segment
                cityObj = Instantiate(prefab, pos, Quaternion.identity);
                if (node != null)
                    PositionCrossing(cityObj, pos, null);
            }
            else
            {
                // this node comes from the else-branch below (osmObj.Element itself, an entry of Loader.Nodes)
                // or the pos.Length == 1 degenerate way case above - either way it isn't a node shared with a
                // normal multi-node way, so there's no way-order tangent to compute here; face/clear whichever
                // nearby road segment is actually closest instead
                Quaternion rotation = Quaternion.identity;
                (Vector3 a, Vector3 b, long wayId, float halfWidth)? nearest = null;
                if (node != null)
                {
                    nearest = FindNearestSegment(pos);
                    Vector3 dir = nearest.HasValue ? DirectionAwayFromSegment(pos, nearest.Value.a, nearest.Value.b) : Vector3.forward;
                    dir = ApplyPrefabFacingQuirk(highwayType, dir);
                    rotation = Quaternion.LookRotation(dir, Vector3.up) * PostUprightCorrection;
                }
                cityObj = Instantiate(prefab, pos, rotation);
                if (nearest.HasValue)
                {
                    // the node itself isn't part of any way, but it stands beside whichever road segment is
                    // nearest (found above) - if a DIFFERENT, crossing road also passes close enough to overlap
                    // it here, slide it along that nearest road's own tangent, clear of the crossing one, same
                    // as an embedded node at a junction (see ResolveCrossWayOverlap)
                    Vector3 tangent = (nearest.Value.b - nearest.Value.a).normalized;
                    cityObj.transform.position = ResolveCrossWayOverlap(cityObj, pos, tangent, nearest.Value.wayId);
                }
            }
            // prefixed (per type) so every instance of a point prop - however many, whatever their OSM id - can
            // be found at once with a plain Hierarchy window search, instead of having to know each id up front
            cityObj.name = propInfo.namePrefix + "ID = " + osmObj.Element.Id;
            SetHideFlagsRecursive(cityObj, HideFlags.NotEditable);
            IsVisible = true;
            // Add infos - not the road-surface material (already correct on the prefab), and no debug Text
            // component either (it would force this Transform into a RectTransform, wrecking the rotation)
            AddObjInfos(applyMaterial: false, addDebugText: false);
            Debug.Log("Added point prop as primitive : " + highwayType);
        }
        else
        {
            Debug.Log("Highway type not found in PointPropByType: " + highwayType);
        }
    }

    //applies flags to root and every descendant - a prefab like LampPost_A carries its own child parts
    //(light fixtures), which don't inherit the root's hideFlags on their own
    private static void SetHideFlagsRecursive(GameObject root, HideFlags flags)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.hideFlags = flags;
    }

    //scans this way's own nodes (already resolved in osmObj.SubNodes) for any tagged with a highway value
    //in PointPropByType (traffic_signals, bus_stop, ...) and spawns the matching prop for each - this is the
    //only place such a node ever gets instantiated, since HighwayLoader never creates a separate
    //Highway/OsmObject for a node shared with a way
    private void SpawnEmbeddedPointProps()
    {
        HighwayLoader loader = (HighwayLoader)osmObj.Loader;
        foreach (Node subNode in osmObj.SubNodes)
        {
            if (subNode.Tags.TryGetValue("highway", out string subType) && PointPropByType.TryGetValue(subType, out var propInfo))
            {
                // a node sitting exactly at a junction is shared by every way that meets there, and each of
                // those ways runs this same scan - only the first one to claim this id spawns it
                if (!loader.TryMarkPointFeatureSpawned(subNode.Id.Value))
                    continue;
                Vector3? subPos = GetNodePosition(subNode);
                if (subPos.HasValue)
                {
                    Vector3? wayDir = ComputeWayDirectionAtNode(osmObj.SubNodes, subNode.Id.Value, subPos.Value);
                    bool backward = IsBackwardDirection(subNode);
                    Quaternion rotation = BuildPostRotation(subType, wayDir, backward);
                    SpawnPointProp(subNode, subPos.Value, wayDir, backward, osmObj.Element.Id ?? -1, rotation, propInfo.prefabPath, propInfo.namePrefix, subType);
                }
            }
            else if (subNode.Tags.ContainsKey("highway"))
            {
                Debug.Log("No point prop for highway type: " + subType);
            }
        }
    }

    //centerlinePos is the raw node position (still on the road's centerline); wayDir/backward pick the
    //border side (see PerpendicularToRoad) and ownWayId is this node's own way, excluded from the cross-way
    //overlap check. The border offset, the post's own mesh half-extent, AND the cross-way clearance push all
    //move the post along one of only two axes (across its own road, or along its own road) - computing the
    //final candidate position FIRST and resolving overlap against THAT (rather than resolving overlap, then
    //separately nudging out by the mesh's own half-extent afterwards) matters: an offset applied after the
    //overlap check can walk the post right back into whatever crossing road the check just cleared it from
    //
    //a "crossing" node is the one exception to all of that: unlike a post/sign, its prefab (the zebra
    //stripes) is meant to span the road, not stand beside it - so it keeps the rotation (still perpendicular
    //to the road, via the same BuildPostRotation the caller already computed, so the stripes run across the
    //road) but skips the border push entirely and stays centered on the node's own position, just lifted
    //clear of the road surface underneath it
    private void SpawnPointProp(Node node, Vector3 centerlinePos, Vector3? wayDir, bool backward, long ownWayId, Quaternion rotation, string prefabPath, string namePrefix, string propType)
    {
        GameObject prefab = Resources.Load<GameObject>(prefabPath);
        GameObject propObj = Instantiate(prefab, centerlinePos, rotation);
        if (propType == "crossing")
        {
            PositionCrossing(propObj, centerlinePos, ownWayId);
        }
        else if (wayDir.HasValue)
        {
            Vector3 perp = PerpendicularToRoad(wayDir.Value, backward);
            float meshHalfExtent = HalfExtentAlongDirection(propObj, perp);
            Vector3 borderPos = centerlinePos + perp * (HighwayWidth / 2f + meshHalfExtent);
            propObj.transform.position = ResolveCrossWayOverlap(propObj, borderPos, wayDir, ownWayId);
        }
        propObj.name = namePrefix + "ID = " + node.Id;
        if (osmObj.Loader.Main.hideMeshInHierarchy)
            SetHideFlagsRecursive(propObj, HideFlags.HideInHierarchy);
        else
            SetHideFlagsRecursive(propObj, HideFlags.NotEditable);
        propObj.transform.SetParent(transform);
        Debug.Log("Added point prop as way prefab : " + namePrefix);
    }

    //every one of these posts stands at the roadside facing across the road, not along it - so its facing
    //direction is the road tangent rotated 90 degrees around the vertical axis, not the tangent itself.
    //TrafficSignalsDirection (falling back to the generic "direction" tag for props other than traffic
    //signals - see IsBackwardDirection) - "forward", the default, or "backward" - picks which of the two
    //perpendiculars: forward keeps the post on the right-hand side of the road relative to the way's own
    //digitisation direction (real-world convention for a post facing traffic travelling that way); backward
    //mirrors it to the opposite side/facing, for a post facing oncoming traffic
    private Quaternion BuildPostRotation(string propType, Vector3? wayDir, bool backward)
    {
        Vector3 dir = PerpendicularToRoad(wayDir ?? Vector3.forward, backward);
        dir = ApplyPrefabFacingQuirk(propType, dir);
        return Quaternion.LookRotation(dir, Vector3.up) * PostUprightCorrection;
    }

    //every prop in PointPropByType except street_lamp is modelled so that, once PostUprightCorrection has
    //stood its pole up, the mesh's own visible face ends up aligned with local Y - which LookRotation(dir)
    //sends to world -dir (see PerpendicularToRoad's comment), so passing the across-the-road direction as
    //dir makes that face point at the road. LampPost_J (street_lamp) is built differently: its "Spotlight"
    //child sits at a large local X offset with near-zero Y (confirmed in the prefab itself) - i.e. its arm
    //bends out along local X, not Y, which LookRotation sends to world Cross(up, dir) instead of -dir - a
    //quarter turn off from every other prop in the pack. Rotating dir itself by the same 90 degrees before
    //LookRotation compensates, so the same "dir = across the road" input still ends up lighting the road
    //instead of running along it
    private static Vector3 ApplyPrefabFacingQuirk(string propType, Vector3 dir)
    {
        return propType == "street_lamp" ? Vector3.Cross(Vector3.up, dir) : dir;
    }

    //a post stands on the side of the road that matches the side of the road ITS TRAFFIC drives on - not
    //the side relative to the post's own facing direction. A post facing dir controls traffic moving toward
    //it, i.e. travelling in -dir, so its side is Cross(up, -dir), the mirror image of Cross(up, dir).
    //backward flips which of dir/-dir is being faced, which in turn flips which side this resolves to -
    //confirmed against several traffic-signal nodes in the Liechtenstein dataset: nodes tagged
    //traffic_signals:direction=backward matched this convention, forward/untagged nodes needed the mirror
    private static Vector3 PerpendicularToRoad(Vector3 dir, bool backward)
    {
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        return backward ? right : -right;
    }

    //combined world-space bounds of every Renderer under go (a prefab can carry separate child parts, e.g. a
    //lamp's light fixture) - false if it has none
    private static bool TryGetRendererBounds(GameObject go, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>())
        {
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
                bounds.Encapsulate(renderer.bounds);
        }
        return hasBounds;
    }

    //half of this instantiated prop's own world-space extent along dir - the standard support-distance
    //formula for an axis-aligned box (Renderer.bounds) projected onto an arbitrary direction. Used to push a
    //post that's already sitting exactly on the road border out by its own thickness, so its near face
    //touches the border instead of its center - which would leave half the mesh buried in the road
    private static float HalfExtentAlongDirection(GameObject go, Vector3 dir)
    {
        if (!TryGetRendererBounds(go, out Bounds bounds))
            return 0f;
        Vector3 extents = bounds.extents;
        return extents.x * Mathf.Abs(dir.x) + extents.y * Mathf.Abs(dir.y) + extents.z * Mathf.Abs(dir.z);
    }

    //uniformly scales go so the longer of its own (already-rotated) horizontal world extents - X and Z, the
    //footprint on the ground, not Y which is thickness after PostUprightCorrection - equals targetWidth. The
    //crossing prefab's zebra-stripe mesh has some fixed width baked in from the source asset, unrelated to
    //any actual road it gets placed on; this resizes it (proportionally, so the stripe pitch isn't distorted)
    //to match whichever road segment it's crossing. Must run before GroundLift, since that reads bounds too
    //and needs them to reflect the final scale, not the prefab's own default one.
    //go's rotation at call time must not include any yaw away from a world-axis-aligned heading - Renderer.
    //bounds is a world-space AABB, and yawing a rectangle to an arbitrary angle inflates that AABB well
    //beyond the rectangle's true footprint (e.g. root-2x too wide on both axes at a 45-degree yaw), which
    //would read as a longer "longest side" than the mesh actually has and undersize it. PostUprightCorrection
    //alone (see PositionCrossing) is safe - a 90-degree-multiple rotation never inflates an AABB - any actual
    //road-heading yaw has to be applied after this call instead, since uniform scale is unaffected by it
    private static void ScaleLongestSideTo(GameObject go, float targetWidth)
    {
        if (targetWidth <= 0f || !TryGetRendererBounds(go, out Bounds bounds))
            return;
        float longestSide = Mathf.Max(bounds.size.x, bounds.size.z);
        if (longestSide <= 0f)
            return;
        go.transform.localScale *= targetWidth / longestSide;
    }

    //how far to raise go's pivot so the BOTTOM of its combined renderer bounds sits at groundY, plus a small
    //TerrainClearance margin above it to avoid z-fighting with the road surface underneath. Unlike
    //HalfExtentAlongDirection, this doesn't assume the pivot sits at the bounds' center - a flat constant
    //offset landed wrong regardless of its size because the crossing prefab's pivot isn't at its base, so
    //this reads the actual (already-rotated) world bounds instead of guessing a fixed lift
    private static float GroundLift(GameObject go, float groundY)
    {
        if (!TryGetRendererBounds(go, out Bounds bounds))
            return 0f;
        return groundY + TerrainClearance - bounds.min.y;
    }

    //a node placed exactly at a multi-way junction is claimed and spawned by only the first way that reaches
    //it (see TryMarkPointFeatureSpawned), so its position/rotation always come from that one way's own
    //tangent - but pushing the post sideways onto its own road's border (SpawnPointProp) can still land it
    //inside a DIFFERENT way's road surface if that other way also passes through (or very near) the same
    //junction. When that happens, this slides the post back along its own way's tangent - away from
    //whichever direction increases clearance - until it's just clear of the other road's edge, ending up at
    //the corner of the intersection rather than buried in the crossing road.
    //a real intersection often has more than one crossing road converging on the same point, so clearing one
    //can walk the post straight into another - this re-checks every nearby different-way segment against the
    //post's current (possibly already-moved) position and keeps pushing until none of them overlap any more,
    //rather than resolving each conflict once and assuming that settles it.
    //propObj must already be instantiated (its rotation fixed) so its own mesh half-extent toward each
    //crossing segment can be measured - clearing just the post's pivot point still leaves half its own mesh
    //poking into the crossing road's surface, the same problem the border-offset mesh push (SpawnPointProp)
    //solves for the post's own road
    private Vector3 ResolveCrossWayOverlap(GameObject propObj, Vector3 pos, Vector3? wayDir, long ownWayId)
    {
        if (!wayDir.HasValue)
            return pos;
        HighwayLoader loader = (HighwayLoader)osmObj.Loader;
        Vector3 dir = wayDir.Value;
        const int maxIterations = 8;
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            bool overlapped = false;
            foreach (var (a, b, wayId, halfWidth) in loader.GetNearbySegments(pos))
            {
                if (wayId == ownWayId)
                    continue;
                Vector3 ab = b - a;
                float lenSqr = ab.sqrMagnitude;
                if (lenSqr <= 0f)
                    continue;
                float t = Mathf.Clamp01(Vector3.Dot(pos - a, ab) / lenSqr);
                Vector3 toSegment = (a + ab * t) - pos;
                float dist = toSegment.magnitude;
                Vector3 towardSegment = dist > 0.0001f ? toSegment / dist : dir;
                float meshExtent = HalfExtentAlongDirection(propObj, towardSegment);
                float requiredClearance = halfWidth + meshExtent;
                if (dist >= requiredClearance)
                    continue;
                overlapped = true;
                float clearance = requiredClearance - dist + HighwayWidth * 0.1f;
                Vector3 forwardCandidate = pos + dir * clearance;
                Vector3 backwardCandidate = pos - dir * clearance;
                float distForward = DistancePointToSegment(forwardCandidate, a, b);
                float distBackward = DistancePointToSegment(backwardCandidate, a, b);
                pos = distForward >= distBackward ? forwardCandidate : backwardCandidate;
            }
            if (!overlapped)
                break;
        }
        return pos;
    }

    private static float DistancePointToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float lenSqr = ab.sqrMagnitude;
        if (lenSqr <= 0f)
            return Vector3.Distance(p, a);
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / lenSqr);
        return Vector3.Distance(p, a + ab * t);
    }

    private bool IsBackwardDirection(Node node)
    {
        if (!node.Tags.TryGetValue("traffic_signals:direction", out string dir))
            node.Tags.TryGetValue("direction", out dir);
        return dir != null && dir.Equals("backward", System.StringComparison.OrdinalIgnoreCase);
    }

    //tangent of the way at nodeId, averaged from its immediate neighbors (same convention as the interior-point
    //direction used by BuildRibbonMesh); falls back to a one-sided direction at either end of the way.
    //wayNodes is expected to be a way's own resolved node list (in the same order as its Way.Nodes id list),
    //so the neighbor at index-1/index+1 is found directly by position, with no separate id-based lookup needed
    private Vector3? ComputeWayDirectionAtNode(Node[] wayNodes, long nodeId, Vector3 nodePos)
    {
        int index = System.Array.FindIndex(wayNodes, n => n.Id == nodeId);
        if (index < 0)
            return null;
        Vector3? prevPos = index > 0 ? GetNodePosition(wayNodes[index - 1]) : null;
        Vector3? nextPos = index < wayNodes.Length - 1 ? GetNodePosition(wayNodes[index + 1]) : null;
        if (prevPos.HasValue && nextPos.HasValue)
            return ((nextPos.Value - nodePos).normalized + (nodePos - prevPos.Value).normalized).normalized;
        if (nextPos.HasValue)
            return (nextPos.Value - nodePos).normalized;
        if (prevPos.HasValue)
            return (nodePos - prevPos.Value).normalized;
        return null;
    }

    //whichever segment, across every way in the loader, passes nearest to pos - used when a point-prop node
    //is its own standalone point rather than a node shared with a road way. HighwayLoader.GetNearbySegments
    //does the heavy lifting via a precomputed spatial grid, so this only ever checks segments already known
    //to be within HighwayLoader.MaxPostToWayDistance's neighborhood, not every segment in the whole loader.
    //excludeWayId skips segments belonging to that one way - used to look past a crossing node's own way
    //(typically a footway that merely touches the road here, not the road itself) to find the actual road
    private (Vector3 a, Vector3 b, long wayId, float halfWidth)? FindNearestSegment(Vector3 pos, long? excludeWayId = null)
    {
        float bestDistSqr = HighwayLoader.MaxPostToWayDistance * HighwayLoader.MaxPostToWayDistance;
        (Vector3, Vector3, long, float)? best = null;
        foreach (var segment in ((HighwayLoader)osmObj.Loader).GetNearbySegments(pos))
        {
            var (a, b, wayId, _) = segment;
            if (excludeWayId.HasValue && wayId == excludeWayId.Value)
                continue;
            Vector3 ab = b - a;
            float lenSqr = ab.sqrMagnitude;
            if (lenSqr <= 0f)
                continue;
            float t = Mathf.Clamp01(Vector3.Dot(pos - a, ab) / lenSqr);
            float distSqr = (pos - (a + ab * t)).sqrMagnitude;
            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                best = segment;
            }
        }
        return best;
    }

    private static Vector3 ClosestPointOnSegment(Vector3 pos, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float lenSqr = ab.sqrMagnitude;
        if (lenSqr <= 0f)
            return a;
        float t = Mathf.Clamp01(Vector3.Dot(pos - a, ab) / lenSqr);
        return a + ab * t;
    }

    //repositions/reorients/resizes an already-instantiated "crossing" zebra-mesh prop so it centers on and
    //spans the actual road it marks, rather than sitting wherever the tagging node itself happens to be:
    //finds the nearest road segment - excluding ownWayId, the node's own way, when given, since that's
    //typically a footway merely touching the road here rather than the road itself, falling back to
    //including it if nothing else is nearby (a crossing tagged directly on a node of the road's own way) -
    //snaps to the closest point on that segment, rotates so the mesh's own already-scaled axis (see
    //ScaleLongestSideTo) runs across it, and sizes it to that road's actual width. Falls back to this way's
    //own HighwayWidth, facing PostUprightCorrection's own baseline yaw, if no road segment is within
    //HighwayLoader.MaxPostToWayDistance at all - which happens for a real crossing node whenever the road it
    //marks simply isn't part of the currently loaded way set (e.g. a smaller test extract that dropped it)
    private void PositionCrossing(GameObject propObj, Vector3 pos, long? ownWayId)
    {
        var nearest = (ownWayId.HasValue ? FindNearestSegment(pos, ownWayId) : null) ?? FindNearestSegment(pos);
        Vector3 groundPos = pos;
        float targetWidth = HighwayWidth;
        Quaternion yaw = Quaternion.identity;
        if (nearest.HasValue)
        {
            groundPos = ClosestPointOnSegment(pos, nearest.Value.a, nearest.Value.b);
            Vector3 tangent = (nearest.Value.b - nearest.Value.a).normalized;
            // LookRotation's forward has to be the road's own tangent here, NOT across it - PostUprightCorrection
            // sits underneath this yaw (see "yaw * PostUprightCorrection" below), and composing that way pre-swaps
            // the mesh's local width axis onto LookRotation's "right" (Cross(up, forward)), not onto "forward"
            // itself. Passing tangent as forward puts "right" (= Cross(up, tangent), i.e. across the road) where
            // the width axis lands - which is what actually needs to run across the road for the crossing to
            // span it, rather than run along it
            yaw = Quaternion.LookRotation(tangent, Vector3.up);
            targetWidth = nearest.Value.halfWidth * 2f;
        }
        else
        {
            Debug.LogWarning("Crossing node has no road within " + HighwayLoader.MaxPostToWayDistance + "m, left unsnapped: " + osmObj.Element.Id);
        }
        // Renderer.bounds (what ScaleLongestSideTo/TryGetRendererBounds read) is a world-space AABB, which
        // for a mesh yawed to an arbitrary heading - the common case, since roads are rarely aligned to
        // world X/Z - is inflated diagonally beyond the mesh's true footprint (e.g. a square yawed 45 degrees
        // measures root-2 times too wide on both axes). That inflated "longest side" made ScaleLongestSideTo
        // shrink the mesh well below the road's real width. PostUprightCorrection alone (no yaw yet) is a
        // 90-degree-multiple rotation, which never inflates an AABB, so scaling has to happen in that pose -
        // uniform scale is unaffected by whatever rotation is applied afterward, so the yaw goes on after
        propObj.transform.rotation = PostUprightCorrection;
        ScaleLongestSideTo(propObj, targetWidth);
        propObj.transform.rotation = yaw * PostUprightCorrection;
        // Road_Crosswalk's own root pivot sits at its local origin, but its mesh's local vertices (baked
        // into the prefab) span roughly y:[9.6, 15] - nowhere near that origin, unlike x:[-5.23, 5.23] which
        // IS centered on it. So the pivot is several meters away from the mesh's actual center, and simply
        // placing the pivot at groundPos leaves the visible mesh displaced off to the side instead of
        // centered on the road. Renderer.bounds.center is measured once, after the final scale/rotation
        // above, and the whole object is shifted by (groundPos - that center) so the CENTER - not the pivot -
        // ends up at the target point; GroundLift's contribution rides along in the same read since it also
        // reads propObj's current (pre-shift) bounds
        TryGetRendererBounds(propObj, out Bounds bounds);
        // same per-tier vertical step every road/path mesh gets from ComputeHeightTierOffset, so the
        // crosswalk sits clear of (instead of z-fighting with) the road surface tier it crosses
        float lift = GroundLift(propObj, groundPos.y) + RankHeightStep * HighwayTypeRank["crossing"];
        Vector3 shift = new Vector3(groundPos.x - bounds.center.x, lift, groundPos.z - bounds.center.z);
        propObj.transform.position += shift;
    }

    //direction from the closest point of segment a-b out to pos, ACROSS the road - the same "outward, away
    //from the road" convention BuildPostRotation/PerpendicularToRoad use for an embedded node (see
    //PerpendicularToRoad's comment: the mesh's own visual front ends up facing back along -dir, i.e. toward
    //the road), so an isolated post built from this direction faces/lights the road the same way an
    //embedded one does
    private static Vector3 DirectionAwayFromSegment(Vector3 pos, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float lenSqr = ab.sqrMagnitude;
        if (lenSqr <= 0f)
            return Vector3.forward;
        Vector3 tangent = ab / Mathf.Sqrt(lenSqr);
        float t = Mathf.Clamp01(Vector3.Dot(pos - a, ab) / lenSqr);
        Vector3 away = pos - (a + ab * t);
        // when t lands strictly between the segment's two endpoints, "away" is already perpendicular to the
        // segment by construction (that's what makes it the closest point). But when pos is beyond either
        // end of this particular segment, t clamps to 0 or 1 and "away" points at that endpoint instead - a
        // vector that can run mostly ALONG the road (e.g. pos sits ahead of/behind a short segment near a
        // junction, not really beside it) rather than across it. The post must always face across the road,
        // never along it, so the component of "away" running along the road's own tangent is discarded here
        // unconditionally - a no-op for the ordinary interior-point case, the actual fix for the clamped one
        Vector3 lateral = away - Vector3.Dot(away, tangent) * tangent;
        return lateral.sqrMagnitude > 0.0001f ? lateral.normalized : Vector3.Cross(Vector3.up, tangent);
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
                // ProBuilder's own triangulator can fail on a highly regular set of points (e.g. a long,
                // straight ribbon evenly subdivided by ComputeSubDivisions) even though the polygon itself is
                // perfectly valid - see CreateFanShapeFromPolygon's comment. Retry with a centroid fan
                // instead of leaving an empty mesh.
                if (!act.ToBool())
                    act = CreateFanShapeFromPolygon(mesh, subDivisions, 0.01f, false);
                mesh.DuplicateAndFlip(mesh.faces.ToArray());
                IsVisible = act.ToBool();
            }
            cityObj.name = "Highway ID = " + osmObj.Element.Id;
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
            cityObj.name = "Highway ID = " + osmObj.Element.Id;
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
            name = "Highway ID = " + osmObj.Element.Id,
            hideFlags = HideFlags.NotEditable
        };
        if (material != null && material.shader == HighwayLoader.DefHighwayMat.shader)
            material = Resources.Load<Material>("Materials/asphalt");
        mesh = cityObj.AddComponent<ProBuilderMesh>();
        ActionResult act = mesh.CreateShapeFromPolygon(pos.ToList(), 0.01f, false);
        // ProBuilder's own triangulator can fail on a highly regular footprint (e.g. a round plaza/roundabout
        // island approximated by many evenly-spaced points) even though the polygon itself is perfectly
        // valid - see CreateFanShapeFromPolygon's comment. Retry with a centroid fan instead of leaving an
        // empty mesh.
        if (!act.ToBool())
            act = CreateFanShapeFromPolygon(mesh, pos, 0.01f, false);
        mesh.DuplicateAndFlip(mesh.faces.ToArray());
        IsVisible = act.ToBool();
        // yOffset is always 0 here (ComputeHeightTierOffset returns 0 for IsArea)
        cityObj.transform.position += new Vector3(0f, yOffset, 0f);
        AddMeshCollider();
        AddObjInfos();
    }

    //applyMaterial is false for a point-prop prefab (LampPost_A, StreetSign_G, ...): its MeshRenderer already
    //has the correct material baked in (e.g. the bus stop's icon-on-blue-background region of the shared
    //Atlas), and UpdateMaterial() would stomp it with the road-surface material (Materials.road2/dirt/...)
    //resolved earlier in Start() for this node's own surface/highway tags - unrelated to the prop's own look.
    //addDebugText is false for the same prefabs, for a more fundamental reason: UnityEngine.UI.Text requires
    //a RectTransform, so AddComponent<Text>() on an object that only has a plain Transform makes Unity
    //silently REPLACE that Transform with a RectTransform - which clobbers the rotation/position we just
    //spent all this effort computing (this is what was actually behind "every point prop has the same
    //rotation", not a wayDir/distance bug), and is also why HideFlags.NotEditable (set on cityObj just before
    //this runs) produces a "can't rename" warning - the Transform-to-RectTransform swap needs to rename
    //something internally and that's blocked by the flag
    private void AddObjInfos(bool applyMaterial = true, bool addDebugText = true)
    {
        // Update the texture and the color
        if (applyMaterial)
            UpdateMaterial();
        if (addDebugText)
        {
            // Add a canvas and text fields for node positions and miscellanous infos
            Text t = cityObj.AddComponent<Text>();
            t.text = GetNodePositionsText();
        }
        if (osmObj.Loader.Main.hideMeshInHierarchy)
            SetHideFlagsRecursive(cityObj, HideFlags.HideInHierarchy);
        cityObj.transform.SetParent(transform);
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
