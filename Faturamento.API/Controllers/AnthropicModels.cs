using System.Text.Json.Serialization;

namespace Faturamento.API.Controllers;

// Classes usadas só para desserializar a resposta da API "Messages" da
// Anthropic (POST https://api.anthropic.com/v1/messages), compartilhadas
// entre NotasFiscaisController (endpoint "validar") e ChatController.
// Modelamos apenas o campo que realmente usamos (o texto gerado) — a
// resposta completa da Anthropic tem outros campos (id, model, usage,
// stop_reason etc.) que não precisamos ler aqui.
public class AnthropicMessageResponse
{
    [JsonPropertyName("content")]
    public List<AnthropicContentBlock> Content { get; set; } = new();
}

public class AnthropicContentBlock
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}
