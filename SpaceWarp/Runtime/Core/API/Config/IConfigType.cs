#nullable enable

using System;
using MoonSharp.Interpreter;

namespace SpaceWarp2.API.Config
{
    /// <summary>
    /// Maps a mod's rich Lua config value to the C# storage value an <c>IConfigEntry</c> holds, and back.
    /// </summary>
    /// <remarks>
    /// The storage value's type drives how the settings menu renders the entry - a <c>UnityEngine.Color</c> storage
    /// value gets a color picker, a string gets a text field, and so on. Every config type is a C# class implementing
    /// this, marked <c>[ConfigType]</c> and <c>[MoonSharpUserData]</c>, which makes it a Lua global that
    /// <c>ConfigBuilder:Type</c> accepts.
    /// </remarks>
    public interface IConfigType
    {
        /// <summary>
        /// The C# type the entry stores, and the type the settings menu renders the entry by.
        /// </summary>
        Type StorageType { get; }

        /// <summary>
        /// Converts a rich Lua value to the C# storage value the entry holds.
        /// </summary>
        /// <param name="value">The rich Lua value.</param>
        /// <returns>The storage value.</returns>
        object Serialize(DynValue value);

        /// <summary>
        /// Converts the C# storage value back to the rich Lua value scripts use.
        /// </summary>
        /// <param name="storage">The storage value.</param>
        /// <returns>The rich Lua value.</returns>
        DynValue Deserialize(object storage);
    }
}
