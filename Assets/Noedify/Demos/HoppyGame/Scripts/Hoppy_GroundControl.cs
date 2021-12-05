using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Hoppy_GroundControl : MonoBehaviour
{
    public float movementSpeed = 1;
    public float resetPosition_x;
    public bool moving = false;
    Vector3 startPos;

    // Start is called before the first frame update
    void Start()
    {
        startPos = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        if (moving)
            transform.Translate(new Vector3(-Time.deltaTime * movementSpeed, 0, 0));
        if (transform.position.x <= resetPosition_x)
           transform.position = startPos;
    }
}
