using System.ComponentModel;
using System.Globalization;
using Quaaly.Infrastructure.Utils;

namespace Quaaly.Infrastructure.Options;

/// <summary>
/// Represents a size value that can be parsed from human-readable formats (e.g., "200KB", "1.5MB").
/// This type automatically converts from string values in configuration files.
/// </summary>
[TypeConverter(typeof(SizeTypeConverter))]
public readonly struct Size
{
    public int Bytes { get; }

    public Size(int bytes)
    {
        Bytes = bytes;
    }

    public Size(string value)
    {
        Bytes = SizeParser.ParseToBytes(value);
    }

    /// <summary>
    /// Implicit conversion to int for seamless usage.
    /// </summary>
    public static implicit operator int(Size size) => size.Bytes;

    /// <summary>
    /// Implicit conversion from int.
    /// </summary>
    public static implicit operator Size(int bytes) => new Size(bytes);

    public override string ToString() => SizeParser.FormatBytes(Bytes);
}

/// <summary>
/// TypeConverter that enables automatic parsing of Size values from configuration strings.
/// The binder automatically uses this when binding configuration to Size properties.
/// </summary>
public class SizeTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string stringValue)
        {
            return new Size(stringValue);
        }
        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is Size size)
        {
            return size.ToString();
        }
        return base.ConvertTo(context, culture, value, destinationType);
    }
}
