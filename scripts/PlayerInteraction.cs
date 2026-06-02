using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Refs")]
    public Camera playerCamera;
    public Transform holdPoint;

    [Header("Pickup")]
    public float pickupRange = 4f;
    public LayerMask pickupMask;
    public float holdForce = 1500f;
    public float holdDamping = 15f;
    public float maxHoldDistance = 6f;
    public float throwForce = 8f;

    [Header("Scroll")]
    public float minHoldDistance = 1f;
    public float maxScrollDistance = 4f;
    public float scrollSpeed = 2f;

    [Header("Highlight")]
    public Color highlightColor = new Color(0.2f, 0.6f, 1f, 1f);
    public float highlightWidth = 5f;

    private Rigidbody heldObject;
    private float currentHoldDistance;
    private float originalAngularDrag;
    private float originalDrag;

    private Outline currentHoverOutline;
    private GameObject currentHoverObject;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (heldObject == null) TryPickup();
            else DropObject(false);
        }

        if (heldObject != null)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
                currentHoldDistance = Mathf.Clamp(currentHoldDistance + scroll * scrollSpeed,
                                                  minHoldDistance, maxScrollDistance);

            if (Input.GetKeyDown(KeyCode.R)) DropObject(true);
        }

        UpdateHoverHighlight();
    }

    void FixedUpdate()
    {
        if (heldObject == null) return;

        Vector3 targetPos = playerCamera.transform.position
                          + playerCamera.transform.forward * currentHoldDistance;

        if (Vector3.Distance(heldObject.position, targetPos) > maxHoldDistance)
        {
            DropObject(false);
            return;
        }

        Vector3 toTarget = targetPos - heldObject.position;
        Vector3 desiredVelocity = toTarget * holdForce * Time.fixedDeltaTime;
        heldObject.linearVelocity = Vector3.Lerp(heldObject.linearVelocity, desiredVelocity,
                                                 holdDamping * Time.fixedDeltaTime);
    }

    void UpdateHoverHighlight()
    {
        if (heldObject != null) { ClearHover(); return; }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupMask))
        {
            GameObject go = hit.collider.attachedRigidbody
                ? hit.collider.attachedRigidbody.gameObject
                : hit.collider.gameObject;

            if (go != currentHoverObject)
            {
                ClearHover();
                currentHoverObject = go;
                currentHoverOutline = go.GetComponent<Outline>();
                if (currentHoverOutline == null)
                    currentHoverOutline = go.AddComponent<Outline>();
                currentHoverOutline.OutlineColor = highlightColor;
                currentHoverOutline.OutlineWidth = highlightWidth;
                currentHoverOutline.OutlineMode = Outline.Mode.OutlineAll;
                currentHoverOutline.enabled = true;
            }
        }
        else
        {
            ClearHover();
        }
    }

    void ClearHover()
    {
        if (currentHoverOutline != null) currentHoverOutline.enabled = false;
        currentHoverOutline = null;
        currentHoverObject = null;
    }

    void TryPickup()
    {
    Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
    if (!Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupMask)) return;
 
    // 0a) ТВ — открыть камеры
    var tv = hit.collider.GetComponentInParent<TVInteractable>();
    if (tv != null) { tv.Activate(); return; }

    var radio = hit.collider.GetComponentInParent<RadioInteractable>();
    if (radio != null) { radio.Activate(); return; }
 
    // 0c) Кнопка «Скип»
    var skip = hit.collider.GetComponentInParent<SkipButton>();
    if (skip != null) { skip.Activate(); return; }

    // 1) Полка — спавним префаб в безопасной точке (holdPoint)
    ShelfItem shelf = hit.collider.GetComponentInParent<ShelfItem>();
    if (shelf != null && shelf.itemPrefab != null)
    {
        Vector3 spawnPos = holdPoint != null
            ? holdPoint.position
            : playerCamera.transform.position + playerCamera.transform.forward * minHoldDistance;

    // Берём ротейт прямо из префаба (-90 X сохранится)
        Quaternion spawnRot = shelf.itemPrefab.transform.rotation;

        Rigidbody spawned = Instantiate(shelf.itemPrefab, spawnPos, spawnRot);
        spawned.linearVelocity = Vector3.zero;
        spawned.angularVelocity = Vector3.zero;
        spawned.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        GrabRigidbody(spawned, spawnPos);
        return;
    }
    // 2) Обычный подбор
    Rigidbody rb = hit.rigidbody;
    if (rb == null || rb.isKinematic) return;
    GrabRigidbody(rb, hit.point);
    }

    void GrabRigidbody(Rigidbody rb, Vector3 hitPoint)
{
    heldObject = rb;
    originalDrag = rb.linearDamping;
    originalAngularDrag = rb.angularDamping;
    rb.linearDamping = 10f;
    rb.angularDamping = 10f;
    rb.useGravity = false;
    rb.angularVelocity = Vector3.zero; // можно оставить — обнулит угловую скорость
 
    currentHoldDistance = Mathf.Clamp(
        Vector3.Distance(playerCamera.transform.position, hitPoint),
        minHoldDistance, maxScrollDistance);
 
    ClearHover();
}

    void DropObject(bool throwIt)
    {
        if (heldObject == null) return;
        heldObject.useGravity = true;
        heldObject.linearDamping = originalDrag;
        heldObject.angularDamping = originalAngularDrag;
        if (throwIt) heldObject.AddForce(playerCamera.transform.forward * throwForce, ForceMode.VelocityChange);
        heldObject = null;
    }
}