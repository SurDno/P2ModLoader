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

        if (src.Name.LocalName == "Object" && target.Name.LocalName == "Object" && backup.Name.LocalName == "Object") {
            var targetId = target.Element("Id")?.Value;
            var sourceId = src.Element("Id")?.Value;
            var backupId = backup.Element("Id")?.Value;
            
            if (targetId == sourceId && targetId == backupId) {
                foreach (var sourceChild in src.Elements()) {
                    var targetChild = target.Element(sourceChild.Name);
                    var backupChild = backup.Element(sourceChild.Name);
                    
                    if (targetChild != null && backupChild != null) {
                        MergeXmlNodes(targetChild, sourceChild, backupChild);
                    } else if (targetChild == null && backupChild == null) {
                        target.Add(new XElement(sourceChild));
                    }
                }
                foreach (var attr in src.Attributes()) {
                    target.SetAttributeValue(attr.Name, attr.Value);
                }
                return;
            }
        }
        
        if (src.Name.LocalName == "Object" && target.Name.LocalName != "Object") {
            var targetId = target.Element("Id")?.Value;
            var sourceId = src.Element("Id")?.Value;
            var backupId = backup.Element("Id")?.Value;
            
            MergeObjectsContainer(target, src, backup);
            return;
        }

        var hasIdentifiers = src.Elements().All(e => e.Attribute("id") != null || e.Element("Id") != null);
        if (!hasIdentifiers && src.Elements().Select(e => e.Name)
                .Any(n => src.Elements(n).Count() - backup.Elements(n).Count() != 0)) {
            var addedNodes = GetNodeDifference(src.Elements(), backup.Elements());
            var removedNodes = GetNodeDifference(backup.Elements(), src.Elements());

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
                Logger.LogInfo($"Adding new node at {xpath}/{sourceChild.Name.LocalName}");
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

    private static void MergeObjectsContainer(XElement target, XElement src, XElement backup) {
        var srcObjects = src.Elements("Object").ToList();
        var srcIds = srcObjects.Select(o => o.Element("Id")?.Value).Where(id => !string.IsNullOrEmpty(id)).ToList();
        var targetObjects = target.Elements("Object").ToList();
        
        var srcIdToIndex = new Dictionary<string, int>();
        for (int i = 0; i < srcObjects.Count; i++) {
            var id = srcObjects[i].Element("Id")?.Value;
            if (!string.IsNullOrEmpty(id)) srcIdToIndex[id] = i;
        }
        
        var existingIds = new HashSet<string>();
        foreach (var obj in targetObjects) {
            var id = obj.Element("Id")?.Value;
            if (!string.IsNullOrEmpty(id)) existingIds.Add(id);
        }
        
        foreach (var srcObj in srcObjects) {
            var id = srcObj.Element("Id")?.Value;
            if (string.IsNullOrEmpty(id)) continue;
            
            if (!existingIds.Contains(id)) {
                existingIds.Add(id);
                Logger.LogInfo($"Adding new Object with Id {id}");
                
                int insertIndex = -1;
                
                var srcIndex = srcIdToIndex[id];
                
                var prevObj = srcIndex > 0 ? srcObjects[srcIndex - 1] : null;
                var nextObj = srcIndex < srcObjects.Count - 1 ? srcObjects[srcIndex + 1] : null;
                
                var prevId = prevObj?.Element("Id")?.Value;
                var nextId = nextObj?.Element("Id")?.Value;
                
                var prevTargetIndex = -1;
                var nextTargetIndex = -1;
                
                if (!string.IsNullOrEmpty(prevId)) {
                    for (int i = 0; i < targetObjects.Count; i++) {
                        if (targetObjects[i].Element("Id")?.Value == prevId) {
                            prevTargetIndex = i;
                            break;
                        }
                    }
                }
                
                if (!string.IsNullOrEmpty(nextId)) {
                    for (int i = 0; i < targetObjects.Count; i++) {
                        if (targetObjects[i].Element("Id")?.Value == nextId) {
                            nextTargetIndex = i;
                            break;
                        }
                    }
                }
                
                if (prevTargetIndex >= 0) {
                    insertIndex = prevTargetIndex + 1;
                } else if (nextTargetIndex >= 0) {
                    insertIndex = nextTargetIndex;
                }
                
                var newElement = new XElement(srcObj);
                
                if (insertIndex >= 0 && insertIndex < targetObjects.Count) {
                    targetObjects[insertIndex].AddBeforeSelf(newElement);
                    targetObjects.Insert(insertIndex, newElement);
                } else {
                    target.Add(newElement);
                    targetObjects.Add(newElement);
                }
            }
        }
        
        foreach (var targetObj in targetObjects) {
            var id = targetObj.Element("Id")?.Value;
            if (string.IsNullOrEmpty(id)) continue;
            
            var srcObj = srcObjects.FirstOrDefault(o => o.Element("Id")?.Value == id);
            var backupObj = backup.Elements("Object").FirstOrDefault(o => o.Element("Id")?.Value == id);
            
            if (srcObj != null && backupObj != null) {
                MergeXmlNodes(targetObj, srcObj, backupObj);
            }
        }
    }

    private static List<string> GetNodeDifference(IEnumerable<XElement> first, IEnumerable<XElement> second) =>
        FormatNodes(first).Except(FormatNodes(second)).ToList();

    private static IEnumerable<string> FormatNodes(IEnumerable<XElement> nodes) =>
        nodes.Select(n => n.ToString(SaveOptions.DisableFormatting));
    
    private static XElement? FindMatchingNode(XElement parent, XElement nodeToFind) {
        var idAttr = nodeToFind.Attribute("id");
        if (idAttr != null) 
            return parent.Elements(nodeToFind.Name).FirstOrDefault(e => e.Attribute("id")?.Value == idAttr.Value);

        if (nodeToFind.Element("Id") != null) {
            var idValue = nodeToFind.Element("Id")!.Value;
            return parent.Elements(nodeToFind.Name).FirstOrDefault(e => e.Element("Id")?.Value == idValue);
        }

        var sameNameElements = parent.Elements(nodeToFind.Name).ToList();
        var indexInSameNameSiblings = nodeToFind.ElementsBeforeSelf(nodeToFind.Name).Count();
        return indexInSameNameSiblings < sameNameElements.Count ? sameNameElements[indexInSameNameSiblings] : null;
    }
}