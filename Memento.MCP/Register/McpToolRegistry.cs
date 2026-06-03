using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using McpDotNet.Protocol.Types;
using McpDotNet.Server;
using Memento.MCP.Register.Attributes;

namespace Memento.MCP.Register;

/// <summary>
/// MCP 工具注册中心 — 支持手动 McpTool 注册和反射 Toolset 注册
/// </summary>
public class McpToolRegistry
{
    private readonly List<McpTool> _tools = [];

    /// <summary>注册一个工具</summary>
    public McpToolRegistry Register(McpTool tool)
    {
        _tools.Add(tool);
        return this;
    }

    /// <summary>链式批量注册</summary>
    public McpToolRegistry RegisterRange(params McpTool[] tools)
    {
        _tools.AddRange(tools);
        return this;
    }

    /// <summary>
    /// 注册一个 Toolset 实例（泛型版本）
    /// </summary>
    public McpToolRegistry RegisterToolset<T>(T instance) where T : class
    {
        RegisterToolsetObject(instance);
        return this;
    }

    /// <summary>
    /// 自动扫描 Register/Toolsets 下所有标记了 [McpToolset] 的 Toolset 类并注册。
    /// 通过参数类型匹配自动注入依赖。
    /// </summary>
    public McpToolRegistry RegisterAllToolsets(params object[] services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var toolsetTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpToolsetAttribute>() != null)
            .Where(t => t.Name.EndsWith("Toolset") || t.Name.EndsWith("Toolset`1"))
            .ToList();

        foreach (var type in toolsetTypes)
        {
            var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault();
            if (ctor == null)
            {
                // 无构造函数→默认构造
                var instance = Activator.CreateInstance(type);
                if (instance != null)
                    RegisterToolsetObject(instance);
                continue;
            }

            var ctorParams = ctor.GetParameters();
            var args = new object?[ctorParams.Length];
            bool resolved = true;

            for (int i = 0; i < ctorParams.Length; i++)
            {
                var paramType = ctorParams[i].ParameterType;
                args[i] = services.FirstOrDefault(s => paramType.IsInstanceOfType(s));
                if (args[i] == null && !paramType.IsValueType)
                {
                    Console.Error.WriteLine($"[McpToolRegistry] 无法为 {type.Name} 解析依赖 '{paramType.Name}'，跳过");
                    resolved = false;
                    break;
                }
            }

            if (resolved)
            {
                var instance = Activator.CreateInstance(type, args);
                if (instance != null)
                    RegisterToolsetObject(instance);
            }
        }

