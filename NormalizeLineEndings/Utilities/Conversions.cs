namespace NormalizeLineEndings.Utilities;

// only what this tool converts, and nothing that needs reflection, so it stays AOT compatible:
// * strings, enums and booleans from command line arguments and registry values,
// * anything else that IConvertible handles.
public static class Conversions
{
    private static readonly string[] _trueTexts = ["y", "yes"];
    private static readonly string[] _falseTexts = ["n", "no"];

    public static string? Nullify(this string? text)
    {
        if (text == null)
            return null;

        if (string.IsNullOrWhiteSpace(text))
            return null;

        var t = text.Trim();
        return t.Length == 0 ? null : t;
    }

    public static T? ChangeType<T>(object? input, T? defaultValue = default, IFormatProvider? provider = null)
    {
        if (!TryChangeType(input, provider, out T? value))
            return defaultValue;

        return value;
    }

    public static bool TryChangeType<T>(object? input, IFormatProvider? provider, out T? value)
    {
        value = default;
        if (input == null)
            return default(T) is null;

        if (input is T typed)
        {
            value = typed;
            return true;
        }

        var conversionType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        var text = string.Format(provider, "{0}", input);
        if (conversionType == typeof(string))
        {
            value = (T)(object)text;
            return true;
        }

        var trimmed = text.Nullify();
        if (trimmed == null)
            return false;

        if (conversionType.IsEnum)
        {
            if (!Enum.TryParse(conversionType, trimmed, true, out var enumValue))
                return false;

            value = (T)enumValue;
            return true;
        }

        if (conversionType == typeof(bool))
        {
            if (!TryParseBoolean(trimmed, provider, out var boolean))
                return false;

            value = (T)(object)boolean;
            return true;
        }

        if (input is not IConvertible)
            return false;

        try
        {
            value = (T)Convert.ChangeType(input, conversionType, provider);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return false;
        }
    }

    private static bool TryParseBoolean(string text, IFormatProvider? provider, out bool value)
    {
        if (bool.TryParse(text, out value))
            return true;

        if (_trueTexts.Contains(text, StringComparer.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }

        if (_falseTexts.Contains(text, StringComparer.OrdinalIgnoreCase))
        {
            value = false;
            return true;
        }

        if (long.TryParse(text, NumberStyles.Integer, provider, out var number))
        {
            value = number != 0;
            return true;
        }

        return false;
    }
}
