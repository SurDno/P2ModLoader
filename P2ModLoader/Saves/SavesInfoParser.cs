using P2ModLoader.Logging;

namespace P2ModLoader.Saves;

public static class SavesInfoParser {
	private static readonly Dictionary<string, string> LocationName = new() {
		["Grief"] = "Bad Grief's Nest",
		["None"] = "Unknown",
		["Notkin"] = "The Soul-and-a-Half Fortress",
		["Government"] = "Town Hall",
		["Theater"] = "Theatre",
		["Georgy"] = "The Crucible: Georgiy's Workshop",
		["Maria"] = "The Crucible: Maria's Throne",
		["Victor"] = "The Crucible: Victor's Abode",
		["Eva"] = "The Stillwater",
		["Julia"] = "The Trammel",
		["Young_Vlad"] = "Vacant House",
		["Kapella"] = "The Lump: Capella's Wing",
		["Olgimski"] = "The Lump: Olgimsky's Seat",
		["Lara"] = "The Shelter",
		["Mishka"] = "Murky's Corner",
		["Saburov"] = "The Rod",
		["Peter_Flat"] = "Stamatin's Loft",
		["Laska"] = "Grace's Lodge",
		["Ospina"] = "Aspity's Hospice",
		["Rubin"] = "Rubin's Hideout",
		["Rubin_Flat"] = "Rubin's Flat",
		["Anna"] = "The Willows",
		["Haruspex"] = "Haruspex's Lair",
		["Isidor"] = "Isidor Burakh's House",
		["Polyhedron"] = "Polyhedron",
		["Cathedral"] = "Cathedral",
		["Boiny"] = "Abattoir",
		["Termitnik"] = "Termitary",
		["AndrewBar"] = "The Broken Heart Pub",
		["Ospina_MN"] = "Refugee's Corner",
		["Bachelor_MN"] = "Bachelor's Place"
	};

	public static string? Parse(string save, bool isLast) {
		using var perf = PerformanceLogger.Log();
		if (string.IsNullOrEmpty(save)) return null;

		var parts = save.Split('-');
		if (parts.Length < 6) return save;

		var day = parts[0].TrimStart('0').PadLeft(2);
		var time = $"{parts[1]}:{parts[2]}";
		var location = LocationName.GetValueOrDefault(parts[3], parts[3]);
		var lastAdd = isLast ? " (last)" : "";
		var ingameData = $"Day {day}, {time}, {location}{lastAdd}";

		var formattedDateTime = ParseDate(save)!.Value.ToString("dd/MM/yyyy HH:mm:ss");

		return $"{ingameData} - {formattedDateTime}";
	}


	public static DateTime? ParseDate(string save) {
		using var perf = PerformanceLogger.Log();
		var parts = save.Split('-');
		if (parts.Length < 6) return null;

		var dt = parts[5].Replace(".xml", "").Split('_').Select(int.Parse).ToArray();
		return new DateTime(dt[0], dt[1], dt[2], dt[3], dt[4], dt[5]);
	}
}