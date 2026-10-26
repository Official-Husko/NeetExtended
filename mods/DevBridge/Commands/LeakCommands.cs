using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using DevBridge.Server;
using Newtonsoft.Json.Linq;

namespace DevBridge.Commands;

/// <summary>
/// Finds what keeps destroyed Unity objects (and everything they reference) alive: walks static
/// fields, ScriptableObject assets and objects kept across scenes, and reports the ones that lead
/// to destroyed objects. A destroyed scene object that's still referenced keeps its textures and meshes in
/// memory, since Unity's asset cleanup only frees what nothing points at.
/// </summary>
internal static class LeakCommands
{
    private const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags AllStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [BridgeCommand("leaks.find", "What still leads to destroyed Unity objects: static fields, ScriptableObject assets and objects kept across scenes. Path and destroyed objects per root.",
        Params = new[] { "depth?: how many references deep to follow, default 6", "type?: only count destroyed objects of this type name",
            "all?: also walk Unity's and .NET's own statics (slower)" })]
    private static object Find(JObject p)
    {
        int maxDepth = p.Int("depth", 6);
        string onlyType = p.Str("type", null);
        var seen = new HashSet<object>(ByReference.Instance);
        var results = new JArray();
        int visited = 0;

        foreach (var (name, value) in Roots(p.Bool("all", false)))
        {
            var found = new Dictionary<string, int>();
            string example = null;
            Walk(value, name, 0, maxDepth, onlyType, seen, found, ref example, ref visited);
            if (found.Count > 0)
                results.Add(new JObject { ["root"] = name, ["destroyed"] = JObject.FromObject(found), ["example"] = example });
        }
        return new JObject { ["objectsVisited"] = visited, ["roots"] = results };
    }

    private static IEnumerable<(string name, object value)> Roots(bool allAssemblies)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => allAssemblies || Interesting(a)))
        {
            foreach (var type in Types(assembly))
            {
                if (type.IsGenericTypeDefinition)
                    continue;
                foreach (var field in type.GetFields(AllStatic))
                {
                    if (field.IsLiteral || field.FieldType.IsPrimitive || field.FieldType == typeof(string))
                        continue;
                    object value;
                    try { value = field.GetValue(null); }
                    catch { continue; }
                    if (value != null)
                        yield return ($"static {type.FullName}.{field.Name}", value);
                }
            }
        }

        // Assets outlive scenes, so a field on one that points into a scene keeps that scene's objects.
        foreach (var asset in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.ScriptableObject>())
            yield return ($"asset {asset.GetType().Name} '{asset.name}'", asset);

        foreach (var behaviour in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.MonoBehaviour>())
        {
            if (behaviour != null && behaviour.gameObject.scene.name == "DontDestroyOnLoad")
                yield return ($"kept object {behaviour.gameObject.name}/{behaviour.GetType().Name}", behaviour);
        }
    }

    private static void Walk(object value, string path, int depth, int maxDepth, string onlyType,
        HashSet<object> seen, Dictionary<string, int> found, ref string example, ref int visited)
    {
        if (value == null || depth > maxDepth || visited > 3_000_000)
            return;
        var type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || value is string || value is Type || value is MemberInfo || value is Assembly)
            return;
        if (!type.IsValueType && !seen.Add(value))
            return;
        visited++;

        if (value is UnityEngine.Object unityObject)
        {
            if (unityObject == null)
            {
                string name = type.Name;
                if (onlyType == null || string.Equals(name, onlyType, StringComparison.OrdinalIgnoreCase))
                {
                    found[name] = found.TryGetValue(name, out int n) ? n + 1 : 1;
                    example ??= $"{path} ({name})";
                }
            }
            // Live Unity objects are walked too: a live object can point at a destroyed one.
        }

        if (value is Delegate del)
        {
            foreach (var single in del.GetInvocationList())
                Walk(single.Target, path + $" -> {single.Method.DeclaringType?.Name}.{single.Method.Name}", depth + 1, maxDepth, onlyType, seen, found, ref example, ref visited);
            return;
        }
        if (value is IEnumerable sequence && !(value is UnityEngine.Transform))
        {
            int index = 0;
            try
            {
                foreach (var item in sequence)
                {
                    if (item == null)
                        continue;
                    var itemType = item.GetType();
                    if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                    {
                        Walk(itemType.GetProperty("Key").GetValue(item), $"{path}[key]", depth + 1, maxDepth, onlyType, seen, found, ref example, ref visited);
                        Walk(itemType.GetProperty("Value").GetValue(item), $"{path}[{index}]", depth + 1, maxDepth, onlyType, seen, found, ref example, ref visited);
                    }
                    else
                    {
                        Walk(item, $"{path}[{index}]", depth + 1, maxDepth, onlyType, seen, found, ref example, ref visited);
                    }
                    if (++index > 50_000)
                        break;
                }
            }
            catch
            {
                // Collections that can't be enumerated here (changed meanwhile, native-backed) are skipped.
            }
            if (type.IsArray || value is IList || value is IDictionary)
                return;
        }

        for (var t = type; t != null && t != typeof(object) && t != typeof(UnityEngine.Object); t = t.BaseType)
        {
            foreach (var field in t.GetFields(AllInstance | BindingFlags.DeclaredOnly))
            {
                if (field.FieldType.IsPrimitive || field.FieldType == typeof(string) || field.FieldType.IsPointer)
                    continue;
                object child;
                try { child = field.GetValue(value); }
                catch { continue; }
                Walk(child, $"{path}.{field.Name}", depth + 1, maxDepth, onlyType, seen, found, ref example, ref visited);
            }
        }
    }

    private static bool Interesting(Assembly assembly)
    {
        string name = assembly.GetName().Name;
        return name.StartsWith("Assembly-CSharp", StringComparison.Ordinal)
               || name.StartsWith("NeetModKit", StringComparison.Ordinal)
               || name.StartsWith("NeetLoader", StringComparison.Ordinal)
               || (!name.StartsWith("Unity", StringComparison.Ordinal) && !name.StartsWith("System", StringComparison.Ordinal)
                   && !name.StartsWith("Mono", StringComparison.Ordinal) && name != "mscorlib" && !name.StartsWith("0Harmony", StringComparison.Ordinal)
                   && !name.StartsWith("Newtonsoft", StringComparison.Ordinal) && !name.StartsWith("Ink", StringComparison.Ordinal));
    }

    private static IEnumerable<Type> Types(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
    }

    private sealed class ByReference : IEqualityComparer<object>
    {
        public static readonly ByReference Instance = new ByReference();

        public new bool Equals(object x, object y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
