using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class TestPredictions_BackgroundTraining : MonoBehaviour
{

    public Texture2D[] sampleImageSet;
    public GameObject[] sampleImagePlanes;
    public Text[] FC_predictionText;
    public Text[] CNN_predictionText;

    public List<Texture2D> sampleImageRandomSet;

    // Start is called before the first frame update
    void Start()
    {
        sampleImageRandomSet = new List<Texture2D>();
        int[] sampleRange = new int[sampleImageSet.Length];
        for (int i = 0; i < sampleRange.Length; i++)
            sampleRange[i] = i;
        Noedify_Utils.Shuffle(sampleRange);
        for (int i = 0; i < sampleImagePlanes.Length; i++)
            sampleImageRandomSet.Add(sampleImageSet[sampleRange[i]]);

        for (int i=0; i < sampleImagePlanes.Length; i++)
        {
            sampleImagePlanes[i].GetComponent<MeshRenderer>().material.SetTexture("_MainTex", sampleImageRandomSet[i]);
        }

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
