using System.ComponentModel.DataAnnotations;
using Mars.Core.Attributes;
using Mars.Nodes.Core;
using Mars.Nodes.Core.Fields;

namespace Mars.SberDevApiPlugin.Front.Nodes.GigaChat;

[FunctionApiDocument("./_plugin/Mars.SberDevApiPlugin/nodes/docs/GigaChatRequestNode/GigaChatRequestNode{.lang}.md")]
[Display(GroupName = "ai")]
public class GigaChatRequestNode : Node
{
    public const string ModelsApiEndpoint = "/api/SberDevApiPlugin/GigaChat/models";

    public InputConfig<GigaChatConfigNode> Config { get; set; }

    public string Prompt { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
    public string ModelId { get; set; } = "GigaChat";

    public GigaChatRequestNode()
    {
        Color = "#66aeb6";
        Inputs = [new()];
        Outputs = [new()];
        Icon = "/_plugin/Mars.SberDevApiPlugin/nodes/img/giga-chat-api-CGMEeF0X.svg";
    }

}
