using System;
using KSP.Game;
using UitkForKsp2.API;
using UitkForKsp2.Controls;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;
using UnityEngine.ResourceManagement.AsyncOperations;

// ReSharper disable once CheckNamespace
namespace DebugTools.Utils
{
    public static class UITKHelper
    {
        public static void LoadUxml(string name, Action<VisualTreeAsset> callback)
        {
            GameManager.Instance.Assets.Load($"Assets/Modules/DebugTools/Assets/UI/{name}.uxml", callback);
        }

        /// <summary>
        /// Empties the text of every label that is a direct child of a table row. A new row starts with the sample or
        /// header text of its row class, which would otherwise show in cells that the controller never fills.
        /// </summary>
        /// <param name="row">Table row.</param>
        public static void ClearLabels(VisualElement row)
        {
            foreach (var child in row.hierarchy.Children())
            {
                if (child is Label label)
                    label.text = string.Empty;
            }
        }

        /// <summary>
        /// Finds the tooltip of the panel an element is in. The hub and each pop-out hold one.
        /// </summary>
        /// <param name="element">An element in the panel.</param>
        /// <returns>The tooltip, or <c>null</c> when the element is not in a panel or the panel has none.</returns>
        public static Tooltip FindTooltip(VisualElement element) => element?.panel?.visualTree.Q<Tooltip>();

        /// <summary>
        /// Shows the element's tooltip text while the pointer is over it.
        /// </summary>
        /// <param name="element">The element, with its <c>tooltip</c> set.</param>
        public static void AttachTooltip(VisualElement element)
        {
            element.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (!string.IsNullOrEmpty(element.tooltip))
                {
                    FindTooltip(element)?.Show(element);
                }
            });
            element.RegisterCallback<PointerLeaveEvent>(_ => FindTooltip(element)?.Close(element));
        }

        /// <summary>
        /// Puts the header of a table into its host above the scroll view of the rows, and keeps it fixed while the rows
        /// scroll up and down. A sideways scroll of the rows moves the header with them.
        /// </summary>
        /// <param name="header">The header row.</param>
        /// <param name="headerHost">The element above the scroll view that clips the header.</param>
        /// <param name="rows">The scroll view of the table rows.</param>
        public static void SetTableHeader(VisualElement header, VisualElement headerHost, ScrollView rows)
        {
            headerHost.Clear();
            headerHost.Add(header);
            rows.horizontalScroller.valueChanged += offset => header.style.translate = new Translate(-offset, 0f);
        }

        /// <summary>
        /// Creates the hub window, which owns its panel. Tools do not use this, they are hosted by the hub.
        /// </summary>
        /// <param name="uxml">The window layout.</param>
        /// <param name="name">The window name.</param>
        /// <param name="minWidth">The smallest width a resize can give the window.</param>
        /// <param name="minHeight">The smallest height a resize can give the window.</param>
        /// <returns>The renderer of the new window.</returns>
        public static PanelRenderer CreateWindowFromUxml(VisualTreeAsset uxml, string name, float minWidth, float minHeight)
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