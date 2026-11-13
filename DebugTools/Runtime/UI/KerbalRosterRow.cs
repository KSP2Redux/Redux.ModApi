using System;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace DebugTools.Runtime.UI
{
    [UxmlElement]
    public partial class KerbalRosterRow : VisualElement
    {
        private const string ClassName = "dt-table__row";
        private const string KerbalNameClassName = "dt-roster-name";
        private const string SimObjectClassName = "dt-roster-object";
        private const string SeatClassName = "dt-roster-seat";
        private const string EnrollmentDateClassName = "dt-roster-date";
        private const string DeleteButtonClassName = "dt-roster-action";

        public readonly Label KerbalName;
        public readonly Label SimObject;
        public readonly Label Seat;
        public readonly Label EnrollmentDate;

        private readonly Button _deleteButton;

        public Action<string> OnDelete;

        [UxmlAttribute]
        public bool IsHeader
        {
            get => _isHeaderValue;
            set
            {
                _isHeaderValue = value;
                EnableInClassList("dt-table__header", value);
                EnableInClassList("dt-table__row", !value);
                if (value)
                {
                    KerbalName.text = "<b>Name</b>";
                    SimObject.text = "<b>Sim Object</b>";
                    Seat.text = "<b>Seat</b>";
                    EnrollmentDate.text = "<b>Enrolled</b>";
                    _deleteButton.visible = false;
                }
                else
                {
                    KerbalName.text = "Valentina Kerman";
                    SimObject.text = "Kerbal 1X";
                    Seat.text = "42";
                    EnrollmentDate.text = "123456789";
                    _deleteButton.visible = true;
                }
            }
        }

        private bool _isHeaderValue;

        public KerbalRosterRow()
        {
            AddToClassList(ClassName);

            KerbalName = new Label();
            KerbalName.AddToClassList("dt-table__cell");
            KerbalName.AddToClassList(KerbalNameClassName);
            hierarchy.Add(KerbalName);

            SimObject = new Label();
            SimObject.AddToClassList("dt-table__cell");
            SimObject.AddToClassList(SimObjectClassName);
            hierarchy.Add(SimObject);

            Seat = new Label();
            Seat.AddToClassList("dt-table__cell");
            Seat.AddToClassList(SeatClassName);
            hierarchy.Add(Seat);

            EnrollmentDate = new Label();
            EnrollmentDate.AddToClassList("dt-table__cell");
            EnrollmentDate.AddToClassList(EnrollmentDateClassName);
            hierarchy.Add(EnrollmentDate);

            _deleteButton = new Button
            {
                text = "Del"
            };
            _deleteButton.AddToClassList(DeleteButtonClassName);
            _deleteButton.AddToClassList("kui-button");
            _deleteButton.clicked += () => OnDelete?.Invoke(KerbalName.text);
            hierarchy.Add(_deleteButton);

            // Initialize with default header values
            IsHeader = true;
        }

        public KerbalRosterRow(bool isHeader) : this()
        {
            IsHeader = isHeader;
        }
    }
}