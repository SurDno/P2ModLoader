using Mono.Cecil;
using P2ModLoader.Logging;

namespace P2ModLoader.Patching.Assembly.ILCloning;

// TODO: OnFire method cloning? Is it even needed in C#? Test.
public static class EventCloner {
    public static EventDefinition CloneEvent(EventDefinition src, ModuleDefinition module, TypeDefinition type) {
        var newEvent = new EventDefinition(src.Name, src.Attributes, module.ImportReference(src.EventType));
            
        if (src.AddMethod != null) {
            var clonedAdd = MethodCloner.CloneMethod(src.AddMethod, module);
            type.Methods.Add(clonedAdd);
            newEvent.AddMethod = clonedAdd;
        }

        if (src.RemoveMethod != null) {
            var clonedRemove = MethodCloner.CloneMethod(src.RemoveMethod, module);
            type.Methods.Add(clonedRemove);
            newEvent.RemoveMethod = clonedRemove;
        }
            
        AttributesCloner.CloneAttributes(src, newEvent, module);
        return newEvent;
    }

    public static void UpdateEvent(EventDefinition src, EventDefinition updatedEvent, ModuleDefinition module, TypeDefinition type) {
        src.Attributes = updatedEvent.Attributes;
        src.EventType = module.ImportReference(updatedEvent.EventType);
        src.CustomAttributes.Clear();
        AttributesCloner.CloneAttributes(updatedEvent, src, module);

        src.AddMethod = SyncAccessor(src.AddMethod, updatedEvent.AddMethod, module, type);
        src.RemoveMethod = SyncAccessor(src.RemoveMethod, updatedEvent.RemoveMethod, module, type);
    }

    private static MethodDefinition? SyncAccessor(MethodDefinition? existing, MethodDefinition? updated,
        ModuleDefinition module, TypeDefinition parentType) {
        if (updated != null) {
            if (existing != null) {
                existing.Attributes = updated.Attributes;
                existing.CustomAttributes.Clear();
                AttributesCloner.CloneAttributes(updated, existing, module);
                MethodCloner.ReplaceMethodBody(existing, updated, module);
                Logger.Log(LogLevel.Info, $"Replaced {existing.Name}");
            } else {
                var clone = MethodCloner.CloneMethod(updated, module);
                parentType.Methods.Add(clone);
                Logger.Log(LogLevel.Info, $"Added {clone.Name}");
                existing = clone;
            }
        } else if (existing != null) {
            parentType.Methods.Remove(existing);
            Logger.Log(LogLevel.Info, $"Removed {existing.Name}");
            existing = null;
        }

        return existing;
    }
}