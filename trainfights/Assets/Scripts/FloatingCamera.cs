using UnityEngine;

public class FloatingCamera : MonoBehaviour
{
    [Header("Floating")]
    public float strength = 1f;
    public float stiffness = 100f;
    public float damping = 10f;
    public float maxOffset = 0.2f;

    [Header("Walking Bobbing")]
    public float bobAmount = 0.035f;
    public float bobSpeed = 7f;
    public float bobReturnSpeed = 8f;

    private Vector3 restPosition;
    private Vector3 offset;
    private Vector3 velocity;
    private Vector3 lastParentPosition;

    private float bobTimer;
    private float currentBobOffset;

    void Start()
    {
        restPosition = transform.localPosition;
        lastParentPosition = transform.parent.position;
    }

    void Update()
    {
        Vector3 currentParentPosition = transform.parent.position;
        Vector3 delta = currentParentPosition - lastParentPosition;

        //floating

        Vector3 localDelta =
            transform.parent.InverseTransformDirection(delta);

        offset -= localDelta * strength;

        Vector3 force =-stiffness * offset - damping * velocity;

        velocity += force * Time.deltaTime;
        offset += velocity * Time.deltaTime;

        offset = Vector3.ClampMagnitude(offset, maxOffset);

        //walk bobbing

        Vector3 horizontalDelta = delta;
        horizontalDelta.y = 0f;

        bool isMoving = horizontalDelta.sqrMagnitude > 0.000001f;

        if (isMoving)
        {
            bobTimer += Time.deltaTime * bobSpeed;

            float targetBobOffset = Mathf.Sin(bobTimer) * bobAmount;

            currentBobOffset = Mathf.Lerp(
                currentBobOffset,
                targetBobOffset,
                15f * Time.deltaTime
            );
        }
        else
        {
            currentBobOffset = Mathf.Lerp(
                currentBobOffset,
                0f,
                bobReturnSpeed * Time.deltaTime
            );
        }

        // Floating + bobbing together
        transform.localPosition =
            restPosition
            + offset
            + Vector3.up * currentBobOffset;

        lastParentPosition = currentParentPosition;
    }
}