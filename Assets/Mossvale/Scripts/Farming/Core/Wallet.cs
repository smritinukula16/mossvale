using System;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>The farm's coins (the only currency). Earned at the market and from rewards, spent on upgrades.</summary>
    public class Wallet : MonoBehaviour, ISaveable
    {
        public static Wallet Instance { get; private set; }

        [SerializeField] int coins;
        [SerializeField] int totalEarned;

        public int Coins => coins;
        public int TotalEarned => totalEarned;
        /// <summary>(newBalance, delta)</summary>
        public event Action<int, int> Changed;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Add(int amount)
        {
            if (amount <= 0) return;
            coins += amount;
            totalEarned += amount;
            Changed?.Invoke(coins, amount);
            SaveManager.MarkDirty();
        }

        public bool CanAfford(int amount) => amount <= coins;

        public bool TrySpend(int amount)
        {
            if (amount < 0 || amount > coins) return false;
            coins -= amount;
            Changed?.Invoke(coins, -amount);
            SaveManager.MarkDirty();
            return true;
        }

        [Serializable] class State { public int coins, totalEarned; }
        public string SaveKey => "wallet";
        public int RestoreOrder => 5;
        public string CaptureState() => JsonUtility.ToJson(new State { coins = coins, totalEarned = totalEarned });
        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<State>(json);
            int delta = s.coins - coins;
            coins = s.coins; totalEarned = s.totalEarned;
            Changed?.Invoke(coins, delta);
        }

        /// <summary>Dev/test helper.</summary>
        public void SetCoins(int value) { int d = value - coins; coins = Mathf.Max(0, value); Changed?.Invoke(coins, d); }
    }
}
