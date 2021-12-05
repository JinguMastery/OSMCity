using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MNISTpredictionPlot : MonoBehaviour
{
    public float barMaxHeight = 40;

    public bool plotNow = false;

    public Image[] barPlots;
    public float[] plotValues;

    // Start is called before the first frame update
    void Start()
    {
        plotValues = new float[10];
        UpdatePlot(plotValues);
    }

    // Update is called once per frame
    void Update()
    {
        if (plotNow)
        {
            plotNow = false;
            UpdatePlot(plotValues);
        }
    }

    public void UpdatePlot(float[] values)
    {
        for (int i=0; i < values.Length; i++)
        {
            barPlots[i].fillAmount = values[i];
            barPlots[i].color = Color.Lerp(Color.blue, Color.green, values[i]);
        }
    }
}
