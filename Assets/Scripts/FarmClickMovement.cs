
using System;
using Mossvale.Farming;
using UnityEngine;
using UnityEngine.EventSystems;

public class FarmClickMovement : MonoBehaviour, IPointClickMover
{
    [Header("Movement")]
    public float moveSpeed = 4f;
    public float turnSpeed = 12f;

    [Header("Camera")]
    public Vector3 cameraOffset = new Vector3(0f, 9f, -12f);
    public float cameraSmoothSpeed = 5f;

    private CharacterController controller;
    private Camera mainCamera;
    private Vector3 destination;
    private bool hasDestination;

    // --- Added for Mossvale farming (additive; movement behaviour unchanged) ---
    /// <summary>Raised after a world click sets a destination (lets the farming code walk-then-interact).</summary>
    public event Action<RaycastHit> Clicked;
    public bool IsMoving => hasDestination;
    public float MoveSpeed => moveSpeed;
    public float TurnSpeed => turnSpeed;
    public void StopMoving() => hasDestination = false;
    public void MoveTo(Vector3 worldPoint)
    {
        destination = worldPoint;
        destination.y = transform.position.y;
        hasDestination = true;
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        mainCamera = Camera.main;

        destination = transform.position;

        if (mainCamera != null)
        {
            mainCamera.transform.position =
                transform.position + cameraOffset;

            mainCamera.transform.LookAt(transform.position);
        }
    }

    void Update()
    {
        if (mainCamera == null)
            return;

        // Clicks on the HUD (toolbar buttons) must not move the player.
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (Input.GetMouseButtonDown(0) && !overUI)
        {
            Ray ray = mainCamera.ScreenPointToRay(
                Input.mousePosition
            );

            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                // Ignore clicks on the player itself.
                if (hit.collider.gameObject != gameObject)
                {
                    destination = hit.point;
                    destination.y = transform.position.y;
                    hasDestination = true;
                    Clicked?.Invoke(hit);
                }
            }
        }

        MovePlayer();
    }

    void MovePlayer()
    {
        if (!hasDestination)
            return;

        Vector3 direction = destination - transform.position;
        direction.y = 0f;

        if (direction.magnitude < 0.15f)
        {
            hasDestination = false;
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime
        );

        controller.Move(
            direction.normalized * moveSpeed * Time.deltaTime
        );
    }

    void LateUpdate()
    {
        if (mainCamera == null)
            return;

        Vector3 targetPosition =
            transform.position + cameraOffset;

        mainCamera.transform.position = Vector3.Lerp(
            mainCamera.transform.position,
            targetPosition,
            cameraSmoothSpeed * Time.deltaTime
        );

        mainCamera.transform.LookAt(transform.position);
    }
}
