using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MouseSketch : MonoBehaviour
{
    public GameObject drawObj;
    public Transform drawParent;
    public Plane drawBGplane;
    public GameObject drawStroke;
    Vector3 drawStartPos;

    // Start is called before the first frame update
    void Start()
    {
        drawBGplane = new Plane(Camera.main.transform.forward * -1, this.transform.position);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began || Input.GetMouseButtonDown(0))
        {
            transform.position = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 5f));

            drawStroke = (GameObject)Instantiate(drawObj, this.transform.position, Quaternion.identity);
            drawStroke.transform.SetParent(drawParent);

            Ray drawRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            float distance;
            if (drawBGplane.Raycast(drawRay, out distance))
            {
                drawStartPos = drawRay.GetPoint(distance);
            }
        }
        else if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Moved || Input.GetMouseButton(0))
        {
            transform.position = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 5f));
            Ray drawRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            float distance;
            if (drawBGplane.Raycast(drawRay, out distance))
            {
                drawStroke.transform.position = drawRay.GetPoint(distance);
            }
        }
        else
        {

        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            for (int i=0; i < drawParent.childCount; i++)
                Destroy(drawParent.GetChild(i).gameObject);
        }


    }
}