        return this;
    }

    private void RegisterToolsetObject(object instance)
    {
        var type = instance.GetType();
        if (type.GetCustomAttribute<McpToolsetAttribute>() == null)
            throw new InvalidOperationException($"类 {type.Name} 缺少 [McpToolset] 属性");

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var toolAttr = method.GetCustomAttribute<ToolAttribute>();
            if (toolAttr == null) continue;

            var toolName = toolAttr.Name ?? method.Name;
            var parameters = method.GetParameters();

            // 检测是否使用 [McpBody]（独占参数）
            var bodyParam = parameters.FirstOrDefault(p => p.GetCustomAttribute<McpBodyAttribute>() != null);
            bool hasBody = bodyParam != null;

            Dictionary<string, JsonSchemaProperty> props;
            List<string> required;

            if (hasBody)
            {
                // ── [McpBody] 模式：从参数类型的属性生成 Schema ──
                var bodyType = bodyParam!.ParameterType;
                (props, required) = BuildSchemaFromType(bodyType);
            }
            else
            {
                // ── [McpParam] 模式：从方法参数生成 Schema ──
                props = [];
                required = [];

                foreach (var param in parameters)
                {
                    var paramAttr = param.GetCustomAttribute<McpParamAttribute>();
                    var hasDefault = param.HasDefaultValue;
                    var jsonType = MapType(param.ParameterType);

                    props[param.Name!] = new JsonSchemaProperty
                    {
                        Type = jsonType,
                        Description = paramAttr?.Description ?? "",
                    };

                    if (!hasDefault)
                        required.Add(param.Name!);
                }
            }

            // 包装调用
            McpTool tool = new()
            {
                Name = toolName,
                Description = toolAttr.Description ?? "",
                Properties = props,
                Required = required,
                Handler = args =>
                {
                    try
                    {
                        object?[] invokeArgs;

                        if (hasBody)
                        {
                            // [McpBody]：整体反序列化
                            var json = JsonSerializer.Serialize(args ?? new Dictionary<string, object?>());
                            var body = JsonSerializer.Deserialize(json, bodyParam!.ParameterType, _jsonOpts);
                            invokeArgs = [body];
                        }
                        else
                        {
                            invokeArgs = MapArguments(parameters, args);
                        }

                        var result = method.Invoke(instance, invokeArgs);
                        if (result is Task<CallToolResponse> task)
                            return task;
                        return Task.FromResult((CallToolResponse)result!);
                    }
                    catch (Exception ex) when (ex is TargetInvocationException tie)
                    {
                        return Task.FromResult(new CallToolResponse
                        {
                            Content = [new() { Text = $"[ERR] {tie.InnerException?.Message ?? ex.Message}", Type = "text" }],
                            IsError = true,
                        });
                    }
                    catch (Exception ex)
                    {
                        return Task.FromResult(new CallToolResponse
                        {
                            Content = [new() { Text = $"[ERR] {ex.Message}", Type = "text" }],
                            IsError = true,
                        });
                    }
                },
            };

            _tools.Add(tool);
        }
    }

    // ── Schema 由 DTO 属性生成 ──

    private static (Dictionary<string, JsonSchemaProperty> Props, List<string> Required) BuildSchemaFromType(Type type)
    {
        var props = new Dictionary<string, JsonSchemaProperty>();
        var required = new List<string>();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanWrite) continue; // 只读属性跳过

            var paramAttr = prop.GetCustomAttribute<McpParamAttribute>();
            var jsonPropAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
            var name = jsonPropAttr?.Name ?? ToSnakeCase(prop.Name);

            var isNullable = IsNullableProperty(prop);

            props[name] = new JsonSchemaProperty
            {
                Type = MapType(prop.PropertyType),
                Description = paramAttr?.Description ?? "",
            };

            if (!isNullable)
                required.Add(name);
        }

        return (props, required);
    }

    private static bool IsNullableProperty(PropertyInfo prop)
    {
        // 值类型 Nullable<T>
        if (Nullable.GetUnderlyingType(prop.PropertyType) != null)
            return true;
        if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
            return true;
        // 引用类型可为 null（通过 NullableAttribute 判断）
        try
        {
            var nullableAttr = prop.CustomAttributes
                .FirstOrDefault(a => a.AttributeType.Name == "NullableAttribute");
            if (nullableAttr != null)
            {
                var args = nullableAttr.ConstructorArguments;
                if (args.Count > 0 && args[0].Value is byte b)
                    return b == 2;
            }
        }
        catch { /* 忽略 */ }
        return false;
    }

    private static string ToSnakeCase(string name) =>
        string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

    // ── 构建 Capabilities ──

    public ServerCapabilities BuildCapabilities() => new()
    {
        Tools = new()
        {
            ListToolsHandler = ListToolsHandler,
            CallToolHandler = CallToolHandler,
        },
    };

    // ── 内部处理 ──

    private Task<ListToolsResult> ListToolsHandler(
        RequestContext<ListToolsRequestParams> context, CancellationToken ct) =>
        Task.FromResult(new ListToolsResult
        {
            Tools = _tools.Select(t => t.ToTool()).ToList(),
        });

    private async Task<CallToolResponse> CallToolHandler(
        RequestContext<CallToolRequestParams> context, CancellationToken ct)
    {
        var name = context.Params?.Name;
        var tool = _tools.FirstOrDefault(t => t.Name == name);
        if (tool == null)
            throw new McpServerException($"未知工具: '{name}'");

        try
        {
            return await tool.Handler(context.Params?.Arguments as Dictionary<string, object?>);
        }
        catch (Exception ex)
        {
            return new CallToolResponse
            {
                Content = [new() { Text = $"执行失败: {ex.Message}", Type = "text" }],
                IsError = true,
            };
        }
    }

    // ── 类型映射 ──

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private static string MapType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(string)) return "string";
        if (t == typeof(int) || t == typeof(long)) return "integer";
        if (t == typeof(double) || t == typeof(float) || t == typeof(decimal)) return "number";
        if (t == typeof(bool)) return "boolean";
        return "string";
    }

    private static object?[] MapArguments(ParameterInfo[] parameters, Dictionary<string, object?>? args)
    {
        var result = new object?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            if (args?.TryGetValue(p.Name!, out var val) == true)
                result[i] = ConvertValue(val, p.ParameterType);
            else if (p.HasDefaultValue)
                result[i] = p.DefaultValue;
            else
                result[i] = p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null;
        }
        return result;
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value == null) return null;
        var t = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (t == typeof(string)) return value.ToString();
        if (t == typeof(int)) return Convert.ToInt32(value);
        if (t == typeof(long)) return Convert.ToInt64(value);
        if (t == typeof(double)) return Convert.ToDouble(value);
        if (t == typeof(bool)) return Convert.ToBoolean(value);
        return value;
    }
}
