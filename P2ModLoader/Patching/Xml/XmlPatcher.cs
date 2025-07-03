using System.Xml.Linq;
using P2ModLoader.Helper;
using P2ModLoader.Logging;

namespace P2ModLoader.Patching.Xml;

public static class XmlPatcher {
    private static readonly Dictionary<XElement, Dictionary<string, XElement>> IdBasedLookupCache = new();
    private static readonly Dictionary<XElement, List<XElement>> ChildrenByNameCache = new();
    
    public static bool PatchXml(string sourcePath, string modPath, string targetPath) {
        using var perf = PerformanceLogger.Log();
        ClearAllCaches();
        
        try {
            Logger.Log(LogLevel.Info, $"Starting XML patch for {Path.GetFileName(targetPath)}");
            
            var baseDoc = XmlFileManager.LoadXmlDocument(sourcePath);
            var modDoc = XmlFileManager.LoadXmlDocument(modPath);
            var targetDoc = XmlFileManager.LoadXmlDocument(targetPath);

            if (baseDoc == null || modDoc == null || targetDoc == null) {
                Logger.Log(LogLevel.Info, $"Failed to load XML documents for {Path.GetFileName(targetPath)}");
                return false;
            }
            
            Logger.Log(LogLevel.Info, $"Merging XML nodes for {Path.GetFileName(targetPath)}");
            Logger.Log(LogLevel.Info, $"Building optimized lookup caches...");
            BuildOptimizedCaches(targetDoc.Root!);
            BuildOptimizedCaches(modDoc.Root!);
            BuildOptimizedCaches(baseDoc.Root!);
            Logger.Log(LogLevel.Info, $"Starting merge process...");
            MergeXmlNodes(targetDoc.Root!, modDoc.Root!, baseDoc.Root!);
            Logger.Log(LogLevel.Info, $"Saving patched XML: {Path.GetFileName(targetPath)}");
            XmlFileManager.SaveXmlDocument(targetPath, targetDoc);
            Logger.Log(LogLevel.Info, $"Successfully patched XML: {Path.GetFileName(targetPath)}.");
            return true;
        } catch (Exception ex) {
            ErrorHandler.Handle($"Failed to patch XML file {Path.GetFileName(targetPath)}.", ex);
            return false;
        } finally {
            ClearAllCaches();
        }
    }
    
    private static void ClearAllCaches() {
        using var perf = PerformanceLogger.Log();
        XPathCalculator.ClearCache();
        IdBasedLookupCache.Clear();
        ChildrenByNameCache.Clear();
    }
    
