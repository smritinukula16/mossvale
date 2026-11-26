using UnityEngine;

namespace Mossvale
{
    /// <summary>
    /// Adds gravity to the player's CharacterController. FarmClickMovement only moves horizontally,
    /// so without this the player stays at whatever height it reaches after stepping onto a porch
    /// or step. Works alongside FarmClickMovement without modifying it.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class MossvaleGravity : MonoBehaviour
    {
        public float gravity = 20f;
        public float groundedStick = 2f;

        CharacterController controller;
        float verticalSpeed;

        void Awake() => controller = GetComponent<CharacterController>();

        void Update()
        {
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -groundedStick;
            else verticalSpeed -= gravity * Time.deltaTime;
            controller.Move(Vector3.up * (verticalSpeed * Time.deltaTime));
        }
    }
}
