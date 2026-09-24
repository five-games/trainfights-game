using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SelfBalancedWalker : MonoBehaviour
{
    [Header("Refs")]
    public Transform footL;
    public Transform footR;
    public Transform visualRoot; // volitelné: mesh/armature, aby se natáčel

    [Header("Ground")]
    public LayerMask groundMask = ~0;
    public float bodyRayHeight = 0.4f;
    public float bodyRayLength = 1.2f;

    [Header("Balance (vertical spring)")]
    public float desiredBodyHeight = 0.95f;     // cílová vzdálenost těla od země
    public float heightSpring = 450f;           // síla pružiny
    public float heightDamping = 55f;           // tlumení (proti kmitání)
    public float extraGravity = 25f;            // přidaná "tíha" pro lepší kontakt

    [Header("Move")]
    public float moveForce = 60f;
    public float maxSpeed = 5f;
    public float braking = 12f;

    [Header("Steps")]
    public float stepDistance = 0.55f;          // když je tělo moc daleko od nohy → krok
    public float stepForward = 0.45f;           // kam dopředu položit nohu
    public float stepSide = 0.22f;              // jak daleko do strany (šířka postoje)
    public float stepUp = 0.15f;                // nadzvednutí při kroku (jen vizuál targetu)
    public float stepSpeed = 10f;               // rychlost přesunu nohy
    public float stepCooldown = 0.18f;          // aby se kroky nemlátily
    public float minMoveToStep = 0.15f;         // při skoro nulovém inputu dělej míň kroků

    [Header("Goofy")]
    [Range(0f, 0.35f)] public float drunkWobble = 0.10f;
    public float wobbleSpeed = 1.2f;

    Rigidbody _rb;

    Vector3 _inputDir;
    float _wobbleT;

    float _cooldownT;
    bool _leftNext = true;

    Vector3 _footLTarget;
    Vector3 _footRTarget;

    bool _footLStepping;
    bool _footRStepping;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true; // zatím držíme tělo vzpřímeně "jednoduše"; ragdoll rotace můžeš přidat později

        if (footL) _footLTarget = footL.position;
        if (footR) _footRTarget = footR.position;
    }

    void Update()
    {
        ReadInput();

        // volitelně natoč vizuál podle pohybu
        if (visualRoot && _inputDir.sqrMagnitude > 0.001f)
        {
            Quaternion target = Quaternion.LookRotation(_inputDir, Vector3.up);
            visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation, target, Time.deltaTime * 12f);
        }
    }

    void FixedUpdate()
    {
        _cooldownT -= Time.fixedDeltaTime;

        ApplyExtraGravity();
        ApplyHeightSpring();
        ApplyMoveForces();

        UpdateFootTargets();
        SolveStep(footL, ref _footLTarget, ref _footLStepping, true);
        SolveStep(footR, ref _footRTarget, ref _footRStepping, false);
    }

    void ReadInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 raw = new Vector3(h, 0f, v);
        raw = raw.sqrMagnitude > 1f ? raw.normalized : raw;

        _wobbleT += Time.deltaTime * wobbleSpeed;
        float wx = (Mathf.PerlinNoise(_wobbleT, 0f) - 0.5f) * 2f * drunkWobble;
        float wz = (Mathf.PerlinNoise(0f, _wobbleT + 33f) - 0.5f) * 2f * drunkWobble;

        _inputDir = raw.magnitude > 0.01f ? (raw + new Vector3(wx, 0f, wz)).normalized : Vector3.zero;
    }

    void ApplyExtraGravity()
    {
        _rb.AddForce(Vector3.down * extraGravity, ForceMode.Acceleration);
    }

    void ApplyHeightSpring()
    {
        // raycast dolů z těla → drž "desiredBodyHeight" od země
        Vector3 origin = transform.position + Vector3.up * bodyRayHeight;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, bodyRayLength, groundMask, QueryTriggerInteraction.Ignore))
        {
            float currentHeight = hit.distance - bodyRayHeight; // vzdálenost od země (přibližně)
            float error = desiredBodyHeight - currentHeight;

            // spring-damper na Y
            float velY = _rb.linearVelocity.y;
            float forceY = (error * heightSpring) - (velY * heightDamping);

            _rb.AddForce(Vector3.up * forceY, ForceMode.Acceleration);
        }
    }

    void ApplyMoveForces()
    {
        Vector3 vel = _rb.linearVelocity;
        Vector3 flatVel = new Vector3(vel.x, 0f, vel.z);

        if (_inputDir.magnitude > 0.01f)
        {
            _rb.AddForce(_inputDir * moveForce, ForceMode.Acceleration);
        }
        else
        {
            // brzdění když není input
            _rb.AddForce(-flatVel * braking, ForceMode.Acceleration);
        }

        // clamp max speed
        flatVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        if (flatVel.magnitude > maxSpeed)
        {
            Vector3 clamped = flatVel.normalized * maxSpeed;
            _rb.linearVelocity = new Vector3(clamped.x, _rb.linearVelocity.y, clamped.z);
        }
    }

    void UpdateFootTargets()
    {
        // pokud nemáš input, default forward = current forward (aby nohy nelítaly)
        Vector3 fwd = _inputDir.sqrMagnitude > 0.001f ? _inputDir : transform.forward;
        fwd.y = 0f;
        fwd = fwd.sqrMagnitude < 0.0001f ? Vector3.forward : fwd.normalized;

        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

        // ideální pozice nohou relativně k tělu
        Vector3 basePos = transform.position;
        Vector3 lIdeal = basePos + right * (-stepSide) + fwd * stepForward;
        Vector3 rIdeal = basePos + right * (stepSide) + fwd * stepForward;

        // promítni na zem
        _footLTarget = ProjectToGround(_footLTarget, lIdeal);
        _footRTarget = ProjectToGround(_footRTarget, rIdeal);
    }

    Vector3 ProjectToGround(Vector3 current, Vector3 desired)
    {
        Vector3 origin = desired + Vector3.up * 1.0f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2.5f, groundMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }
        return current;
    }

    void SolveStep(Transform foot, ref Vector3 target, ref bool stepping, bool isLeft)
    {
        if (!foot) return;

        Vector3 bodyFlat = transform.position; bodyFlat.y = 0f;
        Vector3 footFlat = foot.position; footFlat.y = 0f;

        float dist = Vector3.Distance(bodyFlat, footFlat);
        float moveMag = _inputDir.magnitude;

        bool otherStepping = isLeft ? _footRStepping : _footLStepping;

        // start step?
        if (!stepping && !otherStepping && _cooldownT <= 0f)
        {
            if (dist > stepDistance && moveMag > minMoveToStep)
            {
                // střídání nohou L/R (aby se nemlátily)
                if ((_leftNext && isLeft) || (!_leftNext && !isLeft))
                {
                    stepping = true;
                    _cooldownT = stepCooldown;
                    _leftNext = !_leftNext;
                }
            }
        }

        // move foot towards target (s malým "lift")
        if (stepping)
        {
            Vector3 cur = foot.position;
            Vector3 mid = (cur + target) * 0.5f;
            mid.y += stepUp;

            // jednoduchá "trajektorie": nejdřív do mid, pak do target
            Vector3 next = Vector3.MoveTowards(cur, mid, Time.fixedDeltaTime * stepSpeed);
            if (Vector3.Distance(next, mid) < 0.02f)
                next = Vector3.MoveTowards(cur, target, Time.fixedDeltaTime * stepSpeed);

            foot.position = next;

            // hotovo?
            if (Vector3.Distance(foot.position, target) < 0.03f)
                stepping = false;
        }
        else
        {
            // když nešlape, drž nohu na targetu jemně
            foot.position = Vector3.Lerp(foot.position, target, Time.fixedDeltaTime * stepSpeed);
        }
    }
}