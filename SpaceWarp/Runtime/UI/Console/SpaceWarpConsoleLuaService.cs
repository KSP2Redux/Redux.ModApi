#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Redux.Lua.Scripting;
using SpaceWarp2.API;
using SpaceWarp2.API.Lifecycle;

namespace SpaceWarp2.UI.Console
{
    internal static class SpaceWarpConsoleLuaService
    {
        private static ConsoleScriptRun? _activeRun;
        private static string _activeLuaRunId = string.Empty;
        private static string _activeLuaCompletionText = string.Empty;
        private static bool _activeLuaErrored;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _activeRun = null;
            _activeLuaRunId = string.Empty;
            _activeLuaCompletionText = string.Empty;
            _activeLuaErrored = false;
        }

        public static bool IsLuaRunning => IsLuaRunActive();

        public static SpaceWarpConsoleCSharpResult RunLua(string code)
        {
            if (IsLuaRunActive())
                return SpaceWarpConsoleCSharpResult.Failure("A Lua script is already running.");

            string runId = string.Empty;
            try
            {
                runId = ScriptRunRegistry.BeginScriptRun(ModScriptRuntime.ConsoleModId, "SpaceWarp Console", "UI");
                ScriptRunRegistry.SetCurrentScriptRun(runId);
                _activeLuaRunId = runId;
                _activeRun = ModScriptRuntime.RunConsole(code);
                _activeRun.Resume();
                return CompleteLuaIfFinished("Started. Script.Log output appears on the Logs tab.");
            }
            catch (Exception ex)
            {
                ScriptRunRegistry.CancelScriptRun(_activeLuaRunId);
                ClearActiveLuaRun();
                return SpaceWarpConsoleCSharpResult.Failure(ex.GetBaseException().Message);
            }
            finally
            {
                ScriptRunRegistry.ClearCurrentScriptRun(runId);
            }
        }

        public static SpaceWarpConsoleCSharpResult TickLua()
        {
            if (!IsLuaRunActive())
                return SpaceWarpConsoleCSharpResult.FromSuccess(string.Empty);

            if (_activeRun != null && !_activeRun.IsFinished)
            {
                string runId = _activeLuaRunId;
                try
                {
                    ScriptRunRegistry.SetCurrentScriptRun(runId);
                    _activeRun.Resume();
                }
                finally
                {
                    ScriptRunRegistry.ClearCurrentScriptRun(runId);
                }
            }

            return CompleteLuaIfFinished(string.Empty);
        }

        public static SpaceWarpConsoleCSharpResult StopLua()
        {
            if (!IsLuaRunActive())
                return SpaceWarpConsoleCSharpResult.Failure("No Lua script is running.");

            ScriptRunRegistry.CancelScriptRun(_activeLuaRunId);
            ClearActiveLuaRun();
            return SpaceWarpConsoleCSharpResult.FromSuccess("Stopped.");
        }

