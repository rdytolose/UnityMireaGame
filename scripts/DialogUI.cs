using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogUI : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text textLabel;
    public Button acknowledgeButton;

    [HideInInspector] public bool acknowledged = false;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (acknowledgeButton != null) acknowledgeButton.onClick.AddListener(OnAcknowledge);
    }

    public void Show(string text)
    {
        if (textLabel != null) textLabel.text = text;
        if (panel != null) panel.SetActive(true);
        acknowledged = false;
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    void OnAcknowledge()
    {
        acknowledged = true;
    }
}
