using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Hoppy_AnimationController : MonoBehaviour
{
    public float flapStrength = 1;
    public SpriteRenderer[] sprites;
    public Sprite[] bodySprites;
    public bool isDead = false;

    public Rigidbody2D rbody;
    Vector3 startingPos;

    Color spriteColor;

    Transform hitObstacle;
    Coroutine colorChange_co;

    void Start()
    {
        rbody = GetComponent<Rigidbody2D>();
        startingPos = transform.position;

        spriteColor = Random.ColorHSV(0,1,.3f,.6f,.7f,1);
        for (int i = 0; i < 2; i++)
            sprites[i].color = spriteColor;
        sprites[2].color = Color.red;
        sprites[3].color = Color.red;
        sprites[4].color = Color.white;
    }

    void Update()
    {

        if (!isDead)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Flap();
            }
        }
        else
        {
            if (hitObstacle != null)
            {
                if (hitObstacle.parent)
                    transform.Translate(Vector3.left * Time.deltaTime * hitObstacle.parent.gameObject.GetComponent<Hoppy_Obstacle_Controller>().movementSpeed);
                else if (hitObstacle.gameObject.GetComponent<Hoppy_GroundControl>())
                    transform.Translate(Vector3.left * Time.deltaTime * hitObstacle.gameObject.GetComponent<Hoppy_GroundControl>().movementSpeed);
            }
        }
    }

    public void Reset()
    {
        transform.position = startingPos;
        if (rbody)
            rbody.velocity = Vector2.zero;
        for (int i = 0; i < 2; i++)
            sprites[i].color = spriteColor;
        sprites[2].color = Color.red;
        sprites[3].color = Color.red;
        sprites[4].color = Color.white;

        isDead = false;
        GetComponent<Rigidbody2D>().isKinematic = false;
        GetComponent<Rigidbody2D>().simulated = true;
    }

    public void ImplementDecision(float[] decision)
    {
        /* decision:
         decision[0]: no hop
         decision[1,2,3]: hop sequence
         decision[4]: delay between hops
         decision[5]: hop size
        */
        bool[] decisionSequence = new bool[3];
        for (int i = 0; i < 3; i++)
        {
            if (decision[i + 1] >= 0.5f)
                decisionSequence[i] = true;
        }
        colorChange_co = StartCoroutine(DecisionColor());
        StartCoroutine(FlapSequence(decisionSequence, decision[4], decision[5]));
    }

    void Flap(float magnitude = 1)
    {
        if (transform.position.y < 22)
        {
            rbody.velocity = (Vector3.up * flapStrength * magnitude);
            StartCoroutine(FlapAnimation());
        }
    }

    void PlayerDeath()
    {
        StopCoroutine(colorChange_co);
        foreach (SpriteRenderer SR in sprites)
            SR.GetComponent<SpriteRenderer>().color = Color.gray;
        isDead = true;
        GetComponent<Rigidbody2D>().isKinematic = true;
        GetComponent<Rigidbody2D>().simulated = false;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "Hoppy_Obstacle")
        {
            hitObstacle = collision.transform;
            PlayerDeath();
        }
        else if (collision.gameObject.tag == "Hoppy_Upperlimit")
        {
            rbody.velocity = Vector2.down;
        }
    }

    IEnumerator FlapSequence(bool[] seq, float delay, float hopMag)
    {
        for (int n = 0; n < seq.Length; n++)
        {
            if (seq[n])
                Flap(hopMag);
            yield return new WaitForSeconds(delay*0.3f + 0.5f);
        }
    }

    IEnumerator FlapAnimation()
    {
        sprites[0].sprite = bodySprites[1];
        yield return new WaitForSeconds(.1f);
        sprites[0].sprite = bodySprites[2];
        yield return new WaitForSeconds(.1f);
        sprites[0].sprite = bodySprites[0];
    }

    IEnumerator DecisionColor()
    {
        for (int i = 0; i < sprites.Length; i++)
            sprites[i].color = Color.yellow;
        yield return new WaitForSeconds(0.3f);
        for (int i = 0; i < 2; i++)
            sprites[i].color = spriteColor;
        sprites[2].color = Color.red;
        sprites[3].color = Color.red;
        sprites[4].color = Color.white;
    }


}