        public static IReadOnlyList<string> ListLuaScripts()
        {
            try
            {
                string root = GetLuaScriptRoot();
                if (!Directory.Exists(root))
                {
                    Directory.CreateDirectory(root);
                }

                return Directory
                    .EnumerateFiles(root, "*.lua", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public static SpaceWarpConsoleCSharpResult ReadLuaScript(string relativePath)
        {
            try
            {
                string path = ResolveLuaScriptPath(relativePath, mustBeLuaFile: true);
                return File.Exists(path)
                    ? SpaceWarpConsoleCSharpResult.FromSuccess(File.ReadAllText(path))
                    : SpaceWarpConsoleCSharpResult.Failure("Lua script file does not exist.");
            }
            catch (Exception ex)
            {
                return SpaceWarpConsoleCSharpResult.Failure(ex.GetBaseException().Message);
            }
        }

        public static SpaceWarpConsoleCSharpResult WriteLuaScript(string relativePath, string text)
        {
            try
            {
                string path = ResolveLuaScriptPath(relativePath, mustBeLuaFile: true);
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? GetLuaScriptRoot());
                File.WriteAllText(path, text ?? string.Empty);
                return SpaceWarpConsoleCSharpResult.FromSuccess("Saved " + NormalizeLuaRelativePath(relativePath) + ".");
            }
            catch (Exception ex)
            {
                return SpaceWarpConsoleCSharpResult.Failure(ex.GetBaseException().Message);
            }
        }

        public static SpaceWarpConsoleCSharpResult CreateLuaScript()
        {
            try
            {
                string root = GetLuaScriptRoot();
                Directory.CreateDirectory(root);
                for (int index = 0; index < 1000; index++)
                {
                    string relativePath = index == 0 ? "new_script.lua" : $"new_script_{index}.lua";
                    string path = ResolveLuaScriptPath(relativePath, mustBeLuaFile: true);
                    if (File.Exists(path))
                        continue;

                    File.WriteAllText(path, "-- New Lua script\n");
                    return SpaceWarpConsoleCSharpResult.FromSuccess(relativePath);
                }

                return SpaceWarpConsoleCSharpResult.Failure("Could not create a unique Lua script name.");
            }
            catch (Exception ex)
            {
                return SpaceWarpConsoleCSharpResult.Failure(ex.GetBaseException().Message);
            }
        }

        public static SpaceWarpConsoleCSharpResult OpenLuaScriptFolder()
        {
            try
            {
                string root = GetLuaScriptRoot();
                Directory.CreateDirectory(root);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(root)
                {
                    UseShellExecute = true
                });
                return SpaceWarpConsoleCSharpResult.FromSuccess(root);
            }
            catch (Exception ex)
            {
                return SpaceWarpConsoleCSharpResult.Failure(ex.GetBaseException().Message);
            }
        }

        private static SpaceWarpConsoleCSharpResult CompleteLuaIfFinished(string runningMessage)
        {
            if (_activeRun != null && _activeRun.IsFinished)
            {
                _activeLuaErrored = _activeRun.IsErrored;
                _activeLuaCompletionText = FormatLuaResult(_activeRun.Result);
                _activeRun = null;
                ScriptRunRegistry.CompleteScriptRun(_activeLuaRunId);
            }

            if (IsLuaRunActive())
                return SpaceWarpConsoleCSharpResult.FromSuccess(runningMessage);

            string resultText = string.IsNullOrWhiteSpace(_activeLuaCompletionText)
                ? "Completed."
                : _activeLuaCompletionText;
            bool success = !_activeLuaErrored;
            ClearActiveLuaRun();
            return success
                ? SpaceWarpConsoleCSharpResult.FromSuccess(resultText)
                : SpaceWarpConsoleCSharpResult.Failure(resultText);
        }

        private static string FormatLuaResult(string result)
        {
            return string.Equals(result, "nil", StringComparison.OrdinalIgnoreCase) ? string.Empty : result;
        }

        private static bool IsLuaRunActive() =>
            _activeRun != null || ScriptRunRegistry.IsScriptRunActive(_activeLuaRunId);

        private static void ClearActiveLuaRun()
        {
            _activeRun = null;
            _activeLuaRunId = string.Empty;
            _activeLuaCompletionText = string.Empty;
            _activeLuaErrored = false;
        }

        private static string GetLuaScriptRoot()
        {
            return Path.GetFullPath(CommonPaths.LuaFolder);
        }

        private static string ResolveLuaScriptPath(string relativePath, bool mustBeLuaFile)
        {
            relativePath = NormalizeLuaRelativePath(relativePath);
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("Lua script path is required.");

            if (Path.IsPathRooted(relativePath))
                throw new InvalidOperationException("Lua script path must be relative to Lua.");

            if (mustBeLuaFile && !relativePath.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Lua script path must end with .lua.");

            string root = GetLuaScriptRoot();
            string fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Lua script path is outside Lua.");
            }

            return fullPath;
        }

        private static string NormalizeLuaRelativePath(string relativePath)
        {
            return (relativePath ?? string.Empty).Trim().Replace('\\', '/');
        }
    }
}
