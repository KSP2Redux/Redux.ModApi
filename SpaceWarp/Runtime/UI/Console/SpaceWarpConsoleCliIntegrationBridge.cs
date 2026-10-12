#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Redux.CliIntegration;

namespace SpaceWarp2.UI.Console
{
    /// <summary>
    /// The console's side of the CLI integration: the shared C# evaluator, and the activity log, which only builds
    /// where the integration does.
    /// </summary>
    internal static class SpaceWarpConsoleCliIntegrationBridge
    {
        private const int DISPLAY_LENGTH = 260;

        public static IReadOnlyList<SpaceWarpConsoleCliActivityEntryViewModel> GetActivityEntries()
        {
#if UNITY_EDITOR || REDUX_CLI_INTEGRATION
            return CliIntegrationActivityLog.Snapshot()
                .Reverse()
                .Select(ToActivityViewModel)
                .ToList();
#else
            return Array.Empty<SpaceWarpConsoleCliActivityEntryViewModel>();
#endif
        }

        public static object? AddActivity(string source, string kind, string command, string payload)
        {
#if UNITY_EDITOR || REDUX_CLI_INTEGRATION
            return CliIntegrationActivityLog.AddStarted(source, kind, command, payload);
#else
            return null;
#endif
        }

        public static void CompleteActivity(object? activity, bool success, string result)
        {
#if UNITY_EDITOR || REDUX_CLI_INTEGRATION
            if (activity is CliIntegrationActivityEntry entry)
                CliIntegrationActivityLog.Complete(entry, success, result);
#endif
        }

        public static SpaceWarpConsoleCSharpResult EvaluateCSharp(string code, int depth)
        {
            try
            {
                CliIntegrationCSharpReplResult result = CliIntegrationCSharpRepl.Shared.Evaluate(code, depth);
                return SpaceWarpConsoleCSharpResult.FromSuccess(FormatCSharpResult(result));
            }
            catch (Exception ex)
            {
                return SpaceWarpConsoleCSharpResult.Failure(ex.GetBaseException().Message);
            }
        }

        public static void ResetCSharp() => CliIntegrationCSharpRepl.ResetShared();

        private static string FormatCSharpResult(CliIntegrationCSharpReplResult result)
        {
            string valueText = result.Value?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(valueText) && valueText != "null")
                return valueText;

            if (!string.IsNullOrWhiteSpace(result.Output))
                return result.Output;

            return result.Kind == "compile" ? "Compiled." : "Completed.";
        }

#if UNITY_EDITOR || REDUX_CLI_INTEGRATION
        private static SpaceWarpConsoleCliActivityEntryViewModel ToActivityViewModel(CliIntegrationActivityEntry entry)
        {
            return new SpaceWarpConsoleCliActivityEntryViewModel(
                entry.Id,
                entry.StartedUtc.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                entry.Source,
                entry.Kind,
                entry.Command,
                Trim(entry.Payload ?? string.Empty),
                entry.Status,
                Trim(entry.Result ?? string.Empty)
            );
        }

        private static string Trim(string value) =>
            value.Length <= DISPLAY_LENGTH ? value : value[..DISPLAY_LENGTH] + "...";
#endif
    }

    internal readonly struct SpaceWarpConsoleCSharpResult
    {
        private SpaceWarpConsoleCSharpResult(bool success, string displayText)
        {
            Success = success;
            DisplayText = displayText;
        }

        public bool Success { get; }
        public string DisplayText { get; }

        public static SpaceWarpConsoleCSharpResult FromSuccess(string displayText)
        {
            return new SpaceWarpConsoleCSharpResult(true, displayText);
        }

        public static SpaceWarpConsoleCSharpResult Failure(string displayText)
        {
            return new SpaceWarpConsoleCSharpResult(false, displayText);
        }
    }
}
