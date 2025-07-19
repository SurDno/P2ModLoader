using System.Xml.Linq;

namespace P2ModLoader.Patching.Xml;

public static class XPathCalculator {
    private static readonly Dictionary<XElement, string> XPathCache = new(), IdCache = new();
    
    public static string GetXPathWithIndex(XElement element) { 	
        if (XPathCache.TryGetValue(element, out var cachedPath))
            return cachedPath;
        
        XPathCache[element] = CalculateXPath(element);
        return XPathCache[element];
    }

    private static string CalculateXPath(XElement element) { 	
        if (element.Parent == null)
            return "/" + element.Name.LocalName;

        var identifier = GetCachedIdentifier(element);
        var parentPath = GetXPathWithIndex(element.Parent);
        var xpath = parentPath + "/" + element.Name.LocalName;
        
        if (identifier != null) 
            return xpath + $"[@id='{identifier}']";
        
        return xpath + $"[{element.ElementsBeforeSelf(element.Name).Count() + 1}]";
    }
    
    private static string? GetCachedIdentifier(XElement element) { 	
        if (IdCache.TryGetValue(element, out var cachedId))
            return cachedId;
            
        var idAttr = element.Attribute("id")?.Value;
        var idElement = element.Element("Id")?.Value;
        var identifier = idAttr ?? idElement;
        
        IdCache[element] = identifier;
        return identifier;
    }

    public static void ClearCache() { 	
        XPathCache.Clear();
        IdCache.Clear();
    }
}