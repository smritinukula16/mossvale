using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>Global switch that pauses world input (movement, tools, interaction) while a UI panel is open.</summary>
    public static class GameplayInput
    {
        public static bool Blocked { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Blocked = false;
    }
}