    private static void BuildOptimizedCaches(XElement root) {
        using var perf = PerformanceLogger.Log();
        
        foreach (var element in root.DescendantsAndSelf()) {
            var idLookup = new Dictionary<string, XElement>();
            foreach (var child in element.Elements()) {
                var idAttr = child.Attribute("id")?.Value;
                var idElement = child.Element("Id")?.Value;
                var identifier = idAttr ?? idElement;
                
                if (!string.IsNullOrEmpty(identifier)) 
                    idLookup[identifier] = child;
            }
            if (idLookup.Count > 0) 
                IdBasedLookupCache[element] = idLookup;
            
            var byName = element.Elements().GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.ToList());
            
            if (byName.Count > 0) 
                ChildrenByNameCache[element] = element.Elements().ToList();
        }
    }

    private static void MergeXmlNodes(XElement target, XElement src, XElement backup) {
        using var perf = PerformanceLogger.Log();
        if (XNode.DeepEquals(src, backup)) return;
        
        if (!src.HasElements && src.Value != backup.Value) {
            if (target.Value == src.Value) return;
            var xpath = XPathCalculator.GetXPathWithIndex(target);
            Logger.Log(LogLevel.Info, $"Changing content at {xpath} from '{target.Value}' to '{src.Value}'");
            target.Value = src.Value;
            return;
        }

        var srcElements = src.Elements().ToList();

        if (srcElements.Count > 1000) {
            ProcessChildrenInBatches(target, backup, srcElements);
            return;
        }
        
        ProcessChildrenOptimized(target, backup, srcElements);
        ProcessAttributesBatch(target, src, backup);
    }
    
    private static void ProcessChildrenInBatches(XElement target, XElement backup, List<XElement> srcElements) {
        using var perf = PerformanceLogger.Log();
        Logger.Log(LogLevel.Info, $"Processing {srcElements.Count} children in batches");

        var elementsWithIds = new List<XElement>();
        var elementsWithoutIds = new List<XElement>();
        
        foreach (var srcChild in srcElements) {
            var idAttr = srcChild.Attribute("id")?.Value;
            var idElement = srcChild.Element("Id")?.Value;
            
            if (!string.IsNullOrEmpty(idAttr) || !string.IsNullOrEmpty(idElement)) 
                elementsWithIds.Add(srcChild);
            else 
                elementsWithoutIds.Add(srcChild);
        }
        
        Logger.Log(LogLevel.Info, $"Found {elementsWithIds.Count} elements with IDs, " +
                                  $"{elementsWithoutIds.Count} without IDs");
        
        if (elementsWithIds.Count > 0) 
            ProcessElementsWithIds(target, backup, elementsWithIds);
        
        const int batchSize = 1000;
        for (var i = 0; i < elementsWithoutIds.Count; i += batchSize) {
            var batch = elementsWithoutIds.Skip(i).Take(batchSize).ToList();
            ProcessElementsBatch(target, backup, batch);
        }
    }

    private static void ProcessElementsWithIds(XElement target, XElement backup, List<XElement> elementsWithIds) {
        using var perf = PerformanceLogger.Log();
        var targetIdLookup = IdBasedLookupCache.GetValueOrDefault(target, new Dictionary<string, XElement>());
        var backupIdLookup = IdBasedLookupCache.GetValueOrDefault(backup, new Dictionary<string, XElement>());

        foreach (var srcChild in elementsWithIds) {
            var idAttr = srcChild.Attribute("id")?.Value;
            var idElement = srcChild.Element("Id")?.Value;
            var identifier = idAttr ?? idElement;

            if (string.IsNullOrEmpty(identifier)) continue;

            var targetChild = targetIdLookup.GetValueOrDefault(identifier);
            var backupChild = backupIdLookup.GetValueOrDefault(identifier);

            if (targetChild != null && backupChild != null) {
                if (!XNode.DeepEquals(srcChild, backupChild))
                    MergeXmlNodes(targetChild, srcChild, backupChild);
            } else if (targetChild == null && backupChild == null)
                target.Add(new XElement(srcChild));
        }
    }

    private static void ProcessElementsBatch(XElement target, XElement backup, List<XElement> batch) {
        using var perf = PerformanceLogger.Log();
        var targetChildren = ChildrenByNameCache.GetValueOrDefault(target, target.Elements().ToList());
        var backupChildren = ChildrenByNameCache.GetValueOrDefault(backup, backup.Elements().ToList());
        
        foreach (var srcChild in batch) {
            var targetChild = FindMatchingNodeFast(targetChildren, srcChild);
            var backupChild = FindMatchingNodeFast(backupChildren, srcChild);
            
            if (targetChild != null && backupChild != null) {
                if (!XNode.DeepEquals(srcChild, backupChild))
                    MergeXmlNodes(targetChild, srcChild, backupChild);
            } else if (targetChild == null && backupChild == null) {
                target.Add(new XElement(srcChild));
            }
        }
    }
    
    private static void ProcessChildrenOptimized(XElement target, XElement backup, List<XElement> srcElements) {
        using var perf = PerformanceLogger.Log();
        foreach (var sourceChild in srcElements) {
            var targetChild = FindMatchingNodeUltraFast(target, sourceChild);
            var baseChild = FindMatchingNodeUltraFast(backup, sourceChild);

            if (targetChild != null && baseChild != null) {
                if (!XNode.DeepEquals(sourceChild, baseChild))
                    MergeXmlNodes(targetChild, sourceChild, baseChild);
            } else if (targetChild == null && baseChild == null) 
                target.Add(new XElement(sourceChild));
        }
    }
    
    private static void ProcessAttributesBatch(XElement target, XElement src, XElement backup) {
        using var perf = PerformanceLogger.Log();
        var attributesToUpdate = (from attr in src.Attributes() let baseAttr = backup.Attribute(attr.Name)
            where baseAttr == null || baseAttr.Value != attr.Value select attr).ToList();

        if (attributesToUpdate.Count <= 0) return;
        
        foreach (var attr in attributesToUpdate) {
            Logger.Log(LogLevel.Info, $"Updating attribute {attr.Name} at " +
                                      $"{XPathCalculator.GetXPathWithIndex(target)} to '{attr.Value}'");
            target.SetAttributeValue(attr.Name, attr.Value);
        }
    }
    
    private static XElement? FindMatchingNodeUltraFast(XElement parent, XElement nodeToFind) {
        using var perf = PerformanceLogger.Log();
        var idAttr = nodeToFind.Attribute("id")?.Value;
        if (!string.IsNullOrEmpty(idAttr) && IdBasedLookupCache.TryGetValue(parent, out var idLookup1))
            return idLookup1.GetValueOrDefault(idAttr);
        var idElement = nodeToFind.Element("Id")?.Value;
        if (!string.IsNullOrEmpty(idElement) && IdBasedLookupCache.TryGetValue(parent, out var idLookup2))
            return idLookup2.GetValueOrDefault(idElement);
        
        if (ChildrenByNameCache.TryGetValue(parent, out var childrenList)) 
            return FindMatchingNodeFast(childrenList, nodeToFind);
        
        var sameNameElements = parent.Elements(nodeToFind.Name).ToList();
        var indexInSameNameSiblings = nodeToFind.ElementsBeforeSelf(nodeToFind.Name).Count();
        return indexInSameNameSiblings < sameNameElements.Count ? sameNameElements[indexInSameNameSiblings] : null;
    }
    
    private static XElement? FindMatchingNodeFast(List<XElement> childrenList, XElement nodeToFind) {
        using var perf = PerformanceLogger.Log();
        var sameNameElements = childrenList.Where(e => e.Name == nodeToFind.Name).ToList();
        var indexInSameNameSiblings = nodeToFind.ElementsBeforeSelf(nodeToFind.Name).Count();
        return indexInSameNameSiblings < sameNameElements.Count ? sameNameElements[indexInSameNameSiblings] : null;
    }
}