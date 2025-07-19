using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace P2ModLoader.Patching.Xml;

public static class XmlFileManager {
	public static XDocument? LoadXmlDocument(string path) { 	
		var settings = new XmlReaderSettings { IgnoreWhitespace = false };

		if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)) {
			using var fileStream = File.OpenRead(path);
			using var gzStream = new GZipStream(fileStream, CompressionMode.Decompress);
			using var reader = XmlReader.Create(gzStream, settings);
			return XDocument.Load(reader);
		} else {
			using var reader = XmlReader.Create(path, settings);
			return XDocument.Load(reader);
		}
	}

	public static void SaveXmlDocument(string path, XDocument doc) { 	
		var settings = new XmlWriterSettings { NewLineHandling = NewLineHandling.None, Indent = false };

		if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)) {
			using var fileStream = File.Create(path);
			using var gzStream = new GZipStream(fileStream, CompressionLevel.Optimal);
			using var writer = XmlWriter.Create(gzStream, settings);
			doc.Save(writer);
		} else {
			using var writer = XmlWriter.Create(path, settings);
			doc.Save(writer);
		}
	}
}