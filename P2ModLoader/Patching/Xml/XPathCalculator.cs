using System.Xml.Linq;

namespace P2ModLoader.Patching.Xml;

public static class XPathCalculator {
	private static readonly Dictionary<XElement, string> XPathCache = new();
    
	public static string GetXPathWithIndex(XElement element) {
		if (XPathCache.TryGetValue(element, out var cachedPath))
			return cachedPath;
        
		XPathCache[element] = CalculateXPath(element);
		return XPathCache[element];
	}

	private static string CalculateXPath(XElement element) {
		if (element.Parent == null)
			return "/" + element.Name.LocalName;

		var idAttr = element.Attribute("id")?.Value;
		var idElement = element.Element("Id")?.Value;
		var identifier = idAttr ?? idElement;

		var index = element.Parent.Elements(element.Name).TakeWhile(e => e != element).Count() + 1;
    
		var xpath = GetXPathWithIndex(element.Parent) + "/" + element.Name.LocalName;
		return identifier != null ? xpath + $"[@id='{identifier}']" : xpath + $"[{index}]";
	}

	public static void ClearCache() => XPathCache.Clear();
}