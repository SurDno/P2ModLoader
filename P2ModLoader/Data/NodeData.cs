using System.Xml.Linq;

namespace P2ModLoader.Data;

public class NodeData {
	public enum NodeType {
		Unknown,
		Profile,
		Save
	}
	
	public NodeType Type { get; private init; }
	public XElement? XElement { get; private init; } 
	public string? Path { get; private init; } 
	
	public static NodeData NewSave(string path) => new() { Type = NodeType.Save, Path = path };
	public static NodeData NewProfile(XElement element) => new() { Type = NodeType.Profile, XElement = element };
}