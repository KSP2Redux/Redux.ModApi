using System;
using System.Linq;
using MoonSharp.Interpreter;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PatchManager.LuaPatching;
using PatchManager.LuaPatching.Utility;
using PatchManager.Shared;

namespace PatchManager.Core.Assets
{
    internal static partial class PatchingManager
    {
        private static KSP.Game.Flow.SequentialFlow _traceFlow;
        private static bool _isTraceRequested;
        private static bool _isTraceAsset;
        private static string _traceName;
        private static string _tracePath;

        /// <summary>
        /// Gets the selected trace label.
        /// </summary>
        internal static string TraceLabel { get; private set; }

        /// <summary>
        /// Gets whether the normal load pipeline is capturing a requested trace.
        /// </summary>
        internal static bool IsTraceRecording { get; private set; }

        /// <summary>
        /// Gets the last diagnostic capture, independent of the active game objects.
        /// </summary>
        internal static JObject TraceResult { get; private set; }

        /// <summary>
        /// Arm a single field capture for the next normal set load, rebuilding that set's cache once.
        /// </summary>
        /// <param name="label">The Addressables label.</param>
        /// <param name="name">The asset ID.</param>
        /// <param name="path">A single-result JSONPath, using zero-based array indices.</param>
        internal static void RequestTrace(string label, string name, string path)
        {
            if (IsTraceRecording)
                throw new InvalidOperationException("Wait for the current patch trace to finish.");
            if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Enter a label, asset ID and field path.");
            // Parse the path without loading or changing a game asset.
            new JObject().SelectToken(path);
            TraceLabel = label;
            _traceName = name;
            _tracePath = path;
            _isTraceRequested = true;
            TraceResult = new JObject { ["status"] = "Armed. Load a campaign normally to capture.",
                ["label"] = label, ["asset"] = name, ["path"] = path };
        }

        /// <summary>
        /// Release the capture and cancel any pending diagnostic request.
        /// </summary>
        internal static void ClearTrace()
        {
            DetachTraceFlow();
            _isTraceRequested = false;
            IsTraceRecording = false;
            _isTraceAsset = false;
            TraceResult = null;
            TraceLabel = null;
            _traceName = null;
            _tracePath = null;
        }

        private static void BeginRequestedTrace(KSP.Game.Flow.SequentialFlow flow)
        {
            if (!_isTraceRequested)
                return;
            _traceFlow = flow;
            flow.FlowError += OnTraceFlowError;
            flow.FlowFinished += OnTraceFlowFinished;
            _isTraceRequested = false;
            IsTraceRecording = true;
            TraceResult["status"] = "Recording";
            TraceResult["set"] = ActiveSetId;
            TraceResult["enabledFeatures"] = new JArray(Universe.EnabledFeatures);
            TraceResult["entries"] = new JArray();
        }

        private static void DetachTraceFlow()
        {
            if (_traceFlow == null)
                return;
            _traceFlow.FlowError -= OnTraceFlowError;
            _traceFlow.FlowFinished -= OnTraceFlowFinished;
            _traceFlow = null;
        }

        private static void OnTraceFlowError(object sender, string error)
        {
            if (!IsTraceRecording)
                return;
            if (TraceResult != null)
                TraceResult["status"] = $"Failed: {error}";
            IsTraceRecording = false;
            _isTraceAsset = false;
            DetachTraceFlow();
        }

        private static void OnTraceFlowFinished(object sender, EventArgs args) => FinishRequestedTrace();

        private static bool IsTraceTarget(string label, string name) =>
            IsTraceRecording && label == TraceLabel && name == _traceName;

        private static void BeginTraceAsset(string label, string name, JToken token)
        {
            _isTraceAsset = IsTraceTarget(label, name);
            if (!_isTraceAsset || TraceResult["original"] != null)
                return;
            TraceResult["original"] = TraceValue(token);
        }

        private static void EndTraceAsset(JToken token)
        {
            if (_isTraceAsset)
                TraceResult["effective"] = token == null ? "<asset removed>" : TraceValue(token);
            _isTraceAsset = false;
        }

