using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalRuins
{
    /// <summary>Session-only progress for round trips between the expedition's scenes.</summary>
    public static class RelicCampaignState
    {
        public sealed class LevelSnapshot
        {
            private readonly HashSet<string> collectedPickupIds = new HashSet<string>(StringComparer.Ordinal);
            public int CollectedRelics => collectedPickupIds.Count;
            public int Score { get; internal set; }
            public float ElapsedTime { get; internal set; }
            public int Health { get; internal set; } = 5;

            internal bool Contains(string pickupId) => collectedPickupIds.Contains(pickupId);
            internal bool Collect(string pickupId) => collectedPickupIds.Add(pickupId);
        }

        private static readonly Dictionary<string, LevelSnapshot> Levels = new Dictionary<string, LevelSnapshot>(StringComparer.Ordinal);
        private static string pendingScene;
        private static string pendingArrival;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetCampaign()
        {
            Levels.Clear();
            CancelArrival();
        }

        public static void ResetLevel(string sceneName)
        {
            Levels.Remove(SceneKey(sceneName));
            CancelArrival();
        }

        public static LevelSnapshot GetSnapshot(string sceneName)
        {
            Levels.TryGetValue(SceneKey(sceneName), out LevelSnapshot snapshot);
            return snapshot;
        }

        private static LevelSnapshot GetOrCreate(string sceneName)
        {
            string key = SceneKey(sceneName);
            if (!Levels.TryGetValue(key, out LevelSnapshot snapshot))
            {
                snapshot = new LevelSnapshot();
                Levels.Add(key, snapshot);
            }
            return snapshot;
        }

        public static bool HasCollected(string sceneName, string pickupId)
        {
            LevelSnapshot snapshot = GetSnapshot(sceneName);
            return snapshot != null && !string.IsNullOrEmpty(pickupId) && snapshot.Contains(pickupId);
        }

        public static bool TryCollect(string sceneName, string pickupId)
        {
            return !string.IsNullOrEmpty(pickupId) && GetOrCreate(sceneName).Collect(pickupId);
        }

        public static void Save(string sceneName, int score, float elapsedTime, int health)
        {
            LevelSnapshot snapshot = GetOrCreate(sceneName);
            snapshot.Score = Mathf.Max(0, score);
            snapshot.ElapsedTime = Mathf.Max(0f, elapsedTime);
            snapshot.Health = Mathf.Clamp(health, 1, 5);
        }

        public static void PrepareArrival(string sceneName, string arrivalId)
        {
            pendingScene = SceneKey(sceneName);
            pendingArrival = arrivalId;
        }

        public static bool TryConsumeArrival(string sceneName, out string arrivalId)
        {
            arrivalId = null;
            if (string.IsNullOrEmpty(pendingScene) || pendingScene != SceneKey(sceneName)) return false;
            arrivalId = pendingArrival;
            CancelArrival();
            return true;
        }

        public static void CancelArrival()
        {
            pendingScene = null;
            pendingArrival = null;
        }

        private static string SceneKey(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return "";
            string key = sceneName.Replace('\\', '/');
            int slash = key.LastIndexOf('/');
            if (slash >= 0) key = key.Substring(slash + 1);
            return key.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ? key.Substring(0, key.Length - 6) : key;
        }
    }
}
