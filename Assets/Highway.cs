using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UI;

public class Highway : CityObject
{

    [Header("Highway attributes")]
    public string highwayType = "road";
    [Tooltip("Width must be strictly positive")]
    [Min(0f)]
    [Delayed]
    public float highwayWidth = 3f;
    [Tooltip("Width must be strictly positive")]
    [Min(0f)]
    [Delayed]
    public float highwayLength = 3f;

    private float prevHighwayWidth = 3f, prevHighwayLength = 3f;

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
                    highwayWidth = w;
            }
            return highwayWidth * osmObj.Loader.Main.zMeterScale;
        }
    }

    public float HighwayLength
    {
        get
        {
            if (osmObj.Element.Type == OsmGeoType.Node)
            {
                return highwayLength * osmObj.Loader.Main.xMeterScale;
            }
            else
            {
                Vector3[] pos = GetNodePositions(osmObj.SubNodes);
                if (pos != null && pos.Length > 1)
                {
                    float length = 0f;
                    for (int i = 0; i < pos.Length - 1; i++)
                    {
                        length += Vector3.Distance(pos[i], pos[i + 1]);
                    }
                    highwayLength = length * osmObj.Loader.Main.xMeterScale;
                    return highwayLength;
                }
                else
                {
                    return highwayLength * osmObj.Loader.Main.xMeterScale;
                }
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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        trueMiscellaneous = GetMiscellaneous();
        miscellaneous = trueMiscellaneous;
        trueNodePositions = GetNodePositionsText();
        nodePositions = trueNodePositions;

        // Add a material
        if (Surface != null)
            material = Resources.Load<Material>("Materials/" + surface);
        if (HighwayMaterial == null)
            material = HighwayLoader.DefHighwayMat;
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
                    if (pos.Length > 1)
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
        // initialisation des champs
        _ = Length; _ = Width; _ = Type; _ = Amenity; _ = Source; _ = Elevation; _ = Surface;
        // attributs relatifs aux routes
        _ = HighwayWidth; _ = HighwayLength;
    }

    // Update is called once per frame
    void Update()
    {
        //teste la validité des entrées
        if (highwayWidth <= 0)
            highwayWidth = prevHighwayWidth;
        if (highwayLength <= 0)
            highwayLength = prevHighwayLength;
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
        cityObj.transform.position = new Vector3(pos.x, pos.y, pos.z);
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
            cityObj.transform.position = new Vector3(pos.Value.x, pos.Value.y, pos.Value.z);
            cityObj.transform.localScale = new Vector3(HighwayLength, 0.01f, HighwayWidth);
        }
    }

    private void CreatePolygon(Vector3[] pos)
    {
        cityObj = new GameObject
        {
            name = "ID = " + osmObj.Element.Id,
            hideFlags = HideFlags.NotEditable
        };
        // Add a ProBuilderMesh component (ProBuilder mesh data is stored here)
        mesh = cityObj.AddComponent<ProBuilderMesh>();
        // Compute the node positions corresponding to the highway bounds
        Vector3[] bounds = ComputeHighwayBounds(pos, HighwayWidth);
        // Create a mesh from the polygon shape
        ActionResult act = mesh.CreateShapeFromPolygon(bounds.ToList(), 0.01f, false);
        mesh.DuplicateAndFlip(mesh.faces.ToArray());
        IsVisible = act.ToBool();
        // Add a mesh collider
        AddMeshCollider();
        // Add infos
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
        Vector3[] bounds = new Vector3[pos.Length * 2 + 1];
        for (int i = 0; i < pos.Length; i++)
        {
            Vector3 dir;
            if (i == 0)
                dir = (pos[i + 1] - pos[i]).normalized;
            else if (i == pos.Length - 1)
                dir = (pos[i] - pos[i - 1]).normalized;
            else
                dir = ((pos[i + 1] - pos[i]).normalized + (pos[i] - pos[i - 1]).normalized).normalized;
            Vector3 normal = new Vector3(-dir.z, 0, dir.x);
            bounds[i] = pos[i] + normal * width / 2f;
            bounds[bounds.Length - 2 - i] = pos[i] - normal * width / 2f;
        }
        bounds[bounds.Length - 1] = bounds[0];
        return bounds;
    }

}
