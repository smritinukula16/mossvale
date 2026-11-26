using System.Collections.Generic;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>What the HUD shows for the object under the cursor / in front of the player.</summary>
    public struct InteractionPrompt
    {
        public string action;   // e.g. "Till soil"
        public string detail;   // e.g. "Carrot (Small) - growing 40%"
        public bool canPerform; // false -> action is shown as unavailable, with the reason in detail

        public InteractionPrompt(string action, string detail, bool canPerform)
        { this.action = action; this.detail = detail; this.canPerform = canPerform; }
    }

    /// <summary>Base class for anything the player can use: farm plots, the well, the pond...</summary>
    public abstract class Interactable : MonoBehaviour
    {
        static readonly List<Interactable> all = new List<Interactable>();
        public static IReadOnlyList<Interactable> All => all;

        Collider[] colliders;

        public abstract string DisplayName { get; }
        public abstract InteractionPrompt GetPrompt(PlayerToolController tools);
        /// <summary>Performs the action for the currently selected tool. Returns true on success.</summary>
        public abstract bool Interact(PlayerToolController tools);

        protected virtual void Awake() => colliders = GetComponentsInChildren<Collider>();
        protected virtual void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        protected virtual void OnDisable() => all.Remove(this);

        /// <summary>World bounds of this object's colliders (used for range checks and the highlight).</summary>
        public Bounds WorldBounds
        {
            get
            {
                if (colliders == null || colliders.Length == 0) colliders = GetComponentsInChildren<Collider>();
                bool has = false;
                var b = new Bounds(transform.position, Vector3.zero);
                foreach (var c in colliders)
                {
                    if (c == null || !c.enabled) continue;
                    if (!has) { b = c.bounds; has = true; } else b.Encapsulate(c.bounds);
                }
                return b;
            }
        }

        /// <summary>Height at which the selection outline is drawn.</summary>
        public virtual float HighlightHeight => WorldBounds.min.y + 0.05f;

        public virtual Vector3 InteractionPoint => WorldBounds.center;

        /// <summary>Max distance (m) from the player to this object's edge. Buildings override this.</summary>
        public virtual float InteractionRange => FarmManager.InteractionRange;

        /// <summary>Whether a successful interaction plays the held-tool swing.</summary>
        public virtual bool PlaysToolAnimation => true;

        /// <summary>Horizontal distance from a point to the edge of this object's bounds.</summary>
        public float HorizontalDistance(Vector3 p)
        {
            var b = WorldBounds;
            var flat = new Vector3(p.x, b.center.y, p.z);
            var c = b.ClosestPoint(flat);
            return Vector2.Distance(new Vector2(p.x, p.z), new Vector2(c.x, c.z));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => all.Clear();
    }
}
