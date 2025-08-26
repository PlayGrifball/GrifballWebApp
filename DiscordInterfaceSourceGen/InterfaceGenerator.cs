using NetCord.Services;
using System.Text;
using System.Linq;
using System.Collections.Immutable;
using System.Collections.Generic;

namespace DiscordInterfaceSourceGen;

public class Program
{
    public static void Main(string[] args)
    {
        // Output directory can be passed as an argument, defaults to current directory
        var outputDir = Directory.GetCurrentDirectory();
        outputDir = new DirectoryInfo(outputDir).Parent?.Parent?.Parent?.Parent?.FullName ?? throw new Exception("Failed to find sln folder");
        outputDir = Path.Combine(outputDir, "DiscordInterfaces");

        // Find IInteractionContext interface
        var interactionContextType = typeof(IInteractionContext);

        var sb = new StringBuilder();
        var generatedTypes = new HashSet<string>();
        var interfaceQueue = new Queue<Type>();
        var interfaceBodies = new List<string>();
        var classBodies = new List<string>();
        sb.AppendLine("namespace DiscordInterfaceSourceGen;");
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Immutable;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        // Start with the root interface
        interfaceQueue.Enqueue(interactionContextType);
        while (interfaceQueue.Count > 0)
        {
            var type = interfaceQueue.Dequeue();
            GenerateInterfaceAndClass(type, generatedTypes, interfaceQueue, interfaceBodies, classBodies);
        }
        // Output all interfaces as top-level declarations
        foreach (var body in interfaceBodies)
        {
            sb.AppendLine(body);
            sb.AppendLine();
        }
        // Output all classes as top-level declarations
        foreach (var body in classBodies)
        {
            sb.AppendLine(body);
            sb.AppendLine();
        }
        // Write to file
        var outputPath = Path.Combine(outputDir, "DiscordInteractionContext.g.cs");
        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }

