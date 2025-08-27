using NetCord.Services;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;

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
        sb.AppendLine("using DiscordInterfaceSourceGen;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Linq.Expressions;");
        sb.AppendLine("using System.Collections.Immutable;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Text.Json.Serialization.Metadata;");
        sb.AppendLine();
        sb.AppendLine("namespace DiscordInterfaceSourceGen;");
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
        string typeName = type.Name;
        if (typeName.EndsWith("&") || typeName.EndsWith("[]"))
        {
            return;
        }
        if (typeName.Contains("MessageOptions"))
        {
            var b = 1;
        }
        if (type.IsGenericType)
        {
            typeName = type.Name.Split('`')[0];
            var types = type.GetGenericArguments();
            var final = string.Join(", ", types.Select(t => GetDiscordTypeName(t, interfaceQueue))); // nothing should really be enqueued here...
            typeName = $"{typeName}<{final}>";
        }
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
        // Add Original property to interface
        sb.AppendLine($"    {type.FullName} Original {{ get; }}");
        foreach (var prop in type.GetProperties())
        {
            var memberTypeName = GetDiscordTypeName(prop.PropertyType, interfaceQueue);
            sb.AppendLine($"    {memberTypeName} {prop.Name} {{ get; }}");
        }
        AddMethods(type, interfaceQueue, sb, forInterface: true);
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
        // Add Original property to class
        classSb.AppendLine($"    public {type.FullName} Original => _original;");
        foreach (var prop in type.GetProperties())
        {
            var memberTypeName = GetDiscordTypeName(prop.PropertyType, interfaceQueue);
            // Handle IEnumerable<T> of generated interface
            if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                var argType = prop.PropertyType.GetGenericArguments()[0];
                var argTypeName = GetDiscordTypeName(argType, interfaceQueue);
                if (argTypeName.StartsWith("IDiscord"))
                {
                    var argClassName = $"Discord{GetTypeNameWithoutLeadingI(argType)}";
                    classSb.AppendLine($"    public {memberTypeName} {prop.Name} => _original.{prop.Name}.Select(x => new {argClassName}(x));");
                    continue;
                }
            }
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
        AddMethods(type, interfaceQueue, classSb, forInterface: false);
        classSb.AppendLine("}");
        classBodies.Add(classSb.ToString());
    }

    private static void AddMethods(Type type, Queue<Type> interfaceQueue, StringBuilder sb, bool forInterface)
    {
        // Add methods
        //var foo = type.GetMethods().Where(m => m.Name.Contains("GetAsync")).ToList();
        //var ff = foo.Select(x => new { Type = x, FOO = x.DeclaringType == type });
        string[] ignored = ["GetType", "ToString", "Equals", "GetHashCode", "TryFormat", "<Clone>$", "Clone"];
        var methods = type.GetMethods().Where(m => m.IsPublic && !m.IsSpecialName && !m.IsStatic && !ignored.Contains(m.Name)).ToArray();
        foreach (var method in methods)
        {
            if (method.Name.Contains("Clone"))
            {
                var b = 1;
            }
            if (method.DeclaringType != type && methods.Any(m => MethodsAreEquivalent(m, method) && m.DeclaringType == type))
                continue; // Skip inherited methods that are overridden
            var returnType = GetDiscordTypeName(method.ReturnType, interfaceQueue);
            var parameters = method.GetParameters();
            var paramStrings = new List<string>();
            var argNames = new List<string>();
            foreach (var param in parameters)
            {
                var paramType = GetDiscordTypeName(param.ParameterType, interfaceQueue);
                var paramStr = $"{paramType} {param.Name}";
                if (param.HasDefaultValue)
                {
                    if (param.DefaultValue == null && param.ParameterType.IsValueType) // Value types are default not null.
                    {
                        paramStr += " = default";
                    }
                    else if (param.DefaultValue == null) // If nullable then make sure its marked as such
                    {
                        if (paramType.EndsWith('?')) // TODO: Why do some parameters already have the ? in their type?
                        {
                            paramStr = $"{paramType} {param.Name} = null";
                        }
                        else
                        {
                            paramStr = $"{paramType}? {param.Name} = null";
                        }
                    }
                    else if (param.DefaultValue?.ToString() == "")
                        paramStr += " = \"\"";
                    else if (param.DefaultValue is bool)
                        paramStr += $" = {((bool)param.DefaultValue ? "true" : "false")}";
                    else if (param.DefaultValue is string)
                        paramStr += $" = \"{param.DefaultValue}\"";
                    else if (param.DefaultValue?.GetType().IsValueType == true)
                        paramStr += $" = {param.DefaultValue}";
                    else if (param.DefaultValue?.ToString() == "default")
                        paramStr += " = default";
                    else
                        paramStr += $" = {param.DefaultValue}";
                }
                paramStrings.Add(paramStr);
                if (!forInterface)
                {
                    if (param.ParameterType.Assembly.FullName.StartsWith("NetCord"))
                    {
                        argNames.Add($"{param.Name}.Original");
                    }
                    else
                    {
                        argNames.Add(param.Name);
                    }
                }
            }
            if (forInterface)
            {
                sb.AppendLine($"    {returnType} {method.Name}({string.Join(", ", paramStrings)});");
            }
            else
            {
                var f = method.Name is "ModifyFollowupMessageAsync";
                if (f)
                {
                    var b = 1;
                }
                if (method.ReturnType.Name == "Task`1")
                {
                    var taskReturnType = method.ReturnType.GetGenericArguments()[0];
                    var discordTaskReturnType = GetDiscordTypeName(taskReturnType, interfaceQueue);
                    var trueReturnType = taskReturnType; // Is it generic?
                    // Handle IEnumerable<T> of generated interface
                    if (trueReturnType.IsGenericType && trueReturnType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    {
                        var argType = trueReturnType.GetGenericArguments()[0];
                        var argTypeName = GetDiscordTypeName(argType, interfaceQueue);
                        if (argTypeName.StartsWith("IDiscord"))
                        {
                            var argClassName = $"Discord{GetTypeNameWithoutLeadingI(argType)}";
                            sb.AppendLine($"    public async {returnType} {method.Name}({string.Join(", ", paramStrings)}) => (await _original.{method.Name}({string.Join(", ", argNames)})).Select(x => new {argClassName}(x));");
                            continue;
                        }
                    }
                    // Handle IReadOnlyList<T> of generated interface
                    if (trueReturnType.IsGenericType && trueReturnType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
                    {
                        var argType = trueReturnType.GetGenericArguments()[0];
                        var argTypeName = GetDiscordTypeName(argType, interfaceQueue);
                        if (argTypeName.StartsWith("IDiscord"))
                        {
                            var argClassName = $"Discord{GetTypeNameWithoutLeadingI(argType)}";
                            sb.AppendLine($"    public async {returnType} {method.Name}({string.Join(", ", paramStrings)}) => (await _original.{method.Name}({string.Join(", ", argNames)})).Select(x => new {argClassName}(x)).ToList();");
                            continue;
                        }
                    }
                    // Handle ImmutableDictionary<TKey, TValue> of generated interface
                    if (trueReturnType.IsGenericType && trueReturnType.GetGenericTypeDefinition() == typeof(ImmutableDictionary<,>))
                    {
                        var valueType = trueReturnType.GetGenericArguments()[1];
                        var valueTypeName = GetDiscordTypeName(valueType, interfaceQueue);
                        if (valueTypeName.StartsWith("IDiscord"))
                        {
                            var valueClassName = $"Discord{GetTypeNameWithoutLeadingI(valueType)}";
                            sb.AppendLine($"    public async {returnType} {method.Name}({string.Join(", ", paramStrings)}) => (await _original.{method.Name}({string.Join(", ", argNames)})).ToImmutableDictionary(kv => kv.Key, kv => (I{valueClassName})new {valueClassName}(kv.Value));");
                            continue;
                        }
                    }
                    // Handle IReadOnlyDictionary<TKey, TValue> of generated interface
                    if (trueReturnType.IsGenericType && trueReturnType.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
                    {
                        var valueType = trueReturnType.GetGenericArguments()[1];
                        var valueTypeName = GetDiscordTypeName(valueType, interfaceQueue);
                        if (valueTypeName.StartsWith("IDiscord"))
                        {
                            var valueClassName = $"Discord{GetTypeNameWithoutLeadingI(valueType)}";
                            sb.AppendLine($"    public async {returnType} {method.Name}({string.Join(", ", paramStrings)}) => (await _original.{method.Name}({string.Join(", ", argNames)})).ToDictionary(kv => kv.Key, kv => (I{valueClassName})new {valueClassName}(kv.Value));");
                            continue;
                        }
                    }

                    if (discordTaskReturnType.StartsWith("IDiscord"))
                    {
                        var returnClassName = discordTaskReturnType.TrimStart('I');
                        sb.AppendLine($"    public async {returnType} {method.Name}({string.Join(", ", paramStrings)}) => new {returnClassName}(await _original.{method.Name}({string.Join(", ", argNames)}));");
                    }
                    else
                    {
                        sb.AppendLine($"    public {returnType} {method.Name}({string.Join(", ", paramStrings)}) => _original.{method.Name}({string.Join(", ", argNames)});");
                    }
                }
                else
                {
                    // is method.ReturnType generic type?
                    if (returnType.StartsWith("IDiscord"))
                    {
                        var returnClassName = $"Discord{GetTypeNameWithoutLeadingI(method.ReturnType)}";
                        sb.AppendLine($"    public {returnType} {method.Name}({string.Join(", ", paramStrings)}) => new {returnClassName}(_original.{method.Name}({string.Join(", ", argNames)}));");
                    }
                    else
                    {
                        sb.AppendLine($"    public {returnType} {method.Name}({string.Join(", ", paramStrings)}) => _original.{method.Name}({string.Join(", ", argNames)});");
                    }
                }
            }
        }
    }

    private static string GetDiscordTypeName(Type type, Queue<Type> interfaceQueue)
    {
        if (type.Name.Contains("PaginationProperties"))
        {
            var b = 1;
        }
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
            return $"{innerTypeName}?"; // TODO: Should I remove this ? here
        }
        if (type.IsArray)
        {
            var innerType = type.GetElementType();
            var innerTypeName = GetDiscordTypeName(innerType, interfaceQueue);
            return $"{innerTypeName}[]"; // TODO: Should I remove this ? here
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
            if (type.IsGenericType)
            {
                // Handle generic
                var genericTypeName = type.Name.Split('`')[0];
                var types = type.GetGenericArguments();
                var final = string.Join(", ", types.Select(t => GetDiscordTypeName(t, interfaceQueue)));
                var superName = $"IDiscord{genericTypeName}<{final}>";
                return superName;
            }
            else
                return interfaceName;
        }
        // Handle generic types (e.g., IReadOnlyList<T>, etc.)
        if (type.IsGenericType)
        {
            if (type.Name.Contains("PaginationProperties"))
            {
                var b = 1;
            }
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

    private static bool MethodsAreEquivalent(MethodInfo a, MethodInfo b)
    {
        if (a.Name != b.Name) return false;
        var aParams = a.GetParameters();
        var bParams = b.GetParameters();
        if (aParams.Length != bParams.Length) return false;
        for (int i = 0; i < aParams.Length; i++)
        {
            if (aParams[i].ParameterType != bParams[i].ParameterType) return false;
        }
        return true;
    }
}
