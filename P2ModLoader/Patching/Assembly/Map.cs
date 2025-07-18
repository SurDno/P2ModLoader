using System.Collections;

namespace P2ModLoader.Patching.Assembly;

public class Map<T> : IEnumerable<KeyValuePair<T, T>> where T : notnull {
    private readonly Dictionary<T, T> _inner = new();
    
    public T this[T key] {
        get => _inner[key];
        set => _inner[key] = value;
    }
    
    public bool TryGetValue(T key, out T? value) => _inner.TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<T, T>> GetEnumerator() => _inner.GetEnumerator();

    public T GetValueOrDefault(T key, T defaultValue) => _inner.GetValueOrDefault(key, defaultValue);
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
