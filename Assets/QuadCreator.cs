using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UIElements;

public class QuadCreator : MonoBehaviour
{
    private float width = 1;
    private float height = 1;
    private float length = 3, radius = 5;
    private float hipLength = 0.5f, midWidth = 0.1f, midLength = 0.1f, hipHeight = 0.25f;
    private ProBuilderMesh mesh;

    public void Start()
    {
        //mesh = ShapeGenerator.GeneratePrism(PivotLocation.Center, new Vector3(width, height, length));
        //ToHippedAndGabled(hipLength, hipHeight, midWidth);
        ToCone(5);
        mesh.SetMaterial(mesh.faces, Resources.Load<Material>("Materials/glass"));
    }

    private void ToCone(int subDiv)
    {
        mesh = ShapeGenerator.GenerateCone(PivotLocation.Center, width / 2, height, subDiv);
        mesh.DuplicateAndFlip(mesh.faces.ToArray());
        mesh.ToMesh();
        mesh.Refresh();
    }

    private void ToRound()
    {
        mesh = ShapeGenerator.GenerateArch(PivotLocation.Center, 180, radius, radius, length, (int)(radius * 10), false, true, true, true, true);
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
    }

    private void ToDome()
    {
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
    }

    private void ToSaltbox(float hipL, float hipH)
    {
        mesh = ShapeGenerator.GeneratePrism(PivotLocation.Center, new Vector3(width, height, length));
        if (hipH > 0)
        {
            CreateMidVertices(0, height / 2, false);

            //move the left side of the gabled roof higher
            var midVertices = from sv in mesh.sharedVertices
                              where mesh.positions[sv[0]].y == 0
                              select sv;
            if (midVertices.Count() != 4)
                return;
            foreach (SharedVertex sv in midVertices)
            {
                mesh.TranslateVertices(sv, new Vector3(mesh.positions[sv[0]].x, hipH - height / 2, 0));
            }
        }
        var ridgeVertices = from sv in mesh.sharedVertices
                            where mesh.positions[sv[0]].x == 0
                            select sv;
        if (ridgeVertices.Count() != 2)
            return;
        foreach (SharedVertex sv in ridgeVertices)
        {
            mesh.TranslateVertices(sv, new Vector3(hipL - width / 2, 0, 0));
        }
        mesh.DuplicateAndFlip(mesh.faces.ToArray());
        mesh.ToMesh();
        mesh.Refresh();
    }

    private void ToDoubleSaltbox(float hipL, float hipH)
    {
        mesh = ShapeGenerator.GeneratePrism(PivotLocation.Center, new Vector3(width, height, length));
        if (hipH > 0)
        {
            CreateMidVertices(0, height / 2);

            //move the left side of the gabled roof higher
            var midVerticesLeft = from sv in mesh.sharedVertices
                                  where mesh.positions[sv[0]].y == 0 && mesh.positions[sv[0]].x < 0
                                  select sv;
            if (midVerticesLeft.Count() != 4)
                return;
            foreach (SharedVertex sv in midVerticesLeft)
            {
                mesh.TranslateVertices(sv, new Vector3(mesh.positions[sv[0]].x, hipH - height / 2, 0));
            }
        }
        else
        {
            CreateMidVertices(0, height / 2, true);
        }
        var ridgeVertices = from sv in mesh.sharedVertices
                            where mesh.positions[sv[0]].x == 0
                            select sv;
        if (ridgeVertices.Count() != 2)
            return;
        foreach (SharedVertex sv in ridgeVertices)
        {
            mesh.TranslateVertices(sv, new Vector3(hipL - width / 2, 0, 0));
        }
        var midVerticesRight = from sv in mesh.sharedVertices
                               where mesh.positions[sv[0]].y == 0 && mesh.positions[sv[0]].x > 0
                               select sv;
        if (midVerticesRight.Count() != 4)
            return;
        if (hipL < width / 2)
        {
            foreach (SharedVertex sv in midVerticesRight)
            {
                mesh.TranslateVertices(sv, new Vector3(-mesh.positions[sv[0]].x + width / 2 - hipL, height / 2, 0));
            }
        }
        else
        {
            foreach (SharedVertex sv in midVerticesRight)
            {
                mesh.TranslateVertices(sv, new Vector3(-mesh.positions[sv[0]].x + hipL / 2, height / 2, 0));
            }
        }
        mesh.DuplicateAndFlip(mesh.faces.ToArray());
        mesh.ToMesh();
        mesh.Refresh();
    }

