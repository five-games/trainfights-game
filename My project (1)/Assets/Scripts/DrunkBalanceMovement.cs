using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DrunkBalanceMovement : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 2.5f;
    public float runSpeed = 5f;

    [Tooltip("Jak rychle se tělo snaží přiblížit cílové horizontální rychlosti (síla).")]
    public float moveForce = 35f;

    [Tooltip("Brzdění bez inputu (síla proti současné rychlosti).")]
    public float brakeForce = 25f;

    [Header("Step / Footy Feel")]
    [Tooltip("Po kolika sekundách zhruba padne další krok při plném inputu.")]
    public float stepInterval = 0.28f;

    [Tooltip("Impuls do pohybu na začátku kroku. Dává pocit jednotlivých kroků.")]
    public float stepImpulse = 0.9f;

    [Tooltip("Kolik se stepInterval zkrátí při sprintu (např. 0.8 = rychlejší kroky).")]
    public float sprintStepIntervalMultiplier = 0.8f;

    [Header("Drunk / Goofy Feel")]
    [Range(0f, 0.6f)]
    public float drunkWobble = 0.12f;
    public float wobbleSpeed = 1.3f;

    [Tooltip("Náhodné vychylování rovnováhy (náklon).")]
    public float drunkLean = 6f;

    [Header("Overshoot (došlap po zastavení)")]
    [Range(0f, 1.5f)]
    public float overshootStrength = 0.75f;
    public float overshootDuration = 0.25f;

    [Header("Balance / Upright (TABS-ish)")]
    [Tooltip("Síla, která tělo zvedá do vzpřímené polohy.")]
    public float uprightSpring = 250f;

    [Tooltip("Tlumení vyrovnávání (větší = méně rozkmitané).")]
    public float uprightDamping = 35f;

    [Tooltip("Maximální povolený náklon (stupně). Když je menší, postava působí stabilněji.")]
    public float maxLeanAngle = 35f;

    [Header("Ground Check")]
    public LayerMask groundMask = ~0;
    public float groundCheckDistance = 0.2f;

    [Header("Optional")]
    public bool useAnimator = true;

    Rigidbody _rb;
    Animator _animator;

    Vector3 _rawInput;
    Vector3 _moveDir;          // opilecky upravený směr
    bool _isGrounded;
    bool _wasMoving;
    bool _isSprinting;

    float _overshootTimer;
    Vector3 _overshootDir;

    float _wobbleTime;
    float _stepTimer;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        // DŮLEŽITÉ: Nezamrazovat rotaci, protože rotace = balanc.
        // Místo toho rotaci stabilizujeme pružinou (uprightSpring).
        _rb.freezeRotation = false;

        _animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        ReadInput();
        UpdateAnimator();
    }

    void FixedUpdate()
    {
        CheckGround();

        ApplyUprightBalance();   // drží vzpřímeně, ale dovolí "vyrovnávání"
        ApplyMovementForces();   // síly do pohybu
        ApplyStepCycle();        // krokové impulzy
        ApplyOvershoot();        // došlap po zastavení
        SoftClampLean();         // aby se tělo nepřeklápělo do extrému
    }

    void ReadInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        _rawInput = new Vector3(h, 0f, v);
        Vector3 raw = _rawInput.sqrMagnitude > 1f ? _rawInput.normalized : _rawInput;

        _isSprinting = Input.GetKey(KeyCode.LeftShift) && raw.magnitude > 0.01f;

        // Opilecký wobble do směru
        _wobbleTime += Time.deltaTime * wobbleSpeed;
        float wx = (Mathf.PerlinNoise(_wobbleTime, 0f) - 0.5f) * 2f * drunkWobble;
        float wz = (Mathf.PerlinNoise(0f, _wobbleTime + 99f) - 0.5f) * 2f * drunkWobble;

        _moveDir = raw.magnitude > 0.01f
            ? (raw + new Vector3(wx, 0f, wz)).normalized
            : Vector3.zero;

        bool isMovingNow = _moveDir.magnitude > 0.01f;

        // Detekuj zastavení → overshoot (došlap)
        if (_wasMoving && !isMovingNow)
        {
            Vector3 flatVel = FlatVelocity();
            if (flatVel.magnitude > 0.6f)
                TriggerOvershoot(flatVel);
        }

        _wasMoving = isMovingNow;
    }

    void ApplyMovementForces()
    {
        if (!_isGrounded) return;

        float maxSpeed = _isSprinting ? runSpeed : walkSpeed;

        Vector3 flatVel = FlatVelocity();
        Vector3 desiredVel = _moveDir * maxSpeed;

        // rozjezd: přibližuj se k desiredVel
        if (_moveDir.magnitude > 0.01f)
        {
            Vector3 velDiff = desiredVel - flatVel;
            _rb.AddForce(velDiff * moveForce, ForceMode.Acceleration);
        }
        else
        {
            // brzdění: proti současné rychlosti
            _rb.AddForce(-flatVel * brakeForce, ForceMode.Acceleration);
        }

        // tvrdý clamp max speed (aby to nelítalo při impulsech)
        flatVel = FlatVelocity();
        if (flatVel.magnitude > maxSpeed)
        {
            Vector3 clamped = flatVel.normalized * maxSpeed;
            _rb.linearVelocity = new Vector3(clamped.x, _rb.linearVelocity.y, clamped.z);
        }
    }

    void ApplyStepCycle()
    {
        if (!_isGrounded) return;

        if (_moveDir.magnitude < 0.01f)
        {
            _stepTimer = 0f;
            return;
        }

        float interval = stepInterval * (_isSprinting ? sprintStepIntervalMultiplier : 1f);

        // rychlejší kroky, když se fakt hýbeš (aby při malém inputu nebyl spam)
        float speed01 = Mathf.InverseLerp(0.5f, runSpeed, FlatVelocity().magnitude);
        float intervalScaled = Mathf.Lerp(interval * 1.35f, interval, speed01);

        _stepTimer += Time.fixedDeltaTime;

        if (_stepTimer >= intervalScaled)
        {
            _stepTimer = 0f;

            // krok = krátký impuls dopředu + lehký boční "goofy" šťouch
            Vector3 dir = _moveDir;

            float side = (Mathf.PerlinNoise(_wobbleTime + 50f, 1.23f) - 0.5f) * 2f;
            Vector3 sideDir = Vector3.Cross(Vector3.up, dir).normalized;

            Vector3 impulse = (dir + sideDir * side * drunkWobble * 2f).normalized * stepImpulse;

            // Impuls jen horizontálně
            impulse.y = 0f;
            _rb.AddForce(impulse, ForceMode.VelocityChange);
        }
    }

    void TriggerOvershoot(Vector3 flatVel)
    {
        _overshootTimer = overshootDuration;
        _overshootDir = flatVel.normalized;
    }

    void ApplyOvershoot()
    {
        if (_overshootTimer <= 0f) return;
        _overshootTimer -= Time.fixedDeltaTime;

        // ease-out
        float t = Mathf.Clamp01(_overshootTimer / overshootDuration);
        float strength = (t * t) * overshootStrength;

        // dorovnávací došlap = malý impuls ve směru předchozí rychlosti
        _rb.AddForce(_overshootDir * strength, ForceMode.VelocityChange);
    }

    void ApplyUprightBalance()
    {
        // Cíl: držet "up" vektor těla směrem k Vector3.up
        // torque ~ spring * angleError - damping * angularVelocity

        // opilecký lean (malé náhodné vychylování)
        float leanX = (Mathf.PerlinNoise(_wobbleTime + 10f, 0f) - 0.5f) * 2f * drunkLean;
        float leanZ = (Mathf.PerlinNoise(0f, _wobbleTime + 20f) - 0.5f) * 2f * drunkLean;

        Quaternion drunkTilt = Quaternion.Euler(leanX, 0f, leanZ);

        // upright target = "bezpečně vzpřímeně", plus malý drunken tilt
        Quaternion targetRot = Quaternion.LookRotation(ForwardProjectedOnPlane(), Vector3.up) * drunkTilt;

        // chyba rotace
        Quaternion q = targetRot * Quaternion.Inverse(_rb.rotation);
        q.ToAngleAxis(out float angle, out Vector3 axis);

        if (angle > 180f) angle -= 360f;

        // torque osa
        Vector3 torque = axis * (angle * Mathf.Deg2Rad * uprightSpring) - _rb.angularVelocity * uprightDamping;
        _rb.AddTorque(torque, ForceMode.Acceleration);
    }

    // Pomůže, aby "forward" nebyl náhodný, když stojíš.
    Vector3 ForwardProjectedOnPlane()
    {
        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        return fwd.normalized;
    }

    void SoftClampLean()
    {
        // Jemný clamp náklonu – když se překlopí moc, přitáhni zpátky.
        float angleFromUp = Vector3.Angle(transform.up, Vector3.up);
        if (angleFromUp <= maxLeanAngle) return;

        // čím víc mimo, tím víc vracej
        float excess = angleFromUp - maxLeanAngle;
        Vector3 axis = Vector3.Cross(transform.up, Vector3.up).normalized;
        _rb.AddTorque(axis * (excess * Mathf.Deg2Rad * uprightSpring * 0.6f), ForceMode.Acceleration);
    }

    void UpdateAnimator()
    {
        if (!useAnimator || _animator == null) return;

        float speedRatio = FlatVelocity().magnitude / runSpeed; // 0..1
        _animator.SetFloat("Speed", speedRatio);

        // Pokud chceš: krokování (pro přepínání left/right step animací)
        _animator.SetBool("Grounded", _isGrounded);
    }

    void CheckGround()
    {
        _isGrounded = Physics.Raycast(
            transform.position + Vector3.up * 0.05f,
            Vector3.down,
            groundCheckDistance + 0.05f,
            groundMask
        );
    }

    Vector3 FlatVelocity()
    {
        Vector3 v = _rb.linearVelocity;
        v.y = 0f;
        return v;
    }

    // Debug / API
    public Vector3 InputDirection => _moveDir;
    public float CurrentSpeed => FlatVelocity().magnitude;
    public bool IsGrounded => _isGrounded;
}