using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

public abstract class CityObject : MonoBehaviour
{

    protected string type, trueNodePositions, trueMiscellaneous, prevSurface;
    protected OsmObject osmObj;
    protected ProBuilderMesh mesh;
    protected GameObject cityObj;
    protected Material prevMat;
    protected PrimitiveType prevGeometry = PrimitiveType.Cube;
    protected float prevWidth = 3f, prevLength = 3f;

    [Header("General attributes")]
    [TextArea]
    public string nodePositions;
    // the current texture/material used by this city object
    public Material material;
    // the width, length and geometry fields are used for city objects related to a single node
    public PrimitiveType geometry = PrimitiveType.Cube;
    [Min(0f)]
    [Delayed]
    [Tooltip("Width must be strictly positive")]
    public float width = 3f;
    [Min(0f)]
    [Delayed]
    [Tooltip("Length must be strictly positive")]
    public float length = 3f;
    [Delayed]
    public string objName, amenity, source, surface;
    [Min(0)]
    [Delayed]
    [Tooltip("Elevation must be positive")]
    public int elevation;
    [TextArea]
    public string miscellaneous;

    private float? groundArea, perimeter;
    private Vector3? barycenter;

    public OsmObject OsmObject => osmObj;
    public IList<Vector3> MeshPos => mesh.positions;
    public bool IsMeshCreated { get; protected set; }
    public bool IsVisible { get; protected set; }


    //attributs qualitatifs

