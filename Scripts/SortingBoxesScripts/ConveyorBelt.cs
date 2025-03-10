using UnityEngine;

public class ConveyorBelt : MonoBehaviour
{
    [Header("Conveyor Settings")]
    public float conveyorSpeed = 2.0f; // Speed at which objects move
    public Vector3 conveyorDirection = Vector3.right; // Direction of movement (e.g., Vector3.right for horizontal)

    private void OnCollisionStay(Collision collision)
    {
        // Ensure the object has a rigidbody
        Rigidbody rb = collision.rigidbody;
        if (rb != null)
        {
            // Apply velocity to the object in the direction of the conveyor
            Vector3 movement = conveyorDirection.normalized * conveyorSpeed;
            rb.linearVelocity = new Vector3(movement.x, rb.linearVelocity.y, movement.z);
        }
    }

    private void OnDrawGizmos()
    {
        // Visualize the conveyor direction in the editor
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + conveyorDirection.normalized * 2.0f);
    }
}
