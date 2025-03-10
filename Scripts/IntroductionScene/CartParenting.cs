using UnityEngine;

public class CartParenting : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Check if the entering object is a box
        if (other.CompareTag("StartingBox"))
        {
            // Parent the box to the cart
            other.transform.SetParent(transform);
        }
    }

    private void OnTriggerExit(Collider other)
    {
 
        if (other.CompareTag("StartingBox"))
        {
            other.transform.SetParent(null);
        }
    }
}