    public string Name
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("name", out string name))
            {
                objName = name;
            }
            return objName;
        }
    }
    public string Amenity
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("amenity", out string amenity))
            {
                this.amenity = amenity;
            }
            return this.amenity;
        }
    }

    public string Source
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("source", out string source))
            {
                this.source = source;
            }
            return this.source;
        }
    }

    public string Surface
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("surface", out string surface))
            {
                this.surface = surface;
            }
            return this.surface;
        }
    }

    public int Elevation
    {
        get
        {
            if (osmObj.Element.Tags.TryGetValue("ele", out string ele))
            {
                try
                {
                    elevation = int.Parse(ele, CultureInfo.InvariantCulture);
                }
                catch (System.Exception exc)
                {
                    Debug.LogWarning(exc.Message + ele);
                }
            }
            return elevation;
        }
    }

    //attributs géométriques
    public float Width    //largeur du rectangle circonscrit au polygone associé à la marque du bâtiment
    {
        get
        {
            if (osmObj.Element.Type == OsmGeoType.Node)
            {
                return width * osmObj.Loader.Main.zMeterScale;
            }
            else
            {
                Vector3 maxCoords = osmObj.Loader.Main.GetTerrainCoords(osmObj.Bounds.MaxLat, osmObj.Bounds.MaxLon);
                Vector3 minCoords = osmObj.Loader.Main.GetTerrainCoords(osmObj.Bounds.MinLat, osmObj.Bounds.MinLon);
                double w = maxCoords.z - minCoords.z;
                if (w == 0d)
                    return width * osmObj.Loader.Main.zMeterScale;
                else
                {
                    width = (float)w;
                    return (float)w;
                }
            }
        }
    }

    public float Length    //longueur du rectangle circonscrit au polygone associé à la marque du bâtiment
    {
        get
        {
            if (osmObj.Element.Type == OsmGeoType.Node)
            {
                return length * osmObj.Loader.Main.xMeterScale;
            }
            else
            {
                Vector3 maxCoords = osmObj.Loader.Main.GetTerrainCoords(osmObj.Bounds.MaxLat, osmObj.Bounds.MaxLon);
                Vector3 minCoords = osmObj.Loader.Main.GetTerrainCoords(osmObj.Bounds.MinLat, osmObj.Bounds.MinLon);
                double l = maxCoords.x - minCoords.x;
                if (l == 0d)
                    return length * osmObj.Loader.Main.xMeterScale;
                else
                {
                    length = (float)l;
                    return (float)l;
                }
            }
        }
    }

    public float GroundArea        //computes the ground area of the building
    {
        get
        {
            if (osmObj.Element.Type == OsmGeoType.Node)
            {
                return width * length;
            }
            else
            {
                if (groundArea != null)
                    return (float)groundArea;
                Face groundFace = GetGroundFace();
                if (groundFace == null)
                    return 0;

                // finds the 3D coordinates of the vertices of each triangle that compounds this face
                List<Vector3> vects = new List<Vector3>();
                foreach (var ind in groundFace.indexes)
                {
                    vects.Add(mesh.positions[ind]);
                }

                //computes the area of each triangle that compounds this face using Heron's formula, then sums these to get the ground area
                float totalArea = 0;
                for (int i = 0; i < vects.Count(); i += 3)
                {
                    totalArea += Math.TriangleArea(vects[i], vects[i + 1], vects[i + 2]);
                }
                groundArea = totalArea;
                return totalArea;
            }
        }
    }

    public float Perimeter
    {
        get
        {
            if (osmObj.Element.Type == OsmGeoType.Node)
            {
                return 2 * width + 2 * length;
            }
            else
            {
                if (perimeter != null)
                    return (float)perimeter;
                Vector3[] pos = GetNodePositions(osmObj.SubNodes);
                if (pos == null || pos.Length == 0)
                    return 0;
                float p = 0;
                for (int i = 0; i < pos.Length - 1; i++)
                {
                    p += ComputeDist(pos[i], pos[i + 1]);
                }
                p += ComputeDist(pos[pos.Length-1], pos[0]);
                perimeter = p;
                return p;
            }
        }
    }

    public double NormPerimeterIndex
    {
        get
        {
            return 2 * System.Math.Sqrt(System.Math.PI * GroundArea) / Perimeter;
        }
    }

    public Vector3? Barycenter
    {
        get
        {
            if (osmObj.Element.Type == OsmGeoType.Node)
            {
                return GetNodePosition((Node)osmObj.Element);
            }
            else
            {
                if (barycenter != null)
                    return (Vector3)barycenter;
                double sLat = 0, sLon = 0;
                int count = 0;
                foreach (Node node in osmObj.SubNodes)
                {
                    if (node.Latitude != null && node.Longitude != null)
                    {
                        sLon += (double)node.Longitude;
                        sLat += (double)node.Latitude;
                        count++;
                    }
                }
                // dividing by SubNodes.Length (rather than the count of nodes that actually had
                // coordinates) produced 0/0 = NaN whenever a way's nodes failed to resolve (e.g. a missing
                // nodesFile) or none carried lat/lon - that NaN then crashed GetTerrainCoords several calls
                // downstream instead of being caught here, where "no barycenter" can be reported honestly
                if (count == 0)
                    return null;
                barycenter = osmObj.Loader.Main.GetTerrainCoords(sLat / count, sLon / count);
                return barycenter;
            }
        }
    }

    public Vector3? Center
    {
        get
        {
            if (osmObj.Element.Type == OsmGeoType.Node)
            {
                return GetNodePosition((Node)osmObj.Element);
            }
            else
            {
                double lat = (osmObj.Bounds.MinLat + osmObj.Bounds.MaxLat) / 2;
                double lon = (osmObj.Bounds.MinLon + osmObj.Bounds.MaxLon) / 2;
                return osmObj.Loader.Main.GetTerrainCoords(lat, lon);
            }
        }
    }

    public void SetOsmObj(OsmObject osmObj)
    {
        this.osmObj = osmObj;
    }

    public void SetType(string type)
    {
        this.type = type;
    }

    public Vector3[] GetNodePositions(Node[] nodes)      //get real world coordinates of the nodes included in the array and drop duplicates (coordinates should be ordered)
    {
        if (nodes == null || nodes.Length == 0)
            return null;
        int length;
        if (nodes.Length == 1 || nodes[0].Id != nodes[nodes.Length - 1].Id)
        {
            length = nodes.Length;
        }
        else
        {
            length = nodes.Length - 1;
        }
        List<Vector3> vects = new List<Vector3>();
        for (int i = 0; i < length; i++)
        {
            if (nodes[i].Longitude != null && nodes[i].Latitude != null)
            {
                Vector3 vect = osmObj.Loader.Main.GetTerrainCoords((double)nodes[i].Latitude, (double)nodes[i].Longitude);
                vects.Add(vect);
            }
        }
        return vects.ToArray();
    }

    public Vector3? GetNodePosition(Node node)
    {
        if (node.Latitude != null && node.Longitude != null)
            return osmObj.Loader.Main.GetTerrainCoords((double)node.Latitude, (double)node.Longitude);
        else
            return null;
    }

    protected Face GetGroundFace()
    {
        Face groundFace = null;
        // finds the face of the mesh component on the ground
        if (mesh != null && mesh.faceCount > 0)
        {
            foreach (var face in mesh.faces)
            {
                var verticeInds = from ind in face.distinctIndexes
                                  where mesh.positions[ind].y != 0f
                                  select ind;

                if (!verticeInds.Any())
                {
                    groundFace = face;
                    break;
                }
            }
        }
        return groundFace;
    }

    protected Face GetCeilingFace()
    {
        Face ceilingFace = null;
        // finds the face of the mesh component related to the ceiling of the building
        if (mesh != null && mesh.faceCount > 0)
        {
            foreach (var face in mesh.faces)
            {
                var verticeInds = from ind in face.distinctIndexes
                                  where mesh.positions[ind].y == 0f
                                  select ind;

                if (!verticeInds.Any())
                {
                    ceilingFace = face;
                    break;
                }
            }
        }
        return ceilingFace;
    }

    public void UpdateMesh(float heightOffset)
    {
        if (mesh == null || heightOffset == 0f)
            return;
        Face ceilingFace = GetCeilingFace();
        if (ceilingFace != null)
        {
            mesh.TranslateVertices(new Face[] { ceilingFace }, new Vector3(0, heightOffset, 0));
            mesh.ToMesh();
            mesh.Refresh();
        }
    }

    protected void UpdateMaterial()
    {
        if (cityObj == null)
            return;
        MeshRenderer renderer = cityObj.GetComponent<MeshRenderer>();
        if (material != null)
            renderer.material = material;
    }

    protected void UpdateColor(Color col)
    {
        if (cityObj == null)
            return;
        MeshRenderer renderer = cityObj.GetComponent<MeshRenderer>();
        renderer.material.color = col;
    }

    protected void AddMeshCollider()
    {
        if (mesh == null)
            return;
        mesh.gameObject.AddComponent<MeshCollider>();
        EditorMeshUtility.RebuildColliders(mesh);
    }

    //ProBuilder's own CreateShapeFromPolygon relies on a sweep-line Delaunay triangulator (Triangulation.
    //TriangulateVertices, using the bundled Poly2Tri library) that is documented to fail on highly regular/
    //symmetric polygons - a building or road-area footprint approximating a circle with many evenly-spaced
    //points (e.g. a small round tower) is exactly that kind of input. The failure is silent from the
    //caller's perspective: CreateShapeFromPolygon just returns a Failure ActionResult and clears the mesh
    //to empty, so the GameObject still gets created/named/positioned downstream with nothing visible in it.
    //A triangle fan from the polygon's own centroid is always a correct triangulation for any convex or
    //star-shaped polygon - which every round/near-circular footprint is - so it sidesteps the sweep-line
    //triangulator (and its failure mode) entirely. This mirrors CreateShapeFromPolygon's own
    //duplicate-then-extrude sequence (see ProBuilder's AppendElements.cs) using only its public API, so a
    //caller can use it as a drop-in retry and get equivalent geometry to what a successful call would have
    //produced - including the same normal-facing convention (flipNormals=false faces the cap up).
    internal static ActionResult CreateFanShapeFromPolygon(ProBuilderMesh mesh, IList<Vector3> points, float extrude, bool flipNormals)
    {
        List<Vector3> ring = new List<Vector3>(points);
        if (ring.Count > 1 && ring[0] == ring[ring.Count - 1])
            ring.RemoveAt(ring.Count - 1);
        int n = ring.Count;
        if (n < 3)
        {
            mesh.Clear();
            mesh.ToMesh();
            mesh.Refresh();
            return new ActionResult(ActionResult.Status.NoChange, "Too Few Points");
        }

        Vector3 centroid = Vector3.zero;
        foreach (var p in ring)
            centroid += p;
        centroid /= n;

        List<Vector3> vertices = new List<Vector3> { centroid };
        vertices.AddRange(ring);
        // wound one fixed way (arbitrary - the actual resulting winding depends on the ring's own point
        // order, which callers don't guarantee any particular handedness for) and corrected below by
        // checking the ACTUAL resulting normal, rather than assuming this order faces a particular way:
        // hand-picking a "should be correct" winding here was tried first and got it backwards for this
        // ring's ordering, silently extruding the whole shape underground instead of up
        List<int> indexes = new List<int>();
        for (int i = 0; i < n; i++)
        {
            indexes.Add(0); indexes.Add(1 + i); indexes.Add(1 + (i + 1) % n);
        }
        Face capFace = new Face(indexes.ToArray());
        mesh.RebuildWithPositionsAndFaces(vertices, new Face[] { capFace });

        // same check CreateShapeFromPolygon itself uses internally (see ProBuilder's AppendElements.cs):
        // flipNormals=false should face the cap toward the mesh's own up, so reverse it if it doesn't
        Vector3 nrm = mesh.gameObject.transform.TransformDirection(Math.Normal(mesh, capFace));
        if (flipNormals ? Vector3.Dot(mesh.gameObject.transform.up, nrm) > 0f
                         : Vector3.Dot(mesh.gameObject.transform.up, nrm) < 0f)
            capFace.Reverse();

        if (extrude != 0f)
        {
            // duplicate the flat cap into two coincident faces (one stays as the base, the other gets
            // extruded outward with connecting side walls) - the exact sequence CreateShapeFromPolygon
            // itself uses internally
            mesh.DuplicateAndFlip(mesh.faces.ToArray());
            Face extrudeFace = flipNormals ? mesh.faces[1] : mesh.faces[0];
            mesh.Extrude(new Face[] { extrudeFace }, ExtrudeMethod.IndividualFaces, extrude);
            if ((extrude < 0f && !flipNormals) || (extrude > 0f && flipNormals))
            {
                foreach (var face in mesh.faces)
                    face.Reverse();
            }
        }

        mesh.ToMesh();
        mesh.Refresh();
        return new ActionResult(ActionResult.Status.Success, "Create Polygon Shape (fan fallback)");
    }

    protected string GetNodePositionsText()
    {
        if (osmObj.Element.Type == OsmGeoType.Node)
        {
            Node node = (Node)osmObj.Element;
            return $"lat={node.Latitude} lon={node.Longitude}";
        }
        else
        {
            string text = "";
            foreach (Node node in osmObj.SubNodes)
            {
                text += $"id={node.Id} lat={node.Latitude} lon={node.Longitude}\n";
            }
            return text;
        }
    }

    protected string GetMiscellaneous()
    {
        string text = "";
        foreach (var tag in osmObj.Element.Tags)
        {
            if (tag.Key != "name" && tag.Key != "amenity" && tag.Key != "source" && tag.Key != "surface" && tag.Key != "ele" && tag.Key != "building" && tag.Key != "height")
                text += $"{tag.Key} = {tag.Value}\n";
        }
        return text;
    }

    public static float ComputeDist(Vector3 pos1, Vector3 pos2)
    {
        return (pos1 - pos2).magnitude;
    }

    public static Color? Convert(string strColor)
    {
        switch (strColor)
        {
            case "cream":
                return new Color(1.00f, 0.99f, 0.82f);
            case "sand":
                return new Color(0.76f, 0.70f, 0.50f);
            case "rgb(114,␣200,␣251)":
                return new Color32(114, 200, 251, 255);
            default:
                if (strColor.Contains("rgb"))
                {
                    try
                    {
                        int ind1 = strColor.IndexOf(',');
                        byte r = byte.Parse(strColor.Substring(4, ind1 - 4), CultureInfo.InvariantCulture);
                        int ind2 = strColor.IndexOf(',', ind1 + 1);
                        byte g = byte.Parse(strColor.Substring(ind1 + 1, ind2 - ind1 - 1), CultureInfo.InvariantCulture);
                        byte b = byte.Parse(strColor.Substring(ind2 + 1, strColor.IndexOf(')') - ind2 - 1), CultureInfo.InvariantCulture);
                        return new Color32(r, g, b, 255);
                    }
                    catch (System.Exception exc)
                    {
                        Debug.LogWarning(exc.Message);
                        return null;
                    }
                }
                string newStrColor = strColor.Replace("_", "");
                if (ColorUtility.TryParseHtmlString(newStrColor, out Color col))
                    return col;
                else
                    return null;
        }
    }

    public CityObject[] GetNeighbors(float radius)      //obtient le nombre d'objets voisins du même type dont une partie se trouve dans un rayon donné (en m)
    {
        List<CityObject> neighbors = new List<CityObject>();
        IReadOnlyList<CityObject> cityObjects = osmObj.Loader.CityObjects;
        int indObj = osmObj.Loader.IndexOf(this);
        int nNeighbors = osmObj.Loader.Main.nNeighbors, objLength = cityObjects.Count;
        int begin = nNeighbors < 0 || nNeighbors > objLength ? 0 : Mathf.Max(0, indObj - Mathf.CeilToInt(nNeighbors / 2f));
        int count = nNeighbors < 0 || nNeighbors > objLength ? objLength - 1 : nNeighbors;
        if (nNeighbors >= 0 && nNeighbors <= objLength && indObj + Mathf.FloorToInt(nNeighbors / 2f) > objLength - 1)
            begin = objLength - 1 - nNeighbors;
        for (int i = begin; i <= begin + count; i++)
        {
            CityObject cityObj = cityObjects[i];
            if (cityObj.osmObj.Element.Id != osmObj.Element.Id && cityObj.type == type)
            {
                // a standalone-node city object (e.g. nBuildingNodes/nHighwayNodes set to load isolated
                // nodes) has no SubNodes of its own (see OsmObject.SetSubElements - a single node isn't a
                // polygon with sub-nodes, it IS the point) - fall back to its own single position instead
                Node[] neighborNodes = cityObj.osmObj.SubNodes ?? new[] { (Node)cityObj.osmObj.Element };
                foreach (var node in neighborNodes)
                {
                    Vector3? pos = GetNodePosition(node);
                    if (pos == null || Barycenter == null)
                        continue;
                    float d = ComputeDist(pos.Value, Barycenter.Value);
                    float dx = Mathf.Abs(pos.Value.x - Barycenter.Value.x);
                    float dz = Mathf.Abs(pos.Value.z - Barycenter.Value.z);
                    float xRadius = radius * Mathf.Cos(Mathf.Atan2(dz, dx));
                    float zRadius = radius * Mathf.Sin(Mathf.Atan2(dz, dx));
                    Vector3 realRadius = new Vector3(xRadius * osmObj.Loader.Main.xMeterScale, 0, zRadius * osmObj.Loader.Main.zMeterScale);
                    if (d < realRadius.magnitude && cityObj.Elevation == Elevation)
                    {
                        neighbors.Add(cityObj);
                        break;
                    }
                }
            }
        }
        return neighbors.ToArray();
    }

    public float GetOrientation()       //obtient l'angle d'orientation de l'objet urbain en degrés
    {
        Vector3 minCoord = osmObj.Loader.Main.GetTerrainCoords(osmObj.Bounds.MinLat, osmObj.Bounds.MinLon);
        Vector3[] pos = GetNodePositions(osmObj.SubNodes);
        float l = 1, w = 1;
        foreach (var vect in pos)
        {
            if (vect == minCoord)
                return 0;
            if (vect.x == minCoord.x)
            {
                w = vect.z - minCoord.z;
            }
            else
            {
                if (vect.z == minCoord.z)
                {
                    l = vect.x - minCoord.x;
                }
            }
        }
        return Mathf.Atan2(w, l) * 180 / Mathf.PI;
    }

}
