using UnityEngine;

public class TennisRacketV1 : MonoBehaviour
{
    float speed = 3f; // move speed
    private void Start()
    {
     
    }

    void Update()
    {
        float h = Input.GetAxisRaw("Horizontal"); // get the horizontal axis of the keyboard
        float v = Input.GetAxisRaw("Vertical"); // get the vertical axis of the keyboard

        if ((h != 0 || v != 0)) // if we want to move and we are not hitting the ball
        {
            transform.Translate(new Vector3(h, 0, v) * speed * Time.deltaTime); // move on the court
        }
    }


    private void OnTriggerEnter(Collider other)
    {

    }
}
