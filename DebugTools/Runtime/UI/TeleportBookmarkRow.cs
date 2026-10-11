using System;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace DebugTools.Runtime.UI
{
    [UxmlElement]
    public partial class TeleportBookmarkRow : VisualElement
    {
        private const string ClassName = "teleport-bookmark-row";
        private const string SelectedClassName = ClassName + "__selected";
        private const string NameClassName = ClassName + "__name";
        private const string BodyNameClassName = ClassName + "__body-name";
        private const string TypeClassName = ClassName + "__type";
        private const string TeleportButtonClassName = ClassName + "__teleport-button";
        private const string DeleteButtonClassName = ClassName + "__delete-button";

        public readonly Label Name;
        public readonly Label BodyName;
        public readonly Label Type;

        private readonly Button _teleportButton;
        private readonly Button _deleteButton;

        public Action<string> OnTeleport;
        public Action<string> OnDelete;

        public TeleportBookmarkRow()
        {
            AddToClassList(ClassName);

            Name = new Label();
            Name.AddToClassList(NameClassName);
            hierarchy.Add(Name);

            BodyName = new Label();
            BodyName.AddToClassList(BodyNameClassName);
            hierarchy.Add(BodyName);

            Type = new Label();
            Type.AddToClassList(TypeClassName);
            hierarchy.Add(Type);

            _teleportButton = new Button
            {
                text = "TP"
            };
            _teleportButton.AddToClassList(TeleportButtonClassName);
            _teleportButton.clicked += () => OnTeleport?.Invoke(Name.text);
            hierarchy.Add(_teleportButton);

            _deleteButton = new Button
            {
                text = "Del"
            };
            _deleteButton.AddToClassList(DeleteButtonClassName);
            _deleteButton.clicked += () => OnDelete?.Invoke(Name.text);
            hierarchy.Add(_deleteButton);

            // Set default preview values for UI Builder
            Name.text = "Rings";
            BodyName.text = "Dres";
            Type.text = "Orbit";
        }

        public void SetSelected(bool value)
        {
            EnableInClassList(SelectedClassName, value);
        }
    }
}