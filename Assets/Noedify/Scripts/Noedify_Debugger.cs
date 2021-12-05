using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Noedify_Debugger : MonoBehaviour
{
    public Noedify.Net net;
    public Material plotMat;
    public Material axisMat;

    public void PlotCost(float[] array, float[] scale, Transform plotOrigin)
    {
        if (array.Length < 2)
        {
            print("Error (Noedify_Debugger.cs: PlotCost): Must have at least 2 point for line plot");
            return;
        }
        float[] x = new float[array.Length];
        for (int i = 0; i < array.Length; i++)
            x[i] = i;
        bool plotNaN = false;
        for (int i = 0; i < array.Length; i++)
            if (array[i] > 1e6 | float.IsNaN(array[i]))
            {
                array[i] = 100;
                plotNaN = true;
            }
        if (plotNaN)
            print("Warning (Noedify_Debugger): Attempting to plot NaN values");
            
        Noedify_Plot.LinePlot cost_plot = new Noedify_Plot.LinePlot(plotOrigin, x, array, axisMat, plotMat, new float[2] { 3, 2 }, 0.03f, Noedify_Plot.ScaleType.linear);

    }

    public void PrintNetParameters(int layer = -1)
    {

        for (int l = 1; l < net.LayerCount(); l++)
        {
            if (l == layer || l == -1)
            {
                if (net.layers[l].layer_type == Noedify.LayerType.FullyConnected | net.layers[l].layer_type == Noedify.LayerType.Output)
                {
                    for (int i = 0; i < net.layers[l - 1].layerSize; i++)
                    {
                        string weightString = "layer " + l + " w_" + i + "_j (fullyConnected): ";
                        for (int j = 0; j < net.layers[l].layerSize; j++)
                            weightString += net.layers[l].weights.values[i, j] + ", ";
                        print(weightString);
                    }
                    string biasString = "layer " + l + " biases: ";
                    for (int j = 0; j < net.layers[l].layerSize; j++)
                        biasString += net.layers[l].biases.values[j] + ", ";
                    print(biasString);
                }
                else if (net.layers[l].layer_type == Noedify.LayerType.Convolutional2D)
                {
                    for (int f = 0; f < net.layers[l].conv2DLayer.no_filters; f++)
                    {
                        string weightString = "layer " + l + ":  filter " + f + " weights: ";
                        for (int j = 0; j < net.layers[l].conv2DLayer.N_weights_per_filter; j++)
                            weightString += net.layers[l].weights.valuesConv2D[f, j] + ", ";
                        print(weightString);
                        string biasString = " filter " + f + " bias: ";
                        biasString += net.layers[l].biases.valuesConv2D[f];
                        print(biasString);
                    }
                }
            }
        }
    }
}
