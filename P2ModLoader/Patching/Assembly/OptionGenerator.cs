using System.Globalization;
using System.Text.Json;
using P2ModLoader.Data;
using P2ModLoader.Helper;
using P2ModLoader.Logging;

namespace P2ModLoader.Patching.Assembly;

public static class OptionGenerator {
	public static string[] GetPreprocessorSymbols(ModOptions? options) {
		if (options == null) return [];

		var symbols = new List<string>();

		foreach (var option in options.Categories.SelectMany(c => c.Options)) {
			if (option.CurrentValue == null) continue;
			
			if (option.Type == ModOptions.OptionType.Boolean && (option.CurrentValue is true || 
			                                                     option.CurrentValue is JsonElement je2 && 
			                                                     je2.GetBoolean())) {
				symbols.Add(option.Macro);
			} else if (option is { Type: ModOptions.OptionType.Combo }) {
				if (option.CurrentValue is JsonElement { ValueKind: JsonValueKind.String } je)
					symbols.Add(je.GetString()!);
				else if(option.CurrentValue is string s) 
					symbols.Add(s);
			}
		}

		Logger.Log(LogLevel.Debug, $"Symbols: {string.Join(" ", symbols)}");
		return symbols.ToArray();
	}

	public static string ApplyValueReplacements(string source, ModOptions? options) {
		if (options == null) return source;
		
		foreach (var option in options.Categories.SelectMany(c => c.Options)) {
			if (string.IsNullOrEmpty(option.Macro) || option.CurrentValue == null) continue;
			if (option.Type is not ModOptions.OptionType.Integer and not ModOptions.OptionType.Decimal) continue;
			var value = JsonHelper.TryGetDecimal(option.CurrentValue);
			if (value == null) continue;
			
			var replacement = option.Type == ModOptions.OptionType.Integer ? ((int)value.Value).ToString()
				: value.Value.ToString(CultureInfo.InvariantCulture);

			source = source.Replace(option.Macro, replacement);
		}

		return source;
	}
}