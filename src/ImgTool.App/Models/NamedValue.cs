namespace ImgTool.App.Models;

/// <summary>Пара «отображаемое имя — значение» для ComboBox.</summary>
public sealed record NamedValue<T>(string Name, T Value)
{
    public override string ToString() => Name;
}