    private static void GenerateInterfaceAndClass(Type type, HashSet<string> generatedTypes, Queue<Type> interfaceQueue, List<string> interfaceBodies, List<string> classBodies)
    {
        var typeName = type.Name;
        // Remove leading 'I' if present
        if (typeName.StartsWith("I") && type.IsInterface && typeName.Length > 1 && char.IsUpper(typeName[1]))
            typeName = typeName.Substring(1);
        var interfaceName = $"IDiscord{typeName}";
        var className = $"Discord{typeName}";
        if (generatedTypes.Contains(interfaceName))
            return;
        generatedTypes.Add(interfaceName);
        // Interface
        var sb = new StringBuilder();
        sb.AppendLine($"public interface {interfaceName}");
        sb.AppendLine("{");
        foreach (var prop in type.GetProperties())
        {
            var memberTypeName = GetDiscordTypeName(prop.PropertyType, interfaceQueue);
            sb.AppendLine($"    {memberTypeName} {prop.Name} {{ get; }}");
        }
        sb.AppendLine("}");
        interfaceBodies.Add(sb.ToString());
        // Class
        var classSb = new StringBuilder();
        classSb.AppendLine($"public class {className} : {interfaceName}");
        classSb.AppendLine("{");
        classSb.AppendLine($"    private readonly {type.FullName} _original;");
        classSb.AppendLine($"    public {className}({type.FullName} original)");
        classSb.AppendLine("    {");
        classSb.AppendLine("        _original = original;");
        classSb.AppendLine("    }");
        foreach (var prop in type.GetProperties())
        {
            var memberTypeName = GetDiscordTypeName(prop.PropertyType, interfaceQueue);
            // Handle IReadOnlyList<T> of generated interface
            if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                var argType = prop.PropertyType.GetGenericArguments()[0];
                var argTypeName = GetDiscordTypeName(argType, interfaceQueue);
                if (argTypeName.StartsWith("IDiscord"))
                {
                    var argClassName = $"Discord{GetTypeNameWithoutLeadingI(argType)}";
                    classSb.AppendLine($"    public {memberTypeName} {prop.Name} => _original.{prop.Name}.Select(x => new {argClassName}(x)).ToList();");
                    continue;
                }
            }
            // Handle ImmutableDictionary<TKey, TValue> of generated interface
            if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(ImmutableDictionary<,>))
            {
                var valueType = prop.PropertyType.GetGenericArguments()[1];
                var valueTypeName = GetDiscordTypeName(valueType, interfaceQueue);
                if (valueTypeName.StartsWith("IDiscord"))
                {
                    var valueClassName = $"Discord{GetTypeNameWithoutLeadingI(valueType)}";
                    classSb.AppendLine($"    public {memberTypeName} {prop.Name} => _original.{prop.Name}.ToImmutableDictionary(kv => kv.Key, kv => (I{valueClassName})new {valueClassName}(kv.Value));");
                    continue;
                }
            }
            // Handle IReadOnlyDictionary<TKey, TValue> of generated interface
            if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
            {
                var valueType = prop.PropertyType.GetGenericArguments()[1];
                var valueTypeName = GetDiscordTypeName(valueType, interfaceQueue);
                if (valueTypeName.StartsWith("IDiscord"))
                {
                    var valueClassName = $"Discord{GetTypeNameWithoutLeadingI(valueType)}";
                    classSb.AppendLine($"    public {memberTypeName} {prop.Name} => _original.{prop.Name}.ToDictionary(kv => kv.Key, kv => (I{valueClassName})new {valueClassName}(kv.Value));");
                    continue;
                }
            }
            // If the property type is a generated interface, construct the corresponding class
            if (memberTypeName.StartsWith("IDiscord"))
            {
                var propClassName = $"Discord{GetTypeNameWithoutLeadingI(prop.PropertyType)}";
                classSb.AppendLine($"    public {memberTypeName} {prop.Name} => new {propClassName}(_original.{prop.Name});");
            }
            else
            {
                classSb.AppendLine($"    public {memberTypeName} {prop.Name} => _original.{prop.Name};");
            }
        }
        classSb.AppendLine("}");
        classBodies.Add(classSb.ToString());
    }

    private static string GetDiscordTypeName(Type type, Queue<Type> interfaceQueue)
    {
        // Map .NET types to C# aliases
        var typeMap = new Dictionary<string, string>
        {
            ["UInt64"] = "ulong",
            ["Int64"] = "long",
            ["UInt32"] = "uint",
            ["Int32"] = "int",
            ["UInt16"] = "ushort",
            ["Int16"] = "short",
            ["Byte"] = "byte",
            ["SByte"] = "sbyte",
            ["Boolean"] = "bool",
            ["String"] = "string",
            ["Char"] = "char",
            ["Double"] = "double",
            ["Single"] = "float",
            ["Decimal"] = "decimal",
            ["Object"] = "object"
        };
        // Handle Nullable<T> as T?
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var innerType = type.GetGenericArguments()[0];
            var innerTypeName = GetDiscordTypeName(innerType, interfaceQueue);
            return $"{innerTypeName}?";
        }
        if (typeMap.TryGetValue(type.Name, out var alias))
            return alias;
        // If type is not interface or class, use its name directly
        if (!type.IsInterface && !type.IsClass)
            return type.FullName ?? type.Name;
        // If type is from NetCord, wrap it (but not for enums)
        if (type.Namespace != null && type.Namespace.StartsWith("NetCord"))
        {
            var typeName = type.Name;
            if (typeName.StartsWith("I") && type.IsInterface && typeName.Length > 1 && char.IsUpper(typeName[1]))
                typeName = typeName.Substring(1);
            var interfaceName = $"IDiscord{typeName}";
            // Only enqueue, never generate inline
            if (type.IsInterface || type.IsClass)
            {
                if (!interfaceQueue.Contains(type))
                    interfaceQueue.Enqueue(type);
            }
            return interfaceName;
        }
        // Handle generic types (e.g., IReadOnlyList<T>, etc.)
        if (type.IsGenericType)
        {
            var genericTypeName = type.Name.Split('`')[0];
            var genericArgs = type.GetGenericArguments();
            var args = string.Join(", ", genericArgs.Select(t => GetDiscordTypeName(t, interfaceQueue)));
            return $"{genericTypeName}<{args}>";
        }
        // Otherwise, use the type name as-is
        return type.Name;
    }

    private static string GetTypeNameWithoutLeadingI(Type type)
    {
        var typeName = type.Name;
        if (typeName.StartsWith("I") && type.IsInterface && typeName.Length > 1 && char.IsUpper(typeName[1]))
            typeName = typeName.Substring(1);
        return typeName;
    }
}
