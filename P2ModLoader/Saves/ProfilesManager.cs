using System.Xml;
using System.Xml.Linq;

namespace P2ModLoader.Saves;

public class ProfileManager(string savesDirectory, string profilesPath) {
    private XDocument? _profilesDoc;

    public int CurrentProfileIndex {
        get {
            var indexStr = _profilesDoc?.Root?.Element("CurrentIndex")?.Value;
            return int.TryParse(indexStr, out var index) ? index : -1;
        }
    }

    public void LoadProfiles() { 	
        if (!File.Exists(profilesPath)) return;

        try {
            var xmlSettings = new XmlReaderSettings { IgnoreWhitespace = false };
            using var reader = XmlReader.Create(profilesPath, xmlSettings);
            _profilesDoc = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        } catch (Exception ex) {
            throw new Exception($"Error loading profiles from {profilesPath}", ex);
        }
    }

    public IEnumerable<(XElement Profile, int Index)> GetProfiles() { 	
        return _profilesDoc!.Root!.Elements("Profiles").First().Elements("Item").Select((p, i) => (p, i));
    }

    public void DeleteProfile(XElement profileElement) { 	
        var profileName = profileElement.Element("Name")?.Value;
        if (profileName == null) return;

        var profiles = _profilesDoc!.Descendants("Item").Where(item => item.Parent?.Name == "Profiles").ToList();

        var deletedIndex = profiles.IndexOf(profileElement);
        var currentIndex = CurrentProfileIndex;

        profileElement.Remove();

        if (deletedIndex <= currentIndex)
            _profilesDoc.Root!.Element("CurrentIndex")!.Value = (currentIndex - 1).ToString();

        SaveXmlDocument();

        var profilePath = Path.Combine(savesDirectory, profileName);
        if (Directory.Exists(profilePath))
            Directory.Delete(profilePath, true);
    }

    private void SaveXmlDocument() { 	
        if (_profilesDoc == null) return;

        var xmlSettings = new XmlWriterSettings {
            Indent = true,
            IndentChars = "\t",
            NewLineChars = "\r\n",
            NewLineHandling = NewLineHandling.Replace
        };

        using var writer = XmlWriter.Create(profilesPath, xmlSettings);
        _profilesDoc.Save(writer);
    }

    public void RenameProfile(XElement profileElement, string newFullName) { 	
        var oldName = profileElement.Element("Name")?.Value;
        if (string.IsNullOrEmpty(oldName))
            throw new InvalidOperationException("Profile name not found");

        var oldPath = Path.Combine(savesDirectory, oldName);
        var newPath = Path.Combine(savesDirectory, newFullName);

        profileElement.Element("Name")!.Value = newFullName;
        SaveXmlDocument();

        if (Directory.Exists(oldPath))
            Directory.Move(oldPath, newPath);
    }

    public string GetSavesDirectory() => savesDirectory;
}