using System;
using KSP.Game;
using UitkForKsp2.API;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;
using UnityEngine.ResourceManagement.AsyncOperations;

// ReSharper disable once CheckNamespace
namespace DebugTools.Utils
{
    public static class UITKHelper
    {
        private const float MinResizableWindowWidth = 240f;
        private const float MinResizableWindowHeight = 160f;

        public static void LoadUxml(string name, Action<VisualTreeAsset> callback)
        {
            GameManager.Instance.Assets.Load($"Assets/Modules/DebugTools/Assets/UI/{name}.uxml", callback);
        }

        /// <summary>
        /// Empties the text of every label that is a direct child of a table row. A new row starts with the sample or
        /// header text of its row class, which would otherwise show in cells that the controller never fills.
        /// </summary>
        /// <param name="row">Table row</param>
        public static void ClearLabels(VisualElement row)
        {
            foreach (var child in row.hierarchy.Children())
            {
                if (child is Label label)
                    label.text = string.Empty;
            }
        }

        public static PanelRenderer CreateWindowFromUxml(VisualTreeAsset uxml, string name, float minWidth = MinResizableWindowWidth,
            float minHeight = MinResizableWindowHeight)
        {
            // Create the window options object
            var windowOptions = new WindowOptions
            {
                WindowId = $"DebugTools_{name}",
                IsHidingEnabled = true,
                DisableGameInputForTextFields = true,
                MoveOptions = new MoveOptions
                {
                    IsMovingEnabled = true,
                    CheckScreenBounds = true
                },
                ResizeOptions = new ResizeOptions
                {
                    IsResizingEnabled = true,
                    CheckScreenBounds = true,
                    MinWidth = minWidth,
                    MinHeight = minHeight
                }
            };

            // Create the window
            PanelRenderer window = Window.Create(windowOptions, uxml);
            window.panelSettings.sortingOrder = 9999;
            return window;
        }
    }
}