        /// <summary>
        /// Read a detached field value only while the selected asset is being patched.
        /// </summary>
        /// <param name="patch">The current converter owner.</param>
        /// <param name="value">The converter's current value.</param>
        /// <returns>The JSON field text, or null when capture is inactive.</returns>
        internal static string ReadTraceValue(PatchDefinition patch, DynValue value)
        {
            if (!_isTraceAsset)
                return null;
            try
            {
                return TraceValue(patch.ConverterInstance.ToJson(value));
            }
            catch (Exception exception)
            {
                return $"<capture error: {exception.Message}>";
            }
        }

        /// <summary>
        /// Append the actual outcome without evaluating a predicate or callback again.
        /// </summary>
        /// <param name="patch">The executed patch chain.</param>
        /// <param name="value">The resulting converter value.</param>
        /// <param name="outcome">The outcome recorded by Patch Manager.</param>
        /// <param name="before">The detached field value before execution.</param>
        /// <param name="removed">Whether the callback removed its target.</param>
        internal static void RecordTracePatch(PatchDefinition patch, DynValue value, Summary.SummaryEntry outcome,
            string before, bool removed)
        {
            if (!_isTraceAsset)
                return;
            bool isCopy = patch.DuplicateNameTemplate != null;
            string after = removed ? "<asset removed>" : ReadTraceValue(patch, value);
            var entry = DescribeTracePatch(patch);
            entry["sequence"] = ((JArray)TraceResult["entries"]).Count + 1;
            entry["state"] = outcome?.State.ToString() ?? "Unknown";
            entry["context"] = outcome?.Context;
            entry["before"] = before;
            entry["after"] = after;
            entry["fieldChanged"] = before != after;
            entry["scope"] = isCopy ? $"copy only: {patch.DuplicateNameFor(_traceName)}" : "target";
            ((JArray)TraceResult["entries"]).Add(entry);
        }

        private static string TraceValue(JToken token)
        {
            try
            {
                JToken selected = token?.SelectToken(_tracePath);
                return selected == null ? "<field absent>" : selected.ToString(Formatting.None);
            }
            catch (Exception exception)
            {
                return $"<path error: {exception.Message}>";
            }
        }

        private static JObject DescribeTracePatch(PatchDefinition patch) => new()
        {
            ["chain"] = patch.Name,
            ["mod"] = patch.PatchModId,
            ["pass"] = patch.PassName,
            ["orderInPass"] = patch.Order,
            ["featuresOn"] = new JArray(patch.Selector.Features),
            ["featuresOff"] = new JArray(patch.Selector.NoFeatures)
        };

        private static void FinishRequestedTrace()
        {
            if (!IsTraceRecording)
                return;
            try
            {
                TraceResult["status"] = TraceResult["original"] == null
                    ? "Target not loaded in this feature set. Values unavailable."
                    : "Complete. Values describe this set load, before runtime changes.";
                var excluded = new JArray();
                if (Universe.AllPatches.TryGetValue(TraceLabel, out var patches))
                {
                    foreach (PatchDefinition patch in patches)
                    {
                        if ((patch.Names.Count > 0 && !patch.Names.Any(n => NamePattern.Get(n).Matches(_traceName))) ||
                            patch.DisallowedNames.Any(n => n.Matches(_traceName)))
                            continue;
                        foreach (var removal in Universe.Summary.RemovedPatches.Where(r => r.patchName == patch.Name))
                        {
                            var entry = DescribeTracePatch(patch);
                            entry["state"] = removal.status;
                            entry["context"] = removal.context;
                            excluded.Add(entry);
                        }
                    }
                }
                TraceResult["excludedBeforeExecution"] = excluded;
                TraceResult["fileErrorsNotAttributedToTarget"] = new JArray(ScriptErrorsInSet(
                    ScriptErrorsByMod, Universe.EnabledFeatures).Select(
                    e => new JObject { ["file"] = e.filename, ["error"] = e.reason }));
            }
            finally
            {
                IsTraceRecording = false;
                _isTraceAsset = false;
                DetachTraceFlow();
            }
        }
    }
}
