namespace Nagent.Core.Parsing;

public enum TemplateNodeKind
{
    Text,
    Model,
    Label,
    Role,
    Do,
    Tools,
    Choose,
    Shell,
    Goto,
    Partial
}

public abstract class TemplateNode
{
    public abstract TemplateNodeKind Kind { get; }
}

public sealed class TextNode(string text) : TemplateNode
{
    public string Text { get; } = text;
    public override TemplateNodeKind Kind => TemplateNodeKind.Text;
}

public sealed class ModelNode(IReadOnlyList<string> models) : TemplateNode
{
    public IReadOnlyList<string> Models { get; } = models;
    public override TemplateNodeKind Kind => TemplateNodeKind.Model;
}

public sealed class LabelNode(string name) : TemplateNode
{
    public string Name { get; } = name;
    public override TemplateNodeKind Kind => TemplateNodeKind.Label;
}

public sealed class RoleNode(string role) : TemplateNode
{
    public string Role { get; } = role;
    public override TemplateNodeKind Kind => TemplateNodeKind.Role;
}

public sealed class DoNode(string command) : TemplateNode
{
    public string Command { get; } = command;
    public override TemplateNodeKind Kind => TemplateNodeKind.Do;
}

public sealed class ToolsNode(IReadOnlyList<string> tools) : TemplateNode
{
    public IReadOnlyList<string> Tools { get; } = tools;
    public override TemplateNodeKind Kind => TemplateNodeKind.Tools;
}

public sealed class ChooseNode(IReadOnlyList<string> options) : TemplateNode
{
    public IReadOnlyList<string> Options { get; } = options;
    public override TemplateNodeKind Kind => TemplateNodeKind.Choose;
}

public sealed class ShellNode(string command) : TemplateNode
{
    public string Command { get; } = command;
    public override TemplateNodeKind Kind => TemplateNodeKind.Shell;
}

public sealed class GotoNode(string label) : TemplateNode
{
    public string Label { get; } = label;
    public override TemplateNodeKind Kind => TemplateNodeKind.Goto;
}

public sealed class ParsedTemplate
{
    public required IReadOnlyList<TemplateNode> Nodes { get; init; }
    public required IReadOnlyDictionary<string, int> Labels { get; init; }
}
