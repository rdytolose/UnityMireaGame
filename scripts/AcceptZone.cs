using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AcceptZone : MonoBehaviour
{
    public CustomerManager manager;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void Awake()
    {
        if (manager == null) manager = Object.FindFirstObjectByType<CustomerManager>();
    }

    void OnTriggerEnter(Collider other)
    {
        var d = other.GetComponentInParent<Deliverable>();
        if (d == null) return;
        if (manager != null) manager.OnBottleDelivered(d);
        else Destroy(d.gameObject);
    }
}
