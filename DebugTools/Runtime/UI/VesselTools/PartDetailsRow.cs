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
                if (value)
                {
                    PartName.text = "Name";
                    ModelMass.text = "M<sub>model</sub>";
                    PartMass.text = "M<sub>part</sub>";
                    ResourceMass.text = "M<sub>resource</sub>";
                    GreenMass.text = "M<sub>green</sub>";
                    RBMass.text = "M<sub>rb</sub>";
                    RBPhysXMass.text = "M<sub>rbphysx</sub>";
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

            PartName = new Label();
            PartName.AddToClassList(PartNameClassName);
            hierarchy.Add(PartName);

            ModelMass = new Label();
            ModelMass.AddToClassList(SubItemClassName);
            hierarchy.Add(ModelMass);

            PartMass = new Label();
            PartMass.AddToClassList(SubItemClassName);
            hierarchy.Add(PartMass);

            ResourceMass = new Label();
            ResourceMass.AddToClassList(SubItemClassName);
            hierarchy.Add(ResourceMass);

            GreenMass = new Label();
            GreenMass.AddToClassList(SubItemClassName);
            hierarchy.Add(GreenMass);

            RBMass = new Label();
            RBMass.AddToClassList(SubItemClassName);
            hierarchy.Add(RBMass);

            RBPhysXMass = new Label();
            RBPhysXMass.AddToClassList(SubItemClassName);
            hierarchy.Add(RBPhysXMass);

            WettedArea = new Label();
            WettedArea.AddToClassList(SubItemClassName);
            hierarchy.Add(WettedArea);

            IsHeader = true;
        }

        public PartDetailsRow(bool isHeader) : this()
        {
            IsHeader = isHeader;
        }
    }
}