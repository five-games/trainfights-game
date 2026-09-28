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
    public Transform cameraTransform;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, ground);

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        moveInput = new Vector3(x, 0, z).normalized;

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpVelocity, ForceMode.Impulse);
        }
    }

    void FixedUpdate()
    {
        // tělo se natočí podle toho, kam kouká kamera (jen do stran)
        float yaw = cameraTransform.eulerAngles.y;
        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
        rb.MoveRotation(yawRotation);

        Vector3 targetVelocity = yawRotation * moveInput * speed;
        Vector3 smoothVelocity = Vector3.SmoothDamp(rb.linearVelocity, targetVelocity, ref currentVelocity, smoothTime);
        smoothVelocity.y = rb.linearVelocity.y;
        rb.linearVelocity = smoothVelocity;
    }
}
