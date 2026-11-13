using System;
using System.Collections.Generic;
using KSP.Game;
using KSP.Logging;
using UnityEngine;

namespace DebugTools
{
    /// <summary>
    /// Registry of temporary test state and cheats shared by tools and evidence reports.
    /// </summary>
    /// <remarks>
    /// Call on the game thread. Owners clear entries when their state ends. Active returns a snapshot.
    /// </remarks>
    public static class DebugOverrides
    {
        /// <summary>
        /// A named override and the action that restores normal play.
        /// </summary>
        public readonly struct ActiveOverride
        {
            /// <summary>
            /// Stable owner identifier.
            /// </summary>
            public readonly string Id;
            /// <summary>
            /// Name displayed in the status strip.
            /// </summary>
            public readonly string Name;
            /// <summary>
            /// Action that restores the original state.
            /// </summary>
            public readonly Action Revert;
            /// <summary>
            /// Gets whether this is test state that warrants a save warning.
            /// </summary>
            public bool IsTestOnly { get; }

            internal ActiveOverride(string id, string name, Action revert, bool isTestOnly)
            {
                Id = id;
                Name = name;
                Revert = revert;
                IsTestOnly = isTestOnly;
            }
        }

        private static readonly Dictionary<string, ActiveOverride> _active = new(StringComparer.Ordinal);

        /// <summary>
        /// Raised when reported state changes.
        /// </summary>
        public static event Action Changed;

        /// <summary>
        /// Gets all reported test state and current hub cheats, including when the hub has never opened.
        /// </summary>
        public static IReadOnlyList<ActiveOverride> Active
        {
            get
            {
                var result = new List<ActiveOverride>(_active.Values);
                GameInstance game = GameManager.Instance != null ? GameManager.Instance.Game : null;
                if (game == null)
                    return result.AsReadOnly();

                AddCheat(result, game, CheatSystemItemID.UnbreakableParts, "Unbreakable Parts");
                AddCheat(result, game, CheatSystemItemID.DisableGravity, "Disable Gravity");
                AddCheat(result, game, CheatSystemItemID.DisableAerodynamics, "Disable Aerodynamics");
                AddCheat(result, game, CheatSystemItemID.DisableThermodynamics, "Disable Thermodynamics");
                AddCheat(result, game, CheatSystemItemID.UnlockAllParts, "Unlock All Parts");
                AddDifficulty(result, game, DifficultyOptionsUtils.UNBREAKABLE_JOINTS, "Unbreakable Joints");
                AddDifficulty(result, game, DifficultyOptionsUtils.INFINITE_FUEL, "Infinite Propellant");
                AddDifficulty(result, game, DifficultyOptionsUtils.INFINITE_POWER, "Infinite Electricity");
                return result.AsReadOnly();
            }
        }

        /// <summary>
        /// Gets a save warning naming active test state, or an empty string.
        /// </summary>
        public static string SaveWarning => _active.Count == 0 ? string.Empty :
            $"Test overrides active: {string.Join(", ", GetTestNames())}. Saving may preserve test state. Revert in Dev Tools for normal play.";

        /// <summary>
        /// Gets the names of the active test overrides, joined with commas, or an empty string.
        /// </summary>
        public static string TestOverrideNames => string.Join(", ", GetTestNames());

        /// <summary>
        /// Add or update test state.
        /// </summary>
        /// <param name="id">The stable owner identifier.</param>
        /// <param name="name">The displayed name.</param>
        /// <param name="revert">The action that ends the override.</param>
        public static void Report(string id, string name, Action revert)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || revert == null)
                throw new ArgumentException("An override needs an id, name and revert action.");

            // Owners often report again with a new revert closure. Only a new entry or a new name changes what the
            // strip shows, so only those raise Changed. A rebuild would cancel a Revert waiting for confirmation.
            bool isShownChange = !_active.TryGetValue(id, out ActiveOverride previous) || previous.Name != name;
            _active[id] = new ActiveOverride(id, name, revert, true);
            if (isShownChange)
            {
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Run the current revert action of reported test state.
        /// </summary>
        /// <param name="id">The stable owner identifier.</param>
        /// <returns>True if the owner had reported state, false otherwise.</returns>
        /// <remarks>
        /// A caller that keeps an entry from <see cref="Active"/> may hold an older revert action, since a repeated
        /// report replaces it without raising <see cref="Changed"/>. This always runs the latest one.
        /// </remarks>
        public static bool Revert(string id)
        {
            if (!_active.TryGetValue(id, out ActiveOverride entry))
                return false;

            entry.Revert();
            return true;
        }

        /// <summary>
        /// Check whether an owner has reported test state.
        /// </summary>
        /// <param name="id">The stable owner identifier.</param>
        /// <returns>True if the owner has reported state, false otherwise.</returns>
        public static bool IsReported(string id) => _active.ContainsKey(id);

        /// <summary>
        /// Remove state that its owner has ended.
        /// </summary>
        /// <param name="id">The stable owner identifier.</param>
        public static void Clear(string id)
        {
            if (_active.Remove(id))
            {
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Restore reported test state without changing ordinary cheats.
        /// </summary>
        public static void RevertTestOverrides()
        {
            foreach (ActiveOverride entry in new List<ActiveOverride>(_active.Values))
            {
                try
                {
                    entry.Revert();
                }
                catch (Exception exception)
                {
                    GlobalLog.Warn(LogFilter.Gameplay, $"Could not revert {entry.Name}: {exception.Message}");
                }
            }
        }

        private static List<string> GetTestNames()
        {
            var names = new List<string>();
            foreach (ActiveOverride entry in _active.Values)
            {
                names.Add(entry.Name);
            }
            return names;
        }

        private static void AddCheat(List<ActiveOverride> result, GameInstance game, CheatSystemItemID id, string name)
        {
            if (game.CheatSystem == null)
                return;

            foreach (CheatSystem.ItemInfo info in CheatSystem.gItemInfoArray)
            {
                if (info.CheatSystemItemID != id || game.CheatSystem.Get(id) == info.DefaultValue)
                    continue;

                bool normal = info.DefaultValue;
                result.Add(new ActiveOverride($"cheat:{id}", normal ? $"{name} off" : name,
                    () => game.CheatSystem.Set(id, normal), false));
                break;
            }
        }

        private static void AddDifficulty(List<ActiveOverride> result, GameInstance game, string id, string name)
        {
            if (game.SessionManager != null && game.SessionManager.TryGetDifficultyOptionState(id, out bool enabled) && enabled)
            {
                result.Add(new ActiveOverride($"difficulty:{id}", name,
                    () => game.SessionManager.TrySetDifficultyOptionEnabled(id, false), false));
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _active.Clear();
            Changed = null;
        }
    }
}
