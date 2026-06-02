using UnityEngine;

// Оставляет следы на снегу при ходьбе
public class FootprintTrail : MonoBehaviour
{
    [Header("Footprint Settings")]
    public GameObject footprintPrefab; // Префаб следа
    public float stepDistance = 0.5f; // Расстояние между следами
    public float footprintLifetime = 30f; // Сколько секунд след остается
    public LayerMask snowLayer; // На каких поверхностях оставлять следы

    [Header("Foot Positions")]
    public Transform leftFoot;
    public Transform rightFoot;
    public float footOffset = 0.3f; // Расстояние ног от центра

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
        // Проверяем, движется ли персонаж
        if (characterController != null && characterController.velocity.magnitude < 0.1f)
            return;

        // Проверяем расстояние от последнего следа
        float distanceMoved = Vector3.Distance(transform.position, lastFootprintPosition);
        
        if (distanceMoved >= stepDistance)
        {
            CreateFootprint();
            lastFootprintPosition = transform.position;
            isLeftFoot = !isLeftFoot; // Чередуем ноги
        }
    }

    void CreateFootprint()
    {
        // Raycast вниз чтобы найти поверхность
        Ray ray = new Ray(transform.position + Vector3.up * 0.5f, Vector3.down);
        
        if (Physics.Raycast(ray, out RaycastHit hit, 2f, snowLayer))
        {
            // Вычисляем позицию ноги (слева или справа)
            Vector3 footPosition = hit.point;
            Vector3 sideOffset = transform.right * (isLeftFoot ? -footOffset : footOffset);
            footPosition += sideOffset;

            // Создаем след
            if (footprintPrefab != null)
            {
                GameObject footprint = Instantiate(footprintPrefab, footPosition + Vector3.up * 0.01f, Quaternion.identity);
                
                // Поворачиваем след по направлению движения
                // Quad лежит горизонтально, поэтому используем hit.normal как up
                Quaternion rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
                footprint.transform.rotation = rotation * Quaternion.Euler(90, 0, 0);
                
                // Отражаем для левой ноги
                if (isLeftFoot)
                {
                    Vector3 scale = footprint.transform.localScale;
                    scale.x *= -1;
                    footprint.transform.localScale = scale;
                }

                // Удаляем след через время
                Destroy(footprint, footprintLifetime);
            }
        }
    }

    // Для отладки - показывает где будут следы
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Vector3 leftPos = transform.position + transform.right * -footOffset;
        Vector3 rightPos = transform.position + transform.right * footOffset;
        Gizmos.DrawWireSphere(leftPos, 0.1f);
        Gizmos.DrawWireSphere(rightPos, 0.1f);
    }
}
