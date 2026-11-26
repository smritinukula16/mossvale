using System;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>
    /// Implemented by the existing FarmClickMovement script (Assembly-CSharp) so gameplay code can
    /// react to clicks and stop movement without depending on that script directly.
    /// </summary>
    public interface IPointClickMover
    {
        /// <summary>Raised when a world click (not over UI) hits a collider.</summary>
        event Action<RaycastHit> Clicked;
        bool IsMoving { get; }
        float MoveSpeed { get; }
        float TurnSpeed { get; }
        void StopMoving();
        void MoveTo(Vector3 worldPoint);
    }
}
