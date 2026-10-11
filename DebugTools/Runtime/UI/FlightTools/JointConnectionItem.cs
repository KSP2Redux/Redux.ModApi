using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace DebugTools.Runtime.UI.FlightTools
{
    [UxmlElement]
    public partial class JointConnectionItem : VisualElement
    {
        private const string ClassName = "joint-connection-item";
        private const string NameClassName = ClassName + "_name";

        public readonly Label NumJoints;
        public readonly Label HostName;
        public readonly Label TargetName;
        public readonly Label AttachmentType;
        public readonly Label JointType;
        public readonly Button Destroy;

        public JointConnectionItem()
        {
            AddToClassList(ClassName);

            NumJoints = new Label();
            hierarchy.Add(NumJoints);

            HostName = new Label();
            HostName.AddToClassList(NameClassName);
            hierarchy.Add(HostName);

            TargetName = new Label();
            TargetName.AddToClassList(NameClassName);
            hierarchy.Add(TargetName);

            AttachmentType = new Label();
            hierarchy.Add(AttachmentType);

            JointType = new Label();
            hierarchy.Add(JointType);

            Destroy = new Button { text = "Destroy" };
            hierarchy.Add(Destroy);

            // Set default preview values for UI Builder
            NumJoints.text = "4";
            HostName.text = "Host";
            TargetName.text = "Target";
            AttachmentType.text = "Surface";
            JointType.text = "Physical";
        }
    }
}