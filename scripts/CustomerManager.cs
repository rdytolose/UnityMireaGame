using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class CustomerManager : MonoBehaviour
{
    [Header("Customer positions")]
    public Transform customerSpawnPoint;
    public Transform customerEntryPoint;
    public Transform customerExitPoint;
    public GameObject[] customerVisualPrefabs;

    [Header("Clues")]
    [Range(0f, 1f)] public float badChance = 0.3f;

    [System.Serializable]
    public class CluePrefabMap
    {
        public string clueId;       // id из пула CLUES на бэке
        public GameObject prefab;   // что спавнить (для type="object")
        public Transform spawnPoint; // жёсткая точка спавна этой приметы
    }

    [Tooltip("Карта clue_id → префаб + точка для предметных примет (type='object')")]
    public CluePrefabMap[] clueObjectPrefabs;

    [Header("Order")]
    public int minOrder = 1;
    public int maxOrder = 6;

    [Header("Scoring")]
    public int rewardCorrectServe = 1;
    public int penaltyServeBad = -3;
    public int rewardSkipBad = 1;
    public int penaltySkipGood = -1;

    [Header("Shutter")]
    public Transform shutter;
    public Transform shutterOpenPoint;
    public Transform shutterClosedPoint;
    public float shutterMoveDuration = 0.7f;

    [Header("Timing")]
    public float customerMoveDuration = 1.2f;
    public float pauseAfterShutterClose = 0.3f;
    public float pauseAfterShutterOpen = 0.3f;
    public float pauseBetweenCustomers = 0.8f;

    [Header("UI")]
    public TMP_Text dialogText;
    public TMP_Text scoreText;

    [Header("Visual novel dialog")]
    public DialogUI dialogUI;
    public Camera portraitCamera;
    public Transform dialogCameraTarget;
    public Transform playerCameraTransform;
    public MonoBehaviour[] scriptsToDisableDuringDialog;
    public float cameraMoveDuration = 0.7f;

    [Header("Feedback (flash + shake + sfx)")]
    public FeedbackFX feedbackFX;

    [TextArea(2, 4)]
    public string[] dialogTemplates = new string[]
    {
        "Привет, хорошая ночка сегодня. Мне нужно {N} бутылок.",
        "Здарова. Дай-ка {N} бутылок и пойду.",
        "Слышь, продавец, отсыпь мне {N} бутылок.",
        "Холодно, блин. Чисто согреться — {N} бутылок давай.",
        "Привет. Заказ на {N} бутылок, и побыстрее.",
        "Замёрз я как собака. {N} бутылок будь добр.",
        "Так, мужик, мне {N} бутылок. Молча.",
        "Здравствуйте. Не подскажете... ну в общем {N} бутылок.",
        "Чё стоишь? {N} бутылок мне.",
        "Тёзка, выручай. {N} бутылок и я исчез."
    };

    int score = 0;
    int currentOrder;
    int remaining;
    bool currentIsBad;
    GameObject currentClue;
    string currentCluePhrase;   // для текстовой приметы — фраза в реплику клиента
    readonly List<ClueDTO> revealedClues = new List<ClueDTO>();
    bool cluesReady = false;    // приметы пришли с бэка (или истёк таймаут)
    GameObject currentCustomer;
    bool customerActive = false;
    bool transitioning = false;
    bool useShutter = false;

    Vector3 savedCamPos;
    Quaternion savedCamRot;

    void Start()
    {
        if (feedbackFX == null) feedbackFX = Object.FindFirstObjectByType<FeedbackFX>();

        // Тянем накопленные приметы с бэка (растут случайно по дням, запоминаются).
        GameSession.FetchClues(r =>
        {
            revealedClues.Clear();
            if (r != null && r.clues != null) revealedClues.AddRange(r.clues);
            cluesReady = true;
        });

        // Стартуем с РЕАЛЬНОГО баланса игрока (а не с нуля) — показываем всю сумму.
        GameSession.FetchMe(me => { if (me != null) { score = me.money; UpdateScoreUI(); } });

        UpdateScoreUI();
        if (dialogText != null) dialogText.text = "";
        if (shutter != null && shutterOpenPoint != null)
            shutter.position = shutterOpenPoint.position;
        if (portraitCamera != null) portraitCamera.enabled = false;

        StartCoroutine(CustomerLoop());
    }

    IEnumerator CustomerLoop()
    {
        // Ждём, пока подгрузятся приметы (иначе первый "плохой" клиент будет без улики).
        // Таймаут — чтобы не зависнуть, если бэк недоступен.
        float t = 0f;
        while (!cluesReady && t < 3f) { t += Time.deltaTime; yield return null; }

        while (true)
        {
            transitioning = true;
            PrepareNewCustomer();

            yield return StartCoroutine(SlideObject(currentCustomer,
                customerEntryPoint != null ? customerEntryPoint.position : customerSpawnPoint.position,
                customerSpawnPoint.position,
                customerMoveDuration));

            yield return StartCoroutine(PlayDialog());

            transitioning = false;
            customerActive = true;
            UpdateDialogUI();

            yield return new WaitUntil(() => !customerActive);

            transitioning = true;
            UpdateDialogUI();

            if (useShutter)
            {
                yield return StartCoroutine(MoveShutter(shutterClosedPoint, shutterMoveDuration));
                yield return new WaitForSeconds(pauseAfterShutterClose);

                yield return StartCoroutine(SlideObject(currentCustomer,
                    customerSpawnPoint.position,
                    customerExitPoint != null ? customerExitPoint.position : customerSpawnPoint.position,
                    customerMoveDuration));

                ClearCurrent();

                yield return StartCoroutine(MoveShutter(shutterOpenPoint, shutterMoveDuration));
                yield return new WaitForSeconds(pauseAfterShutterOpen);

                useShutter = false;
            }
            else
            {
                yield return StartCoroutine(SlideObject(currentCustomer,
                    customerSpawnPoint.position,
                    customerExitPoint != null ? customerExitPoint.position : customerSpawnPoint.position,
                    customerMoveDuration));

                ClearCurrent();
                yield return new WaitForSeconds(pauseBetweenCustomers);
            }
        }
    }

    IEnumerator PlayDialog()
    {
        if (dialogUI == null) yield break;

        if (playerCameraTransform != null)
        {
            savedCamPos = playerCameraTransform.position;
            savedCamRot = playerCameraTransform.rotation;
        }

        if (portraitCamera != null) portraitCamera.enabled = true;

        SetControlsEnabled(false);

        if (dialogCameraTarget != null && playerCameraTransform != null)
            yield return StartCoroutine(MoveCameraTo(dialogCameraTarget.position, dialogCameraTarget.rotation, cameraMoveDuration));

        string template = dialogTemplates != null && dialogTemplates.Length > 0
            ? dialogTemplates[Random.Range(0, dialogTemplates.Length)]
            : "Дай мне {N} бутылок.";
        string text = template.Replace("{N}", currentOrder.ToString());

        // Текстовая примета — вшиваем фразу в реплику плохого клиента.
        if (!string.IsNullOrEmpty(currentCluePhrase))
            text += "\n" + currentCluePhrase;

        dialogUI.Show(text);

        while (!dialogUI.acknowledged) yield return null;

        dialogUI.Hide();

        if (playerCameraTransform != null)
            yield return StartCoroutine(MoveCameraTo(savedCamPos, savedCamRot, cameraMoveDuration));

        if (portraitCamera != null) portraitCamera.enabled = false;

        SetControlsEnabled(true);
    }

    IEnumerator MoveCameraTo(Vector3 targetPos, Quaternion targetRot, float duration)
    {
        if (playerCameraTransform == null) yield break;
        Vector3 startPos = playerCameraTransform.position;
        Quaternion startRot = playerCameraTransform.rotation;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            playerCameraTransform.position = Vector3.Lerp(startPos, targetPos, k);
            playerCameraTransform.rotation = Quaternion.Slerp(startRot, targetRot, k);
            yield return null;
        }
        playerCameraTransform.position = targetPos;
        playerCameraTransform.rotation = targetRot;
    }

    void SetControlsEnabled(bool enabled)
    {
        if (scriptsToDisableDuringDialog != null)
        {
            foreach (var s in scriptsToDisableDuringDialog)
                if (s != null) s.enabled = enabled;
        }

        if (enabled)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void PrepareNewCustomer()
    {
        currentOrder = Random.Range(minOrder, maxOrder + 1);
        remaining = currentOrder;
        currentIsBad = Random.value < badChance;
        currentCluePhrase = null;

        if (customerVisualPrefabs != null && customerVisualPrefabs.Length > 0 && customerEntryPoint != null)
        {
            var prefab = customerVisualPrefabs[Random.Range(0, customerVisualPrefabs.Length)];
            currentCustomer = Instantiate(prefab, customerEntryPoint.position, customerEntryPoint.rotation);
        }

        if (currentIsBad) SpawnClueForBadCustomer();
    }

    // Плохой клиент проявляет ОДНУ из накопленных примет (как в Papers Please).
    void SpawnClueForBadCustomer()
    {
        if (revealedClues.Count == 0) return;

        var clue = revealedClues[Random.Range(0, revealedClues.Count)];
        if (clue.type == "text")
        {
            currentCluePhrase = clue.phrase;   // вошьётся в реплику в PlayDialog
            return;
        }

        // Предметная примета — строго свой префаб в своей точке.
        var map = FindClueMap(clue.id);
        if (map != null && map.prefab != null && map.spawnPoint != null)
            currentClue = Instantiate(map.prefab, map.spawnPoint.position, map.spawnPoint.rotation);
    }

    CluePrefabMap FindClueMap(string id)
    {
        if (clueObjectPrefabs != null)
            foreach (var m in clueObjectPrefabs)
                if (m != null && m.clueId == id) return m;
        return null;
    }

    IEnumerator SlideObject(GameObject obj, Vector3 from, Vector3 to, float duration)
    {
        if (obj == null) yield break;
        float t = 0f;
        obj.transform.position = from;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            if (obj != null) obj.transform.position = Vector3.Lerp(from, to, k);
            yield return null;
        }
        if (obj != null) obj.transform.position = to;
    }

    IEnumerator MoveShutter(Transform target, float duration)
    {
        if (shutter == null || target == null) yield break;
        Vector3 from = shutter.position;
        Vector3 to = target.position;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            shutter.position = Vector3.Lerp(from, to, k);
            yield return null;
        }
        shutter.position = to;
    }

    public void OnBottleDelivered(Deliverable d)
    {
        if (!customerActive || transitioning) { Destroy(d.gameObject); return; }
        Destroy(d.gameObject);

        // СРАЗУ: если плохой — это ошибка
        if (currentIsBad)
        {
            ChangeScore(penaltyServeBad);
            if (feedbackFX != null) feedbackFX.Wrong();
            useShutter = false;
            customerActive = false;
            return;
        }

        remaining--;
        UpdateDialogUI();

        // СРАЗУ: если добили заказ — это правильно
        if (remaining <= 0)
        {
            ChangeScore(rewardCorrectServe);
            if (feedbackFX != null) feedbackFX.Correct();
            useShutter = false;
            customerActive = false;
        }
    }

    public void PlayerSkips()
    {
        if (!customerActive || transitioning) return;

        // СРАЗУ при нажатии skip даём feedback
        if (currentIsBad)
        {
            ChangeScore(rewardSkipBad);
            if (feedbackFX != null) feedbackFX.Correct();
        }
        else
        {
            ChangeScore(penaltySkipGood);
            if (feedbackFX != null) feedbackFX.Wrong();
        }

        useShutter = true;
        customerActive = false;
    }

    void ClearCurrent()
    {
        if (currentClue != null) Destroy(currentClue);
        if (currentCustomer != null) Destroy(currentCustomer);
        currentClue = null;
        currentCustomer = null;
    }

    void ChangeScore(int delta)
    {
        score += delta;            // оптимистично сразу показываем
        UpdateScoreUI();
        // Синкаем с бэком и корректируем по авторитетной сумме (на бэке не уходит ниже 0).
        GameSession.Earn(delta, money => { score = money; UpdateScoreUI(); });
    }
    void UpdateScoreUI() { if (scoreText != null) scoreText.text = "$" + score; }

    void UpdateDialogUI()
    {
        if (dialogText == null) return;
        if (!customerActive) { dialogText.text = ""; return; }
        dialogText.text = $"Нужно бутылок: {remaining}/{currentOrder}";
    }
}
