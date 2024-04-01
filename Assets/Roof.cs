using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

public enum RoofShape
{
    Gabled, Pyramidal, Hipped, HalfHipped, Skillion, Gambrel, Mansard, Round, Dome, Flat, None,
}

public class Roof : MonoBehaviour
{

    [Header("General roof attributes")]
    public RoofShape shape = RoofShape.None;
    public bool isAcross;
    public Material material;
    public Color color = Color.clear;
    [Tooltip("Number of levels must be greater or equal to 1")]
    [Min(1)]
    [Delayed]
    public int nLevels = 1;
    [Tooltip("Height must be strictly positive")]
    [Min(0f)]
    [Delayed]
    public float height = 1;
    [Tooltip("Slope angle must be between 0° and 90° included")]
    [Range(0f, 90f)]
    public float slope;
    [Tooltip("Direction angle must be between 0° and 360° included")]
    [Range(0f, 360f)]
    public float direction;
    [Header("Hip roof attributes")]
    [Tooltip("Hip length must be positive")]
    [Min(0f)]
    public float hipLength = 1;
    [Tooltip("Hip height must be strictly positive")]
    [Min(0f)]
    public float hipHeight = 0.5f;
    [Tooltip("Mid width must be strictly positive")]
    [Min(0f)]
    public float midWidth = 0.1f;
    [Tooltip("Mid length must be strictly positive")]
    [Min(0f)]
    public float midLength = 0.1f;

    private Building building;      //The building this roof is attached to
    private ProBuilderMesh mesh;
    private float prevHipLength = 1, prevHipHeight = 0.5f, prevMidWidth = 0.1f, prevMidLength = 0.1f, prevSlope, prevDirection, prevHeight, prev_height, maxMidWidth, maxMidLength;
    private Material prevMat;
    private Color prevColor;
    private RoofShape prevShape;
    private bool prevIsAcross;

    public RoofShape Shape
    {
        get
        {
            if (building.OsmObject.Element.Tags.TryGetValue("roof:shape", out string shape) | building.OsmObject.Element.Tags.TryGetValue("building:roof:shape", out string shape2))
            {
                string strShape = shape ?? shape2;
                if (Enum.TryParse(strShape, true, out RoofShape value))
                    this.shape = value;
                else
                {
                    if (strShape == "pitched")
                        this.shape = RoofShape.Gabled;
                    else
                    {
                        if (strShape == "half-hipped")
                            this.shape = RoofShape.HalfHipped;
                        else
                            this.shape = RoofShape.None;
                    }
                }
            }
            return this.shape;
        }
    }

    public int NLevels
    {
        get
        {
            if (building.OsmObject.Element.Tags.TryGetValue("roof:levels", out string nLevels))
            {
                try
                {
                    this.nLevels = int.Parse(nLevels, CultureInfo.InvariantCulture);
                }
                catch (Exception exc)
                {
                    Debug.LogWarning(exc.Message + nLevels);
                }
            }
            return this.nLevels;
        }
    }

    public bool Orientation
    {
        get
        {
            if (building.OsmObject.Element.Tags.TryGetValue("roof:orientation", out string orientation))
            {
                if (orientation == "across" || orientation == "accross" || orientation == "acr")
                    isAcross = true;
                else
                    isAcross = false;
            }
            return isAcross;
        }
    }

    public float Direction
    {
        get
        {
            if (building.OsmObject.Element.Tags.TryGetValue("roof:direction", out string direction))
            {
                this.direction = GetDirectionAngle(direction);
            }
            return this.direction;
        }
    }

    public Material RoofMaterial
    {
        get
        {
            if (building.OsmObject.Element.Tags.TryGetValue("roof:material", out string strMaterial))
            {
                Material mat = Resources.Load<Material>("Materials/" + strMaterial);
                if (mat != null)
                    material = mat;
            }
            return material;
        }
    }

    public Color RoofColor
    {
        get
        {
            if (building.OsmObject.Element.Tags.TryGetValue("roof:colour", out string strColor))
            {
                Color? col = CityObject.Convert(strColor);
                color = col ?? Color.clear;
            }
            return color;
        }
    }

