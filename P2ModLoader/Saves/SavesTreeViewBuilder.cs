using System.Xml.Linq;
using P2ModLoader.Data;

namespace P2ModLoader.Saves;

public class SavesTreeViewBuilder(string savesDirectory, ProfileManager profileManager) {
    public TreeNode CreateProfileNode(XElement profile, int profileIndex) { 	
        var name = profile.Element("Name")?.Value ?? "Unknown Profile";
        var lastSave = profile.Element("LastSave")?.Value;

        if (profileIndex == profileManager.CurrentProfileIndex)
            name = $"{name} (current)";

        var profileNode = new TreeNode(name) { Tag = NodeData.NewProfile(profile) };

        AddDataItems(profileNode, profile);
        AddSaveFiles(profileNode, name, lastSave);

        return profileNode;
    }

    private void AddDataItems(TreeNode profileNode, XElement profile) { 	
        var dataElement = profile.Element("Data");
        if (dataElement?.HasElements != true) return;

        var dataNode = new TreeNode("Data");
        foreach (var item in dataElement.Elements("Item")) {
            var itemName = item.Element("Name")?.Value ?? "Unknown";
            var itemValue = item.Element("Value")?.Value ?? "N/A";
            dataNode.Nodes.Add(new TreeNode($"{itemName}: {itemValue}"));
        }

        profileNode.Nodes.Add(dataNode);
    }

    private void AddSaveFiles(TreeNode profileNode, string profileName, string? lastSave) { 	
        var profileDirectory = Path.Combine(savesDirectory, profileName);
        if (!Directory.Exists(profileDirectory)) return;

        var savesNode = new TreeNode("Saves");
        var saveDirectories = Directory.GetDirectories(profileDirectory);
        var saveInfos = new List<SaveInfo>();

        foreach (var dir in saveDirectories) {
            var dirName = Path.GetFileName(dir);
            var dateTime = SavesInfoParser.ParseDate(dirName);
            if (dateTime == null) continue;
            saveInfos.Add(new SaveInfo { DirectoryPath = dir, DirectoryName = dirName, DateTime = dateTime });
        }

        foreach (var saveInfo in saveInfos.OrderByDescending(i => i.DateTime).ToList()) {
            var displayName = SavesInfoParser.Parse(saveInfo.DirectoryName, saveInfo.DirectoryName == lastSave);
            var saveNode = new TreeNode(displayName) { Tag = NodeData.NewSave(saveInfo.DirectoryPath) };
            savesNode.Nodes.Add(saveNode);
        }

        if (savesNode.Nodes.Count > 0)
            profileNode.Nodes.Add(savesNode);
    }
}