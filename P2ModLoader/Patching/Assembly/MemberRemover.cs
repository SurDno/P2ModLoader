using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mono.Cecil;
using P2ModLoader.Logging;

namespace P2ModLoader.Patching.Assembly;

public static class MemberRemover {
    public static bool IsMarkedForRemoval(MemberDeclarationSyntax member) {
        return member.AttributeLists.SelectMany(al => al.Attributes).Any(a => {
            var name = a.Name.ToString();
            return name is "P2ModLoader.Remove" or "P2ModLoader.RemoveAttribute";
        });
    }

    public static void RemoveMarkedMembers(
        TypeDefinition originalType,
        string fullTypeName,
        List<MethodDeclarationSyntax> methodsToRemove,
        List<PropertyDeclarationSyntax> propertiesToRemove,
        List<FieldDeclarationSyntax> fieldsToRemove,
        List<EventDeclarationSyntax> eventsToRemove,
        List<EventFieldDeclarationSyntax> eventFieldsToRemove) {
        
        RemoveMethods(originalType, fullTypeName, methodsToRemove);
        RemoveProperties(originalType, fullTypeName, propertiesToRemove);
        RemoveFields(originalType, fullTypeName, fieldsToRemove);
        RemoveEvents(originalType, fullTypeName, eventsToRemove);
        RemoveEventFields(originalType, fullTypeName, eventFieldsToRemove);
    }

    private static void RemoveMethods(TypeDefinition originalType, string fullTypeName, 
        List<MethodDeclarationSyntax> methodsToRemove) {
        foreach (var methodSyntax in methodsToRemove) {
            var methodName = methodSyntax.Identifier.Text;
            var paramTypes = methodSyntax.ParameterList.Parameters
                .Select(p => p.Type.ToString()).ToList();

            var methodToRemove = originalType.Methods.FirstOrDefault(m =>
                m.Name == methodName &&
                m.Parameters.Count == paramTypes.Count &&
                m.Parameters.Select(p => p.ParameterType.Name).SequenceEqual(
                    paramTypes.Select(pt => pt.Split('.').Last())
                ));

            if (methodToRemove != null) {
                originalType.Methods.Remove(methodToRemove);
                Logger.Log(LogLevel.Info, $"Removed method {methodName} from type {fullTypeName}");
            } else {
                Logger.Log(LogLevel.Warning, $"Could not find method {methodName} to remove from type {fullTypeName}");
            }
        }
    }

    private static void RemoveProperties(TypeDefinition originalType, string fullTypeName,
        List<PropertyDeclarationSyntax> propertiesToRemove) {
        foreach (var propSyntax in propertiesToRemove) {
            var propName = propSyntax.Identifier.Text;
            var propToRemove = originalType.Properties.FirstOrDefault(p => p.Name == propName);

            if (propToRemove != null) {
                if (propToRemove.GetMethod != null)
                    originalType.Methods.Remove(propToRemove.GetMethod);
                if (propToRemove.SetMethod != null)
                    originalType.Methods.Remove(propToRemove.SetMethod);

                var backingFieldName = $"<{propName}>k__BackingField";
                var backingField = originalType.Fields.FirstOrDefault(f => f.Name == backingFieldName);
                if (backingField != null)
                    originalType.Fields.Remove(backingField);

                originalType.Properties.Remove(propToRemove);
                Logger.Log(LogLevel.Info, $"Removed property {propName} from type {fullTypeName}");
            } else {
                Logger.Log(LogLevel.Warning, $"Could not find property {propName} to remove from type {fullTypeName}");
            }
        }
    }

    private static void RemoveFields(TypeDefinition originalType, string fullTypeName,
        List<FieldDeclarationSyntax> fieldsToRemove) {
        foreach (var fieldSyntax in fieldsToRemove) {
            foreach (var variable in fieldSyntax.Declaration.Variables) {
                var fieldName = variable.Identifier.Text;
                var fieldToRemove = originalType.Fields.FirstOrDefault(f => f.Name == fieldName);

                if (fieldToRemove != null) {
                    originalType.Fields.Remove(fieldToRemove);
                    Logger.Log(LogLevel.Info, $"Removed field {fieldName} from type {fullTypeName}");
                } else {
                    Logger.Log(LogLevel.Warning, $"Could not find field {fieldName} to remove from type {fullTypeName}");
                }
            }
        }
    }

    private static void RemoveEvents(TypeDefinition originalType, string fullTypeName,
        List<EventDeclarationSyntax> eventsToRemove) {
        foreach (var eventSyntax in eventsToRemove) {
            var eventName = eventSyntax.Identifier.Text;
            var eventToRemove = originalType.Events.FirstOrDefault(e => e.Name == eventName);

            if (eventToRemove != null) {
                if (eventToRemove.AddMethod != null)
                    originalType.Methods.Remove(eventToRemove.AddMethod);
                if (eventToRemove.RemoveMethod != null)
                    originalType.Methods.Remove(eventToRemove.RemoveMethod);

                originalType.Events.Remove(eventToRemove);
                Logger.Log(LogLevel.Info, $"Removed event {eventName} from type {fullTypeName}");
            } else {
                Logger.Log(LogLevel.Warning, $"Could not find event {eventName} to remove from type {fullTypeName}");
            }
        }
    }

    private static void RemoveEventFields(TypeDefinition originalType, string fullTypeName,
        List<EventFieldDeclarationSyntax> eventFieldsToRemove) {
        foreach (var eventFieldSyntax in eventFieldsToRemove) {
            foreach (var variable in eventFieldSyntax.Declaration.Variables) {
                var eventName = variable.Identifier.Text;
                var eventToRemove = originalType.Events.FirstOrDefault(e => e.Name == eventName);

                if (eventToRemove != null) {
                    if (eventToRemove.AddMethod != null)
                        originalType.Methods.Remove(eventToRemove.AddMethod);
                    if (eventToRemove.RemoveMethod != null)
                        originalType.Methods.Remove(eventToRemove.RemoveMethod);

                    var backingField = originalType.Fields.FirstOrDefault(f => f.Name == eventName);
                    if (backingField != null)
                        originalType.Fields.Remove(backingField);

                    originalType.Events.Remove(eventToRemove);
                    Logger.Log(LogLevel.Info, $"Removed event field {eventName} from type {fullTypeName}");
                } else {
                    Logger.Log(LogLevel.Warning, $"Could not find event field {eventName} to remove from type {fullTypeName}");
                }
            }
        }
    }
    
    public static bool RemoveType(AssemblyDefinition assembly, string fullTypeName) {
        var typeToRemove = assembly.MainModule.GetType(fullTypeName);
        if (typeToRemove != null) {
            assembly.MainModule.Types.Remove(typeToRemove);
            Logger.Log(LogLevel.Info, $"Removed type {fullTypeName}");
            return true;
        }
        
        Logger.Log(LogLevel.Warning, $"Could not find type {fullTypeName} to remove");
        return false;
    }
}