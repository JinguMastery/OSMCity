using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

public class Roof : MonoBehaviour
{

    public string shape, orientation, direction;
    public Material material;
    public int nLevels = 1;
    public Color color = Color.clear;
    public float height = 1, angle, hipWidth = 1;

    private Building building;      //The building this roof is attached to
    private ProBuilderMesh mesh;

    public string Shape
    {
        get
        {
            if (building.OsmObject.Element.Tags.TryGetValue("roof:shape", out string shape) | building.OsmObject.Element.Tags.TryGetValue("building:roof:shape", out string shape2))
            {
                this.shape = shape ?? shape2;
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

    public string Orientation
    {
        get
        {
            if (building.OsmObject.Element.Tags.TryGetValue("roof:orientation", out string orientation))
            {
                this.orientation = orientation;
                if (orientation == "accross")
                    this.orientation = "across";
            }
            return this.orientation;
        }
    }

    public string Direction
    {
        get
        {
            if (building.OsmObject.Element.Tags.TryGetValue("roof:direction", out string direction))
            {
                this.direction = direction;
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

    //along: ridge is perpendicular to the shortest side of the roof, across: ridge is perpendicular to the longest side of the roof
    public float Width
    {
        get
        {
            if (Orientation == "across")
                return Mathf.Max(building.Length, building.Width);
            else
                return Mathf.Min(building.Length, building.Width);
        }
    }

    //along: ridge is parallel to the longest side of the roof, across: ridge is parallel to the shortest side of the roof
    public float Length
    {
        get
        {
            if (Orientation == "across")
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
                    if (Angle != 0f)
                        return Mathf.Tan(angle) * Width / 2;
                    else
                        return height * building.OsmObject.Loader.Main.yMeterScale * NLevels;
                }
                else
                {
                    height = h * building.OsmObject.Loader.Main.yMeterScale;
                    return height;
                }
            }
            else
            {
                if (Angle != 0f)
                    return Mathf.Tan(angle) * Width / 2;
                else
                    return height * building.OsmObject.Loader.Main.yMeterScale * NLevels;
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
                    angle = float.Parse(strAngle, CultureInfo.InvariantCulture);
                }
                catch (Exception exc)
                {
                    Debug.LogWarning(exc.Message + strAngle);
                }
            }
            return angle;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        // Add a material
        _ = RoofMaterial;
        if (material == null)
            material = BuildingLoader.DefRoofMat;

        if (building.OsmObject.Element.Type == OsmGeoType.Way)
        {
            switch (Shape)
            {
                case "pitched":
                case "gabled":
                    hipWidth = 0;
                    goto case "hipped";
                case "pyramidal":
                    hipWidth = Length / 2;
                    goto case "hipped";
                case "hipped":
                    mesh = ShapeGenerator.GeneratePrism(PivotLocation.Center, new Vector3(Width, Height, Length));
                    ToHipped(hipWidth);
                    UpdateRoof();
                    break;
                case "skillion":
                    ToSkillion();
                    UpdateRoof();
                    break;
                case "half-hipped":
                    mesh = ShapeGenerator.GeneratePrism(PivotLocation.Center, new Vector3(Width, Height, Length));
                    ToHalfHipped(hipWidth);
                    UpdateRoof();
                    break;
                case "flat":
                    height = 0.01f;
                    mesh = ShapeGenerator.GeneratePlane(PivotLocation.Center, building.Width, building.Length, 0, 0, Axis.Up);
                    mesh.DuplicateAndFlip(mesh.faces.ToArray());
                    UpdateRoof();
                    break;
                case "round":
                    height = Width / 2;
                    ToRound(10);
                    UpdateRoof();
                    break;
                case "dome":
                    height = Width / 2;
                    ToDome(5);
                    UpdateRoof();
                    break;
                default:
                    // in this case, it doesn't have a roof
                    building.UpdateRoofMaterial(material);
                    break;
            }
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
        
    }

    public void SetBuilding(Building building)
    {
        this.building = building;
    }

    private void ToDome(int subDiv)
    {
        mesh = ShapeGenerator.GenerateIcosahedron(PivotLocation.Center, Width / 2, subDiv, false);
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
        mesh = ShapeGenerator.GenerateArch(PivotLocation.Center, 180, Width / 2, Width / 2, Length, (int)(Width / 2 * radialCutsMult), false, true, true, true, true);
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

    private void ToHipped(float w)
    {
        if (mesh == null)
            return;
        if (w > 0)
        {
            var ridgeVertices = from sv in mesh.sharedVertices
                                where mesh.positions[sv[0]].y > 0
                                select sv;
            if (ridgeVertices.Count() != 2)
                return;
            float x1 = mesh.positions[ridgeVertices.First()[0]].x, x2 = mesh.positions[ridgeVertices.Last()[0]].x;
            float z1 = mesh.positions[ridgeVertices.First()[0]].z, z2 = mesh.positions[ridgeVertices.Last()[0]].z;
            float magnitude = Mathf.Sqrt((x2 - x1) * (x2 - x1) + (z2 - z1) * (z2 - z1));
            Vector3 hipVect = new Vector3(w / magnitude * (x2 - x1), 0, w / magnitude * (z2 - z1));
            mesh.TranslateVertices(ridgeVertices.First(), hipVect);
            mesh.TranslateVertices(ridgeVertices.Last(), -hipVect);
        }
        mesh.DuplicateAndFlip(mesh.faces.ToArray());
        mesh.ToMesh();
        mesh.Refresh();
    }

    private void ToHalfHipped(float w)
    {
        if (mesh == null || w == 0f)
            return;
        var baseVertices = from sv in mesh.sharedVertices
                           where mesh.positions[sv[0]].y < 0
                           select mesh.positions[sv[0]];
        if (baseVertices.Count() != 4)
            return;
        List<Vector3> newPos = new List<Vector3>();
        foreach (Vector3 v in baseVertices)
        {
            newPos.Add(new Vector3(v.x / 2, 0, v.z));
        }
        List<Face> faces = new List<Face>(mesh.faces);
        for (int i = 0; i < mesh.faceCount; i++)
        {
            if (mesh.faces[i].distinctIndexes.Count == 3)
            {
                var ridgeVerticesFace = from ind in mesh.faces[i].distinctIndexes
                                        where mesh.positions[ind].y > 0
                                        select mesh.positions[ind];
                var newVerticesFace = from pos in newPos
                                      where ridgeVerticesFace.First().z == pos.z
                                      select pos;
                Face newFace = mesh.AppendVerticesToFace(mesh.faces[i], newVerticesFace.ToArray());
                faces[i] = newFace;
                mesh.faces = faces;
            }
            else
            {
                if (mesh.faces[i].distinctIndexes.Count == 4)
                {
                    var ridgeVerticesFace = from ind in mesh.faces[i].distinctIndexes
                                            where mesh.positions[ind].y > 0
                                            select mesh.positions[ind];
                    if (ridgeVerticesFace.Any())
                    {
                        var baseVerticesFace = from ind in mesh.faces[i].distinctIndexes
                                               where mesh.positions[ind].y < 0
                                               select mesh.positions[ind];
                        var newVerticesFace = from pos in newPos
                                              where pos.x > Mathf.Min(ridgeVerticesFace.First().x, baseVerticesFace.First().x) && pos.x < Mathf.Max(ridgeVerticesFace.First().x, baseVerticesFace.First().x)
                                              select pos;
                        Face newFace = mesh.AppendVerticesToFace(mesh.faces[i], newVerticesFace.ToArray());
                        faces[i] = newFace;
                        mesh.faces = faces;
                    }
                }
            }
        }
        //get the hipped roof shape
        ToHipped(w);
    }

    private void ToSkillion()
    {
        Vector3[] vertices = new Vector3[6]
        {
            new Vector3(0, 0, 0),
            new Vector3(Width, 0, 0),
            new Vector3(0, Height, 0),
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

    private void UpdateRoof()
    {
        // Firstly, we update the height of the building facade
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
        if (shape != "round")
            UpdateMaterial();
        if (RoofColor != Color.clear)
            UpdateColor(color);
        // Update the mesh position
        Vector3 pos = (Vector3)building.Center;
        pos.y = building.Height - height / 2f;
        mesh.transform.position = pos;
        // Update the mesh rotation
        float angle = GetDirectionAngle(), preAngle;
        if (shape != "flat" && shape != "dome" && Length == building.Length)
            preAngle = 90;
        else
            preAngle = 0;
        if (!float.IsNaN(angle))
            mesh.transform.rotation = Quaternion.Euler(0, preAngle + angle, 0);
        else
            mesh.transform.rotation = Quaternion.Euler(0, preAngle, 0);
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

    private float GetDirectionAngle()       //retourne l'angle de direction auquel la face principale du toit fait face en degrés
    {
        try
        {
            return float.Parse(Direction, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return direction switch
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
                _ => float.NaN,
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
