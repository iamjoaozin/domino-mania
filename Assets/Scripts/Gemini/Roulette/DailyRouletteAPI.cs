using System;
using UnityEngine;

namespace Gemini.Roulette
{
    public class DailyRouletteAPI
    {
        private const string LastSpinKey = "LastRouletteSpin";
        private const string CoinsKey = "Coins";
        private const int CooldownHours = 24;

        public readonly int[] Rewards = { 50, 100, 200, 500, 1000, 2000 };

        public bool CanSpin()
        {
            return GetTimeRemaining() <= 0;
        }

        public long GetTimeRemaining()
        {
            string lastSpinRaw = PlayerPrefs.GetString(LastSpinKey, string.Empty);
            if (string.IsNullOrEmpty(lastSpinRaw)) return 0;

            if (!DateTime.TryParse(lastSpinRaw, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime lastSpin))
                return 0;

            double secondsLeft = (TimeSpan.FromHours(CooldownHours) - (DateTime.Now - lastSpin)).TotalSeconds;
            return secondsLeft > 0 ? (long) secondsLeft : 0;
        }

        public int Spin()
        {
            if (!CanSpin()) return -1;

            PlayerPrefs.SetString(LastSpinKey, DateTime.Now.ToString("O"));
            PlayerPrefs.Save();

            int roll = UnityEngine.Random.Range(0, 100);
            if (roll < 40) return 0;
            if (roll < 70) return 1;
            if (roll < 85) return 2;
            if (roll < 95) return 3;
            if (roll < 99) return 4;
            return 5;
        }

        public void ClaimReward(int amount)
        {
            int currentCoins = PlayerPrefs.GetInt(CoinsKey, 0);
            PlayerPrefs.SetInt(CoinsKey, currentCoins + amount);
            PlayerPrefs.Save();

            Debug.Log($"[RouletteAPI] Recompensa de {amount} moedas creditada. Saldo atual: {currentCoins + amount}");
        }

        public int GetBalance()
        {
            return PlayerPrefs.GetInt(CoinsKey, 0);
        }

        public int[] GetRewards()
        {
            return Rewards;
        }

        public string GetRewardsCsv()
        {
            return string.Join(",", Rewards);
        }

        public string GetCooldownText()
        {
            long remaining = GetTimeRemaining();
            if (remaining <= 0) return "DISPONIVEL";

            TimeSpan span = TimeSpan.FromSeconds(remaining);
            return $"{span.Hours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";
        }
    }
}
