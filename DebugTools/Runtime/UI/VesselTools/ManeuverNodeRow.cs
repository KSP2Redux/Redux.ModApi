using UnityEngine.UIElements;

namespace DebugTools.Runtime.UI.VesselTools
{
    [UxmlElement]
    public partial class ManeuverNodeRow : VisualElement
    {
        private const string ClassName = "maneuver-node";
        private const string PartNameClassName = ClassName + "__name";
        private const string SubItemClassName = ClassName + "__item";

        public readonly Label NodeName;
        public readonly Label UniversalTime;
        public readonly Label DeltaV;
        public readonly Label GUID;

        [UxmlAttribute]
        public bool IsHeader
        {
            get => _isHeader;
            set
            {
                _isHeader = value;
                EnableInClassList("dt-table__header", value);
                EnableInClassList("dt-table__row", !value);
                if (value)
                {
                    NodeName.text = "Name";
                    UniversalTime.text = "UT (s)";
                    DeltaV.text = "Δv (m/s)";
                    GUID.text = "GUID";
                }
                else
                {
                    NodeName.text = "Node-1";
                    UniversalTime.text = "1234.5";
                    DeltaV.text = "1234.5";
                    GUID.text = "abc-def-ghi";
                }
            }
        }

        private bool _isHeader;

        public ManeuverNodeRow()
        {
            AddToClassList(ClassName);
            AddToClassList("dt-table__row");

            NodeName = new Label();
            NodeName.AddToClassList(PartNameClassName);
            NodeName.AddToClassList("dt-table__cell");
            hierarchy.Add(NodeName);

            UniversalTime = new Label();
            UniversalTime.AddToClassList(SubItemClassName);
            UniversalTime.AddToClassList("dt-table__cell");
            hierarchy.Add(UniversalTime);

            DeltaV = new Label();
            DeltaV.AddToClassList("dt-vector");
            DeltaV.AddToClassList(SubItemClassName);
            DeltaV.AddToClassList("dt-table__cell");
            hierarchy.Add(DeltaV);

            GUID = new Label();
            GUID.AddToClassList("dt-guid");
            GUID.AddToClassList(SubItemClassName);
            GUID.AddToClassList("dt-table__cell");
            hierarchy.Add(GUID);

            IsHeader = true;
        }

        public ManeuverNodeRow(bool isHeader) : this()
        {
            IsHeader = isHeader;
        }
    }
}