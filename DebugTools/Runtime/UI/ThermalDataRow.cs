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
        private const string WideEntryClassName = "data-row-entry-wide";
        private const string TemperatureEntryClassName = "data-row-entry-temp";

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
                    // Columns have a fixed width, so the headers are short plain text and the tooltip spells them out.
                    // The cells print their own units, so the headers carry none.
                    SetHeader(PartName, "Part", "Part name");
                    SetHeader(ThermalMass, "Th mass", "Thermal mass (J/K)");
                    SetHeader(Temperature, "Temp / Max", "Part temperature and its maximum (K)");
                    SetHeader(WettedArea, "A wet", "Wetted area (m²)");
                    SetHeader(ShockMult, "Shock %", "Exposed area proportion (%)");
                    SetHeader(ShockArea, "A reent", "Reentry area, the drag cube area times the exposed proportion (m²)");
                    SetHeader(ReentryFlux, "Q reent", "Reentry heat flux (W)");
                    SetHeader(EnvironmentFlux, "Q env", "Environment heat flux (W)");
                    SetHeader(OtherFlux, "Q other", "Other heat flux (W)");
                    SetHeader(CoolingFlux, "Q cool", "Cooling flux, the heat removed (W)");
                    SetHeader(ExposedArea, "A exp", "Exposed area, the drag cube area (m²)");
                    SetHeader(ConeType, "Cone", "Shock cone type, Obl for oblique or Bow");
                    SetHeader(ShockAngle, "Angle", "Shock angle");
                    SetHeader(ShockDistance, "Dist", "Shock distance");
                    SetHeader(IsShielded, "Shld", "Heat shield");
                }
                else
                {
                    foreach (VisualElement child in hierarchy.Children())
                    {
                        child.tooltip = string.Empty;
                    }

                    PartName.text = "Waow";
                    ThermalMass.text = "99.9J/K";
                    Temperature.text = "999K/9.99K";
                    WettedArea.text = "9.99m²";
                    ShockMult.text = "99.9%";
                    EnvironmentFlux.text = "999W";
                    ReentryFlux.text = "999W";
                    OtherFlux.text = "999W";
                    CoolingFlux.text = "-999W";
                    ExposedArea.text = "999m²";
                    ShockArea.text = "999m²";
                    ConeType.text = "Obl";
                    ShockAngle.text = "99°";
                    ShockDistance.text = "99";
                    IsShielded.text = "X";
                }
            }
        }

        private bool _isHeader;

        private static void SetHeader(Label cell, string text, string tooltip)
        {
            cell.text = text;
            cell.tooltip = tooltip;
        }

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
            ThermalMass.AddToClassList(WideEntryClassName);
            ThermalMass.AddToClassList("dt-table__cell");
            hierarchy.Add(ThermalMass);

            Temperature = new Label();
            Temperature.AddToClassList(EntryClassName);
            Temperature.AddToClassList(TemperatureEntryClassName);
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