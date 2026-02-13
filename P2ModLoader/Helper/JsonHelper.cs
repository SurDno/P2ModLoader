using System.Text.Json;

namespace P2ModLoader.Helper;

public class JsonHelper {
	public static decimal? TryGetDecimal(object? value) {
		if (value == null) return null;
        
		if (value is JsonElement je) {
			if (je.ValueKind != JsonValueKind.Number) return null;
			if (je.TryGetDecimal(out var d)) return d;
			if (je.TryGetDouble(out var dbl)) return (decimal)dbl;
			if (je.TryGetInt32(out var i)) return i;
		} else if (value is IConvertible) {
			return Convert.ToDecimal(value);
		}
        
		return null;
	}
}