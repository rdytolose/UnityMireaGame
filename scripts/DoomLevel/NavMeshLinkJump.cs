using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Плавное прохождение NavMesh Link / Off-Mesh Link: вместо «телепорта» агент
/// перелетает линк по дуге (прыжок). Повесь на префаб врага (рядом с NavMeshAgent).
///
/// Работает и с современным NavMeshLink (AI Navigation), и со старым OffMeshLink —
/// оба отдаются через agent.isOnOffMeshLink / currentOffMeshLinkData.
/// </summary>
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
        _agent.autoTraverseOffMeshLink = false;   // анимируем пересечение сами
    }

    void Update()
    {
        if (_agent.isOnOffMeshLink && !_traversing)
            StartCoroutine(Traverse());
    }

    IEnumerator Traverse()
    {
        _traversing = true;

        // На время прыжка сами рулим transform.position, чтобы агент не перетирал дугу.
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
        _agent.Warp(end);               // ресинхронизируем внутреннюю позицию агента
        _agent.CompleteOffMeshLink();   // вернуть управление NavMeshAgent
        _traversing = false;
    }
}
