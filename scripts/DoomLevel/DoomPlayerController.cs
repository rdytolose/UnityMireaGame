using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class DoomPlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 7f;
    public float runSpeed = 12f;
    public float jumpForce = 8f;
    public float gravity = 20f;

    [Header("Mouse Look")]
    public Camera playerCamera;
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 80f;

    private CharacterController controller;
    private Vector3 moveDirection = Vector3.zero;
    private float verticalRotation = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(0, mouseX, 0);

        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
        playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
    }

    void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        if (controller.isGrounded)
        {
            Vector3 forward = transform.forward * vertical;
            Vector3 right = transform.right * horizontal;
            moveDirection = (forward + right).normalized;

            float speed = isRunning ? runSpeed : walkSpeed;
            moveDirection *= speed;

            if (Input.GetButtonDown("Jump"))
                moveDirection.y = jumpForce;
        }

        moveDirection.y -= gravity * Time.deltaTime;
        controller.Move(moveDirection * Time.deltaTime);
    }
}
