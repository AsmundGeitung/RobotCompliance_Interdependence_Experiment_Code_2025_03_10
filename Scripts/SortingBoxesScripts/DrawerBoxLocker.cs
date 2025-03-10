using UnityEngine;

public class DrawerBoxLocker : MonoBehaviour
{
    [Header("Assign the Anchor (child of Drawer, scale = 1,1,1)")]
    [SerializeField] private Transform drawerAnchor;

    private Transform lockedBoxTransform;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Box"))
        {
            // Lock the box inside the drawer.
            LockBoxInDrawer(other.transform, drawerAnchor);
            lockedBoxTransform = other.transform;

            Debug.Log($"{other.name} locked in the drawer.");
        }
    }

    // Called when another collider leaves this trigger.
    private void OnTriggerExit(Collider other)
    {
        // If the object leaving is the same box we locked, unlock it.
        if (lockedBoxTransform != null && other.transform == lockedBoxTransform)
        {
            UnlockBoxFromDrawer(lockedBoxTransform);
            lockedBoxTransform = null;

            Debug.Log($"{other.name} unlocked from the drawer.");
        }
    }

    private void LockBoxInDrawer(Transform boxTransform, Transform anchor)
    {
        boxTransform.SetParent(anchor, false);

        boxTransform.localPosition = Vector3.zero;
        boxTransform.localRotation = Quaternion.identity;
        boxTransform.localScale = Vector3.one;
    }

    private void UnlockBoxFromDrawer(Transform boxTransform)
    {
        boxTransform.SetParent(null, true);
    }
}

