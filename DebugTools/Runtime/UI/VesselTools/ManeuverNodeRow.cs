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
                if (value)
                {
                    NodeName.text = "Name";
                    UniversalTime.text = "UT";
                    DeltaV.text = "Δv";
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

            NodeName = new Label();
            NodeName.AddToClassList(PartNameClassName);
            hierarchy.Add(NodeName);

            UniversalTime = new Label();
            UniversalTime.AddToClassList(SubItemClassName);
            hierarchy.Add(UniversalTime);

            DeltaV = new Label();
            DeltaV.AddToClassList(SubItemClassName);
            hierarchy.Add(DeltaV);

            GUID = new Label();
            GUID.AddToClassList(SubItemClassName);
            hierarchy.Add(GUID);

            IsHeader = true;
        }

        public ManeuverNodeRow(bool isHeader) : this()
        {
            IsHeader = isHeader;
        }
    }
}