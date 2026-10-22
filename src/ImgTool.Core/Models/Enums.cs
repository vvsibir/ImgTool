namespace ImgTool.Core.Models;

/// <summary>Способ изменения размера изображения.</summary>
public enum ResizeMode
{
    /// <summary>Вписать в заданные рамки с сохранением пропорций.</summary>
    Fit,

    /// <summary>Заполнить рамки с сохранением пропорций и обрезкой лишнего.</summary>
    Cover,

    /// <summary>Растянуть до точных размеров без сохранения пропорций.</summary>
    Stretch,

    /// <summary>Изменить размер на заданный процент.</summary>
    Percent
}

/// <summary>Формат сохранения результата.</summary>
public enum ImageFormatKind
{
    /// <summary>Оставить исходный формат (если поддерживается).</summary>
    Keep,
    Jpeg,
    Png,
    WebP,
    Bmp
}

/// <summary>Способ обрезки изображения.</summary>
public enum CropMode
{
    /// <summary>Обрезка под заданную пропорцию с привязкой к стороне.</summary>
    AspectRatio,

    /// <summary>Обрезка одинаковыми полями со всех сторон.</summary>
    Margins,

    /// <summary>Обрезка фиксированным прямоугольником.</summary>
    Rectangle
}

/// <summary>Единицы измерения области обрезки.</summary>
public enum CropUnit
{
    /// <summary>Пиксели.</summary>
    Pixels,

    /// <summary>Проценты от ширины/высоты изображения.</summary>
    Percent
}

/// <summary>Точка привязки при обрезке и позиция водяного знака.</summary>
public enum Anchor
{
    TopLeft,
    Top,
    TopRight,
    Left,
    Center,
    Right,
    BottomLeft,
    Bottom,
    BottomRight
}

/// <summary>Тип водяного знака.</summary>
public enum WatermarkKind
{
    Text,
    Logo
}

/// <summary>Что делать, если выходной файл уже существует.</summary>
public enum CollisionPolicy
{
    /// <summary>Добавить числовой суффикс к имени.</summary>
    Rename,

    /// <summary>Перезаписать существующий файл.</summary>
    Overwrite,

    /// <summary>Пропустить файл.</summary>
    Skip
}

/// <summary>Режим вписывания изображения в ячейку коллажа.</summary>
public enum CollageFit
{
    /// <summary>Заполнить ячейку с обрезкой.</summary>
    Cover,

    /// <summary>Вписать целиком с полями.</summary>
    Contain
}

/// <summary>Раскладка изображений в коллаже.</summary>
public enum CollageLayout
{
    /// <summary>Сетка с заданным числом колонок.</summary>
    Grid,

    /// <summary>Вертикальная склейка: один столбец, ширина по самому широкому кадру.</summary>
    VerticalStrip,

    /// <summary>Горизонтальная склейка: одна строка, высота по самому высокому кадру.</summary>
    HorizontalStrip
}