    private void ToHippedAndGabled(float hipL, float hipH, float midW)
    {
        if (mesh == null || hipL == 0f)
            return;
        //get the hipped roof shape
        ToHipped(hipL);
        CreateMidVertices(hipL, hipH);
        TranslateMidVertices(hipL, -midW, 0);
        var midVertices = from sv in mesh.sharedVertices
                          where mesh.positions[sv[0]].y > -height / 2 && mesh.positions[sv[0]].y < height / 2
                          select sv;
        Vector3 midVect = new Vector3(0, 0, hipL * (1 - hipH / height));
        foreach (var midVertex in midVertices)
        {
            if (mesh.positions[midVertex[0]].z < 0)
                mesh.TranslateVertices(midVertex, midVect);
            else
                mesh.TranslateVertices(midVertex, -midVect);
        }
    }

    private void ToGabledHeightMoved(float hipH)
    {
        mesh = ShapeGenerator.GeneratePrism(PivotLocation.Center, new Vector3(width, height, length));
        if (hipH > 0)
        {
            CreateMidVertices(0, height / 2, true);

            //move the right side of the gabled roof higher
            var ridgeVertices = from sv in mesh.sharedVertices
                                where mesh.positions[sv[0]].y > 0
                                select sv;
            var midVertices = from sv in mesh.sharedVertices
                              where mesh.positions[sv[0]].y == 0
                              select sv;
            if (ridgeVertices.Count() != 2 || midVertices.Count() != 4)
                return;
            float ridgeHeight = mesh.positions[ridgeVertices.First()[0]].y;
            foreach (SharedVertex sv in ridgeVertices)
            {
                mesh.TranslateVertices(sv, new Vector3(0, hipH, 0));
            }
            foreach (SharedVertex sv in midVertices)
            {
                mesh.TranslateVertices(sv, new Vector3(-mesh.positions[sv[0]].x, ridgeHeight, 0));
            }
        }
        mesh.DuplicateAndFlip(mesh.faces.ToArray());
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

        float wNorm = Mathf.Sqrt(4 * height * height + width * width), lNorm = Mathf.Sqrt(height * height + hipL * hipL);
        foreach (var midVertex in midVertices)
        {
            float sx = Mathf.Sign(mesh.positions[midVertex[0]].x), sz = Mathf.Sign(mesh.positions[midVertex[0]].z);
            Vector3 midVect = new Vector3(2 * height * midW / wNorm * sx, midW * width / wNorm + midL * hipL / lNorm, midL * height / lNorm * sz);
            mesh.TranslateVertices(midVertex, midVect);
            float x = mesh.positions[midVertex[0]].x, y = mesh.positions[midVertex[0]].y, z = mesh.positions[midVertex[0]].z;
            if (x < -width / 2 || x > width / 2)
                x = x < -width / 2 ? -width / 2 : width / 2;
            if (y < -height / 2 || y > height / 2)
                y = y < -height / 2 ? -height / 2 : height / 2;
            if (z < -length / 2 || z > length / 2)
                z = z < -length / 2 ? -length / 2 : length / 2;
            mesh.SetSharedVertexPosition(mesh.sharedVertices.IndexOf(midVertex), new Vector3(x, y, z));
        }
        mesh.DuplicateAndFlip(mesh.faces.ToArray());
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

    private void ToSideHipped(float hipL)
    {
        if (mesh == null || hipL == 0f)
            return;
        var ridgeVertex = from sv in mesh.sharedVertices
                          where mesh.positions[sv[0]].y > 0 && mesh.positions[sv[0]].z < 0
                          select sv;
        if (ridgeVertex.Count() != 1)
            return;
        mesh.TranslateVertices(ridgeVertex.First(), new Vector3(0, 0, hipL));
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

    private void ToSideHalfHipped(float hipL, float hipH)
    {
        if (mesh == null || hipL == 0f)
            return;
        CreateMidVertices(0, hipH);

        //get the side-hipped roof shape
        ToSideHipped(hipL);
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
            newPos.Add(new Vector3(v.x - hipH * width / (2 * height) * Mathf.Sign(v.x), v.y + hipH, v.z - hipH * hipL / height * Mathf.Sign(v.z)));
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

    private void CreateMidVertices(float hipL, float hipH, bool right)
    {
        var baseVertices = right ? from sv in mesh.sharedVertices
                                   where mesh.positions[sv[0]].y == -height / 2 && mesh.positions[sv[0]].x > 0
                                   select mesh.positions[sv[0]] :
                                   from sv in mesh.sharedVertices
                                   where mesh.positions[sv[0]].y == -height / 2 && mesh.positions[sv[0]].x < 0
                                   select mesh.positions[sv[0]];
        if (baseVertices.Count() != 2)
            return;
        List<Vector3> newPos = new List<Vector3>();
        foreach (Vector3 v in baseVertices)
        {
            newPos.Add(new Vector3(v.x - hipH * width / (2 * height) * Mathf.Sign(v.x), v.y + hipH, v.z - hipH * hipL / height * Mathf.Sign(v.z)));
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
            if (!newVerticesFace.Any())
                continue;
            Face newFace = mesh.AppendVerticesToFace(mesh.faces[i], newVerticesFace.ToArray());
            faces[i] = newFace;
            mesh.faces = faces;
        }
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
