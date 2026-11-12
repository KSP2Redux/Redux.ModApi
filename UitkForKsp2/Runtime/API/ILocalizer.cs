using System;

namespace UitkForKsp2.API;

/// <summary>
/// The game's localization, as UI Toolkit documents and mods need it. The game sets <see cref="Instance" /> at startup.
/// </summary>
public interface ILocalizer
{
    public static ILocalizer Instance;
    public event Action OnLocalize;

    public string? GetTranslation(string key, params object[] p);

    public void AddCsvSource(string csv);
    public void AddI2CsvSource(string i2Csv);
}