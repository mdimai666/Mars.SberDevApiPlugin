using System.Diagnostics;
using GigaChatAPI.Models;
using Mars.Core.Extensions;
using Mars.Nodes.Core;
using Mars.Nodes.Core.Implements;
using Mars.SberDevApiPlugin.Front.Nodes.GigaChat;
using Mars.SberDevApiPlugin.Services;

namespace Mars.SberDevApiPlugin.GigaChat.NodesImplement;

internal class GigaChatRequestNodeImpl : INodeImplement<GigaChatRequestNode>, INodeImplement
{
    private readonly GigaChatManager _gigaChatManager;

    public GigaChatRequestNode Node { get; }
    public IRED RED { get; set; }
    Node INodeImplement<Node>.Node => Node;

    public GigaChatRequestNodeImpl(GigaChatRequestNode node, IRED red, GigaChatManager gigaChatManager)
    {
        Node = node;
        RED = red;
        _gigaChatManager = gigaChatManager;
        Node.Config = RED.GetConfig(node.Config);
    }

    public async Task Execute(NodeMsg input, ExecuteAction callback, ExecutionParameters parameters)
    {
        var client = _gigaChatManager.GetClient(Node.Config.Value ?? throw new ArgumentNullException("config not exist"));

        var prompt = Node.Prompt.IsNullOrEmpty() ? input.Payload.ToString()! : Node.Prompt;

        var sw = new Stopwatch();
        sw.Start();
        RED.Status(new NodeStatus("think..."));

        List<ChatMessage>? conversationHistory = null;

        if (Node.SystemPrompt.IsNotNullOrEmpty())
        {
            conversationHistory =
            [
                new ChatMessage
                {
                    Role = "system",
                    Content = Node.SystemPrompt
                }
            ];
        }

        var response = await client.SendMessageFullResponseAsync(prompt,
                                                        model: Node.ModelId,
                                                        conversationHistory: conversationHistory,
                                                        cancellationToken: parameters.CancellationToken);

        var llmText = response.Choices[0].Message.Content;

        input.Payload = llmText;

        callback(input);

        sw.Stop();
        var totalTime = sw.ElapsedMilliseconds > 1000 ? $"{sw.Elapsed.TotalSeconds:0.0}s" : $"{sw.ElapsedMilliseconds / 1000:0.00}ms";
        RED.Status(new NodeStatus(totalTime + ", tokens: " + response.Usage.TotalTokens));
    }
}
