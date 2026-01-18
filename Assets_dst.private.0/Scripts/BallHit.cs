using UnityEngine;

public class BallHit : MonoBehaviour
{
    private Rigidbody rb;
    public float speedMultiplier = 1.5f;
    public Vector3 initialPos;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        initialPos = transform.position;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 1. Check if we hit a Racket
        if (collision.gameObject.CompareTag("Racket"))
        {
            // Get the motion data from the racket
            MotionReceiverMulti receiver = FindFirstObjectByType<MotionReceiverMulti>();
            
            // Calculate direction: Use the racket's "Forward" or the normal of the hit
            Vector3 hitDirection = collision.contacts[0].normal;
            
            // Optional: Add some "upward" force to make it feel like a tennis hit
            hitDirection += Vector3.up * 0.2f;

            // Reflect the ball and add speed
            float swingSpeed = receiver.CurrentVelocity.magnitude;
            float finalSpeed = Mathf.Max(swingSpeed * speedMultiplier, 10f); // Ensure a minimum speed

            rb.linearVelocity = -hitDirection * finalSpeed;
            
            Debug.Log("Ball Hit! Speed: " + finalSpeed);
        }

        // 2. Wall Logic (Your existing code)
        if (collision.transform.CompareTag("Wall"))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero; // Stop spinning too
            transform.position = initialPos;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
