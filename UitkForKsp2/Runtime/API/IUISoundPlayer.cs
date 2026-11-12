using UnityEngine.UIElements;

namespace UitkForKsp2.API;

/// <summary>
/// Plays the game's UI sounds for UI Toolkit elements. The game sets <see cref="Instance" /> at startup.
/// </summary>
public interface IUISoundPlayer
{
    public static IUISoundPlayer Instance;

    public void PostAKEventWithPositionalRTPC(string eventToPost, VisualElement target);
}
