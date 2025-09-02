using NetCord.Services;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
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
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Linq.Expressions;");
        sb.AppendLine("using System.Collections.Immutable;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Serialization.Metadata;");
        sb.AppendLine();
        sb.AppendLine("namespace DiscordInterface.Generated;");
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
        var outputPath = Path.Combine(outputDir, "DiscordInteractionContextGen.cs");
        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }

    private static void GenerateInterfaceAndClass(Type type, HashSet<string> generatedTypes, Queue<Type> interfaceQueue, List<string> interfaceBodies, List<string> classBodies)
    {
        string typeName = type.Name;
        if (typeName.EndsWith("&") || typeName.EndsWith("[]"))
        {
            return;
        }
        if (type.IsGenericType)
        {
            type = type.GetGenericTypeDefinition();
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

        var genericConstraint = " ";
        if (type.IsGenericType)
        {
            genericConstraint += string.Join(" ", type.GetGenericArguments().Where(x => x.BaseType != null && x.BaseType != typeof(object))
                .Select(t =>
                {
                    var typ = t.BaseType?.FullName ?? throw new Exception("Full name null");
                    if (typ == "System.ValueType")
                        return $"where {t.Name} : struct";
                    return $"where {t.Name} : {t.BaseType.FullName}";
                }));
        }

        // Interface
        var sb = new StringBuilder();
        sb.AppendLine($"public interface {interfaceName} {genericConstraint}");
        sb.AppendLine("{");
        // Add Original property to interface
        if (type.IsGenericType)
        {
            var genericArgs = string.Join(", ", type.GetGenericArguments().Select(t => t.Name));
            var fullTypeName = $"{type.FullName?.Split('`')[0]}<{genericArgs}>";
            sb.AppendLine($"    {fullTypeName} Original {{ get; }}");
        }
        else
        {
            sb.AppendLine($"    {type.FullName} Original {{ get; }}");
        }
        foreach (var prop in type.GetProperties())
        {
            if (prop.Name is "Emojis" && type.Name == "JsonGuild")
            {
                var f = 1;
            }
            if (prop.CustomAttributes.Any(a => a.AttributeType.FullName == "System.ObsoleteAttribute"))
                continue; // Skip obsolete properties
            var memberTypeName = GetDiscordTypeName(prop.PropertyType, interfaceQueue);
            var isStatic = prop.GetMethod?.IsStatic is true;
            var staticModifier = isStatic ? "static " : "";
            if (isStatic)
            {
                var withoutI = memberTypeName.Substring(1);
                sb.AppendLine($"    {staticModifier}{memberTypeName} {prop.Name} => new {withoutI}({type.FullName}.{prop.Name});");
            }
            else
            {
                string nullableModifier = NullabilityModifier(prop);
                if (memberTypeName.EndsWith("?"))
                    nullableModifier = ""; // Already handled in type name
                var hasSetter = prop.SetMethod != null && prop.SetMethod.IsPublic;
                var accessor = hasSetter ? "{ get; set; }" : "{ get; }";
                sb.AppendLine($"    {staticModifier}{memberTypeName}{nullableModifier} {prop.Name} {accessor}");
            }
        }
        AddMethods(type, interfaceQueue, sb, forInterface: true);
        sb.AppendLine("}");
        interfaceBodies.Add(sb.ToString());
        // Class
        var classSb = new StringBuilder();
        classSb.AppendLine($"public class {className} : {interfaceName}{genericConstraint}");
        classSb.AppendLine("{");
        if (type.IsGenericType)
        {
            var genericArgs = string.Join(", ", type.GetGenericArguments().Select(t => t.Name));
            var fullTypeName = $"{type.FullName?.Split('`')[0]}<{genericArgs}>";

            classSb.AppendLine($"    private readonly {fullTypeName} _original;");
            var actualClassName = className.Split('<')[0];
            classSb.AppendLine($"    public {actualClassName}({fullTypeName} original)");
            classSb.AppendLine("    {");
            classSb.AppendLine("        _original = original;");
            classSb.AppendLine("    }");
            // Add Original property to class
            classSb.AppendLine($"    public {fullTypeName} Original => _original;");
        }
        else
        {
            classSb.AppendLine($"    private readonly {type.FullName} _original;");
            classSb.AppendLine($"    public {className}({type.FullName} original)");
            classSb.AppendLine("    {");
            classSb.AppendLine("        _original = original;");
            classSb.AppendLine("    }");
            // Add Original property to class
            classSb.AppendLine($"    public {type.FullName} Original => _original;");
        }
        
        foreach (var prop in type.GetProperties())
        {
            if (prop.CustomAttributes.Any(a => a.AttributeType.FullName == "System.ObsoleteAttribute"))
                continue; // Skip obsolete properties
            string nullableModifier = NullabilityModifier(prop);
            var isNullable = nullableModifier is "?";
            var hasSetter = prop.SetMethod != null && prop.SetMethod.IsPublic;
            var nullCheck = isNullable ? $"_original.{prop.Name} is null ? null : " : "";

            var memberTypeName = GetDiscordTypeName(prop.PropertyType, interfaceQueue);
            if (memberTypeName.EndsWith("?"))
                nullableModifier = ""; // Already handled in type name

            if (prop.GetMethod?.IsStatic is true)
            {
                //continue;
                var withoutI = memberTypeName.Substring(1);
                classSb.AppendLine($"    public static {memberTypeName}{nullableModifier} {prop.Name} => {nullCheck}new {withoutI}({type.FullName}.{prop.Name});");
                continue;
            }

            // Handle IEnumerable<T> of generated interface
            if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                var argType = prop.PropertyType.GetGenericArguments()[0];
                var argTypeName = GetDiscordTypeName(argType, interfaceQueue);
                if (argTypeName.StartsWith("IDiscord"))
                {
                    var argClassName = $"Discord{GetTypeNameWithoutLeadingI(argType, false)}";
                    if (!hasSetter)
                    {
                        classSb.AppendLine($"    public {memberTypeName}{nullableModifier} {prop.Name} => {nullCheck}_original.{prop.Name}.Select(x => new {argClassName}(x));");
                    }
                    else
                    {
                        classSb.AppendLine($"    public {memberTypeName}{nullableModifier} {prop.Name} {{ get {{ return {nullCheck}_original.{prop.Name}.Select(x => new {argClassName}(x)); }} set {{ _original.{prop.Name} = value{nullableModifier}.Select(x => x.Original); }} }}");
                    }
                    continue;
                }
            }
            // Handle ImmutableArray<T> of generated interface
            if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(ImmutableArray<>))
            {
                var argType = prop.PropertyType.GetGenericArguments()[0];
                var argTypeName = GetDiscordTypeName(argType, interfaceQueue);
                if (argTypeName.StartsWith("IDiscord"))
                {
                    var argClassName = $"Discord{GetTypeNameWithoutLeadingI(argType, false)}";
                    if (!hasSetter)
                    {
                        // Not sure why cast to as I{argClassName} is needed here, but it is
                        classSb.AppendLine($"    public {memberTypeName}{nullableModifier} {prop.Name} => {nullCheck}_original.{prop.Name}.Select(x => new {argClassName}(x) as I{argClassName}).ToImmutableArray();");
                    }
                    else
                    {
                        classSb.AppendLine($"    public {memberTypeName}{nullableModifier} {prop.Name} {{ get {{ return {nullCheck}_original.{prop.Name}.Select(x => new {argClassName}(x) as I{argClassName}).ToImmutableArray(); }} set {{ _original.{prop.Name} = value{nullableModifier}.Select(x => x.Original).ToImmutableArray(); }} }}");
                    }
                    continue;
                }
            }
            // Handle array of generated interface
            if (prop.PropertyType.IsArray)
            {
                var argType = prop.PropertyType.GetElementType() ?? throw new Exception("Array is missing element type");
                var argTypeName = GetDiscordTypeName(argType, interfaceQueue);
                if (argTypeName.StartsWith("IDiscord"))
                {
                    var argClassName = $"Discord{GetTypeNameWithoutLeadingI(argType, false)}";
                    if (!hasSetter)
                    {
                        classSb.AppendLine($"    public {memberTypeName}{nullableModifier} {prop.Name} => {nullCheck}_original.{prop.Name}.Select(x => new {argClassName}(x)).ToArray();");
                    }
                    else
                    {
                        classSb.AppendLine($"    public {memberTypeName}{nullableModifier} {prop.Name} {{ get {{ return {nullCheck}_original.{prop.Name}.Select(x => new {argClassName}(x)).ToArray(); }} set {{ _original.{prop.Name} = value{nullableModifier}.Select(x => x.Original).ToArray(); }} }}");
                    }
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
                    var argClassName = $"Discord{GetTypeNameWithoutLeadingI(argType, false)}";
                    classSb.AppendLine($"    public {memberTypeName}{nullableModifier} {prop.Name} => {nullCheck}_original.{prop.Name}.Select(x => new {argClassName}(x)).ToList();");
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
                    var valueClassName = $"Discord{GetTypeNameWithoutLeadingI(valueType, false)}";
                    if (!hasSetter)
                    {
                        classSb.AppendLine($"    public {memberTypeName}{nullableModifier} {prop.Name} => {nullCheck}_original.{prop.Name}.ToImmutableDictionary(kv => kv.Key, kv => (I{valueClassName})new {valueClassName}(kv.Value));");
                    }
                    else
                    {
                        classSb.AppendLine($"    public {memberTypeName}{nullableModifier} {prop.Name} {{ get {{ return {nullCheck}_original.{prop.Name}.ToImmutableDictionary(kv => kv.Key, kv => (I{valueClassName})new {valueClassName}(kv.Value)); }} set {{ _original.{prop.Name} = value{nullableModifier}.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.Original); }} }}");
                    }
                    
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
                    var valueClassName = $"Discord{GetTypeNameWithoutLeadingI(valueType, false)}";
                    if (!hasSetter)
                    {
                        classSb.AppendLine($"    public {memberTypeName}{nullableModifier} {prop.Name} => {nullCheck}_original.{prop.Name}.ToDictionary(kv => kv.Key, kv => (I{valueClassName})new {valueClassName}(kv.Value));");
                    }
                    else
                    {
                        classSb.AppendLine($"    public {memberTypeName}{nullableModifier} {prop.Name} {{ get {{ return {nullCheck}_original.{prop.Name}.ToDictionary(kv => kv.Key, kv => (I{valueClassName})new {valueClassName}(kv.Value)); }} set {{ _original.{prop.Name} = value{nullableModifier}.ToDictionary(kv => kv.Key, kv => kv.Value.Original); }} }}");
                    }
                    
                    continue;
                }
            }
            // If the property type is a generated interface, construct the corresponding class
            if (memberTypeName.StartsWith("IDiscord"))
            {
                var propClassName = $"Discord{GetTypeNameWithoutLeadingI(prop.PropertyType, false)}";
                if (!hasSetter)
                {
                    classSb.AppendLine($"    public {memberTypeName}{nullableModifier} {prop.Name} => {nullCheck}new {propClassName}(_original.{prop.Name});");
                }
                else
                {
                    classSb.AppendLine($"    public {memberTypeName}{nullableModifier} {prop.Name} {{ get {{ return {nullCheck}new {propClassName}(_original.{prop.Name}); }} set {{ _original.{prop.Name} = value{nullableModifier}.Original; }} }}");
                }
            }
            else
            {
                if (!hasSetter)
                {
                    classSb.AppendLine($"    public {memberTypeName}{nullableModifier} {prop.Name} => _original.{prop.Name};");
                }
                else
                {
                    classSb.AppendLine($"    public {memberTypeName}{nullableModifier} {prop.Name} {{ get {{ return _original.{prop.Name}; }} set {{ _original.{prop.Name} = value; }} }}");
                }
            }
        }
        AddMethods(type, interfaceQueue, classSb, forInterface: false);
        classSb.AppendLine("}");
        classBodies.Add(classSb.ToString());
    }

    private static string NullabilityModifier(Type prop)
    {
        bool isNullableValueType = Nullable.GetUnderlyingType(prop) != null;
        var nullable = prop.CustomAttributes
            .FirstOrDefault(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");
        bool isNullableReferenceType = nullable != null &&
            nullable.ConstructorArguments.Count > 0 &&
            (nullable.ConstructorArguments[0].Value?.ToString() == "2");
        //bool isNullable = isNullableValueType || isNullableReferenceType;
        bool isNullable = isNullableReferenceType;
        var nullableModifier = isNullable ? "?" : "";
        return nullableModifier;
    }

    private static string NullabilityModifier(MethodInfo prop)
    {
        bool isNullableValueType = Nullable.GetUnderlyingType(prop.ReturnType) != null;
        var nullable = prop.CustomAttributes
            .FirstOrDefault(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute");
        bool isNullableReferenceType = nullable != null &&
            nullable.ConstructorArguments.Count > 0 &&
            (nullable.ConstructorArguments[0].Value?.ToString() == "2");
        //bool isNullable = isNullableValueType || isNullableReferenceType;
        bool isNullable = isNullableReferenceType;
        var nullableModifier = isNullable ? "?" : "";
        return nullableModifier;
    }

    //prop.ParameterType.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute")

    private static string NullabilityModifier(ParameterInfo? prop, int index = 0)
    {
        if (prop is null)
            return "";
        bool isNullableValueType = Nullable.GetUnderlyingType(prop.ParameterType) != null;
        var nullable = prop.CustomAttributes
            .FirstOrDefault(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");

        // Default: not nullable
        bool isNullableReferenceType = false;

        if (nullable != null && nullable.ConstructorArguments.Count > 0)
        {
            var arg = nullable.ConstructorArguments[0];
            if (arg.ArgumentType == typeof(byte[]))
            {
                // For generic types, this is an array
                var arr = (ReadOnlyCollection<CustomAttributeTypedArgument>)arg.Value!;
                // For single generic argument, use arr[0]
                // For multiple, you may want to handle each separately
                // Here, just use the last one (usually the value type)
                if (index > arr.Count - 1)
                {
                    throw new Exception("Index out of range for NullableAttribute array");
                }
                isNullableReferenceType = arr.Count > 0 && arr[index].Value?.ToString() == "2";
            }
            else
            {
                // For non-generic, just check the value
                isNullableReferenceType = arg.Value?.ToString() == "2";
            }
        }
        var mustBeNullable = prop.HasDefaultValue is true && prop.DefaultValue is null && prop.ParameterType.Name is not "CancellationToken";
        //bool isNullable = isNullableValueType || isNullableReferenceType;
        bool isNullable = isNullableReferenceType;// || mustBeNullable;
        var nullableModifier = isNullable ? "?" : "";

        return nullableModifier;
    }

    // Dictionary of properties known to be nullable since some cannot be detected via reflection
    private static readonly Dictionary<string, bool> _nullablePropertyWarnings = new()
    {
        { "User.GlobalName", true },
        { "User.AvatarHash", true },
        { "User.BannerHash", true },
        { "User.Locale", true },
        { "User.Email", true },
        { "User.AvatarDecorationData", true },
        { "RestRequestProperties.AuditLogReason", true },
        { "RestRequestProperties.ErrorLocalization", true },
        { "GuildUser.Nickname", true },
        { "GuildUser.GuildAvatarHash", true },
        { "GuildUser.GuildBannerHash", true },
        { "GuildUser.GuildAvatarDecorationData", true },
        { "GuildScheduledEvent.Description", true },
        { "GuildScheduledEvent.Location", true },
        { "GuildScheduledEvent.Creator", true },
        { "GuildScheduledEvent.CoverImageHash", true },
        { "GuildScheduledEvent.RecurrenceRule", true },
        { "RestInvite.Guild", true },
        { "RestInvite.Channel", true },
        { "RestInvite.Inviter", true },
        { "RestInvite.TargetUser", true },
        { "RestInvite.TargetApplication", true },
        { "RestInvite.StageInstance", true },
        { "RestInvite.GuildScheduledEvent", true },
        { "Webhook.Creator", true },
        { "Webhook.Name", true },
        { "Webhook.AvatarHash", true },
        { "Webhook.Guild", true },
        { "Webhook.Channel", true },
        { "Webhook.Url", true },
        { "MessageProperties.Content", true },
        { "MessageProperties.Nonce", true },
        { "MessageProperties.AllowedMentions", true },
        { "MessageProperties.MessageReference", true },
        { "MessageProperties.StickerIds", true },
        { "MessageProperties.Poll", true },
        { "Embed.Title", true },
        { "Embed.Description", true },
        { "Embed.Url", true },
        { "Embed.Footer", true },
        { "Embed.Image", true },
        { "Embed.Thumbnail", true },
        { "Embed.Video", true },
        { "Embed.Provider", true },
        { "Embed.Author", true },
        { "Application.IconHash", true },
        { "Application.Bot", true },
        { "Application.TermsOfServiceUrl", true },
        { "Application.PrivacyPolicyUrl", true },
        { "Application.Owner", true },
        { "Application.Team", true },
        { "Application.Guild", true },
        { "Application.Slug", true },
        { "Application.CoverImageHash", true },
        { "Application.InteractionsEndpointUrl", true },
        { "Application.RoleConnectionsVerificationUrl", true },
        { "Application.InstallParams", true },
        { "Application.CustomInstallUrl", true },
        { "EmbedProperties.Title", true },
        { "EmbedProperties.Description", true },
        { "EmbedProperties.Url", true },
        { "EmbedProperties.Footer", true },
        { "EmbedProperties.Image", true },
        { "EmbedProperties.Thumbnail", true },
        { "EmbedProperties.Author", true },
        { "UserActivity.Url", true },
        { "UserActivity.Timestamps", true },
        { "UserActivity.Details", true },
        { "UserActivity.State", true },
        { "UserActivity.Emoji", true },
        { "UserActivity.Party", true },
        { "UserActivity.Assets", true },
        { "UserActivity.Secrets", true },
        { "GuildScheduledEventRecurrenceRule.ByWeekday", true },
        { "GuildScheduledEventRecurrenceRule.ByMonth", true },
        { "GuildScheduledEventRecurrenceRule.ByMonthDay", true },
        { "GuildScheduledEventRecurrenceRule.ByYearDay", true },
        { "EmbedAuthor.Name", true },
        { "EmbedAuthor.Url", true },
        { "EmbedAuthor.IconUrl", true },
        { "EmbedAuthor.ProxyIconUrl", true },
        { "MessagePollMedia.Text", true },
        { "MessagePollMedia.Emoji", true },
        { "EmbedFooterProperties.Text", true },
        { "EmbedFooterProperties.IconUrl", true },
        { "EmbedImageProperties.Url", true },
        { "EmbedThumbnailProperties.Url", true },
        { "EmbedAuthorProperties.Name", true },
        { "EmbedAuthorProperties.Url", true },
        { "EmbedAuthorProperties.IconUrl", true },
        { "EmbedFieldProperties.Name", true },
        { "EmbedFieldProperties.Value", true },
        { "MessagePollMediaProperties.Text", true },
        { "MessagePollMediaProperties.Emoji", true },
        { "WebhookMessageProperties.Content", true },
        { "WebhookMessageProperties.Username", true },
        { "WebhookMessageProperties.AvatarUrl", true },
        { "WebhookMessageProperties.AllowedMentions", true },
        { "WebhookMessageProperties.ThreadName", true },
        { "WebhookMessageProperties.AppliedTags", true },
        { "WebhookMessageProperties.Poll", true },
        { "UserActivityAssets.LargeImageId", true },
        { "UserActivityAssets.LargeText", true },
        { "UserActivityAssets.SmallImageId", true },
        { "UserActivityAssets.SmallText", true },
        { "UserActivitySecrets.Join", true },
        { "UserActivitySecrets.Spectate", true },
        { "UserActivitySecrets.Match", true },
        { "AutoModerationActionMetadataProperties.CustomMessage", true },
        { "GuildOnboardingPromptOptionProperties.ChannelIds", true },
        { "GuildOnboardingPromptOptionProperties.RoleIds", true },
        { "GuildOnboardingPromptOptionProperties.EmojiName", true },
        { "GuildOnboardingPromptOptionProperties.Description", true },
        { "ApplicationRoleConnectionProperties.PlatformName", true },
        { "ApplicationRoleConnectionProperties.PlatformUsername", true },
        { "ApplicationIntegrationTypeConfigurationProperties.OAuth2InstallParams", true },
        { "WebhookClientConfiguration.Client", true },
        { "AuditLogChange`1.NewValue", true },
        { "AuditLogChange`1.OldValue", true },
        // Add more entries as needed, format: { "Name.PropertyName", true }
    };

    // Dictionary of methods known to return nullable since some cannot be detected via reflection
    private static readonly Dictionary<string, bool> _nullableMethodWarnings = new()
    {
        { "User.GetAvatarUrl", true },
        { "User.GetBannerUrl", true },
        { "User.GetAvatarDecorationUrl", true },
        { "GuildUser.GetGuildAvatarDecorationUrl", true },
        { "GuildScheduledEvent.GetCoverImageUrl", true },
        { "Application.GetIconUrl", true },
        { "Application.GetCoverUrl", true },
        { "Application.GetAssetUrl", true },
        { "Application.GetStorePageAssetUrl", true },
        // Add more entries as needed, format: { "Name.PropertyName", true }
    };

    private static string NullabilityModifier(PropertyInfo prop)
    {
        bool isNullableValueType = Nullable.GetUnderlyingType(prop.PropertyType) != null;
        var nullable = prop.CustomAttributes
            .FirstOrDefault(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");

        bool isNullableReferenceType = false;
        if (nullable != null && nullable.ConstructorArguments.Count > 0)
        {
            var arg = nullable.ConstructorArguments[0];
            if (arg.ArgumentType == typeof(byte[]))
            {
                // For reference types, this is an array
                var arr = (ReadOnlyCollection<CustomAttributeTypedArgument>)arg.Value!;
                if (arr.Count > 0)
                    isNullableReferenceType = arr[0].Value?.ToString() == "2";
            }
            else
            {
                // For value types, just check the value
                isNullableReferenceType = arg.Value?.ToString() == "2";
            }
        }

        // Assume its nullable if it has [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        if (isNullableReferenceType is false)
        {
            var jsonIgnore = prop.CustomAttributes
            .FirstOrDefault(a => a.AttributeType.FullName == "System.Text.Json.Serialization.JsonIgnoreAttribute");

            if (jsonIgnore is not null && jsonIgnore.NamedArguments.Any(a => a.MemberName == "Condition" && (int?)a.TypedValue.Value == 3))
            {
                isNullableReferenceType = true;
            }
        }

        // Final check: use dictionary from build warnings
        if (!isNullableReferenceType)
        {
            var name = prop.DeclaringType?.Name ?? throw new Exception("Missing declaring type");
            if (name.StartsWith("Partial"))
                name = name.Substring(7);
            var key = $"{name}.{prop.Name}";
            if (_nullablePropertyWarnings.TryGetValue(key, out var _))
            {
                isNullableReferenceType = true;
            }
        }

        bool isNullable = isNullableReferenceType;
        var nullableModifier = isNullable ? "?" : "";
        return nullableModifier;
    }

    private static void AddMethods(Type type, Queue<Type> interfaceQueue, StringBuilder sb, bool forInterface)
    {
        string[] ignored = ["GetType", "ToString", "Equals", "GetHashCode", "TryFormat", "<Clone>$", "Clone"];
        var methods = type.GetMethods().Where(m => m.IsPublic && !m.IsSpecialName && !ignored.Contains(m.Name)).ToArray();
        foreach (var method in methods)
        {
            if (method.CustomAttributes.Any(a => a.AttributeType.FullName == "System.ObsoleteAttribute"))
                continue; // Skip obsolete methods

            if (method.DeclaringType != type && methods.Any(m => MethodsAreEquivalent(m, method) && m.DeclaringType == type))
                continue; // Skip inherited methods that are overridden
            if (method.IsStatic)
            {
                var f = 1;
            }
            var returnType = GetDiscordTypeName(method.ReturnType, interfaceQueue, method.IsGenericMethod, method.ReturnParameter, 0, method); // Handle nullables here, its get infeasible to do it later
            var parameters = method.GetParameters();
            var paramStrings = new List<string>();
            var argNames = new List<string>();
            var anyOutParams = parameters.Any(p => p.IsOut);
            var staticModifier = method.IsStatic ? "static " : "";

            foreach (var param in parameters)
            {
                var paramType = GetDiscordTypeName(param.ParameterType, interfaceQueue, method.IsGenericMethod, param, 0, method);
                var paramStr = $"{paramType} {param.Name}";
                if (param.IsOut)
                {
                    paramStr = $"out {paramType.Split("`")[0]} {param.Name}";
                }
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
                if (!forInterface || method.IsStatic)
                {
                    if (param.ParameterType.IsGenericType && param.ParameterType.GetGenericTypeDefinition() == typeof(Action<>))
                    {
                        var trueType = param.ParameterType.GenericTypeArguments[0];
                        if (trueType?.Assembly?.FullName is null)
                            throw new Exception("Full name null");
                        if (trueType.Assembly.FullName.StartsWith("NetCord")) // Missing logic to make sure this is one of our types: IDiscord.
                        {
                            var trueTypeName = GetDiscordTypeName(trueType, interfaceQueue);
                            if (trueTypeName.StartsWith("IDiscord"))
                            {
                                var trueClassName = $"Discord{GetTypeNameWithoutLeadingI(trueType, method.IsGenericMethod)}";
                                argNames.Add($"x => {param.Name}(new {trueClassName}(x))");
                                continue;
                            }
                        }
                    }

                    // TODO: figure out func
                    //if (param.ParameterType.IsGenericType && param.ParameterType.GetGenericTypeDefinition() == typeof(Func))
                    //{
                    //    var trueType = param.ParameterType.GenericTypeArguments[0];
                    //    if (trueType?.Assembly?.FullName is null)
                    //        throw new Exception("Full name null");
                    //    if (trueType.Assembly.FullName.StartsWith("NetCord")) // Missing logic to make sure this is one of our types: IDiscord.
                    //    {
                    //        var trueTypeName = GetDiscordTypeName(trueType, interfaceQueue);
                    //        if (trueTypeName.StartsWith("IDiscord"))
                    //        {
                    //            var trueClassName = $"Discord{GetTypeNameWithoutLeadingI(trueType, method.IsGenericMethod)}";
                    //            argNames.Add($"x => {param.Name}(new {trueClassName}(x))");
                    //            continue;
                    //        }
                    //    }
                    //}

                    if (param.ParameterType.IsGenericType && param.ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    {
                        var trueType = param.ParameterType.GetGenericArguments()[0];
                        if (trueType?.Assembly?.FullName is null)
                            throw new Exception("Full name null");
                        if (trueType.Assembly.FullName.StartsWith("NetCord")) // Missing logic to make sure this is one of our types: IDiscord.
                        {
                            var trueTypeName = GetDiscordTypeName(trueType, interfaceQueue);
                            if (trueTypeName.StartsWith("IDiscord"))
                            {
                                var nullableModifier = NullabilityModifier(param);
                                argNames.Add($"{param.Name}{nullableModifier}.Select(x => x.Original)");
                                continue;
                            }
                        }
                    }

                    if (param.ParameterType.IsArray)
                    {
                        var trueType = param.ParameterType.GetElementType();
                        if (trueType?.Assembly?.FullName is null)
                            throw new Exception("Full name null");
                        if (trueType.Assembly.FullName.StartsWith("NetCord")) // Missing logic to make sure this is one of our types: IDiscord.
                        {
                            var trueTypeName = GetDiscordTypeName(trueType, interfaceQueue);
                            if (trueTypeName.StartsWith("IDiscord"))
                            {
                                var nullableModifier = NullabilityModifier(trueType);
                                argNames.Add($"{param.Name}.Select(x => x{nullableModifier}.Original).ToArray()");
                                continue;
                            }
                        }
                    }

                    if (param.ParameterType.Assembly.FullName is null)
                        throw new Exception("Full name null");

                    if (param.IsOut) // Can't call original on out parameters
                    {
                        argNames.Add($"out var {param.Name}Temp");
                    }
                    else if (param.ParameterType.IsGenericType && param.ParameterType.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
                    {
                        var types = param.ParameterType.GetGenericArguments();
                        if (types.Any(x =>
                        {
                            if (x?.Assembly?.FullName is null)
                                throw new Exception("Full name null");
                            var netCord = x.Assembly.FullName.StartsWith("NetCord");
                            var f = GetDiscordTypeName(x, interfaceQueue, method.IsGenericMethod);
                            return netCord && f.StartsWith("IDiscord");
                        }))
                        {
                            var nullableModifier = NullabilityModifier(param);
                            if (types.Any(x => param.ParameterType.Assembly.FullName is null))
                                throw new Exception("Full name null");
                            if (types.All(x => param.ParameterType.Assembly.FullName!.StartsWith("NetCord") && GetDiscordTypeName(x, interfaceQueue, method.IsGenericMethod).StartsWith("IDiscord")))
                            {
                                var nullableModifier0 = NullabilityModifier(types[0]);
                                var nullableModifier1 = NullabilityModifier(types[1]);
                                argNames.Add($"{param.Name}{nullableModifier}.ToDictionary(kv => kv.Key{nullableModifier0}.Original, kv => kv.Value{nullableModifier1}.Original)");
                            }
                            else if (types[0].Assembly.FullName!.StartsWith("NetCord") && GetDiscordTypeName(types[0], interfaceQueue, method.IsGenericMethod).StartsWith("IDiscord"))
                            {
                                var nullableModifier0 = NullabilityModifier(types[0]);
                                argNames.Add($"{param.Name}{nullableModifier}.ToDictionary(kv => kv.Key{nullableModifier0}.Original, kv => kv.Value)");
                            }
                            else if (types[1].Assembly.FullName!.StartsWith("NetCord") && GetDiscordTypeName(types[1], interfaceQueue, method.IsGenericMethod).StartsWith("IDiscord"))
                            {
                                var nullableModifier1 = NullabilityModifier(types[1]);
                                argNames.Add($"{param.Name}{nullableModifier}.ToDictionary(kv => kv.Key, kv => kv.Value{nullableModifier1}.Original)");
                            }
                            else
                            {
                                throw new ArgumentOutOfRangeException("Detected NetCord type with IDiscord generated interface but then somehow did not find it on dictionary");
                            }
                        }
                        else
                        {
                            argNames.Add(param.Name ?? throw new Exception("Name null"));
                        }
                    }
                    else if (param.ParameterType.IsGenericType && param.ParameterType.GetGenericTypeDefinition() == typeof(ImmutableDictionary<,>))
                    {
                        var types = param.ParameterType.GetGenericArguments();
                        if (types.Any(x =>
                        {
                            if (x?.Assembly?.FullName is null)
                                throw new Exception("Full name null");
                            var netCord = x.Assembly.FullName.StartsWith("NetCord");
                            var f = GetDiscordTypeName(x, interfaceQueue, method.IsGenericMethod);
                            return netCord && f.StartsWith("IDiscord");
                        }))
                        {
                            var nullableModifier = NullabilityModifier(param);
                            if (types.Any(x => param.ParameterType.Assembly.FullName is null))
                                throw new Exception("Full name null");
                            if (types.All(x => param.ParameterType.Assembly.FullName!.StartsWith("NetCord") && GetDiscordTypeName(x, interfaceQueue, method.IsGenericMethod).StartsWith("IDiscord")))
                            {
                                var nullableModifier0 = NullabilityModifier(types[0]);
                                var nullableModifier1 = NullabilityModifier(types[1]);
                                argNames.Add($"{param.Name}{nullableModifier}.ToImmutableDictionary(kv => kv.Key{nullableModifier0}.Original, kv => kv.Value{nullableModifier1}.Original)");
                            }
                            else if (types[0].Assembly.FullName!.StartsWith("NetCord") && GetDiscordTypeName(types[0], interfaceQueue, method.IsGenericMethod).StartsWith("IDiscord"))
                            {
                                var nullableModifier0 = NullabilityModifier(types[0]);
                                argNames.Add($"{param.Name}{nullableModifier}.ToImmutableDictionary(kv => kv.Key{nullableModifier0}.Original, kv => kv.Value)");
                            }
                            else if (types[1].Assembly.FullName!.StartsWith("NetCord") && GetDiscordTypeName(types[1], interfaceQueue, method.IsGenericMethod).StartsWith("IDiscord"))
                            {
                                var nullableModifier1 = NullabilityModifier(types[1]);
                                argNames.Add($"{param.Name}{nullableModifier}.ToImmutableDictionary(kv => kv.Key, kv => kv.Value{nullableModifier1}.Original)");
                            }
                            else
                            {
                                throw new ArgumentOutOfRangeException("Detected NetCord type with IDiscord generated interface but then somehow did not find it on dictionary");
                            }
                        }
                        else
                        {
                            argNames.Add(param.Name ?? throw new Exception("Name null"));
                        }
                    }
                    else if (param.ParameterType.Assembly.FullName.StartsWith("NetCord") && paramType.StartsWith("IDiscord"))
                    {
                        var nullableModifier = NullabilityModifier(param);

                        // Fallback check that paramter is really not null..
                        if (nullableModifier == "" && param.HasDefaultValue && param.DefaultValue == null && param.ParameterType.IsValueType is false)
                        {
                            nullableModifier = "?";
                        }
                        argNames.Add($"{param.Name}{nullableModifier}.Original");
                    }
                    else
                    {
                        argNames.Add(param.Name ?? throw new Exception("Name null"));
                    }
                }
            }
            // Add generic parameter list to method signature if method is generic
            var genericDecl = method.IsGenericMethod
                ? $"<{string.Join(", ", method.GetGenericArguments().Select(t => $"{t.Name}Param"))}>"
                : "";
            var genericConstraint = "";
            if (method.IsGenericMethod)
            {
                genericConstraint = string.Join(" ", method.GetGenericArguments().Where(x => x.BaseType != null && x.BaseType != typeof(object)).Select(t => $"where {t.Name}Param : {t.BaseType?.FullName ?? throw new Exception("Full name null")}"));
            }
            if (forInterface)
            {
                //if (method.IsStatic)
                //    continue; // Static methods not supported in classes for now
                var nullableModifier = NullabilityModifier(method); // This might be wrong for generic methods
                if (method.IsStatic)
                {
                    sb.AppendLine($"    {staticModifier}{returnType}{nullableModifier} {method.Name}{genericDecl}({string.Join(", ", paramStrings)}){genericConstraint}");
                    sb.AppendLine($"    {{");
                    var withoutI = returnType.Substring(1);
                    sb.AppendLine($"        return new {withoutI}({type.FullName}.{method.Name}({string.Join(", ", argNames)}));");
                    sb.AppendLine($"    }}");
                }
                else
                {
                    sb.AppendLine($"    {returnType}{nullableModifier} {method.Name}{genericDecl}({string.Join(", ", paramStrings)}){genericConstraint};");
                }
            }
            else
            {
                if (method.IsStatic)
                    continue; // Static methods not supported in classes for now
                // Have to map out parameter to original
                if (anyOutParams)
                {
                    if (returnType.StartsWith("IDiscord"))
                    {
                        var returnClassName = $"Discord{GetTypeNameWithoutLeadingI(method.ReturnType, method.IsGenericMethod)}";
                        sb.AppendLine($"    public {returnType} {method.Name}{genericDecl}({string.Join(", ", paramStrings)}) {genericConstraint}");
                        sb.AppendLine("    {");
                        sb.AppendLine($"        var result = new {returnClassName}(_original.{CallMethod(method)}({string.Join(", ", argNames)}));");
                        var outParams = parameters.Where(p => p.IsOut).Select(p => p.Name);
                        foreach (var outParam in outParams)
                        {
                            var bar = GetDiscordTypeName(parameters.First(p => p.Name == outParam).ParameterType, interfaceQueue);
                            var withoutI = bar.Split("`")[0].Substring(1);
                            sb.AppendLine($"        {outParam} = new {withoutI}({outParam}Temp);");
                        }
                        sb.AppendLine($"        return result;");
                        sb.AppendLine("    }");
                        continue;
                    }
                    else
                    {
                        sb.AppendLine($"    public {returnType} {method.Name}{genericDecl}({string.Join(", ", paramStrings)}) {genericConstraint}");
                        sb.AppendLine("    {");
                        sb.AppendLine($"        var result = _original.{CallMethod(method)}({string.Join(", ", argNames)});");
                        var outParams = parameters.Where(p => p.IsOut).Select(p => p.Name);
                        foreach (var outParam in outParams)
                        {
                            var bar = GetDiscordTypeName(parameters.First(p => p.Name == outParam).ParameterType, interfaceQueue);
                            var withoutI = bar.Split("`")[0].Substring(1);
                            sb.AppendLine($"        {outParam} = new {withoutI}({outParam}Temp);");
                        }
                        sb.AppendLine($"        return result;");
                        sb.AppendLine("    }");
                        continue;
                    }
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
                            var argClassName = $"Discord{GetTypeNameWithoutLeadingI(argType, method.IsGenericMethod)}";
                            sb.AppendLine($"    public async {returnType} {method.Name}{genericDecl}({string.Join(", ", paramStrings)}) {genericConstraint}");
                            sb.AppendLine($"    {{");
                            sb.AppendLine($"        return (await _original.{CallMethod(method)}({string.Join(", ", argNames)})).Select(x => new {argClassName}(x));");
                            sb.AppendLine($"    }}");
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
                            var argClassName = $"Discord{GetTypeNameWithoutLeadingI(argType, method.IsGenericMethod)}";
                            sb.AppendLine($"    public async {returnType} {method.Name}{genericDecl}({string.Join(", ", paramStrings)}) {genericConstraint}");
                            sb.AppendLine($"    {{");
                            sb.AppendLine($"        return (await _original.{CallMethod(method)}({string.Join(", ", argNames)})).Select(x => new {argClassName}(x)).ToList();");
                            sb.AppendLine($"    }}");
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
                            var valueClassName = $"Discord{GetTypeNameWithoutLeadingI(valueType, method.IsGenericMethod)}";
                            sb.AppendLine($"    public async {returnType} {method.Name}{genericDecl}({string.Join(", ", paramStrings)}) {genericConstraint}");
                            sb.AppendLine($"    {{");
                            sb.AppendLine($"        return (await _original.{CallMethod(method)}({string.Join(", ", argNames)})).ToImmutableDictionary(kv => kv.Key, kv => (I{valueClassName})new {valueClassName}(kv.Value));");
                            sb.AppendLine($"    }}");
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
                            var valueClassName = $"Discord{GetTypeNameWithoutLeadingI(valueType, method.IsGenericMethod)}";
                            sb.AppendLine($"    public async {returnType} {method.Name}{genericDecl}({string.Join(", ", paramStrings)}) {genericConstraint}");
                            sb.AppendLine($"    {{");
                            sb.AppendLine($"        return (await _original.{CallMethod(method)}({string.Join(", ", argNames)})).ToDictionary(kv => kv.Key, kv => (I{valueClassName})new {valueClassName}(kv.Value));");
                            sb.AppendLine($"    }}");
                            continue;
                        }
                    }


                    var nullableModifier = NullabilityModifier(method.ReturnParameter, 1); // 0 Gets the outer type (Task), 1 gets the inner type
                    var isNullable = nullableModifier is "?";
                    if (discordTaskReturnType.StartsWith("IDiscord"))
                    {
                        var returnClassName = discordTaskReturnType.TrimStart('I');
                        sb.AppendLine($"    public async {returnType} {method.Name}{genericDecl}({string.Join(", ", paramStrings)}) {genericConstraint}");
                        sb.AppendLine($"    {{");
                        if (isNullable)
                        {
                            sb.AppendLine($"        var temp = await _original.{CallMethod(method)}({string.Join(", ", argNames)});");
                            sb.AppendLine($"        return temp is null ? null : new {returnClassName}(temp);");
                        }
                        else
                        {
                            sb.AppendLine($"        return new {returnClassName}(await _original.{CallMethod(method)}({string.Join(", ", argNames)}));");
                        }
                        sb.AppendLine($"    }}");
                    }
                    else
                    {
                        sb.AppendLine($"    public {returnType} {method.Name}{genericDecl}({string.Join(", ", paramStrings)}) {genericConstraint}");
                        sb.AppendLine($"    {{");
                        sb.AppendLine($"        return _original.{CallMethod(method)}({string.Join(", ", argNames)});");
                        sb.AppendLine($"    }}");
                    }
                }
                else
                {
                    // Handle IAsyncEnumerable<T> of generated interface
                    if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
                    {
                        var argType = method.ReturnType.GetGenericArguments()[0];
                        var argTypeName = GetDiscordTypeName(argType, interfaceQueue);
                        if (argTypeName.StartsWith("IDiscord"))
                        {
                            var argClassName = $"Discord{GetTypeNameWithoutLeadingI(argType, method.IsGenericMethod)}";
                            sb.AppendLine($"    public async {returnType} {method.Name}{genericDecl}({string.Join(", ", paramStrings)}) {genericConstraint}");
                            sb.AppendLine($"    {{");
                            sb.AppendLine($"        await foreach(var original in _original.{CallMethod(method)}({string.Join(", ", argNames)}))");
                            sb.AppendLine($"        {{");
                            sb.AppendLine($"            yield return new {argClassName}(original);");
                            sb.AppendLine($"        }}");
                            sb.AppendLine($"    }}");
                            continue;
                        }
                    }

                    // is method.ReturnType generic type?
                    if (returnType.StartsWith("IDiscord"))
                    {
                        var returnClassName = $"Discord{GetTypeNameWithoutLeadingI(method.ReturnType, method.IsGenericMethod)}";
                        var nullableModifier = NullabilityModifier(method);
                        var isNullable = nullableModifier is "?" || returnType.EndsWith("?"); // Nullable reflection check busted on some types so gotta check the ending of the returnType
                        sb.AppendLine($"    public {returnType}{nullableModifier} {method.Name}{genericDecl}({string.Join(", ", paramStrings)}) {genericConstraint}");
                        sb.AppendLine($"    {{");
                        if (isNullable)
                        {
                            sb.AppendLine($"        var temp = _original.{CallMethod(method)}({string.Join(", ", argNames)});");
                            sb.AppendLine($"        return temp is null ? null : new {returnClassName}(temp);");
                        }
                        else
                        {
                            sb.AppendLine($"        return new {returnClassName}(_original.{CallMethod(method)}({string.Join(", ", argNames)}));");
                        }
                            
                        sb.AppendLine($"    }}");
                    }
                    else
                    {
                        var nullableModifier = NullabilityModifier(method);
                        sb.AppendLine($"    public {returnType}{nullableModifier} {method.Name}{genericDecl}({string.Join(", ", paramStrings)}) {genericConstraint}");
                        sb.AppendLine($"    {{");
                        var returnModifier = returnType == "void" ? "" : "return ";
                        sb.AppendLine($"        {returnModifier}_original.{CallMethod(method)}({string.Join(", ", argNames)});");
                        sb.AppendLine($"    }}");
                    }
                }
            }
        }
    }

    private static string CallMethod(MethodInfo method)
    {
        if (method.IsGenericMethod)
        {
            var genericArgs = string.Join(", ", method.GetGenericArguments().Select(t => $"{t.Name}Param"));
            return $"{method.Name}<{genericArgs}>";
        }
        return method.Name;
    }

    private static string GetDiscordTypeName(Type type, Queue<Type> interfaceQueue, bool isGenericMethod = false, ParameterInfo? withNullable = null, int genericIndex = 0, MethodInfo? method = null)
    {
        // If this is a generic type parameter, just use its name and do not enqueue for generation
        if (type.IsGenericParameter && isGenericMethod)
            return type.Name + "Param" + NullabilityModifier(withNullable, genericIndex);
        if(type.IsGenericParameter)
            return type.Name;
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
            ["Object"] = "object",
            ["Void"] = "void",
        };
        // Handle Nullable<T> as T?
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var innerType = type.GetGenericArguments()[0];
            var innerTypeName = GetDiscordTypeName(innerType, interfaceQueue, isGenericMethod, withNullable);
            return $"{innerTypeName}?"; // TODO: Should I remove this ? here
        }
        if (type.IsArray)
        {
            var innerType = type.GetElementType() ?? throw new Exception("Inner type null");
            var innerTypeName = GetDiscordTypeName(innerType, interfaceQueue, isGenericMethod, withNullable);
            return $"{innerTypeName}[]";
        }
        if (typeMap.TryGetValue(type.Name, out var alias))
            return alias + NullabilityModifier(type);
        // If type is not interface or class, use its name directly
        if (!type.IsInterface && !type.IsClass && !type.IsGenericType)
            return type.FullName + NullabilityModifier(withNullable, genericIndex) ?? type.Name + NullabilityModifier(withNullable, genericIndex); // Add nullable ? here
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
                var final = string.Join(", ", types.Select(t => GetDiscordTypeName(t, interfaceQueue, isGenericMethod, withNullable)));
                var superName = $"IDiscord{genericTypeName}<{final}>";
                return superName + NullabilityModifier(withNullable, genericIndex); // Add nullable ? here
            }
            else
            {
                var m = NullabilityModifier(withNullable, genericIndex);
                if (m == "" && method is not null) // Fallback to dictionary for known nullables
                {
                    var typ = method.DeclaringType?.Name ?? throw new Exception("Declaring type null");
                    if (typ.StartsWith("Partial"))
                        typ = typ.Substring(7);
                    var name = method.Name;
                    if (_nullableMethodWarnings.TryGetValue($"{typ}.{name}", out var _))
                    {
                        m = "?";
                    }
                }
                return interfaceName + m; // Add nullable ? here
            }
        }
        // Handle generic types (e.g., IReadOnlyList<T>, etc.)
        if (type.IsGenericType)
        {
            var genericTypeName = type.Name.Split('`')[0];
            var isDict = genericTypeName is "IReadOnlyDictionary" or "IDictionary" or "Dictionary" or "ImmutableDictionary" or "IEnumerable" or "Func";
            var incrementBy = isDict ? 0 : 1; // Dictionary has 2 generic args, so don't increment index for nullability
            var genericArgs = type.GetGenericArguments();
            var args = string.Join(", ", genericArgs.Select((t, i) =>
            {
                if (i is 0 && isDict) // dict key is never null
                {
                    return GetDiscordTypeName(t, interfaceQueue, isGenericMethod);
                }
                else
                {
                    return GetDiscordTypeName(t, interfaceQueue, isGenericMethod, withNullable, genericIndex + i + incrementBy);
                }
            })); // Index 0 is outer type
            return $"{genericTypeName}<{args}>" + NullabilityModifier(withNullable, genericIndex);
        }
        // Otherwise, use the type name as-is
        return type.Name + NullabilityModifier(withNullable, genericIndex); // Add nullable ? here
    }

    private static string GetTypeNameWithoutLeadingI(Type type, bool isGenericMethod)
    {
        var typeName = type.Name;
        if (type.IsGenericType)
        {
            typeName = type.Name.Split('`')[0];
            var types = type.GetGenericArguments();
            var final = string.Join(", ", types.Select(t => GetDiscordTypeName(t, new Queue<Type>(), isGenericMethod)));
            typeName = $"{typeName}<{final}>";
        }
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
