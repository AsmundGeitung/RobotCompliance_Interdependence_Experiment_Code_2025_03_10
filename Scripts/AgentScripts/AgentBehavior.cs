using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit.Interactables;


#if UNITY_EDITOR
using UnityEditor.Animations;
#endif
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Controls the robot agent in two modes, cooperation (handing over boxes),
//  or coexistence mode (performing unrelated tasks).
/// </summary>
public class AgentBehavior : MonoBehaviour
{
    #region Inspector Fields

    public Transform handOverArea;            // Where the robot delivers boxes in cooperation mode.
    public Transform handArea;                // Area to simulate where the robot holds the box.
    public Transform[] wanderPoints;          // Points the robot wanders to in coexistence mode.
    public Transform playerTransform;

    public Animator robotAnimator;

    public float moveSpeed = 3f;
    public float turningSpeed = 7f;
    public float moveInterval = 5f;

    public AudioSource footstepAudioSource;
    public AudioClip footstepClip;

    public bool cooperation = false;
    private bool isDeliveringBox;

    #endregion

    #region Private Fields
    private Vector3 originalPosition;
    private bool isMoving = false;
    private bool isCarryingBox = false;
    private GameObject currentCarriedBox; 
    private Coroutine coexistenceCoroutine;
    private bool awaitingPickup = false;

    // Random "work" animations the robot may play in coexistence mode.
    private readonly List<string> workAnimations = new List<string> { "Work1", "Work2", "Work3", "Work4" };
    private int lastWanderIndex = -1;

    #endregion

    #region Unity Methods

    private void Start()
    {
        originalPosition = transform.position;
        SetAnimationBool("IsWalking", false);
        robotAnimator.applyRootMotion = false;

        if (!cooperation)
        {
            coexistenceCoroutine = StartCoroutine(CoexistenceBehavior());
        }
    }


    private void Update()
    {
        // Keep carried box positioned correctly infront of the robot.
        if (isCarryingBox && currentCarriedBox != null && !awaitingPickup)
        {
            currentCarriedBox.transform.position = handArea.position;
            currentCarriedBox.transform.rotation = handArea.rotation;
        }
    }

    #endregion

    #region Coexistence Mode

    private IEnumerator CoexistenceBehavior()
    {
        while (true)
        {
            yield return new WaitForSeconds(moveInterval);

            if (!isMoving)
            {
                Transform targetPoint = GetRandomWanderPoint();
                if (targetPoint != null)
                {
                    isMoving = true;
                    // Begin walking animation
                    SetAnimationBool("IsWalking", true);

                    // Move to a random wander point, then run random work animation.
                    yield return StartCoroutine(MoveAndFaceTarget(targetPoint.position, null, false));

                    // Stop walking animation
                    SetAnimationBool("IsWalking", false);

                    PlayRandomWorkAnimation();
                    yield return new WaitForSeconds(GetCurrentAnimationLength());

                    // Pause briefly before wandering again.
                    yield return new WaitForSeconds(Random.Range(1f, 2f));
                    isMoving = false;
                }
            }
        }
    }

    //Chooses a random point, but never the same one right after each other.
    private Transform GetRandomWanderPoint()
    {
        if (wanderPoints == null || wanderPoints.Length == 0)
        {
            Debug.LogWarning("No wander points assigned.");
            return null;
        }

        int newIndex;
        if (wanderPoints.Length == 1)
        {
            newIndex = 0;
        }
        else
        {
            do
            {
                newIndex = Random.Range(0, wanderPoints.Length);
            }
            while (newIndex == lastWanderIndex);
        }

        lastWanderIndex = newIndex;
        return wanderPoints[newIndex];
    }

    private void PlayRandomWorkAnimation()
    {
        if (workAnimations.Count == 0) return;
        string chosenAnim = workAnimations[Random.Range(0, workAnimations.Count)];
        robotAnimator.SetTrigger(chosenAnim);
    }

    #endregion

    #region Cooperation Mode


    public void OnUserRequestedBox(GameObject newBox)
    {
        if (cooperation && !isMoving && newBox != null && !isDeliveringBox)
        {
            BoxPickupHandler handler = newBox.GetComponent<BoxPickupHandler>();
            if (handler != null) handler.agentBehavior = this;
            
            isDeliveringBox = true;

            StartCoroutine(CooperatePickupAndDeliverBox(newBox));
        }
    }



    /// <summary>
    /// Full sequence for picking up a box, delivering it, and waiting for user pickup.
    /// </summary>
    private IEnumerator CooperatePickupAndDeliverBox(GameObject box)
    {
        XRGrabInteractable interactable = box.GetComponent<XRGrabInteractable>();
        BoxPickupHandler boxPickupHandler= box.GetComponent<BoxPickupHandler>();
        if (interactable != null)
        {
            interactable.enabled = false;
        }
        if (boxPickupHandler != null)
        {
            boxPickupHandler.enabled = false;
        }

        isMoving = true;
        SetAnimationBool("IsWalking", true);

        Vector3 approachOffset = box.transform.forward * 0.5f;
        Vector3 targetPosition = box.transform.position - approachOffset;

        yield return StartCoroutine(MoveAndFaceTarget(targetPosition, null, false));

        SetAnimationBool("IsWalking", false);
        SetAnimationTrigger("PickUpBox");
        yield return new WaitForSeconds(GetCurrentAnimationLength());

        currentCarriedBox = box;
        isCarryingBox = true;

        SetAnimationBool("IsWalking", true);
        SetAnimationTrigger("StartWalkCarry");
        yield return StartCoroutine(MoveAndFaceTarget(handOverArea.position, null, false));

        yield return StartCoroutine(Rotate45DegreesLeft());
        FreezeBoxInMidAir(box);

        SetAnimationBool("IsWalking", false);
        SetAnimationBool("HoldingBox", true);
        SetAnimationTrigger("HoldBox");
        isMoving = false;
        awaitingPickup = true;
        StartCoroutine(FacePlayerWhileHolding());
        isDeliveringBox = false;
        
        if (interactable != null)
        {
            interactable.enabled = true;
        }

        if (boxPickupHandler != null)
        {
            boxPickupHandler.enabled = true;
        }

    }

