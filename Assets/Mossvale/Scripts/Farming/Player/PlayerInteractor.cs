using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Mossvale.Farming
{
    /// <summary>
    /// Finds what the player can interact with and performs the action of the selected tool.
    ///  - Mouse: hover shows a prompt; left-click walks to the object (via FarmClickMovement) and uses it on arrival.
    ///  - Keyboard: E or Space uses the object in front of the player (within range).
    /// The player turns to face the object before acting.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] PlayerToolController tools;
        [SerializeField] Camera viewCamera;
        [Tooltip("Objects within this angle of the player's facing direction are preferred for keyboard use.")]
        [SerializeField] float facingAngle = 80f;
        [SerializeField] bool readInput = true;

        IPointClickMover mover;
        Vector3 lastPos;
        float stuckTime;

        public Interactable Hovered { get; private set; }
        public Interactable KeyboardTarget { get; private set; }
        public Interactable PendingTarget { get; private set; }
        /// <summary>The object the HUD prompt and highlight describe.</summary>
        public Interactable Current => Hovered != null ? Hovered : KeyboardTarget;
        public InteractionPrompt CurrentPrompt { get; private set; }
        public bool CurrentInRange { get; private set; }
        public float Range => FarmManager.InteractionRange;

        /// <summary>(target, success)</summary>
        public event Action<Interactable, bool> Interacted;

        void Awake()
        {
            if (tools == null) tools = GetComponent<PlayerToolController>();
            mover = GetComponent<IPointClickMover>();
        }

        void OnEnable() { if (mover != null) mover.Clicked += HandleClick; }
        void OnDisable() { if (mover != null) mover.Clicked -= HandleClick; }

        void Update()
        {
            if (viewCamera == null) viewCamera = Camera.main;

            bool blocked = GameplayInput.Blocked;   // a building panel is open
            Hovered = readInput && !blocked ? RaycastPointer() : null;
            KeyboardTarget = FindKeyboardTarget();
            var cur = Current;
            CurrentPrompt = cur != null ? cur.GetPrompt(tools) : default;
            CurrentInRange = cur != null && InRange(cur);

            if (readInput && !blocked && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space)))
                InteractWithKeyboardTarget();

            UpdatePending();
        }

        // ------------------------------------------------------------------ click-to-walk-and-use
        /// <summary>Called by FarmClickMovement after a world click. Public so tests can simulate clicks.</summary>
        public void HandleClick(RaycastHit hit)
        {
            var target = hit.collider != null ? hit.collider.GetComponentInParent<Interactable>() : null;
            PendingTarget = target;
            stuckTime = 0f;
            lastPos = transform.position;
            if (target != null && InRange(target))
            {
                mover?.StopMoving();
                PendingTarget = null;
                TryInteract(target);
            }
        }

        public void CancelPending() => PendingTarget = null;

        void UpdatePending()
        {
            if (PendingTarget == null) return;
            if (!PendingTarget.isActiveAndEnabled) { PendingTarget = null; return; }
            if (InRange(PendingTarget))
            {
                mover?.StopMoving();
                var t = PendingTarget;
                PendingTarget = null;
                TryInteract(t);
                return;
            }
            // Blocked on the way (e.g. by a fence)? Give up after a moment instead of pushing forever.
            float moved = Vector3.Distance(transform.position, lastPos);
            lastPos = transform.position;
            stuckTime = moved < 0.002f ? stuckTime + Time.deltaTime : 0f;
            if (stuckTime > 0.6f || (mover != null && !mover.IsMoving))
            {
                FarmFeedback.Raise("Can't reach that from here", PendingTarget.InteractionPoint, FeedbackKind.Failure);
                mover?.StopMoving();
                PendingTarget = null;
            }
        }

        // ------------------------------------------------------------------ actions
        public bool InRange(Interactable target) =>
            target != null && target.HorizontalDistance(transform.position) <= target.InteractionRange;

        /// <summary>Uses the selected tool on a target (range-checked). Public for tests / other input schemes.</summary>
        public bool TryInteract(Interactable target)
        {
            if (target == null) return false;
            if (!InRange(target))
            {
                FarmFeedback.Raise("Too far away - walk closer", target.InteractionPoint, FeedbackKind.Failure);
                Interacted?.Invoke(target, false);
                return false;
            }
            Face(target.InteractionPoint);
            bool ok = target.Interact(tools);
            if (ok && target.PlaysToolAnimation) tools.PlayUseAnimation();
            Interacted?.Invoke(target, ok);
            return ok;
        }

        public void InteractWithKeyboardTarget()
        {
            var t = KeyboardTarget != null ? KeyboardTarget : (Hovered != null && InRange(Hovered) ? Hovered : null);
            if (t == null)
            {
                FarmFeedback.Raise("Nothing to use here - walk up to a plot", transform.position + Vector3.up * 2f, FeedbackKind.Info);
                return;
            }
            TryInteract(t);
        }

        void Face(Vector3 point)
        {
            var d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d);
        }

        // ------------------------------------------------------------------ targeting
        Interactable RaycastPointer()
        {
            if (viewCamera == null) return null;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return null;
            var ray = viewCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)) return null;
            return hit.collider.GetComponentInParent<Interactable>();
        }

        Interactable FindKeyboardTarget()
        {
            Interactable best = null;
            float bestScore = float.MaxValue;
            var fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
            foreach (var it in Interactable.All)
            {
                if (it == null || !it.isActiveAndEnabled) continue;
                float dist = it.HorizontalDistance(transform.position);
                if (dist > it.InteractionRange) continue;
                var to = it.InteractionPoint - transform.position; to.y = 0f;
                float angle = to.sqrMagnitude < 0.0001f ? 0f : Vector3.Angle(fwd, to);
                if (angle > facingAngle && dist > 0.25f) continue;      // must be roughly in front
                float score = dist + angle * 0.02f;                       // nearer + more in front wins
                if (score < bestScore) { bestScore = score; best = it; }
            }
            return best;
        }
    }
}
