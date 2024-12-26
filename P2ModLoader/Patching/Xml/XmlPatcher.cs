using System.Xml.Linq;
using P2ModLoader.Helper;

namespace P2ModLoader.Patching.Xml;

public static class XmlPatcher {
    public static bool PatchXml(string sourcePath, string modPath, string targetPath) {
        XPathCalculator.ClearCache();
        try {
            var baseDoc = XmlFileManager.LoadXmlDocument(sourcePath);
            var modDoc = XmlFileManager.LoadXmlDocument(modPath);
            var targetDoc = XmlFileManager.LoadXmlDocument(targetPath);

            if (baseDoc == null || modDoc == null || targetDoc == null) {
                Logger.LogInfo($"Failed to load XML documents for {Path.GetFileName(targetPath)}");
                return false;
            }

            Logger.LogInfo($"Merging XML nodes for {Path.GetFileName(targetPath)}");
            MergeXmlNodes(targetDoc.Root!, modDoc.Root!, baseDoc.Root!);

            Logger.LogInfo($"Saving patched XML: {Path.GetFileName(targetPath)}");
            XmlFileManager.SaveXmlDocument(targetPath, targetDoc);

            Logger.LogInfo($"Successfully patched XML: {Path.GetFileName(targetPath)}");
            return true;
        } catch (Exception ex) {
            ErrorHandler.Handle($"Failed to patch XML file {Path.GetFileName(targetPath)}", ex);
            return false;
        }
    }

    private static void MergeXmlNodes(XElement target, XElement src, XElement backup) {
        var xpath = XPathCalculator.GetXPathWithIndex(target);

        if (!src.HasElements && src.Value != backup.Value) {
            Logger.LogInfo($"Changing content at {xpath} from '{target.Value}' to '{src.Value}'");
            target.Value = src.Value;
            return;
        }

        var hasIdentifiers = src.Elements().All(e => e.Attribute("id") != null || e.Element("Id") != null);
        if (!hasIdentifiers && src.Elements().Select(e => e.Name)
                .Any(n => src.Elements(n).Count() - backup.Elements(n).Count() != 0)) {
            var addedNodes = GetNodeDifference(src.Elements(), target.Elements());
            var removedNodes = GetNodeDifference(target.Elements(), src.Elements());

            if (addedNodes.Count != 0)
                Logger.LogInfo($"Added nodes at {xpath}: {string.Join("", addedNodes)}");
            if (removedNodes.Count != 0)
                Logger.LogInfo($"Removed nodes at {xpath}: {string.Join("", removedNodes)}");

            target.ReplaceNodes(new XElement(src).Nodes());
            return;
        }

        foreach (var sourceChild in src.Elements()) {
            var targetChild = FindMatchingNode(target, sourceChild);
            var baseChild = FindMatchingNode(backup, sourceChild);

            if (targetChild != null && baseChild != null) {
                MergeXmlNodes(targetChild, sourceChild, baseChild);
            } else if (targetChild == null && baseChild == null) {
                target.Add(new XElement(sourceChild));
            }
        }

        foreach (var attr in src.Attributes()) {
            var baseAttr = backup.Attribute(attr.Name);
            if (baseAttr != null && baseAttr.Value == attr.Value) continue;
            Logger.LogInfo($"Updating attribute {attr.Name} at {xpath} to '{attr.Value}'");
            target.SetAttributeValue(attr.Name, attr.Value);
        }
    }

    private static List<string> GetNodeDifference(IEnumerable<XElement> first, IEnumerable<XElement> second) =>
        FormatNodes(first).Except(FormatNodes(second)).ToList();

    private static IEnumerable<string> FormatNodes(IEnumerable<XElement> nodes) =>
        nodes.Select(n => n.ToString(SaveOptions.DisableFormatting));
    
    private static XElement? FindMatchingNode(XElement parent, XElement nodeToFind) {
        var idAttr = nodeToFind.Attribute("id");
        if (idAttr != null)
            return parent.Elements().FirstOrDefault(e => e.Attribute("id")?.Value == idAttr.Value);
        var idNode = nodeToFind.Element("Id");
        if (idNode != null)
            return parent.Elements().FirstOrDefault(e => e.Element("Id")?.Value == idNode.Value);

        var sameNameElements = parent.Elements(nodeToFind.Name).ToList();
        var indexInSameNameSiblings = nodeToFind.ElementsBeforeSelf(nodeToFind.Name).Count();
        return indexInSameNameSiblings < sameNameElements.Count ? sameNameElements[indexInSameNameSiblings] : null;
    }
}