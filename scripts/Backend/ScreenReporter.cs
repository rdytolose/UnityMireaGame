using UnityEngine;

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
