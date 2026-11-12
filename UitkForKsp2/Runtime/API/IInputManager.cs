using System;

namespace UitkForKsp2.API;

/// <summary>
/// The game's input locking, as UI Toolkit windows need it. The game sets <see cref="Instance" /> at startup.
/// </summary>
public interface IInputManager
{
    public static IInputManager Instance;
    
    public bool Ready { get; }

    public void SetUitkInputLocks();

    public void RestoreUitkInputLocks();

    public void SetUitkTextInputLocks();

    public void RestoreUitkTextInputLocks();

    public void BindHideAction(Action<bool> onHide);
    public void UnbindHideAction(Action<bool> onHide);
}