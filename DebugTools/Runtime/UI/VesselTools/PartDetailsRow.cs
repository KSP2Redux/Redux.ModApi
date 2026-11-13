using UnityEngine.UIElements;

namespace DebugTools.Runtime.UI.VesselTools
{
    [UxmlElement]
    public partial class PartDetailsRow : VisualElement
    {
        private const string ClassName = "part-details";
        private const string PartNameClassName = ClassName + "__name";
        private const string SubItemClassName = ClassName + "__item";

        public readonly Label PartName;
        public readonly Label ModelMass;
        public readonly Label PartMass;
        public readonly Label ResourceMass;
        public readonly Label GreenMass;
        public readonly Label RBMass;
        public readonly Label RBPhysXMass;
        public readonly Label WettedArea;

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
                    PartName.text = "Name";
                    ModelMass.text = "Model (t)";
                    PartMass.text = "Dry (t)";
                    ResourceMass.text = "Resource (t)";
                    GreenMass.text = "Green (t)";
                    RBMass.text = "RB (t)";
                    RBPhysXMass.text = "PhysX (t)";
                    WettedArea.text = "A<sub>wet</sub>";
                }
                else
                {
                    PartName.text = "Waow";
                    ModelMass.text = "99.99";
                    PartMass.text = "33.33";
                    ResourceMass.text = "33.33";
                    GreenMass.text = "33.33";
                    RBMass.text = "99.99";
                    RBPhysXMass.text = "99.99";
                    WettedArea.text = "9.99 m<sup>2</sup>";
                }
            }
        }

        private bool _isHeader;

        public PartDetailsRow()
        {
            AddToClassList(ClassName);
            AddToClassList("dt-table__row");

            PartName = new Label();
            PartName.AddToClassList(PartNameClassName);
            PartName.AddToClassList("dt-table__cell");
            hierarchy.Add(PartName);

            ModelMass = new Label();
            ModelMass.AddToClassList(SubItemClassName);
            ModelMass.AddToClassList("dt-table__cell");
            hierarchy.Add(ModelMass);

            PartMass = new Label();
            PartMass.AddToClassList(SubItemClassName);
            PartMass.AddToClassList("dt-table__cell");
            hierarchy.Add(PartMass);

            ResourceMass = new Label();
            ResourceMass.AddToClassList(SubItemClassName);
            ResourceMass.AddToClassList("dt-table__cell");
            hierarchy.Add(ResourceMass);

            GreenMass = new Label();
            GreenMass.AddToClassList(SubItemClassName);
            GreenMass.AddToClassList("dt-table__cell");
            hierarchy.Add(GreenMass);

            RBMass = new Label();
            RBMass.AddToClassList(SubItemClassName);
            RBMass.AddToClassList("dt-table__cell");
            hierarchy.Add(RBMass);

            RBPhysXMass = new Label();
            RBPhysXMass.AddToClassList(SubItemClassName);
            RBPhysXMass.AddToClassList("dt-table__cell");
            hierarchy.Add(RBPhysXMass);

            WettedArea = new Label();
            WettedArea.AddToClassList(SubItemClassName);
            WettedArea.AddToClassList("dt-table__cell");
            hierarchy.Add(WettedArea);

            IsHeader = true;
        }

        public PartDetailsRow(bool isHeader) : this()
        {
            IsHeader = isHeader;
        }
    }
}