    /// <summary>
    /// Called when the user physically takes the box from the robot's hand.
    /// </summary>
    public void ReleaseBox()
    {
        if (currentCarriedBox != null)
        {
            UnfreezeBox(currentCarriedBox);
            currentCarriedBox = null;
            isCarryingBox = false;
            SetAnimationBool("HoldingBox", false);
            awaitingPickup = false;
        }
    }

    void FreezeBoxInMidAir(GameObject box)
    {
        Rigidbody rb = box.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = box.AddComponent<Rigidbody>(); // Ensure it has a Rigidbody
        }

        // Stop any motion immediately
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Manually update physics to ensure changes take effect instantly
        rb.WakeUp();
        Physics.SyncTransforms();

        // Creates and attaches a FixedJoint
        FixedJoint joint = box.GetComponent<FixedJoint>();
        if (joint == null)
        {
            joint = box.AddComponent<FixedJoint>();
        }

        joint.connectedBody = null;
        joint.breakForce = Mathf.Infinity;
        joint.breakTorque = Mathf.Infinity;

        box.transform.position = box.transform.position;
    }


    private void UnfreezeBox(GameObject box)
    {
       
       FixedJoint joint = box.GetComponent<FixedJoint>();
            if (joint != null)
            {
                Destroy(joint);
            }
    }



    #endregion

    #region Facing the Player Only While Holding


    //When the robot is holding the box it should be facing the player.
    private IEnumerator FacePlayerWhileHolding()
    {
        while (IsInStandHoldingAnimation())
        {
          
            if (playerTransform != null)
            {
                Vector3 dir = playerTransform.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dir);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turningSpeed * Time.deltaTime);
                }
            }
            yield return null;
        }
    }

    private bool IsInStandHoldingAnimation()
    {
        AnimatorStateInfo stateInfo = robotAnimator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName("HoldBox");
    }

    #endregion

    #region Movement & Rotation

    /// <summary>
    /// A single coroutine to move toward a position and/or rotate.
    /// </summary>
    private IEnumerator MoveAndFaceTarget(Vector3 destination, Transform faceTarget, bool continueFacingAfterArrival)
    {
        // Ensure we only move on the XZ-plane at the original Y level
        Vector3 finalPos = (destination == Vector3.zero) ? transform.position : destination;
        finalPos.y = originalPosition.y;

        // Move & turn until we arrive
        while (Vector3.Distance(transform.position, finalPos) > 0.1f)
        {
            // Turn toward target position
            Vector3 dir = finalPos - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f)
            {
                float angle = Quaternion.Angle(transform.rotation, Quaternion.LookRotation(dir));
                if (angle > 2f)
                {
                    SetAnimationBool("IsTurning", true);
                    float step = turningSpeed * Time.deltaTime;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), step);
                }
                else
                {
                    SetAnimationBool("IsTurning", false);
                }
            }

            Vector3 newPos = Vector3.MoveTowards(transform.position, finalPos, moveSpeed * Time.deltaTime);
            newPos.y = originalPosition.y;
            transform.position = newPos;

            yield return null;
        }

        SetAnimationBool("IsTurning", false);

        if (faceTarget != null && continueFacingAfterArrival)
        {
            while (true)
            {
                Vector3 dir = faceTarget.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dir);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turningSpeed * Time.deltaTime);
                }
                yield return null;
            }
        }
    }

    #endregion

    #region Animation & Audio Helpers

    private void SetAnimationBool(string parameter, bool value)
    {
        if (robotAnimator != null)
        {
            robotAnimator.SetBool(parameter, value);
        }
    }

    private void SetAnimationTrigger(string parameter)
    {
        if (robotAnimator != null)
        {
            robotAnimator.SetTrigger(parameter);
        }
    }

    /// <summary>
    /// Returns the length of the current animation on layer 0.
    /// </summary>
    private float GetCurrentAnimationLength()
    {
        AnimatorStateInfo stateInfo = robotAnimator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.length;
    }

    /// <summary>
    /// Plays a footstep sound (triggered by an Animation Event).
    /// </summary>
    public void OnFootstep()
    {
        if (footstepAudioSource != null && footstepClip != null && isMoving)
        {
            footstepAudioSource.pitch = Random.Range(0.90f, 1.10f);
      
            footstepAudioSource.PlayOneShot(footstepClip, 0.02f); 

            footstepAudioSource.pitch = 1f;
        }
    }


    /// <summary>
    /// Used to rotate the robot when coming back with the box.
    /// </summary>
    private IEnumerator Rotate45DegreesLeft()
    {
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = startRotation * Quaternion.Euler(0, -45f, 0);

        float duration = 0.3f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.rotation = targetRotation;
    }


    #endregion
}
