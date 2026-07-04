using System.ComponentModel.DataAnnotations;
using Mars.Core.Attributes;
using Mars.Nodes.Core.Nodes.Common;

namespace Mars.SberDevApiPlugin.Front.Nodes.SaluteSpeech;

[FunctionApiDocument("./_plugin/Mars.SberDevApiPlugin/nodes/docs/SaluteSpeechConfigNode/SaluteSpeechConfigNode{.lang}.md")]
[Display(GroupName = "voice")]
public class SaluteSpeechConfigNode : ConfigNode
{
    [Required]
    public string AuthKey { get; set; } = "";
}
