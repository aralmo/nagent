using System.Text.Json.Nodes;
using Nagent.Core.Domain;
using Nagent.Core.Shell;

namespace Nagent.Core.Tools;

public sealed class ShellCommandTool(ShellRunner shellRunner, CustomToolDefinition definition) : ITool
{
    private readonly IReadOnlyList<string> _parameterNames = definition.Parameters
        .Select(p => p.Name)
        .Where(n => !string.IsNullOrWhiteSpace(n))
        .ToList();

    public string Name => definition.Name;
    public string Description => definition.Description;

    public JsonObject ParametersSchema
    {
        get
        {
            var properties = new JsonObject();
            foreach (var parameter in definition.Parameters)
            {
                if (string.IsNullOrWhiteSpace(parameter.Name))
                {
                    continue;
                }

                properties[parameter.Name] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = parameter.Description
                };
            }

            var schema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = properties
            };

            if (_parameterNames.Count > 0)
            {
                schema["required"] = new JsonArray(_parameterNames.Select(n => JsonValue.Create(n)).ToArray());
            }

            return schema;
        }
    }

    public Task<string> InvokeAsync(
        JsonObject arguments,
        string workingPath,
        CancellationToken cancellationToken = default) =>
        InvokeAsync(arguments, workingPath, context: null, cancellationToken);

    public async Task<string> InvokeAsync(
        JsonObject arguments,
        string workingPath,
        AgentContext? context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = CommandTemplateSubstitution.Substitute(
                definition.Command,
                _parameterNames,
                arguments,
                context);

            return (await shellRunner.RunAsync(command, workingPath, cancellationToken)).Output;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
