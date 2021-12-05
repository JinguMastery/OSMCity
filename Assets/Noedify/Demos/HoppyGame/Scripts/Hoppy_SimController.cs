using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Hoppy_SimController : MonoBehaviour
{
    public bool spawnObstacles = true;
    public int currentRun = 0;
    public float randomness = 1;
    public int runsPerTraining = 10; // number of simulation runs before training network
    public float obstacleSpawnTime = 1; // time between obstacle spawns
    public float obstacleRandomHeightAmount = 5; // random y-offset of obstacle gaps
    public Vector3 obstacleSpawnPos;
    public Noedify.Net net;

    [Space(10)]
    [Header("Training Parameters")]
    public int trainingBatchSize = 4;
    public int trainingEpochs = 100;
    public float trainingRate = 1f;

    [Space(10)]
    public GameObject startButton;
    public GameObject randomnessSlider;
    public GameObject timeScaleSlider;
    public GameObject noPlayersSelect;
    public Text runText;
    public GameObject scoreUI;
    public Text currentScoreText;
    public Text topScoreText;
    public GameObject obstaclePrefab;
    public GameObject playerPrefab;
    public Hoppy_GroundControl ground;

    public List<Hoppy_Obstacle_Controller> obstacles;
    public List<Hoppy_Brain> players;

    const int no_observations = 4;
    const int no_decisions = 6;
    const int N_threads = 8;

    bool simStarted = false;
    int no_players;
    float lastObstacleSpawn_t;
    int currentScore;
    int topScore;
    float runStart_t;

    Noedify_Solver trainingSolver;

    public List<Hoppy_Brain.PlayerObservationDecision> trainingSet;

    void Start()
    {
        trainingSolver = Noedify.CreateSolver();
        trainingSet = new List<Hoppy_Brain.PlayerObservationDecision>();
        BuildNetwork();
        no_players = 1;
        ChangeNoPlayers(false);
        lastObstacleSpawn_t = -obstacleSpawnTime;
        topScore = 0;
    }

    void Update()
    {
        if (CheckPlayersDead() & simStarted) // if all Hoppys are dead, restart the simulation
        {
            if (currentRun % runsPerTraining == 0 & currentRun > 0)
                StartCoroutine(TrainNetwork());
             ResetSim();
        }
        else if (simStarted)
        {
            currentScore = Mathf.FloorToInt(Time.time - runStart_t);
            currentScoreText.text = currentScore.ToString();
        }

        if ((Time.time - lastObstacleSpawn_t) > obstacleSpawnTime & simStarted & spawnObstacles) // spawn new obstacle
        {
            SpawnObstacle();
            lastObstacleSpawn_t = Time.time;
        }

        Time.timeScale = timeScaleSlider.GetComponent<Slider>().value;
    }

    void SpawnObstacle()
    {
        GameObject newObstacles = (GameObject)Instantiate(obstaclePrefab, obstacleSpawnPos + new Vector3(0, Random.Range(-100f, 100f) / 100f * obstacleRandomHeightAmount, 0), Quaternion.identity);
        obstacles.Add(newObstacles.GetComponent<Hoppy_Obstacle_Controller>());
        newObstacles.GetComponent<Hoppy_Obstacle_Controller>().simController = this;
    }

    bool CheckPlayersDead()
    {
        foreach (Hoppy_Brain player in players)
        {
            if (!player.animationController.isDead)
                return false;
        }
        return true;
    }

    void ResetSim()
    {
        if (currentScore > topScore)
            topScoreText.text = currentScore.ToString();
        foreach (Hoppy_Obstacle_Controller obst in obstacles)
        {
            Destroy(obst.gameObject);
        }
        obstacles = new List<Hoppy_Obstacle_Controller>();
        randomness = randomnessSlider.GetComponent<Slider>().value;
        currentRun++;
        runText.text = "Run " + currentRun;
        if (randomness > 0)
        {
            foreach (Hoppy_Brain player in players)
                player.Reset();
        }
        else
        {
            players[0].Reset();
        }
        lastObstacleSpawn_t = -obstacleSpawnTime;
        runStart_t = Time.time;
    }

    public void StartSim()
    {
        simStarted = true;
        startButton.SetActive(false);
        randomnessSlider.SetActive(true);
        timeScaleSlider.SetActive(true);
        noPlayersSelect.SetActive(false);
        scoreUI.SetActive(true);
        ground.moving = true;

        for (int n=0; n<no_players; n++)
        {
            GameObject newPlayer = (GameObject)Instantiate(playerPrefab);
            players.Add(newPlayer.GetComponent<Hoppy_Brain>());
            players[n].transform.position = new Vector3(-6.2f, -3f + Random.Range(0, 100) / 100f * 2f + 1f, -n);
        }


        ResetSim();
    }

    // Add new set of observations/decision to the training set list
    public void AddTrainingSet(Hoppy_Brain.PlayerObservationDecision newSet)
    {
        trainingSet.Add(newSet);
    }

    public void ChangeNoPlayers(bool increase)
    {
        if (increase)
        {
            if (no_players == 1)
                no_players = 10;
            else
                no_players += 10;
        }
        else
        {
            if (no_players == 10)
                no_players = 1;
            else if (no_players > 1)
                no_players -= 10;
        }
        noPlayersSelect.GetComponent<Text>().text = no_players.ToString();
    }

    void BuildNetwork()
    {
        net = new Noedify.Net();

        /* Input layer */
        Noedify.Layer inputLayer = new Noedify.Layer(
            Noedify.LayerType.Input, // layer type
            no_observations, // input size
            "input layer" // layer name
            );
        net.AddLayer(inputLayer);

        // Hidden layer 1 
        Noedify.Layer hiddenLayer0 = new Noedify.Layer(
            Noedify.LayerType.FullyConnected, // layer type
            150, // layer size
            Noedify.ActivationFunction.Sigmoid, // activation function
            "fully connected 1" // layer name
            );
        net.AddLayer(hiddenLayer0);

        /* Output layer */
        Noedify.Layer outputLayer = new Noedify.Layer(
            Noedify.LayerType.Output, // layer type
            no_decisions, // layer size
            Noedify.ActivationFunction.Sigmoid, // activation function
            "output layer" // layer name
            );
        net.AddLayer(outputLayer);

        net.BuildNetwork();
    }

    public IEnumerator TrainNetwork()
    {
        if (trainingSet != null)
        {
            if (trainingSet.Count > 0)
            {
                while (trainingSolver.trainingInProgress) { yield return null; }
                List<float[,,]> observation_inputs = new List<float[,,]>();
                List<float[]> decision_outputs = new List<float[]>();
                List<float> trainingSetWeights = new List<float>();
                for (int n=0; n < trainingSet.Count; n++)
                {
                    observation_inputs.Add(Noedify_Utils.AddTwoSingularDims(trainingSet[n].observation));
                    decision_outputs.Add(trainingSet[n].decision);
                    trainingSetWeights.Add(trainingSet[n].weight);
                }
                trainingSolver.TrainNetwork(net, observation_inputs, decision_outputs, trainingEpochs, trainingBatchSize, trainingRate, Noedify_Solver.CostFunction.MeanSquare, Noedify_Solver.SolverMethod.MainThread, trainingSetWeights, N_threads);
            }
        }
    }
}
