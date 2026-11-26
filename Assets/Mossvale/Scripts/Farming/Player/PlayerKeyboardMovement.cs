using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>
    /// WASD / arrow-key movement relative to the camera, alongside click-to-move.
    /// Pressing a key cancels any click destination. Uses the CharacterController, so it collides like clicking does.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerKeyboardMovement : MonoBehaviour
    {
        [Tooltip("0 = use FarmClickMovement's moveSpeed.")]
        [SerializeField] float moveSpeed = 0f;
        [SerializeField] bool readInput = true;

        CharacterController controller;
        IPointClickMover mover;
        PlayerInteractor interactor;
        Camera cam;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            mover = GetComponent<IPointClickMover>();
            interactor = GetComponent<PlayerInteractor>();
        }

        void Update()
        {
            if (!readInput || GameplayInput.Blocked) return;
            var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            Move(input, Time.deltaTime);
        }

        /// <summary>Moves the player for one frame. Public so tests can drive it without a keyboard.</summary>
        public void Move(Vector2 input, float dt)
        {
            if (input.sqrMagnitude < 0.01f) return;
            mover?.StopMoving();
            interactor?.CancelPending();

            if (cam == null) cam = Camera.main;
            Vector3 fwd = cam != null ? cam.transform.forward : Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 dir = Vector3.ClampMagnitude(fwd * input.y + right * input.x, 1f);

            float speed = moveSpeed > 0f ? moveSpeed : (mover != null ? mover.MoveSpeed : 4f);
            float turn = mover != null ? mover.TurnSpeed : 12f;
            controller.Move(dir * (speed * dt));
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), turn * dt);
        }
    }
}
