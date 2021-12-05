using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

public class QuadCreator : MonoBehaviour
{
    private float width = 1;
    private float height = 1;
    private float length = 3, radius = 5;
    private float hipWidth = 0.5f;
    private ProBuilderMesh mesh;

    public void Start()
    {
        //mesh = ShapeGenerator.GeneratePrism(PivotLocation.Center, new Vector3(width, height, length));
        //ToHalfHipped(hipWidth);
        mesh = ShapeGenerator.GenerateArch(PivotLocation.Center, 180, radius, radius, length, (int)(radius*10), false, true, true, true, true);
        mesh.DuplicateAndFlip(mesh.faces.ToArray());
        var archFaces = from face in mesh.faces
                        where face.distinctIndexes.Count == 4
                        select face;
        MeshRenderer renderer = mesh.GetComponent<MeshRenderer>();
        renderer.sharedMaterials = new Material[2]
        {
            Resources.Load<Material>("Materials/building"), Resources.Load<Material>("Materials/roof")
        };
        foreach (Face face in archFaces)
            face.submeshIndex = 1;
        mesh.ToMesh();
        mesh.Refresh();

        /*
        mesh = ShapeGenerator.GenerateIcosahedron(PivotLocation.Center, radius, 5, false);
        
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
        mesh.SetMaterial(mesh.faces, Resources.Load<Material>("Materials/glass"));
        */
    }

    private void ToHipped(float w)
    {
        if (mesh == null || w == 0f)
            return;
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

    private void DisplayFaceVertices(Face face)
    {
        Debug.Log("Face with " + face.distinctIndexes.Count + " vertices :");
        foreach (int ind in face.distinctIndexes)
        {
            Debug.Log("x = " + mesh.positions[ind].x + ", y = " + mesh.positions[ind].y + ", z = " + mesh.positions[ind].z);
        }
    }

}
