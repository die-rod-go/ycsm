using UnityEngine;
using UnityEngine.SceneManagement;

enum Direction
{
    LEFT, RIGHT, NONE
}

public class BulletMovement : MonoBehaviour
{
    // readable sttate for other shit
    public Vector2 BulletPosition => transform.position;
    public Vector2 HeadingDir => heading;
    public bool GrappleActive => grapplePoint.HasValue;
    public Vector2? GrappleAnchor => grapplePoint;   // null if not latched
    public float RopeLengthMeters => ropeLength;

    // events
    public event System.Action<Vector2, Vector2> onGrappleFired; // location direction
    public event System.Action onGrappleLatched;
    public event System.Action<Vector2> onGrappleMissed;  // failure Location
    public event System.Action onGrappleReleased;
    public event System.Action<Vector2, Vector2> onRicochet;       // point, normal
    public event System.Action onDeath;


    [Header("Rendering Stuff")]
    private LineRenderer lineRenderer;

    [Header("Motion Stuff")]
    [SerializeField] private float speed;
    [SerializeField] private float speedWhenGrappled;   // the bullet "feels" slightly slower when grappled so i artificially speed it up
    [SerializeField] private float radius;        // bullet "thickness"
    [SerializeField] private float skin;       // tiny nudge off surfaces when reflecting

    [Header("Grapple Stuff")]
    [SerializeField] private float grappleRange = 12f;
    [SerializeField] private float ropeLength;
    [SerializeField] private float grappleOffset = 0.5f; // used to shoot other adjacent grapples for game feel purposes

    private Vector2? grapplePoint;

    private Vector2 heading = Vector2.right;

    // input rollover state
    private Direction currentHeld = Direction.NONE; //  who owns the direction now
    private Direction pending = Direction.NONE; //  who takes over if still held on release
    private Direction armed = Direction.RIGHT; //  default next grapple direction
    private Direction lastArmed = Direction.RIGHT;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.enabled = false;
        lineRenderer.startWidth = 0.01f;
        lineRenderer.endWidth = 0.01f;

