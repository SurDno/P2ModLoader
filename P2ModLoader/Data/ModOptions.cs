using System.Text.Json;
using System.Text.Json.Serialization;
using P2ModLoader.Logging;

namespace P2ModLoader.Data;

public class ModOptions {
    public List<Category> Categories { get; set; } = [];

    public static ModOptions? FromFile(string filePath) {
        try {
            var jsonOptions = new JsonSerializerOptions {
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };
            return JsonSerializer.Deserialize<ModOptions>(File.ReadAllText(filePath), jsonOptions);
        } catch (Exception ex) {
            Logger.Log(LogLevel.Warning, $"Failed to load options from path {filePath}: {ex.Message}");
            return null;
        }
    }
    
    public class Category {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<Option> Options { get; set; } = [];
    }

    public class Option {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Macro { get; set; } = string.Empty;
        public OptionType Type { get; set; }
        public object? DefaultValue { get; set; }
        public object? CurrentValue { get; set; }
        public string? DependsOn { get; set; }
        
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        
        public List<Combo>? Options { get; set; }
    }
    
    public enum OptionType {
        Boolean,
        Integer,
        Decimal,
        Combo
    }
    
    public class Combo {
        public string Label { get; set; } = string.Empty;
        public string Macro { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}