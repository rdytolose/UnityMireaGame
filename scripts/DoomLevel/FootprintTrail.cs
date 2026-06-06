using UnityEngine;

public class FootprintTrail : MonoBehaviour
{
    [Header("Footprint Settings")]
    public GameObject footprintPrefab;
    public float stepDistance = 0.5f;
    public float footprintLifetime = 30f;
    public LayerMask snowLayer;

    [Header("Foot Positions")]
    public Transform leftFoot;
    public Transform rightFoot;
    public float footOffset = 0.3f;

    private Vector3 lastFootprintPosition;
    private bool isLeftFoot = true;
    private CharacterController characterController;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        lastFootprintPosition = transform.position;
    }

    void Update()
    {
        if (characterController != null && characterController.velocity.magnitude < 0.1f)
            return;

        float distanceMoved = Vector3.Distance(transform.position, lastFootprintPosition);

        if (distanceMoved >= stepDistance)
        {
            CreateFootprint();
            lastFootprintPosition = transform.position;
            isLeftFoot = !isLeftFoot;
        }
    }

    void CreateFootprint()
    {
        Ray ray = new Ray(transform.position + Vector3.up * 0.5f, Vector3.down);

        if (Physics.Raycast(ray, out RaycastHit hit, 2f, snowLayer))
        {
            Vector3 footPosition = hit.point;
            Vector3 sideOffset = transform.right * (isLeftFoot ? -footOffset : footOffset);
            footPosition += sideOffset;

            if (footprintPrefab != null)
            {
                GameObject footprint = Instantiate(footprintPrefab, footPosition + Vector3.up * 0.01f, Quaternion.identity);

                Quaternion rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
                footprint.transform.rotation = rotation * Quaternion.Euler(90, 0, 0);

                if (isLeftFoot)
                {
                    Vector3 scale = footprint.transform.localScale;
                    scale.x *= -1;
                    footprint.transform.localScale = scale;
                }

                Destroy(footprint, footprintLifetime);
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Vector3 leftPos = transform.position + transform.right * -footOffset;
        Vector3 rightPos = transform.position + transform.right * footOffset;
        Gizmos.DrawWireSphere(leftPos, 0.1f);
        Gizmos.DrawWireSphere(rightPos, 0.1f);
    }
}
