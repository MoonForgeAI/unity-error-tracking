using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoonForge.ErrorTracking
{
    /// <summary>
    /// Tracks and collects game state information
    /// </summary>
    public class GameStateCollector
    {
        private string _gameMode;
        private string _levelId;
        private Dictionary<string, object> _customData;

        private static GameStateCollector _instance;
        public static GameStateCollector Instance => _instance ??= new GameStateCollector();

        private GameStateCollector()
        {
            _customData = new Dictionary<string, object>();
        }

        /// <summary>
        /// Set the current game mode
        /// </summary>
        /// <param name="gameMode">Game mode identifier (e.g., "pvp", "campaign", "tutorial")</param>
        public void SetGameMode(string gameMode)
        {
            _gameMode = gameMode;
        }

        /// <summary>
        /// Set the current level/stage identifier
        /// </summary>
        /// <param name="levelId">Level identifier</param>
        public void SetLevelId(string levelId)
        {
            _levelId = levelId;
        }

        /// <summary>
        /// Set custom game state data
        /// </summary>
        /// <param name="key">Data key</param>
        /// <param name="value">Data value (must be JSON serializable)</param>
        public void SetCustomData(string key, object value)
        {
            if (value == null)
            {
                _customData.Remove(key);
            }
            else
            {
                _customData[key] = value;
            }
        }

        /// <summary>
        /// Set multiple custom data values at once
        /// </summary>
        /// <param name="data">Dictionary of key-value pairs</param>
        public void SetCustomData(Dictionary<string, object> data)
        {
            if (data == null) return;

            foreach (var kvp in data)
            {
                SetCustomData(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// Clear all custom data
        /// </summary>
        public void ClearCustomData()
        {
            _customData.Clear();
        }

        /// <summary>
        /// Collect current game state
        /// </summary>
        public GameState Collect()
        {
            var state = new GameState
            {
                sceneName = GetCurrentSceneName(),
                gameMode = _gameMode,
                levelId = _levelId,
                customData = _customData.Count > 0 ? new Dictionary<string, object>(_customData) : null
            };

            return state;
        }

        /// <summary>
        /// Get the current scene name
        /// </summary>
        private string GetCurrentSceneName()
        {
            var scene = SceneManager.GetActiveScene();
            return scene.IsValid() ? scene.name : null;
        }

        /// <summary>
        /// Reset all game state (call on game restart or logout)
        /// </summary>
        public void Reset()
        {
            _gameMode = null;
            _levelId = null;
            _customData.Clear();
        }
    }
}
