using Unity.VisualScripting;
using UnityEngine;

public class player : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 5f;
    public float smoothTime = 0.1f;
    private Vector3 currentVelocity;
    private Vector3 moveInput;

    [Header("Jump")]
    public float jumpVelocity = 7f;
    public Transform groundCheck;
    public LayerMask ground;
    public float groundCheckRadius = 0.2f;
    private bool isGrounded;

    [Header("Camera")]
    public float Sensitivity = 150f;
    public Transform camera;
    private float xRotation = 0f;

    private Rigidbody rb;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        Cursor.lockState = CursorLockMode.Locked;
    }

    // Update is called once per frame
    void Update()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, ground);

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        moveInput = new Vector3(x, 0, z).normalized;

        jump();

        CameraRotation();
    }

    void FixedUpdate()
    {
        Vector3 targetVelocity = transform.TransformDirection(moveInput) * speed;
        Vector3 smoothVelocity = Vector3.SmoothDamp(rb.linearVelocity, targetVelocity, ref currentVelocity, smoothTime);

        smoothVelocity.y = rb.linearVelocity.y;

        rb.linearVelocity = smoothVelocity;
    }

    public void jump()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpVelocity, ForceMode.Impulse);
        }
    }

    public void CameraRotation()
    {
        float mouseX = Input.GetAxis("Mouse X") * Sensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * Sensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        camera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, mouseX, 0f));
    }

}
