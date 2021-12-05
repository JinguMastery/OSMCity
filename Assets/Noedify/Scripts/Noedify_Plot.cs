using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Noedify_Plot
{
    public enum ScaleType { linear, log10 };

    public class LinePlot
    {
        public float[] xdata;
        public float[] ydata;

        public float[] xaxis;
        public float[] yaxis;

        public ScaleType scaleType;

        public Transform origin;

        public Material axisMat;
        public Material lineMat;

        public float[] drawScale;
        public float lineWidth;

        public LinePlot(Transform plotOriginTransform, float[] x, float[] y, Material plotAxisMat, Material plotLineMat, float[] plotDrawScale, float plotLineWidth = 0.01f, ScaleType plotScaleType = ScaleType.linear, float[] plotXaxis = null, float[] plotYaxis = null)
        {
            
            scaleType = plotScaleType;
            if (scaleType == ScaleType.log10)
                for (int i = 0; i < y.Length; i++) {
                    if (y[i] > 0.0001)
                        y[i] = Mathf.Log10(y[i]);
                    else
                        y[i] = -1000f;
                    }
                    

            if (plotXaxis == null)
            {
                float max = -1e6f;
                float min = 1e6f;
                for (int i = 0; i < x.Length; i++) {
                    if (x[i] > max)
                        max = x[i];
                    if (x[i] < min)
                        min = x[i];
                }
                xaxis = new float[2] { 0, max*1.2f };
                max = -1e6f;
                min = 1e6f;
                for (int i = 0; i < y.Length; i++)
                {
                    if (y[i] > max)
                        max = y[i];
                    if (y[i] < min)
                        min = y[i];
                }
                if (min<0)
                    yaxis = new float[2] { min*1.2f, max * 1.2f };
                else
                    yaxis = new float[2] { 0, max*1.2f };


            }

            xdata = x;
            ydata = y;
            origin = plotOriginTransform;
            axisMat = plotAxisMat;
            lineMat = plotLineMat;
            drawScale = plotDrawScale;
            lineWidth = plotLineWidth;

            Redraw();
        }

        public void Redraw()
        {
            if (origin.childCount > 0)
                for (int i = 0; i < origin.childCount; i++)
                    GameObject.Destroy(origin.GetChild(i).gameObject);

            int no_points = xdata.Length;

            GameObject xaxisObj = new GameObject("x axis");
            xaxisObj.transform.transform.SetParent(origin);
            LineRenderer xlineRend = xaxisObj.AddComponent<LineRenderer>();
            Vector3 xposition1 = new Vector3(origin.position.x + drawScale[0] * xaxis[0] / (xaxis[1] - xaxis[0]), origin.position.y, 0);
            Vector3 xposition2 = new Vector3(origin.position.x + drawScale[0] * xaxis[1] / (xaxis[1] - xaxis[0]), origin.position.y, 0);

            xlineRend.SetPositions(new Vector3[2] { xposition1, xposition2 });
            xlineRend.startWidth = lineWidth*2;
            xlineRend.material = axisMat;

            GameObject yaxisObj = new GameObject("y axis");
            yaxisObj.transform.transform.SetParent(origin);
            LineRenderer ylineRend = yaxisObj.AddComponent<LineRenderer>();
            Vector3 yposition1 = new Vector3(origin.position.x, origin.position.y + drawScale[1] * yaxis[0] / (yaxis[1] - yaxis[0]), 0);
            Vector3 yposition2 = new Vector3(origin.position.x, origin.position.y + drawScale[1] * yaxis[1] / (yaxis[1] - yaxis[0]), 0);

            ylineRend.SetPositions(new Vector3[2] { yposition1, yposition2 });
            ylineRend.startWidth = lineWidth*2;
            ylineRend.material = axisMat;

            GameObject background = GameObject.CreatePrimitive(PrimitiveType.Cube);
            background.name = "background";
            background.transform.localScale = new Vector3(drawScale[0]*1.2f, drawScale[1]*1.2f, .2f);
            background.transform.SetParent(origin);
            background.transform.localPosition = new Vector3(drawScale[0] / 2, drawScale[1] / 2, .2f);
            Material BGmat = new Material(axisMat);
            BGmat.color = Color.white;
            background.GetComponent<MeshRenderer>().material = BGmat;

            for (int n = 0; n < (no_points - 1); n++)
            {
                GameObject lineObj = new GameObject("point " + n);
                lineObj.transform.SetParent(origin);
                LineRenderer lineRend = lineObj.AddComponent<LineRenderer>();
                Vector3 position1 = new Vector3(origin.position.x + drawScale[0] / (xaxis[1] - xaxis[0]) * xdata[n], origin.position.y + drawScale[1] / (yaxis[1]-yaxis[0]) * ydata[n], 0);
                Vector3 position2 = new Vector3(origin.position.x + drawScale[0] / (xaxis[1] - xaxis[0]) * xdata[n+1], origin.position.y + drawScale[1] / (yaxis[1] - yaxis[0]) * ydata[n + 1], 0);

                lineRend.SetPositions(new Vector3[2] { position1, position2 });
                lineRend.startWidth = lineWidth;
                lineRend.material = lineMat;
            }

            GameObject textLabel1 = new GameObject("Text Label 1");
            TextMesh textMesh1 = textLabel1.AddComponent<TextMesh>();
            textMesh1.text = ydata[0].ToString();
            textMesh1.fontSize = 100;
            textMesh1.transform.localScale = Vector3.one / 25f;
            textMesh1.alignment = TextAlignment.Center;
            textMesh1.transform.SetParent(origin);
            textLabel1.transform.position = new Vector3(origin.position.x + drawScale[0] / (xaxis[1] - xaxis[0] + .00001f) * xdata[0], origin.position.y + drawScale[1] / (yaxis[1] - yaxis[0] + .00001f) * ydata[0] * 1.1f, 0);
            textMesh1.color = Color.red;

            GameObject textLabel2 = new GameObject("Text Label 2");
            TextMesh textMesh2 = textLabel2.AddComponent<TextMesh>();
            textMesh2.text = ydata[no_points-1].ToString();
            textMesh2.fontSize = 100;
            textMesh2.transform.localScale = Vector3.one / 25f;
            textMesh2.alignment = TextAlignment.Center;
            textMesh2.transform.SetParent(origin);
            textLabel2.transform.position = new Vector3(origin.position.x + drawScale[0] / (xaxis[1] - xaxis[0]) * xdata[no_points-1], origin.position.y + drawScale[1] / (yaxis[1] - yaxis[0]) * ydata[no_points-1] * 1.1f, 0);
            textMesh2.color = Color.red;
        }

    }
}
