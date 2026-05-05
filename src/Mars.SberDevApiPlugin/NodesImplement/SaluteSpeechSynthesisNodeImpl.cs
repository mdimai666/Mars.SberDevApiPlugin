using Mars.Core.Extensions;
using Mars.Nodes.Core;
using Mars.Nodes.Core.Implements;
using Mars.SberDevApiPlugin.Front.Nodes;
using Mars.SberDevApiPlugin.Services;

namespace Mars.SberDevApiPlugin.NodesImplement;

internal class SaluteSpeechSynthesisNodeImpl : INodeImplement<SaluteSpeechSynthesisNode>, INodeImplement
{
    private readonly SaluteSpeechManager _saluteSpeechManager;

    public SaluteSpeechSynthesisNode Node { get; }
    public IRED RED { get; set; }
    Node INodeImplement<Node>.Node => Node;

    public SaluteSpeechSynthesisNodeImpl(SaluteSpeechSynthesisNode node, IRED red, SaluteSpeechManager saluteSpeechManager)
    {
        Node = node;
        RED = red;
        _saluteSpeechManager = saluteSpeechManager;
        Node.Config = RED.GetConfig(node.Config);
    }

    public async Task Execute(NodeMsg input, ExecuteAction callback, ExecutionParameters parameters)
    {
        var client = _saluteSpeechManager.GetClient(Node.Config.Value ?? throw new ArgumentNullException("config not exist"));

        var maxChars = 10_000;

        if (input.Payload is null) throw new ArgumentException("Payload is null");

        var text = input.Payload.ToString()!.TextEllipsis(maxChars);

        RED.Status(new() { Text = "generate..." });
        var bytes = await client.SynthesizeToBytesWithCacheAsync(text, language: Node.Language, voice: Node.VoiceId);

        input.Payload = bytes;

        RED.Status(new() { Text = $"complete {bytes.Length.ToHumanizedSize()}" });

        callback(input);
    }
}
