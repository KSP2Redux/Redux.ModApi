using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace DebugTools.Runtime.UI.FlightTools
{
    [UxmlElement]
    public partial class SimObjectItem : VisualElement
    {
        private const string ClassName = "sim-object-item";
        private const string NameClassName = ClassName + "_name";
        private const string PhysicsModeClassName = ClassName + "_physics-mode";
        private const string ParentFrameClassName = ClassName + "_parent-frame";
        private const string ToggleLoadUnloadClassName = ClassName + "_load-unload";

        public readonly Label TextName;
        public readonly Label TextPhysicsMode;
        public readonly Label TextParentReferenceFrame;
        public readonly Button ToggleLoadUnload;
        public readonly Button SetActive;
        public readonly Button Destroy;

        [UxmlAttribute]
        public bool IsLoaded
        {
            get => _isLoaded;
            set
            {
                _isLoaded = value;
                ToggleLoadUnload.text = value ? "Unload" : "Load";
            }
        }
        private bool _isLoaded;

        public SimObjectItem()
        {
            AddToClassList(ClassName);

            TextName = new Label();
            TextName.AddToClassList(NameClassName);
            hierarchy.Add(TextName);

            TextPhysicsMode = new Label();
            TextPhysicsMode.AddToClassList(PhysicsModeClassName);
            hierarchy.Add(TextPhysicsMode);

            TextParentReferenceFrame = new Label();
            TextParentReferenceFrame.AddToClassList(ParentFrameClassName);
            hierarchy.Add(TextParentReferenceFrame);

            ToggleLoadUnload = new Button { text = "Load" };
            ToggleLoadUnload.AddToClassList(ToggleLoadUnloadClassName);
            hierarchy.Add(ToggleLoadUnload);

            SetActive = new Button { text = "Set Active" };
            hierarchy.Add(SetActive);

            Destroy = new Button { text = "Destroy" };
            hierarchy.Add(Destroy);

            // Set default preview values for UI Builder
            TextName.text = "Waow";
            TextPhysicsMode.text = "RigidBody";
            TextParentReferenceFrame.text = "loremipsum - Celestial";
        }
    }
}