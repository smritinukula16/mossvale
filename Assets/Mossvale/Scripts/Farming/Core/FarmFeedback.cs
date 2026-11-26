using System;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>
    /// Decoupled feedback channel: gameplay code reports what happened, the HUD and effects listen.
    /// </summary>
    public static class FarmFeedback
    {
        /// <summary>(message, world position, kind)</summary>
        public static event Action<string, Vector3, FeedbackKind> Raised;

        /// <summary>Last message raised; handy for tests and debugging.</summary>
        public static string LastMessage { get; private set; }
        public static FeedbackKind LastKind { get; private set; }

        public static void Raise(string message, Vector3 worldPos, FeedbackKind kind)
        {
            LastMessage = message;
            LastKind = kind;
            Raised?.Invoke(message, worldPos, kind);
        }

        public static Color ColorFor(FeedbackKind kind)
        {
            switch (kind)
            {
                case FeedbackKind.Success: return new Color(0.80f, 1f, 0.62f);
                case FeedbackKind.Failure: return new Color(1f, 0.55f, 0.48f);
                case FeedbackKind.Growth: return new Color(0.62f, 0.95f, 0.55f);
                case FeedbackKind.Harvest: return new Color(1f, 0.86f, 0.35f);
                case FeedbackKind.Star: return new Color(0.55f, 0.82f, 1f);
                default: return new Color(0.96f, 0.94f, 0.89f);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Raised = null; LastMessage = null; }
    }
}
