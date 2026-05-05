using System.ComponentModel.DataAnnotations;
using Mars.Core.Attributes;
using Mars.Nodes.Core;
using Mars.Nodes.Core.Fields;

namespace Mars.SberDevApiPlugin.Front.Nodes;

[FunctionApiDocument("./_plugin/Mars.SberDevApiPlugin/nodes/docs/SaluteSpeechSynthesisNode/SaluteSpeechSynthesisNode{.lang}.md")]
[Display(GroupName = "voice")]
public class SaluteSpeechSynthesisNode : Node
{
    public InputConfig<SaluteSpeechConfigNode> Config { get; set; }

    public string VoiceId { get; set; } = "Nec_24000";
    public string Language { get; set; } = "ru-RU";

    public SaluteSpeechSynthesisNode()
    {
        Color = "#3fa0bb";
        Inputs = [new()];
        Outputs = [new()];
        Icon = "/_plugin/Mars.SberDevApiPlugin/nodes/img/icon_SaluteSppechApi.png";
    }

    public static Dictionary<string, string> AvailableVoices = new()
    {
        { "Nec_24000", "Наталья (24 кГц)" },
        { "Nec_8000", "Наталья (8 кГц)" },
        { "Bys_24000", "Борис (24 кГц)" },
        { "Bys_8000", "Борис (8 кГц)" },
        { "May_24000", "Марфа (24 кГц)" },
        { "May_8000", "Марфа (8 кГц)" },
        { "Tur_24000", "Тарас (24 кГц)" },
        { "Tur_8000", "Тарас (8 кГц)" },
        { "Ost_24000", "Александра (24 кГц)" },
        { "Ost_8000", "Александра (8 кГц)" },
        { "Pon_24000", "Сергей (24 кГц)" },
        { "Pon_8000", "Сергей (8 кГц)" },
        { "Kin_24000", "Kira (24 кГц) — синтез английской речи" },
        { "Kin_8000", "Kira (8 кГц) — синтез английской речи" }
    };

    public static Dictionary<string, string> AvailableLanguages = new()
    {
        { "ru-RU", "Русский" },
        { "en-US", "Английский" },
        { "kk-KZ", "Казахский" },
        { "ky-KG", "Киргизский" },
        { "uz-UZ", "Узбекский" },
        { "pt-PT", "Португальский" },
        { "pl-PL", "Польский" },
        { "nl-NL", "Нидерландский" },
        { "de-DE", "Немецкий" },
        { "es-ES", "Испанский" },
        { "fr-FR", "Французский" },
        { "it-IT", "Итальянский" }
    };
}
