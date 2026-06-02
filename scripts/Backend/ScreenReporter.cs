using UnityEngine;

/// <summary>
/// Drop-in: повесь на любой объект в сцене и впиши, какой экран должен показать
/// телефон, пока эта сцена активна. Телефон сам переключится на нужную страницу.
///
/// Рекомендация какой Screen ставить в какой сцене:
///   - Сцена магазина (обслуживание клиентов) → clues
///   - Сцена магазина снаряжения (EquipmentShop) → shop
///   - Сцена Дума → chat
///   - Загрузка/меню/катсцены → wait
/// Для паузы вызывай из меню паузы GameSession.SetScreen("pause"),
/// а при возобновлении — верни прежний экран (например "chat").
/// </summary>
public class ScreenReporter : MonoBehaviour
{
    [Tooltip("wait | clues | shop | chat | pause")]
    public string screen = "wait";

    void Start()
    {
        GameApi.Ensure();
        GameSession.SetScreen(screen);
    }
}
