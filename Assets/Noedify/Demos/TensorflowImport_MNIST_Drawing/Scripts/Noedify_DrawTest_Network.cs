using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Noedify_DrawTest_Network : MonoBehaviour
{
    public Noedify.Net net;

    public SampleDrawingMNIST sampleDrawing;
    public MNISTpredictionPlot plotPrediction;
    public Noedify_Solver solver;
    Noedify.Net mnist_FC_net;

    float lastSample = -5f;

    public bool modelImportComplete = false;

    // Start is called before the first frame update
    void Start()
    {
        mnist_FC_net = BuildAndImportModel();
    }

    void Update()
    {
        if (mnist_FC_net != null)
        {
            if ((Time.time - lastSample) > .1f)
            {
                TestMNISTDrawing(mnist_FC_net);
                lastSample = Time.time;
            }
        }
    }

    Noedify.Net BuildAndImportModel()
    {
        modelImportComplete = false;
        int no_labels = 10;

        bool importTensorflowModel = false;

        net = new Noedify.Net();

        // Attempt to load network saved as a binary file
        // This is much faster than importing form a parameters file
        bool status = net.LoadModel("Noedify-Model_Digit_Drawing_Test");
        if (status == false)
        {
            print("Binary file not found. Importing Tensorflow parameters.");
            importTensorflowModel = true;
        }
        // If the binary file doesn't exist yet, import the parameters from the Tensorflow file
        // This is slower, so we will save the network as a binary file after importing it
        if (importTensorflowModel)
        {
            /* Input layer */
            Noedify.Layer inputLayer = new Noedify.Layer(
                Noedify.LayerType.Input2D, // layer type
                new int[2] { 28, 28 }, // input size
                1, // # of channels
                "input layer" // layer name
                );
            net.AddLayer(inputLayer);

            // Hidden layer 0
            Noedify.Layer hiddenLayer0 = new Noedify.Layer(
                Noedify.LayerType.FullyConnected, // layer type
                600, // layer size
                Noedify.ActivationFunction.Sigmoid, // activation function
                "fully connected 1" // layer name
                );
            net.AddLayer(hiddenLayer0);

            // Hidden layer 2
            Noedify.Layer hiddenLayer1 = new Noedify.Layer(
                Noedify.LayerType.FullyConnected, // layer type
                300, // layer size
                Noedify.ActivationFunction.Sigmoid, // activation function
                "fully connected 2" // layer name
                );
            net.AddLayer(hiddenLayer1);

            // Hidden layer 2
            Noedify.Layer hiddenLayer2 = new Noedify.Layer(
                Noedify.LayerType.FullyConnected, // layer type
                140, // layer size
                Noedify.ActivationFunction.Sigmoid, // activation function
                "fully connected 3" // layer name
                );
            net.AddLayer(hiddenLayer2);

            /* Output layer */
            Noedify.Layer outputLayer = new Noedify.Layer(
                Noedify.LayerType.Output, // layer type
                no_labels, // layer size
                Noedify.ActivationFunction.Sigmoid, // activation function
                "output layer" // layer name
                );
            net.AddLayer(outputLayer);

            net.BuildNetwork();

            status = NSAI_Manager.ImportNetworkParameters(net, "FC_mnist_600x300x140_parameters");
            if (status)
                print("Successfully loaded model.");
            else
            {
                print("Tensorflow model load failed. Have you moved the \"...Assets/Noedify/Resources\" folder to: \"...Assets/Resources\" ?");
                print("All model parameter files must be stored in: \"...Assets/Resources/Noedify/ModelParameterFiles\"");
                return null;
            }
            net.SaveModel("Noedify-Model_Digit_Drawing_Test");
            print("Saved binary model file \"Noedify-Model_Digit_Drawing_Test\"");
        }
        solver = Noedify.CreateSolver();
        solver.suppressMessages = true;
        modelImportComplete = true;
        return net;
    }

    void TestMNISTDrawing(Noedify.Net net)
    {
        float[] modelInputs = sampleDrawing.SampleDrawing(new int[2] { 28, 28 });
        float[,,] modelInputsFormatted = new float[1, 1, 28 * 28];
        for (int i = 0; i < 28 * 28; i++)
            modelInputsFormatted[0, 0, i] = modelInputs[i];

        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        solver.Evaluate(net, modelInputsFormatted, Noedify_Solver.SolverMethod.MainThread);
        sw.Stop();
   
        StartCoroutine(UpdatePlotWhenComplete());
    }

    IEnumerator UpdatePlotWhenComplete()
    {
        while (solver.evaluationInProgress)
        {
            yield return null;
        }
        float[] predictions = solver.prediction;
        float[] predictions_log = new float[predictions.Length];
        for (int i = 0; i < predictions.Length; i++)
        {
            predictions_log[i] = (Mathf.Log10(predictions[i]) + 5f) / 5f;
        }
        plotPrediction.UpdatePlot(predictions_log);
    }

    public float[,,] ImportSingleImageTrainingData(Texture2D inputImage)
    {
        int w = inputImage.width;
        int h = inputImage.height;
        float[,,] trainingData = new float[1, w, h];

        float maxPixel = -10;

        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < h; j++)
            {
                Color pixel = inputImage.GetPixel(i, j);
                float sum = pixel.r + pixel.b + pixel.g;
                if (sum > maxPixel)
                    maxPixel = sum;
                trainingData[0, i, j] = sum;
            }
        }
        for (int i = 0; i < w; i++)
            for (int j = 0; j < h; j++)
                trainingData[0, i, j] /= maxPixel;

        return trainingData;
    }
}
