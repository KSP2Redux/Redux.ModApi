using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace DebugTools.Runtime.UI
{
    [UxmlElement]
    public partial class ThermalDataRow : VisualElement
    {
        private const string ClassName = "data-row";
        private const string EntryClassName = "data-row-entry";
        private const string SmallEntryClassName = "data-row-entry-small";
        private const string LargeEntryClassName = "data-row-entry-large";

        public readonly Label PartName;
        public readonly Label ThermalMass;
        public readonly Label Temperature;
        public readonly Label WettedArea;
        public readonly Label ShockMult;
        public readonly Label ShockArea;
        public readonly Label ReentryFlux;
        public readonly Label EnvironmentFlux;
        public readonly Label OtherFlux;
        public readonly Label CoolingFlux;
        public readonly Label ExposedArea;
        public readonly Label ConeType;
        public readonly Label ShockAngle;
        public readonly Label ShockDistance;
        public readonly Label IsShielded;

        [UxmlAttribute("IsHeader")]
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
                    PartName.text = "Part";
                    ThermalMass.text = "M<sub>th</sub>";
                    Temperature.text = "T<sub>part</sub>/T<sub>max</sub>";
                    WettedArea.text = "A<sub>wet</sub>";
                    ShockMult.text = "p<sub>shock</sub>";
                    EnvironmentFlux.text = "Q<sub>env</sub>";
                    ReentryFlux.text = "Q<sub>reentry</sub>";
                    OtherFlux.text = "Q<sub>other</sub>";
                    CoolingFlux.text = "Q<sub>cool</sub>";
                    ExposedArea.text = "A<sub>exp</sub>";
                    ShockArea.text = "A<sub>reentry</sub>";
                    ConeType.text = "C<sub>type</sub>";
                    ShockAngle.text = "Z<sub>shk</sub>";
                    ShockDistance.text = "D<sub>shk</sub>";
                    IsShielded.text = "Shield";
                }
                else
                {
                    PartName.text = "Waow";
                    ThermalMass.text = "99.9J/K";
                    Temperature.text = "999K/9.99K";
                    WettedArea.text = "9.99m<sup>2</sup>";
                    ShockMult.text = "99.9%";
                    EnvironmentFlux.text = "999W";
                    ReentryFlux.text = "999W";
                    OtherFlux.text = "999W";
                    CoolingFlux.text = "-999W";
                    ExposedArea.text = "999m<sup>2</sup>";
                    ShockArea.text = "999m<sup>2</sup>";
                    ConeType.text = "Obl";
                    ShockAngle.text = "99°";
                    ShockDistance.text = "99";
                    IsShielded.text = "X";
                }
            }
        }

        private bool _isHeader;

        public ThermalDataRow()
        {
            AddToClassList(ClassName);
            AddToClassList("dt-table__row");

            PartName = new Label();
            PartName.AddToClassList(LargeEntryClassName);
            PartName.AddToClassList("dt-table__cell");
            hierarchy.Add(PartName);

            ThermalMass = new Label();
            ThermalMass.AddToClassList(EntryClassName);
            ThermalMass.AddToClassList("dt-table__cell");
            hierarchy.Add(ThermalMass);

            Temperature = new Label();
            Temperature.AddToClassList(LargeEntryClassName);
            Temperature.AddToClassList("dt-table__cell");
            hierarchy.Add(Temperature);

            WettedArea = new Label();
            WettedArea.AddToClassList(EntryClassName);
            WettedArea.AddToClassList("dt-table__cell");
            hierarchy.Add(WettedArea);

            ShockMult = new Label();
            ShockMult.AddToClassList(EntryClassName);
            ShockMult.AddToClassList("dt-table__cell");
            hierarchy.Add(ShockMult);

            ShockArea = new Label();
            ShockArea.AddToClassList(EntryClassName);
            ShockArea.AddToClassList("dt-table__cell");
            hierarchy.Add(ShockArea);

            ReentryFlux = new Label();
            ReentryFlux.AddToClassList(EntryClassName);
            ReentryFlux.AddToClassList("dt-table__cell");
            hierarchy.Add(ReentryFlux);

            EnvironmentFlux = new Label();
            EnvironmentFlux.AddToClassList(EntryClassName);
            EnvironmentFlux.AddToClassList("dt-table__cell");
            hierarchy.Add(EnvironmentFlux);

            OtherFlux = new Label();
            OtherFlux.AddToClassList(EntryClassName);
            OtherFlux.AddToClassList("dt-table__cell");
            hierarchy.Add(OtherFlux);

            CoolingFlux = new Label();
            CoolingFlux.AddToClassList(EntryClassName);
            CoolingFlux.AddToClassList("dt-table__cell");
            hierarchy.Add(CoolingFlux);

            ExposedArea = new Label();
            ExposedArea.AddToClassList(EntryClassName);
            ExposedArea.AddToClassList("dt-table__cell");
            hierarchy.Add(ExposedArea);

            ConeType = new Label();
            ConeType.AddToClassList(SmallEntryClassName);
            ConeType.AddToClassList("dt-table__cell");
            hierarchy.Add(ConeType);

            ShockAngle = new Label();
            ShockAngle.AddToClassList(SmallEntryClassName);
            ShockAngle.AddToClassList("dt-table__cell");
            hierarchy.Add(ShockAngle);

            ShockDistance = new Label();
            ShockDistance.AddToClassList(SmallEntryClassName);
            ShockDistance.AddToClassList("dt-table__cell");
            hierarchy.Add(ShockDistance);

            IsShielded = new Label();
            IsShielded.AddToClassList(SmallEntryClassName);
            IsShielded.AddToClassList("dt-table__cell");
            hierarchy.Add(IsShielded);

            IsHeader = true;
        }

        public ThermalDataRow(bool isHeader) : this()
        {
            IsHeader = isHeader;
        }
    }
}