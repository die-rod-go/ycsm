using UnityEngine;
using UnityEngine.SceneManagement;

enum Direction
{ 
    LEFT, RIGHT, NONE
}

public class BulletMovement : MonoBehaviour
{
    [Header("Rendering Stuff")]
    private LineRenderer lineRenderer;

    [Header("Motion Stuff")]
    [SerializeField] private float speed;
    [SerializeField] private float speedWhenGrappled;   // the bullet "feels" slightly slower when grappled so i artificially speed it up
    [SerializeField] private float radius;        // bullet "thickness"
    [SerializeField] private float skin;       // tiny nudge off surfaces

    [Header("Grapple Stuff")]
    [SerializeField] private float grappleRange = 12f;
    [SerializeField] private float ropeLength;
    private Vector2? grapplePoint;

    private Vector2 heading = Vector2.right;

    // input rollover state
    private Direction currentHeld = Direction.NONE; //  who owns the direction now
    private Direction pending = Direction.NONE; //  who takes over if still held on release
    [SerializeField] private Direction armed = Direction.RIGHT; //  default next grapple direction
    [SerializeField] private Direction lastArmed = Direction.RIGHT;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 2; 
        lineRenderer.enabled = false;
        lineRenderer.startWidth = 0.01f;
        lineRenderer.endWidth = 0.01f;

        heading = Vector2.right;
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
            transform.Translate(heading * moveSpeed * deltaTime);
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
                // reflect
                heading = Vector2.Reflect(heading, hit.normal).normalized;
            }
            else
            {
                Debug.Log("hit a wall or something");
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
        // calc direction to shoot grapple
        Vector2 aimDir = (fireDirection == Direction.LEFT)
        ? new Vector2(-heading.y, heading.x)   // left perp (+90 deg)
        : new Vector2(heading.y, -heading.x);  // right perp (-90 deg)

        // shoot ray
        RaycastHit2D hit = Physics2D.Raycast(transform.position, aimDir, grappleRange);

        var collider = hit.collider;

        if (collider != null)
        {
            // we hit a wall we can grapple
            if (collider.CompareTag("GrippableWall"))
            {
                Debug.Log("Let's GOOOO");
                grapplePoint = hit.point;
                ropeLength = Vector2.Distance(transform.position, grapplePoint.Value);
                Debug.DrawRay(transform.position, aimDir * hit.distance, Color.green, 0.15f);
            }
            else
            {
                Debug.Log("Grapple Hit Non-Grippable-Surface");
            }

        }
        else
        {
            Debug.Log("Grapple Hit Nothing");
        }
    }

    void ReleaseGrapple()
    {
        grapplePoint = null;
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
        if(grapplePoint != null)
            Debug.DrawLine(transform.position, new Vector3(grapplePoint.Value.x, grapplePoint.Value.y), Color.green);
    }
}

