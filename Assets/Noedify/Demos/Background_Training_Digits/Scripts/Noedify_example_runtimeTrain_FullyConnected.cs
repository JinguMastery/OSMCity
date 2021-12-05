using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Noedify_example_runtimeTrain_FullyConnected : MonoBehaviour
{
    /*
        This example script will import a handwritten digit dataset and train 
        a fully-connected (dense) model during runtime using parallel processing.
    */

    public float trainingRate = 0.4f;

    public int no_epochs = 10;
    public int batch_size = 2;

    public Noedify_Solver.CostFunction costFunction;

    public Noedify_Debugger debugger;
    public Transform costPlotOrigin;

    public TestPredictions_BackgroundTraining predictionTester;

    public UnityEngine.UI.Toggle solverMethodToggle;

    [Header("MNIST training images")]
    public Texture2D[] MNIST_images0;
    public Texture2D[] MNIST_images1;
    public Texture2D[] MNIST_images2;
    public Texture2D[] MNIST_images3;
    public Texture2D[] MNIST_images4;
    public Texture2D[] MNIST_images5;
    public Texture2D[] MNIST_images6;
    public Texture2D[] MNIST_images7;
    public Texture2D[] MNIST_images8;
    public Texture2D[] MNIST_images9;

    Noedify.Net net;

    Noedify_Solver solver;

    int no_labels = 10; // number of output labels

    System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

    void Start()
    {
        BuildModel();      
    }

    void BuildModel()
    {
        net = new Noedify.Net();

        /* Input layer */
        
        Noedify.Layer inputLayer = new Noedify.Layer(
            Noedify.LayerType.Input2D, // layer type
            new int[2] { 28, 28 }, // input size
            1, // # of channels
            "input layer" // layer name
            );
        net.AddLayer(inputLayer);

        // Hidden layer 1 
        Noedify.Layer hiddenLayer0 = new Noedify.Layer(
            Noedify.LayerType.FullyConnected, // layer type
            200,
            Noedify.ActivationFunction.Sigmoid,
            "fully connected 1" // layer name
            );
        net.AddLayer(hiddenLayer0);

        // Output layer 
        Noedify.Layer outputLayer = new Noedify.Layer(
            Noedify.LayerType.Output, // layer type
            no_labels, // layer size
            Noedify.ActivationFunction.SoftMax, // activation function
            "output layer" // layer name
            );
        net.AddLayer(outputLayer);

        net.BuildNetwork();
    }

    public void TrainModel()
    {
        List<float[,,]> trainingData = new List<float[,,]>();
        List<float[]> outputData = new List<float[]>();

        List<Texture2D[]> MNIST_images = new List<Texture2D[]>();
        MNIST_images.Add(MNIST_images0);
        MNIST_images.Add(MNIST_images1);
        MNIST_images.Add(MNIST_images2);
        MNIST_images.Add(MNIST_images3);
        MNIST_images.Add(MNIST_images4);
        MNIST_images.Add(MNIST_images5);
        MNIST_images.Add(MNIST_images6);
        MNIST_images.Add(MNIST_images7);
        MNIST_images.Add(MNIST_images8);
        MNIST_images.Add(MNIST_images9);
        Noedify_Utils.ImportImageData(ref trainingData, ref outputData, MNIST_images, true);
        debugger.net = net;

        Noedify_Solver.SolverMethod solverMethod = Noedify_Solver.SolverMethod.MainThread;
        if (solverMethodToggle != null)
            if (solverMethodToggle.isOn)
                solverMethod = Noedify_Solver.SolverMethod.Background;

        if (solver == null)
            solver = Noedify.CreateSolver();
        solver.debug = new Noedify_Solver.DebugReport();
        sw.Start();
        //solver.costThreshold = 0.01f; // Add a cost threshold to prematurely end training when a suitably low error is achieved
        //solver.suppressMessages = true; // suppress training messages from appearing in editor the console
        solver.TrainNetwork(net, trainingData, outputData, no_epochs, batch_size, trainingRate, costFunction, solverMethod, null, 8);
        float[] cost = solver.cost_report;

        StartCoroutine(PlotCostWhenComplete(solver, cost));
    }

    IEnumerator PlotCostWhenComplete(Noedify_Solver solver, float[] cost)
    {
        while (solver.trainingInProgress)
        {
            yield return null;
        }
        sw.Stop();
        print("Elapsed: " + sw.ElapsedMilliseconds + " ms");
        debugger.PlotCost(cost, new float[2] { 1.0f / no_epochs * 2.5f, 5 }, costPlotOrigin);
        for (int n = 0; n < predictionTester.sampleImagePlanes.Length; n++)
        {
            float[,,] testInputImage = new float[1, 1, 1];
            Noedify_Utils.ImportImageData(ref testInputImage, predictionTester.sampleImageRandomSet[n], true);
            solver.Evaluate(net, testInputImage);
            int prediction = Noedify_Utils.ConvertOneHotToInt(solver.prediction);
            predictionTester.FC_predictionText[n].text = prediction.ToString();
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

}
