using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NavMeshLinkJump : MonoBehaviour
{
    [Tooltip("Высота дуги прыжка.")]
    public float jumpHeight = 1.5f;
    [Tooltip("Длительность пролёта по линку, сек.")]
    public float duration = 0.5f;

    NavMeshAgent _agent;
    bool _traversing;

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.autoTraverseOffMeshLink = false;
    }

    void Update()
    {
        if (_agent.isOnOffMeshLink && !_traversing)
            StartCoroutine(Traverse());
    }

    IEnumerator Traverse()
    {
        _traversing = true;

        bool prevUpdatePos = _agent.updatePosition;
        _agent.updatePosition = false;

        OffMeshLinkData data = _agent.currentOffMeshLinkData;
        Vector3 start = _agent.transform.position;
        Vector3 end = data.endPos + Vector3.up * _agent.baseOffset;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, duration);
            float arc = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)) * jumpHeight;
            _agent.transform.position = Vector3.Lerp(start, end, t) + Vector3.up * arc;
            yield return null;
        }

        _agent.transform.position = end;
        _agent.updatePosition = prevUpdatePos;
        _agent.Warp(end);
        _agent.CompleteOffMeshLink();
        _traversing = false;
    }
}
