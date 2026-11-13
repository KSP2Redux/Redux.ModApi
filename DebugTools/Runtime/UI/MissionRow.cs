using System;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace DebugTools.Runtime.UI
{
    /// <summary>
    /// One mission in the Missions table. A click anywhere on the row selects the mission.
    /// </summary>
    public class MissionRow : VisualElement
    {
        private const string ClassName = "mission-row";
        private const string SelectedClassName = ClassName + "__selected";
        private const string IdClassName = ClassName + "__id";
        private const string NameClassName = ClassName + "__name";
        private const string TypeClassName = ClassName + "__type";
        private const string OwnerClassName = ClassName + "__owner";
        private const string StateClassName = ClassName + "__state";
        private const string StageInfoClassName = ClassName + "__stage-info";

        private const string StateActiveClass = ClassName + "__state--active";
        private const string StateInactiveClass = ClassName + "__state--inactive";
        private const string StateCompleteClass = ClassName + "__state--complete";
        private const string StateFailedClass = ClassName + "__state--failed";

        public readonly Label Id;
        public readonly Label Name;
        public readonly Label Type;
        public readonly Label Owner;
        public readonly Label State;
        public readonly Label StageInfo;

        public Action OnSelect;

        public MissionRow()
        {
            AddToClassList("dt-table__row");
            AddToClassList(ClassName);

            Id = AddCell(IdClassName);
            Name = AddCell(NameClassName);
            Type = AddCell(TypeClassName);
            Owner = AddCell(OwnerClassName);
            State = AddCell(StateClassName);
            StageInfo = AddCell(StageInfoClassName);

            RegisterCallback<ClickEvent>(_ => OnSelect?.Invoke());
        }

        private Label AddCell(string className)
        {
            var cell = new Label();
            cell.AddToClassList("dt-table__cell");
            cell.AddToClassList(className);
            Add(cell);
            return cell;
        }

        public void SetStateClass(string stateText)
        {
            RemoveFromClassList(StateActiveClass);
            RemoveFromClassList(StateInactiveClass);
            RemoveFromClassList(StateCompleteClass);
            RemoveFromClassList(StateFailedClass);

            switch (stateText)
            {
                case "Active":
                    AddToClassList(StateActiveClass);
                    break;
                case "Complete":
                    AddToClassList(StateCompleteClass);
                    break;
                case "Failed":
                    AddToClassList(StateFailedClass);
                    break;
                default:
                    AddToClassList(StateInactiveClass);
                    break;
            }
        }

        public void SetSelected(bool value)
        {
            EnableInClassList(SelectedClassName, value);
        }
    }
}
