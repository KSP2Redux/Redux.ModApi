using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using PatchManager.Core.Cache;
using Redux.QA;
using SpaceWarp2.API.Mods;
using UnityEngine.UIElements;

namespace DebugTools.Runtime.Controllers
{
    /// <summary>
    /// On-demand mod load, patch outcome and exception snapshots.
    /// </summary>
    public class ModDiagnosticsWindowController : BaseWindowController
    {
        private Label _output;
        private Label _status;
        private bool _isBound;

        private void OnEnable()
        {
            if (_isBound)
                return;
            _isBound = true;
            _output = RootElement.Q<Label>("output");
            _status = RootElement.Q<Label>("status");
            RootElement.Q<Button>("refresh").clicked += Refresh;
            RootElement.Q<Button>("trace").clicked += DebugToolsPlugin.OpenPatchTrace;
            RootElement.Q<Button>("capture").clicked += Capture;
            Refresh();
        }

        private void Refresh()
        {
            try
            {
                var text = new StringBuilder();
                var outcomes = CacheManager.ActiveSet == null ? null : CacheManager.Inventory.ModPatchOutcomes;
                var exceptions = new Dictionary<string, (int count, string last)>(StringComparer.Ordinal);
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var exception in ModDiagnostics.GetExceptions())
                {
                    string assembly = TopFrameAssembly(exception.stack, assemblies);
                    exceptions.TryGetValue(assembly, out var previous);
                    exceptions[assembly] = (previous.count + 1, $"{exception.message}\n{exception.stack}");
                }
                text.AppendLine("LOAD ORDER (dependency-resolved plugin list)\nTime = synchronous plugin construction and lifecycle callbacks, excluding catalog I/O.\nPatches = per-asset outcomes in the active set inventory, including cached outcomes. Removed targets count as applied.\nExceptions = retained last 128 Unity exception logs. Top-frame attribution is not proof of fault.\n");
                int order = 0;
                foreach (var mod in PluginList.AllLoadedPlugins)
                {
                    order++;
                    int[] counts = null;
                    outcomes?.TryGetValue(mod.Guid, out counts);
                    string timing = mod.LoadMilliseconds.HasValue ? mod.LoadMilliseconds.Value.ToString("F2", CultureInfo.InvariantCulture) + " ms" : "not recorded";
                    string patches = outcomes == null ? "not recorded in this cache (rebuild normally to record)" : FormattableString.Invariant(
                        $"{counts?[0] ?? 0} applied / {counts?[1] ?? 0} skipped / {counts?[2] ?? 0} failed");
                    text.AppendLine(FormattableString.Invariant($"#{order} {mod.Name} | {mod.Guid} | version {mod.SWInfo.Version}\nLoad callbacks: {timing} | Patches: {patches}"));
                    if (mod.Assemblies.Count == 0)
                        text.AppendLine("Assembly: none recorded (asset/script-only or built-in mod)");
                    foreach (Assembly assembly in mod.Assemblies)
                    {
                        string name = assembly.GetName().Name;
                        exceptions.TryGetValue(name, out var evidence);
                        text.AppendLine(FormattableString.Invariant($"Assembly: {name} | {evidence.count} retained exceptions"));
                    }
                    foreach (var error in mod.ScriptErrors)
                        text.AppendLine($"Script load failure: {error.key}\n{error.reason}");
                    text.AppendLine();
                }
                if (outcomes != null && outcomes.TryGetValue("<unattributed>", out var unknown))
                    text.AppendLine(FormattableString.Invariant($"Unattributed patch applications: {unknown.Sum()}"));
                text.AppendLine("Lua setup errors and removed setup patches are separate from application counts. Use Patch trace and the patch summary for detail.\n");
                foreach (var pair in exceptions.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    text.AppendLine(FormattableString.Invariant($"EXCEPTIONS: {pair.Key} | {pair.Value.count}\nLast:\n{pair.Value.last}\n"));
                _output.text = text.ToString();
                _status.text = "Snapshot only. Refresh after later exceptions or a patch set change.";
            }
            catch (Exception exception)
            {
                _status.text = exception.Message;
            }
        }

        private static string TopFrameAssembly(string stack, Assembly[] assemblies)
        {
            string frame = stack.Split('\n').FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))?.Trim() ?? "";
            if (frame.StartsWith("at ", StringComparison.Ordinal))
                frame = frame.Substring(3);
            int arguments = frame.IndexOf('(');
            if (arguments < 0)
                return "Unknown (top frame unavailable)";
            string method = frame.Substring(0, arguments).TrimEnd();
            int separator = Math.Max(method.LastIndexOf('.'), method.LastIndexOf(':'));
            if (separator < 0)
                return "Unknown (top frame type unavailable)";
            string type = method.Substring(0, separator).TrimEnd('.').Replace('/', '+');
            var matches = new HashSet<string>(StringComparer.Ordinal);
            foreach (Assembly assembly in assemblies)
            {
                if (assembly.GetType(type, false) != null)
                    matches.Add(assembly.GetName().Name);
            }
            return matches.Count == 1 ? matches.First() : $"Unknown or ambiguous ({type})";
        }

        private void Capture()
        {
            try
            {
                var report = new QaReport("Mod diagnostics", "bench-run");
                report.Add(new QaReportResult("loaded-mods", "Loaded mods", "mod-diagnostics", "snapshot",
                    QaVerdict.NeedsReview, _output.text));
                _status.text = $"Captured: {report.Write()}";
            }
            catch (Exception exception)
            {
                _status.text = exception.Message;
            }
        }
    }
}
