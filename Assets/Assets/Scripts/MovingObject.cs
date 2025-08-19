using UnityEngine;

public class MovingObject : MonoBehaviour
{
    [Header("Controls")]
    [SerializeField] private GameObject point1;
    [SerializeField] private GameObject point2;
    [SerializeField] private GameObject objectToMove;
    [SerializeField] private float speed = 1f;
    [SerializeField] private float positionTolerance = 0.01f;
    [SerializeField] private bool startAtPoint1 = true;

    private Vector3 targetPos;

    void Start()
    {
        if (point1 == null || point2 == null || objectToMove == null) return;

        // set starting position and target
        if (startAtPoint1)
        {
            objectToMove.transform.position = point1.transform.position;
            targetPos = point2.transform.position;
        }
        else
        {
            objectToMove.transform.position = point2.transform.position;
            targetPos = point1.transform.position;
        }
    }

    void Update()
    {
        if (point1 == null || point2 == null || objectToMove == null) return;

        // move toward current target
        objectToMove.transform.position = Vector2.MoveTowards(
            objectToMove.transform.position,
            targetPos,
            speed * Time.deltaTime
        );

        // if close enough, flip to the other point
        if ((objectToMove.transform.position - targetPos).sqrMagnitude <= positionTolerance * positionTolerance)
        {
            targetPos = (targetPos == point1.transform.position)
                ? point2.transform.position
                : point1.transform.position;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (point1 == null || point2 == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(point1.transform.position, point2.transform.position);
        Gizmos.DrawWireSphere(point1.transform.position, 0.05f);
        Gizmos.DrawWireSphere(point2.transform.position, 0.05f);
    }
}