    //Along: ridge is perpendicular to the shortest side of the roof, across: ridge is perpendicular to the longest side of the roof
    //Ridge is always perpendicular to the Width
    public float Width
    {
        get
        {
            if (isAcross)
                return Mathf.Max(building.Length, building.Width);
            else
                return Mathf.Min(building.Length, building.Width);
        }
    }

    //Along: ridge is parallel to the longest side of the roof, across: ridge is parallel to the shortest side of the roof
    //Ridge is always parallel to the Length
    public float Length
    {
        get
        {
            if (isAcross)
                return Mathf.Min(building.Length, building.Width);
            else
                return Mathf.Max(building.Length, building.Width);
        }
    }

    public float Height
    {
        get
        {
            if (building.OsmObject.Element.Tags.TryGetValue("roof:height", out string strHeight) | building.OsmObject.Element.Tags.TryGetValue("building:roof:height", out string strHeight2))
            {
                float h = 0;
                try
                {
                    h = float.Parse(strHeight ?? strHeight2, CultureInfo.InvariantCulture);
                }
                catch (Exception exc)
                {
                    Debug.LogWarning(exc.Message + strHeight ?? strHeight2);
                }
                if (h == 0f)
                {
                    return GetHeight(slope);
                }
                else
                {
                    height = h * building.OsmObject.Loader.Main.yMeterScale;
                    return height;
                }
            }
            else
            {
                return GetHeight(slope);
            }
        }
    }

