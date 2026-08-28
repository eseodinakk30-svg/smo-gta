using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;

namespace SanMonica.Economy
{
    /// <summary>
    /// The player's wallet and the city's cash flow: mission pay, side jobs,
    /// property income, purchases, repairs and fines all pass through here.
    /// </summary>
    public class EconomySystem : MonoBehaviour
    {
        [System.Serializable]
        public struct Transaction
        {
            public long Amount;
            public string Reason;
            public float Time;
        }

        public long Money { get; private set; }
        public long TotalEarned { get; private set; }
        public long TotalSpent { get; private set; }
        public readonly List<Transaction> History = new List<Transaction>(64);

        [Header("Balance")]
        public long StartingMoney = 250;
        public float IncomeIntervalHours = 24f;

        private float _incomeTimer;

        public void Initialize()
        {
            Money = StartingMoney;
            GameEvents.RaiseMoney(Money, 0);
        }

        public void SetMoney(long amount)
        {
            Money = System.Math.Max(0, amount);
            GameEvents.RaiseMoney(Money, 0);
        }

        public bool CanAfford(long amount) => Money >= amount;

        public void AddMoney(long amount, string reason)
        {
            if (amount == 0) return;
            Money += amount;
            if (amount > 0) TotalEarned += amount; else TotalSpent += -amount;
            Record(amount, reason);
            GameEvents.RaiseMoney(Money, amount);
        }

        public bool TrySpend(long amount, string reason)
        {
            if (amount <= 0) return true;
            if (Money < amount)
            {
                GameEvents.Notify("Not enough money", 2f);
                Services.Audio?.PlayUi("error");
                return false;
            }
            Money -= amount;
            TotalSpent += amount;
            Record(-amount, reason);
            GameEvents.RaiseMoney(Money, -amount);
            Services.Audio?.PlayUi("purchase");
            return true;
        }

        private void Record(long amount, string reason)
        {
            History.Add(new Transaction { Amount = amount, Reason = reason, Time = Time.time });
            if (History.Count > 200) History.RemoveAt(0);
        }

        private void Update()
        {
            var clock = Services.Clock;
            if (clock == null) return;
            _incomeTimer += clock.GameHoursDelta;
            if (_incomeTimer < IncomeIntervalHours) return;
            _incomeTimer = 0f;

            long income = Services.Property != null ? Services.Property.DailyIncome() : 0;
            if (income > 0)
            {
                AddMoney(income, "Property income");
                GameEvents.Notify("Property income: $" + income.ToString("N0"), 3f);
            }
        }

        /// <summary>Fine paid when the player is arrested.</summary>
        public long ApplyBustedPenalty()
        {
            long fine = System.Math.Min(Money, System.Math.Max(250, (long)(Money * 0.12f)));
            if (fine > 0) AddMoney(-fine, "Bail and fines");
            return fine;
        }

        public long ApplyHospitalPenalty()
        {
            long fee = System.Math.Min(Money, System.Math.Max(150, (long)(Money * 0.08f)));
            if (fee > 0) AddMoney(-fee, "Medical fees");
            return fee;
        }
    }
}
