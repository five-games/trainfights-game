using System.Runtime.CompilerServices;
using UnityEngine;

public class floating : MonoBehaviour
{
    public float strength = 1f;
    public float stiffness = 100f;
    public float damping = 10f;
    public float maxOffset = 0.2f;
    
    private Vector3 restPosition;
    private Vector3 offset;
    private Vector3 velocity;
    private Vector3 lastParentPosition;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        restPosition = transform.localPosition;
        lastParentPosition = transform.parent.position;
    }

    // Update is called once per frame
    void Update()
    {
        //difference in positions
        Vector3 currentParentPosition = transform.parent.position;
        Vector3 delta = currentParentPosition - lastParentPosition;

        Vector3 localDelta = transform.parent.InverseTransformDirection(delta); ;

        offset -= localDelta * strength;

        //getting camera back
        Vector3 force = -stiffness * offset - damping * velocity;
        velocity += force * Time.deltaTime;
        offset += velocity * Time.deltaTime;

        offset = Vector3.ClampMagnitude(offset, maxOffset);

        transform.localPosition = restPosition + offset;

        lastParentPosition = currentParentPosition;
    }
}
