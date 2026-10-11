using UnityEngine;

namespace UitkForKsp2;

/// <summary>
/// Writes UI Toolkit for KSP 2 messages to the Unity log, prefixed with a name the way the game's loggers do.
/// </summary>
/// <remarks>
/// UI Toolkit for KSP 2 sits below the game assembly, so it cannot use the game's log providers.
/// </remarks>
internal sealed class UitkLogger
{
    private readonly string _prefix;

    /// <summary>
    /// Creates a logger that prefixes every message with <paramref name="name" /> in brackets.
    /// </summary>
    /// <param name="name">The name shown with each message.</param>
    internal UitkLogger(string name)
    {
        _prefix = $"[{name}] ";
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    internal void LogInfo(object message) => Debug.Log(_prefix + message);

    /// <summary>
    /// Logs a warning.
    /// </summary>
    /// <param name="message">The message to log.</param>
    internal void LogWarning(object message) => Debug.LogWarning(_prefix + message);

    /// <summary>
    /// Logs an error.
    /// </summary>
    /// <param name="message">The message to log.</param>
    internal void LogError(object message) => Debug.LogError(_prefix + message);
}