    public float Angle
    {
        get
        {
            if (building.OsmObject.Element.Tags.TryGetValue("roof:angle", out string strAngle))
            {
                try
                {
                    slope = float.Parse(strAngle.Replace("°", ""), CultureInfo.InvariantCulture);
                }
                catch (Exception exc)
                {
                    Debug.LogWarning(exc.Message + strAngle);
                }
            }
            return slope;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        // Add a material
        if (RoofMaterial == null)
            material = BuildingLoader.DefRoofMat;
        prevMat = material; prevColor = RoofColor; prevHeight = Height; prev_height = prevHeight; prevIsAcross = Orientation; prevSlope = Angle; prevDirection = Direction; prevShape = Shape;
        maxMidWidth = Width * Mathf.Sqrt(Width * Width + 4 * prev_height * prev_height) / (8 * prev_height);
        maxMidLength = Length * Mathf.Sqrt(Length * Length + 4 * prev_height * prev_height) / (8 * prev_height);

        if (building.OsmObject.Element.Type == OsmGeoType.Way)
        {
            UpdateShape();
            prev_height = height;
            UpdateRoof();
        }
        else
        {
            if (building.OsmObject.Element.Type == OsmGeoType.Node)
            {

            }
            else
            {
                // rooflines must be connected to buildings via relations
                CheckRoofLines();
            }
        }

    }

    // Update is called once per frame
    void Update()
    {
        //teste la validité des entrées
        if (height <= 0 || height >= building.Height)
            height = prev_height;
        if (hipHeight <= 0 || hipHeight >= height)
            hipHeight = prevHipHeight;
        if (hipLength < 0 || hipLength > Length / 2)
            hipLength = prevHipLength;
        if (midLength <= 0 || midLength > maxMidLength)
            midLength = prevMidLength;
        if (midWidth <= 0 || midWidth > maxMidWidth)
            midWidth = prevMidWidth;
        if (slope < 0 || slope > 90)
            slope = prevSlope;
        if (direction < 0 || direction > 360)
            direction = prevDirection;

        //met à jour la texture et la couleur si besoin
        if (material != null && prevMat != null && material.name != prevMat.name)
        {
            UpdateMaterial();
            prevMat = material;
        }
        if (color != prevColor)
        {
            UpdateColor(color);
            prevColor = color;
        }
        
        //met à jour la forme et l'orientation si besoin
        if (shape != prevShape || isAcross != prevIsAcross) {
            if (mesh != null) {
                Destroy(mesh.gameObject);
                if (building.Height > height)
                    building.UpdateMesh(height);
            }
            height = 1;
            prevHeight = Height;
            UpdateShape();
            prev_height = height;
            if (shape != RoofShape.None)
                UpdateRoof();
            prevShape = shape;
            prevIsAcross = isAcross;
        }

        prev_height = Height;
        if (prev_height != prevHeight)
        {
            prevHeight = prev_height;
        }
        prev_height = height;

    }

    public void SetBuilding(Building building)
    {
        this.building = building;
    }

    private void UpdateShape() {
        switch (shape)
        {
            case RoofShape.Gabled:
                hipLength = 0; prevHipLength = 0;
                goto case RoofShape.Hipped;
            case RoofShape.Pyramidal:
                hipLength = Length / 2; prevHipLength = hipLength;
                goto case RoofShape.Hipped;
            case RoofShape.Hipped:
                mesh = ShapeGenerator.GeneratePrism(PivotLocation.Center, new Vector3(Width, height, Length));
                ToHipped(hipLength);
                break;
            case RoofShape.HalfHipped:
                mesh = ShapeGenerator.GeneratePrism(PivotLocation.Center, new Vector3(Width, height, Length));
                ToHalfHipped(hipLength, hipHeight);
                break;
            case RoofShape.Gambrel:
                mesh = ShapeGenerator.GeneratePrism(PivotLocation.Center, new Vector3(Width, height, Length));
                ToGambrel(hipHeight, midWidth);
                break;
            case RoofShape.Mansard:
                mesh = ShapeGenerator.GeneratePrism(PivotLocation.Center, new Vector3(Width, height, Length));
                ToMansard(hipLength, hipHeight, midWidth, midLength);
                break;
            case RoofShape.Skillion:
                ToSkillion();
                break;
            case RoofShape.Flat:
                height = 0.01f; prevHeight = Height;
                mesh = ShapeGenerator.GeneratePlane(PivotLocation.Center, building.Width, building.Length, 0, 0, Axis.Up);
                mesh.DuplicateAndFlip(mesh.faces.ToArray());
                break;
            case RoofShape.Round:
                height = Width / 2; prevHeight = Height;
                ToRound(10);
                break;
            case RoofShape.Dome:
                height = Width / 2; prevHeight = Height;
                ToDome(5);
                break;
            case RoofShape.None:
                // in this case, it doesn't have a roof
                building.UpdateRoofMaterial(material);
                break;
        }
    }

    private float GetHeight(float angle)
    {
        if (angle != 0f || angle != 90f || angle != 180f || angle != 270f)
            return Mathf.Abs(Mathf.Tan(angle * Mathf.Deg2Rad)) * Width / 2;
        else
            return height * building.OsmObject.Loader.Main.yMeterScale * NLevels;
    }

    private void ToDome(int subDiv)
    {
        mesh = ShapeGenerator.GenerateIcosahedron(PivotLocation.Center, height, subDiv, false);
        var facesBelow = from face in mesh.faces
                         where mesh.positions[face.distinctIndexes[0]].y <= 0 && mesh.positions[face.distinctIndexes[1]].y <= 0 && mesh.positions[face.distinctIndexes[2]].y <= 0
                         select face;
        mesh.DeleteFaces(facesBelow);
        List<Face> faces = new List<Face>(mesh.faces);
        var nullPosInd = from pos in mesh.positions
                         where pos.y == 0f
                         select mesh.positions.IndexOf(pos);
        Face baseFace = mesh.CreatePolygon(nullPosInd.ToList(), true);
        faces.Add(baseFace);
        mesh.faces = faces;
        mesh.DuplicateAndFlip(mesh.faces.ToArray());
        mesh.ToMesh();
        mesh.Refresh();
    }

    private void ToRound(float radialCutsMult)
    {
        mesh = ShapeGenerator.GenerateArch(PivotLocation.Center, 180, height, height, Length, (int)(height * radialCutsMult), false, true, true, true, true);
        mesh.DuplicateAndFlip(mesh.faces.ToArray());
        var archFaces = from face in mesh.faces
                        where face.distinctIndexes.Count == 4
                        select face;
        MeshRenderer renderer = mesh.GetComponent<MeshRenderer>();
        renderer.sharedMaterials = new Material[2]
        {
            BuildingLoader.DefBuildingMat, material
        };
        foreach (Face face in archFaces)
            face.submeshIndex = 1;
        mesh.ToMesh();
        mesh.Refresh();
    }

    private void ToHipped(float hipL)
    {
        if (mesh == null || hipL == 0f)
            return;
        var ridgeVertices = from sv in mesh.sharedVertices
                            where mesh.positions[sv[0]].y == height / 2
                            select sv;

        if (ridgeVertices.Count() != 2)
            return;
        float x1 = mesh.positions[ridgeVertices.First()[0]].x, x2 = mesh.positions[ridgeVertices.Last()[0]].x;
        float z1 = mesh.positions[ridgeVertices.First()[0]].z, z2 = mesh.positions[ridgeVertices.Last()[0]].z;
        float norm = Mathf.Sqrt((x2 - x1) * (x2 - x1) + (z2 - z1) * (z2 - z1));
        Vector3 hipVect = new Vector3(hipL / norm * (x2 - x1), 0, hipL / norm * (z2 - z1));
        mesh.TranslateVertices(ridgeVertices.First(), hipVect);
        mesh.TranslateVertices(ridgeVertices.Last(), -hipVect);
        mesh.DuplicateAndFlip(mesh.faces.ToArray());
        mesh.ToMesh();
        mesh.Refresh();
    }

    private void ToHalfHipped(float hipL, float hipH)
    {
        if (mesh == null || hipL == 0f)
            return;
        CreateMidVertices(0, hipH);

        //get the hipped roof shape
        ToHipped(hipL);
    }

    private void CreateMidVertices(float hipL, float hipH)
    {
        var baseVertices = from sv in mesh.sharedVertices
                           where mesh.positions[sv[0]].y == -height / 2
                           select mesh.positions[sv[0]];
        if (baseVertices.Count() != 4)
            return;
        List<Vector3> newPos = new List<Vector3>();
        foreach (Vector3 v in baseVertices)
        {
            newPos.Add(new Vector3(v.x - hipH * Width / (2 * height) * Mathf.Sign(v.x), v.y + hipH, v.z - hipH * hipL / height * Mathf.Sign(v.z)));
        }
        List<Face> faces = new List<Face>(mesh.faces);
        for (int i = 0; i < mesh.faceCount; i++)
        {
            var ridgeVerticesFace = from ind in mesh.faces[i].distinctIndexes
                                    where mesh.positions[ind].y == height / 2
                                    select mesh.positions[ind];
            if (!ridgeVerticesFace.Any())
                continue;
            var baseVerticesFace = from ind in mesh.faces[i].distinctIndexes
                                   where mesh.positions[ind].y == -height / 2
                                   select mesh.positions[ind];
            IEnumerable<Vector3> newVerticesFace;
            if (mesh.faces[i].distinctIndexes.Count == 3)
            {
                newVerticesFace = from pos in newPos
                                  where pos.z >= Mathf.Min(ridgeVerticesFace.First().z, baseVerticesFace.First().z) && pos.z <= Mathf.Max(ridgeVerticesFace.First().z, baseVerticesFace.First().z)
                                  select pos;
            }
            else
            {
                newVerticesFace = from pos in newPos
                                  where pos.x > Mathf.Min(ridgeVerticesFace.First().x, baseVerticesFace.First().x) && pos.x < Mathf.Max(ridgeVerticesFace.First().x, baseVerticesFace.First().x)
                                  select pos;
            }
            Face newFace = mesh.AppendVerticesToFace(mesh.faces[i], newVerticesFace.ToArray());
            faces[i] = newFace;
            mesh.faces = faces;
        }
    }

    private void ToSkillion()
    {
        Vector3[] vertices = new Vector3[6]
        {
            new Vector3(0, 0, 0),
            new Vector3(Width, 0, 0),
            new Vector3(0, height, 0),
            new Vector3(Width, 0, Length),
            new Vector3(0, height, Length),
            new Vector3(0, 0, Length)
        };

        Face[] tris = new Face[]
        {   new Face(new int[]
            {
            // back triangle face
            0, 1, 2,
            }), new Face(new int[]
            {
            // front triangle face
            3, 4, 5,
            }), new Face(new int[]
            {
            //steep rectangle face
            1, 2, 3, 2, 4, 3,
            }), new Face(new int[]
            {
            //straight rectangle face
            0, 2, 5, 2, 4, 5
            }), new Face(new int[]
            {
            //underneath rectange face
            3, 1, 5, 1, 0, 5
            })
        };

        mesh = ProBuilderMesh.Create(vertices, tris);
        mesh.SetPivot(new Vector3(Width / 2, height / 2, Length / 2));
        mesh.DuplicateAndFlip(tris);
        mesh.ToMesh();
        mesh.Refresh();
    }

    private void ToMansard(float hipL, float hipH, float midW, float midL)
    {
        if (mesh == null || hipL == 0f || midL == 0f)
            return;
        //get the hipped roof shape
        ToHipped(hipL);
        CreateMidVertices(hipL, hipH);
        TranslateMidVertices(hipL, midW, midL);
    }

    private void ToGambrel(float hipH, float midW)
    {
        if (mesh == null || midW == 0f)
            return;
        CreateMidVertices(0, hipH);
        TranslateMidVertices(0, midW, 0);
    }

    private void TranslateMidVertices(float hipL, float midW, float midL)
    {
        if (mesh == null || midW == 0f && midL == 0f)
            return;
        var midVertices = from sv in mesh.sharedVertices
                          where mesh.positions[sv[0]].y > -height / 2 && mesh.positions[sv[0]].y < height / 2
                          select sv;

        float wNorm = Mathf.Sqrt(4 * height * height + Width * Width), lNorm = Mathf.Sqrt(height * height + hipL * hipL);
        foreach (var midVertex in midVertices)
        {
            float sx = Mathf.Sign(mesh.positions[midVertex[0]].x), sz = Mathf.Sign(mesh.positions[midVertex[0]].z);
            Vector3 midVect = new Vector3(2 * height * midW / wNorm * sx, midW * Width / wNorm + midL * hipL / lNorm, midL * height / lNorm * sz);
            mesh.TranslateVertices(midVertex, midVect);
            float x = mesh.positions[midVertex[0]].x, y = mesh.positions[midVertex[0]].y, z = mesh.positions[midVertex[0]].z;
            if (x < -Width / 2 || x > Width / 2)
                x = x < -Width / 2 ? -Width / 2 : Width / 2;
            if (y < -height / 2 || y > height / 2)
                y = y < -height / 2 ? -height / 2 : height / 2;
            if (z < -Length / 2 || z > Length / 2)
                z = z < -Length / 2 ? -Length / 2 : Length / 2;
            mesh.SetSharedVertexPosition(mesh.sharedVertices.IndexOf(midVertex), new Vector3(x, y, z));
        }
        mesh.DuplicateAndFlip(mesh.faces.ToArray());
        mesh.ToMesh();
        mesh.Refresh();
    }

    private void UpdateRoof()
    {
        if (mesh == null)
            return;
        // Firstly, we update the height of the building facade
        if (building.Height > height)
            building.UpdateMesh(-height);
        if (building.OsmObject.Loader.Main.hideMeshInHierarchy)
            mesh.hideFlags = HideFlags.HideInHierarchy;
        else
        {
            mesh.transform.SetParent(((BuildingLoader)building.OsmObject.Loader).RoofMeshes.transform);
            mesh.gameObject.hideFlags = HideFlags.NotEditable;
        }
        mesh.name = "ID = " + building.OsmObject.Element.Id;
        // Update the texture and the color
        if (shape != RoofShape.Round)
            UpdateMaterial();
        if (color != Color.clear)
            UpdateColor(color);
        // Update the mesh position
        Vector3 pos = (Vector3)building.Center;
        if (shape == RoofShape.Dome)
            pos.y = building.Height > height ? building.Height - height : building.Height;
        else
            pos.y = building.Height > height ? building.Height - height / 2f : building.Height + height / 2f;
        mesh.transform.position = pos;
        // Update the mesh rotation
        float preAngle;
        if (shape != RoofShape.Flat && shape != RoofShape.Dome && Length == building.Length)
            preAngle = 90;
        else
            preAngle = 0;
        mesh.transform.rotation = Quaternion.Euler(0, preAngle + direction, 0);
    }

    private void UpdateMaterial()
    {
        if (mesh == null)
            return;
        MeshRenderer renderer = mesh.gameObject.GetComponent<MeshRenderer>();
        if (material != null)
            renderer.material = material;
    }

    private void UpdateColor(Color col)
    {
        if (mesh == null)
            return;
        MeshRenderer renderer = mesh.gameObject.GetComponent<MeshRenderer>();
        renderer.material.color = col;
    }

    private float GetDirectionAngle(string dir)       //retourne l'angle de direction auquel la face principale du toit fait face en degrés
    {
        try
        {
            return float.Parse(dir, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return dir switch
            {
                "N" => 0,
                "E" => 90,
                "S" => 180,
                "W" => 270,
                "NE" => 45,
                "SE" => 135,
                "SW" => 225,
                "NW" => 315,
                "NNE" => 22.5f,
                "ENE" => 67.5f,
                "ESE" => 112.5f,
                "SSE" => 157.5f,
                "SSW" => 202.5f,
                "WSW" => 247.5f,
                "WNW" => 292.5f,
                "NNW" => 337.5f,
                _ => 0,
            };
        }
    }

    private void CheckRoofLines()
    {
        List<Node> buildingNodes = new List<Node>();
        List<Way> ridges = new List<Way>(), edges = new List<Way>();
        foreach (var member in ((Relation)building.OsmObject.Element).Members)
        {
            if (member.Type == OsmGeoType.Way)
            {
                var ways = from way in building.OsmObject.SubWays
                           where way.Id == member.Id
                           select way;
                if (ways.Any())
                {
                    if (ways.First().Tags.TryGetValue("roof:ridge", out string _) || ways.First().Tags.TryGetValue("building:roof:ridge", out string _))
                    {
                        ridges.Add(ways.First());
                    }
                    else
                    {
                        if (ways.First().Tags.TryGetValue("roof:edge", out string _) || ways.First().Tags.TryGetValue("building:roof:edge", out string _))
                        {
                            edges.Add(ways.First());
                        }
                        else
                        {
                            var nodes = from node in building.OsmObject.SubNodes
                                        where node.Id != null && ways.First().Nodes.ToList().Contains((long)node.Id)
                                        select node;
                            buildingNodes.AddRange(nodes);
                        }
                    }
                }
            }
        }
        if (ridges.Any() && edges.Any() && buildingNodes.Any())
            BuildFromRoofLines(ridges, edges, buildingNodes);
    }

    private void BuildFromRoofLines(List<Way> ridges, List<Way> edges, List<Node> buildingNodes)
    {
        Dictionary<long, int> meshInd = new Dictionary<long, int>();
        List<Vector3> meshPos = new List<Vector3>();
        List<Face> roofFaces = new List<Face>();

        //find positions for the roof base, without any duplicates
        var ceilingPos = from pos in building.MeshPos
                         where pos.y != 0f
                         select pos;
        meshPos.AddRange(ceilingPos.ToArray());
        Dictionary<long, Node[]> visitedEdges = new Dictionary<long, Node[]>();
        List<Node> sortedEdgeNodes = new List<Node>(), visitedBuildingNodes = new List<Node>();
        Vector3[] nodePos = building.GetNodePositions(buildingNodes.ToArray());
        for (int i = 0; i < meshPos.Count; i++)
        {
            for (int j = 0; j < nodePos.Length; j++)
            {
                if (nodePos[j].x == meshPos[i].x && nodePos[j].z == meshPos[i].z)
                {
                    meshInd.Add((long)buildingNodes[j].Id, i);
                    break;
                }
            }
        }

        //find connected edges for each ridge
        foreach (var ridge in ridges)
        {
            var ridgeNodes = from node in building.OsmObject.SubNodes
                             where node.Id != null && ridge.Nodes.ToList().Contains((long)node.Id)
                             select node;
            Vector3[] ridgePos = building.GetNodePositions(ridgeNodes.ToArray());
            float h = building.Height;
            for (int i = 0; i < ridgePos.Length; i++)
            {
                meshPos.Add(new Vector3(ridgePos[i].x, h, ridgePos[i].z));
                meshInd.Add((long)ridgeNodes.ElementAt(i).Id, meshPos.Count - 1);
            }
            foreach (var ridgeNode in ridgeNodes)
            {
                foreach (var edge in edges)
                {
                    sortedEdgeNodes.Clear();
                    if (edge.Id == null || visitedEdges.ContainsKey((long)edge.Id))
                        continue;
                    var edgeNodes = from node in building.OsmObject.SubNodes
                                    where node.Id != null && edge.Nodes.ToList().Contains((long)node.Id)
                                    select node;
                    //test whether the edge contains the current ridge node or not and if yes, store it at the beginning of the list of sorted nodes
                    //also store the remaining edge nodes which belong to the building at the end of the list
                    bool hasRidgeNode = false;
                    foreach (var edgeNode in edgeNodes)
                    {
                        if (edgeNode.Id == ridgeNode.Id)
                        {
                            hasRidgeNode = true;
                            sortedEdgeNodes.Insert(0, edgeNode);
                        }
                        else
                        {
                            if (buildingNodes.Select(node => node.Id).Contains(edgeNode.Id))
                            {
                                sortedEdgeNodes.Add(edgeNode);
                            }
                        }
                    }
                    //if true, we create a new face to add to the roof mesh by finding a visited edge already connected to it
                    //then we store all sorted nodes (2 at minimum) with the edge id in the dictionary of visited edges
                    if (hasRidgeNode && sortedEdgeNodes.Count > 1)
                    {
                        foreach (var sortedEdgeNode in sortedEdgeNodes)
                        {
                            if (sortedEdgeNode == sortedEdgeNodes[0])
                                continue;
                            for (int i = 0; i < buildingNodes.Count; i++)
                            {
                                if (buildingNodes[i].Id == sortedEdgeNode.Id)
                                {
                                    foreach (var visitedEdge in visitedEdges)
                                    {
                                        foreach (var visitedEdgeNode in visitedEdge.Value)
                                        {
                                            if (visitedEdgeNode == visitedEdge.Value[0])
                                                continue;
                                            if (i > 0 && visitedEdgeNode.Id == buildingNodes[i - 1].Id && !(visitedBuildingNodes.Contains(buildingNodes[i]) && visitedBuildingNodes.Contains(buildingNodes[i - 1])))
                                            {
                                                if (!visitedBuildingNodes.Contains(buildingNodes[i]))
                                                    visitedBuildingNodes.Add(buildingNodes[i]);
                                                if (!visitedBuildingNodes.Contains(buildingNodes[i - 1]))
                                                    visitedBuildingNodes.Add(buildingNodes[i - 1]);
                                                CreateFace(visitedEdgeNode, sortedEdgeNode, visitedEdge.Value[0], ridgeNode, meshInd, roofFaces);
                                            }
                                            else
                                            {
                                                if (i < buildingNodes.Count - 1 && visitedEdgeNode.Id == buildingNodes[i + 1].Id && !(visitedBuildingNodes.Contains(buildingNodes[i]) && visitedBuildingNodes.Contains(buildingNodes[i + 1])))
                                                {
                                                    if (!visitedBuildingNodes.Contains(buildingNodes[i]))
                                                        visitedBuildingNodes.Add(buildingNodes[i]);
                                                    if (!visitedBuildingNodes.Contains(buildingNodes[i + 1]))
                                                        visitedBuildingNodes.Add(buildingNodes[i + 1]);
                                                    CreateFace(visitedEdgeNode, sortedEdgeNode, visitedEdge.Value[0], ridgeNode, meshInd, roofFaces);
                                                }
                                            }
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                        visitedEdges.Add((long)edge.Id, sortedEdgeNodes.ToArray());
                    }
                }
            }
        }
        mesh = ProBuilderMesh.Create(meshPos, roofFaces);
    }

    private void CreateFace(Node e1, Node e2, Node r1, Node r2, Dictionary<long, int> meshInd, List<Face> roofFaces)     //create a face from 2 edge nodes and 2 ridge nodes
    {
        if (meshInd.TryGetValue((long)e1.Id, out int e1ind) && meshInd.TryGetValue((long)e2.Id, out int e2ind) && meshInd.TryGetValue((long)r1.Id, out int r1ind) && meshInd.TryGetValue((long)r2.Id, out int r2ind))
        {
            if (r1ind == r2ind)
            {
                roofFaces.Add(new Face(new int[] { e1ind, e2ind, r1ind }));
            }
            else
            {
                roofFaces.Add(new Face(new int[] { e1ind, e2ind, r1ind, e2ind, r2ind, r1ind }));
            }
        }
    }

}
