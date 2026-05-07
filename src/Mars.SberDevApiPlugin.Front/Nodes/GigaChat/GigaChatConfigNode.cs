using System.ComponentModel.DataAnnotations;
using Mars.Core.Attributes;
using Mars.Nodes.Core.Nodes;

namespace Mars.SberDevApiPlugin.Front.Nodes.GigaChat;

[FunctionApiDocument("./_plugin/Mars.SberDevApiPlugin/nodes/docs/GigaChatConfigNode/GigaChatConfigNode{.lang}.md")]
[Display(GroupName = "ai")]
public class GigaChatConfigNode : ConfigNode
{
    [Required]
    public string AuthKey { get; set; } = "";
}
