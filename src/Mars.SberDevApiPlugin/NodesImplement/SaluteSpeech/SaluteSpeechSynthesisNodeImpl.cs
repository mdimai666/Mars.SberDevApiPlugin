using Mars.Core.Extensions;
using Mars.Nodes.Core;
using Mars.Nodes.Host.Shared;
using Mars.SberDevApiPlugin.Front.Nodes.SaluteSpeech;
using Mars.SberDevApiPlugin.Services;

namespace Mars.SberDevApiPlugin.NodesImplement.SaluteSpeech;

internal class SaluteSpeechSynthesisNodeImpl : INodeImplement<SaluteSpeechSynthesisNode>
{
    private readonly SaluteSpeechManager _saluteSpeechManager;

    public SaluteSpeechSynthesisNode Node { get; }
    public IRuntimeNodeScope RNS { get; set; }
    Node INodeImplement.Node => Node;

    public SaluteSpeechSynthesisNodeImpl(SaluteSpeechSynthesisNode node, IRuntimeNodeScope rns, SaluteSpeechManager saluteSpeechManager)
    {
        Node = node;
        RNS = rns;
        _saluteSpeechManager = saluteSpeechManager;
        Node.Config = RNS.GetConfig(node.Config);
    }

    public async Task Execute(NodeMsg input, ExecuteAction callback, ExecutionParameters parameters)
    {
        var client = _saluteSpeechManager.GetClient(Node.Config.Value ?? throw new ArgumentNullException("config not exist"));

        var maxChars = 10_000;

        if (input.Payload is null) throw new ArgumentException("Payload is null");

        var text = input.Payload.ToString()!.TextEllipsis(maxChars);

        RNS.Status(new() { Text = "generate..." });
        var bytes = await client.SynthesizeToBytesWithCacheAsync(text, language: Node.Language, voice: Node.VoiceId);

        input.Payload = bytes;

        RNS.Status(new() { Text = $"complete {bytes.Length.ToHumanizedSize()}" });

        callback(input);
    }
}
