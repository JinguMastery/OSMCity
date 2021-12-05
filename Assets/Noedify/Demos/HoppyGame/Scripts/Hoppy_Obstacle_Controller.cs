using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Hoppy_Obstacle_Controller : MonoBehaviour
{
    public float movementSpeed = 1f;
    public float destroyPos = -30f;
    public Hoppy_SimController simController;
    public Transform gap;

    // Update is called once per frame
    void Update()
    {
        if (transform.position.x < destroyPos)
            Destroy(gameObject);
        else if (transform.position.x < -2f)
            simController.obstacles.Remove(this);
        transform.Translate(Vector3.left * Time.deltaTime * movementSpeed);
    }
}
