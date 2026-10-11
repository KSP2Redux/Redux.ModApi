#nullable enable

using System;
using JetBrains.Annotations;

namespace SpaceWarp2.API.Logging;

/// <summary>
/// Hands out the loggers that the game, SpaceWarp and mods write to.
/// </summary>
/// <remarks>
/// The game replaces <see cref="Provider" /> and sets <see cref="TimestampFormatSource" /> from its own settings when
/// it starts. Until then loggers come from a plain <see cref="UnityLogProvider" /> at its default filter level.
/// </remarks>
[PublicAPI]
public static class LogManager
{
    /// <summary>
    /// The timestamp format used until the game supplies its setting.
    /// </summary>
    public const string DEFAULT_TIMESTAMP_FORMAT = "HH:mm:ss.fff";

    /// <summary>
    /// The provider that creates every logger, and whose <see cref="ILogProvider.OnLog" /> event sees every message.
    /// </summary>
    public static ILogProvider Provider { get; set; } = new UnityLogProvider();

    /// <summary>
    /// Reads the current timestamp format setting. It is a delegate, so a changed setting applies without a restart.
    /// </summary>
    public static Func<string>? TimestampFormatSource { get; set; }

    /// <summary>
    /// The timestamp format file logs use when their creator passes none.
    /// </summary>
    public static string TimestampFormat => TimestampFormatSource?.Invoke() ?? DEFAULT_TIMESTAMP_FORMAT;

    /// <summary>
    /// Creates a logger that prefixes its messages with <paramref name="name" />.
    /// </summary>
    /// <param name="name">The name shown with each message, usually the mod or subsystem name.</param>
    /// <returns>A logger from the current <see cref="Provider" />.</returns>
    public static ILogger GetLogger(string name) => Provider.GetLogger(name);
}