        heading = new Vector2(1, 0);
    }

    void Update()
    {
        ReadInput();
        MoveBullet();
        drawMiscDebug();
        RenderGrapple();
    }


    void ReadInput()
    {
        // KeyDown: establish who owns the input
        // if nothing is currently held, the new key takes control (currentHeld).
        // if something is already held, the new key becomes "pending".
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            if (currentHeld == Direction.NONE) currentHeld = Direction.LEFT;
            else pending = Direction.LEFT;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            if (currentHeld == Direction.NONE) currentHeld = Direction.RIGHT;
            else pending = Direction.RIGHT;
        }

        // KeyUp: release ownership. transfer if a pending key is still held
        // if the released key was the current owner, check if the pending key
        // is still being held down. If yes, it takes over. If not, nothing is held.
        // if the released key was only pending, clear it (ignored).
        if (Input.GetKeyUp(KeyCode.LeftArrow) || Input.GetKeyUp(KeyCode.A))
        {
            if (currentHeld == Direction.LEFT)
            {
                // if RIGHT was pending and is still being held -> transfer control to RIGHT
                if (pending == Direction.RIGHT && (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)))
                {
                    currentHeld = Direction.RIGHT;
                    pending = Direction.NONE;
                }
                else
                {
                    // no valid pending -> release control
                    currentHeld = Direction.NONE;
                }
            }
            else if (pending == Direction.LEFT)
            {
                // if LEFT was pending but released first, ignore it
                pending = Direction.NONE;
            }
        }

        if (Input.GetKeyUp(KeyCode.RightArrow) || Input.GetKeyUp(KeyCode.D))
        {
            if (currentHeld == Direction.RIGHT)
            {
                // if LEFT was pending and is still being held -> transfer control to LEFT
                if (pending == Direction.LEFT && (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)))
                {
                    currentHeld = Direction.LEFT;
                    pending = Direction.NONE;
                }
                else
                {
                    // no valid pending -> release control
                    currentHeld = Direction.NONE;
                }
            }
            else if (pending == Direction.RIGHT)
            {
                // if RIGHT was pending but released first, ignore it
                pending = Direction.NONE;
            }
        }

        // update the armed direction
        // the "armed" direction is the one that the next grapple will use.
        // if no keys are held, it's NONE
        armed = currentHeld;


        // check for change -> fire grapple
        if (armed != lastArmed)
        {
            ReleaseGrapple();
            FireGrapple(armed);
        }

        lastArmed = armed;
    }

    void MoveBullet()
    {
        float deltaTime = Time.deltaTime;
        // if we have a grapple point, rotate heading so it's tangent to the circle around the anchor.
        if (grapplePoint != null)
        {
            Vector2 grapplePointPosition = grapplePoint.Value;
            Vector2 arcRadius = (Vector2)transform.position - grapplePointPosition;
            if (arcRadius.sqrMagnitude > 0.0001f) // grapple is right next to bullet
            {
                // Compute both tangents (+90° and -90°) and pick the one closest to current heading
                Vector2 tLeft = new Vector2(-arcRadius.y, arcRadius.x).normalized;
                Vector2 tRight = new Vector2(arcRadius.y, -arcRadius.x).normalized;

                // Choose the tangent that best matches our current heading to avoid sudden flips
                heading = (Vector2.Dot(heading, tLeft) >= Vector2.Dot(heading, tRight)) ? tLeft : tRight;
            }
        }

        float moveSpeed = (grapplePoint == null) ? speed : speedWhenGrappled;
        // sweep a circle from current position to where the bullet will go this frame
        // not entirely sure how it works yet tbh
        RaycastHit2D hit = Physics2D.CircleCast(transform.position, radius, heading, moveSpeed * deltaTime);

        var collider = hit.collider;

        if (collider == null)
        {
            transform.Translate(heading * moveSpeed * deltaTime, Space.World);
        }
        // handle collisions and stuff
        else
        {
            // move to the impact point
            Vector2 nudge = hit.normal * skin;
            transform.position = hit.point + nudge;

            // decide what we hit by tag
            if (collider.CompareTag("Target"))
            {
                Debug.Log("TARGET HIT");
            }
            else if (collider.CompareTag("RicochetWall"))
            {
                Debug.Log("Ricochet");
                onRicochet?.Invoke(hit.point, hit.normal);
                // reflect
                heading = Vector2.Reflect(heading, hit.normal).normalized;
            }
            else
            {
                Debug.Log("hit a wall or something");
                onDeath?.Invoke();
                // you dead
                // reload scene - need to fix up this is just for testing
                string currentSceneName = SceneManager.GetActiveScene().name;
                SceneManager.LoadScene(currentSceneName);
            }
        }

        //  prevent bullet drift
        if (grapplePoint != null)
        {
            Vector2 grapplePointPosition = grapplePoint.Value;
            Vector2 arcRadius = (Vector2)transform.position - grapplePointPosition;
            if (arcRadius.sqrMagnitude > 0.0001f)
                transform.position = grapplePointPosition + arcRadius.normalized * ropeLength;
        }

        //  renormalize just in case
        heading = heading.normalized;

        //  rotate gameobject to face heading direction (flip that shit)
        //  https://discussions.unity.com/t/rotate-2d-sprite-towards-moving-direction/94695
        float angle = Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        // debug stuff
        if (hit.collider) Debug.DrawRay(hit.point, hit.normal * 0.4f, Color.yellow, 0.1f);
        // render heading
        Debug.DrawLine(transform.position, transform.position + new Vector3(heading.x, heading.y), Color.white, 0f);
        // where it tried to go and the normal if it hit
        Debug.DrawRay(transform.position, heading * speed * deltaTime, Color.cyan, 0f);
    }

    void FireGrapple(Direction fireDirection)
    {
        if (fireDirection == Direction.NONE)
            return;

        // base perpendicular aim (same as before)
        Vector2 dir = (fireDirection == Direction.LEFT)
            ? new Vector2(-heading.y, heading.x)   // +90°
            : new Vector2(heading.y, -heading.x); // -90°
        dir = dir.normalized;

        // three origins along heading: backward, center, forward
        Vector2 originCenter = transform.position;
        Vector2 originForward = originCenter + heading.normalized * grappleOffset;
        Vector2 originBack = originCenter - heading.normalized * grappleOffset;

        onGrappleFired?.Invoke(originCenter, dir);

        // cast rays from each origin, all in the same perpendicular direction
        RaycastHit2D hitCenter = Physics2D.Raycast(originCenter, dir, grappleRange);
        RaycastHit2D hitForward = Physics2D.Raycast(originForward, dir, grappleRange);
        RaycastHit2D hitBack = Physics2D.Raycast(originBack, dir, grappleRange);

        // choose with a strong bias for the center ray
        RaycastHit2D chosen = default;

        if (hitCenter.collider != null && hitCenter.collider.CompareTag("GrippableWall"))
        {
            // take center immediately if valid
            chosen = hitCenter;
        }
        else
        {
            bool frontRayHit = hitForward.collider != null && hitForward.collider.CompareTag("GrippableWall");
            bool backRayHit = hitBack.collider != null && hitBack.collider.CompareTag("GrippableWall");

            if (frontRayHit && backRayHit)
            {
                // prefer the hit whose world hit-point is closer to the current position
                float distanceFront = Vector2.Distance(originCenter, hitForward.point);
                float distanceBack = Vector2.Distance(originCenter, hitBack.point);
                chosen = (distanceFront <= distanceBack) ? hitForward : hitBack;
            }
            else if (frontRayHit) chosen = hitForward;
            else if (backRayHit) chosen = hitBack;
        }

        if (chosen.collider != null)
        {
            grapplePoint = chosen.point;
            ropeLength = Vector2.Distance(transform.position, grapplePoint.Value);
            onGrappleLatched?.Invoke();
            Debug.Log("LETS GOOO");
        }
        else
        {
            Vector2 failure = originCenter + dir * grappleRange;
            onGrappleMissed?.Invoke(failure);
            Debug.Log("grapple hit invalid");
        }
    }

    void ReleaseGrapple()
    {
        grapplePoint = null;
        onGrappleReleased?.Invoke();
        Debug.Log("Grapple Released");
    }

    void RenderGrapple()
    {
        if (grapplePoint != null)
        {
            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, grapplePoint.Value);
        }
        else
        {
            lineRenderer.enabled = false;
        }
    }
    private void drawMiscDebug()
    {
        if (grapplePoint != null)
            Debug.DrawLine(transform.position, new Vector3(grapplePoint.Value.x, grapplePoint.Value.y), Color.green);
    }
}
