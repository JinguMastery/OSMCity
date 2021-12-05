using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Hoppy_Brain : MonoBehaviour
{
    public int score = 0;
    public int scoreOffset = 0;
    public float decisionPeriod = 2;
    public float decisionOutcomeCheckDelay = 1.5f;

    public Hoppy_AnimationController animationController;

    const int no_observations = 4;
    const int no_decisions = 6;

    Hoppy_SimController simController;
    PlayerObservationDecision decisionFrame;

    List<float[,,]> observation_inputs;
    List<float[]> decision_outputs;
    List<float> decision_weights;

    float lastMovement_t;
    float preDecisionScore = 0;
    bool lastDecisionRandom;
    float modifiedDecisionPeriod;

    Noedify_Solver evalSolver;

    [System.Serializable]
    public class PlayerObservationDecision
    {
        public float[] observation;
        public float[] decision;
        public float weight;
    }

    // Start is called before the first frame update
    void Start()
    {
        if (simController == null)
            simController = GameObject.Find("SimController").GetComponent<Hoppy_SimController>();
        observation_inputs = new List<float[,,]>();
        decision_outputs = new List<float[]>();
        decision_weights = new List<float>();
        lastMovement_t = -decisionPeriod;
        evalSolver = Noedify.CreateSolver();
        modifiedDecisionPeriod = decisionPeriod;
    }

    // Update is called once per frame
    void Update()
    {
        float[] samples = AcquireObservations();

        if ((samples[0] < -.6f) | (samples[1] < -0.7f) | (samples[2] < 0.7f))
            modifiedDecisionPeriod = decisionPeriod / 2f;
        else
            modifiedDecisionPeriod = decisionPeriod;
        if ((Time.time - lastMovement_t) > modifiedDecisionPeriod)
        {
            if (!animationController.isDead)
            {
                decisionFrame = new PlayerObservationDecision();
                decisionFrame.observation = AcquireObservations();
                decisionFrame.decision = new float[no_decisions];
                float[] newRandomDecision = new float[no_decisions];
                float[] newAIDecision = new float[no_decisions];

                newRandomDecision = RandomDecision(decisionFrame.observation);
                if (simController.randomness < 1)
                    newAIDecision = AIDecision(decisionFrame.observation);

                for (int i=0; i < no_decisions; i++)
                    decisionFrame.decision[i] = (simController.randomness * newRandomDecision[i]  + (1-simController.randomness) * newAIDecision[i]);
                preDecisionScore = score;
                string outpu = "";
                for (int i = 0; i < no_decisions; i++)
                    outpu += "r" + newRandomDecision[i] + ",ai" + newAIDecision[i] + " ";
                //print(outpu);
                animationController.ImplementDecision(decisionFrame.decision);
                // after some delay, check if the decision was successful
                // if so, add the observation/decision to the training set
                StartCoroutine(CheckDecisionOutcome(decisionFrame));
                lastMovement_t = Time.time;
            }
        }
    }

    float[] AcquireObservations()
    {
        /* sensorInputs[4]
         sensorInputs[0]: y-position
         sensorInputs[1]: y-velocity
         sensorInputs[2]: x-distance to obstacle
         sensorInputs[3]: y-distance to obstacle
        */
        float[] sensorInputs = new float[no_observations];
        sensorInputs[0] = (transform.position.y - 3.5f) / 10.5f;
        sensorInputs[1] = (animationController.rbody.velocity.y) / 20f;

        if (simController.obstacles.Count > 0)
        {
            sensorInputs[2] = (simController.obstacles[0].transform.position.x + 1.5f)/15f;
            sensorInputs[3] = (simController.obstacles[0].gap.transform.position.y - 3.5f) / 10.5f;
        }
        else
        {
            sensorInputs[2] = 10;
            sensorInputs[3] = 0;
        }
        return sensorInputs;
    }

    // Randomly generate decision
    float[] RandomDecision(float[] observation)
    {

        float[] decision = new float[no_decisions];
        /* decision:
         decision[0]: no hop
         decision[1,2,3]: hop sequence
         decision[4]: delay between hops
         decision[5]: hop size
        */
        int randHop = Random.Range(0, 100);
        if (observation[0] < observation[3] | observation[0] < -0.75f)
            randHop += 30;
        else
            randHop -= 20;
        if (randHop < 40)
            decision[0] = 1;
        else
        {
            int hopChanceOffset = 0;
            if (observation[0] < -0.7f)
                hopChanceOffset += 1;
            randHop = Random.Range(0, 50) + hopChanceOffset;
            if (randHop >= 24)
                decision[1] = 1;
            randHop = Random.Range(0, 50) + hopChanceOffset;
            if (randHop >= 24)
                decision[2] = 1;
            randHop = Random.Range(0, 50) + hopChanceOffset;
            if (randHop >= 24)
                decision[3] = 1;
            float hopSpeed = ((float)Random.Range(0, 200) / 100f - 1f);
            if (observation[0] < observation[3] | observation[0] < -0.75f)
                hopSpeed -= 0.5f;
            float jumpMag = (float)Random.Range(40, 120) / 100f;
            decision[4] = hopSpeed;
            decision[5] = jumpMag;
        }
        return decision;
    }

    // Evaluate netowrk to generate decision
    float[] AIDecision(float[] observation)
    {
        evalSolver.Evaluate(simController.net, Noedify_Utils.AddTwoSingularDims(observation), Noedify_Solver.SolverMethod.MainThread);
        return evalSolver.prediction;
    }

    public void Reset()
    {
        lastMovement_t = -decisionPeriod;
        animationController.Reset();
    }

    // Check whether decision was successful
    IEnumerator CheckDecisionOutcome(PlayerObservationDecision observationDecision)
    {
        int startingRun = simController.currentRun;
        yield return new WaitForSeconds(decisionOutcomeCheckDelay);
        if (!animationController.isDead & startingRun == simController.currentRun) // if Hoppy is still alive
        {
            observationDecision.weight = 1; // set the weight to 1, more complex strategies can use an adaptive weight
            simController.AddTrainingSet(observationDecision); // add observation/decision to training set
        }
    }

    public IEnumerator TrainNetwork()
    {
        yield return null;
    }
}
