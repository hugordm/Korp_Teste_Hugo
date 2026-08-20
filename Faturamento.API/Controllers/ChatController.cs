using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace Faturamento.API.Controllers;

// [ApiController] liga comportamentos automáticos de API REST nesta classe,
// como a validação automática do modelo recebido no corpo da requisição.
[ApiController]
// [Route("api/[controller]")] -> "ChatController" vira a rota "api/chat".
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string? _anthropicApiKey;

    // system prompt fixo, definido uma vez aqui: ele dá à Anthropic o
    // CONTEXTO de que este chat é sobre o sistema KORP especificamente
    // (produtos, notas fiscais, impressão), em vez de ser um assistente
    // genérico sem noção nenhuma do domínio. É isso que faz o Claude
    // responder "o saldo de um produto é abatido quando a nota é impressa"
    // em vez de uma resposta genérica sobre "o que é uma nota fiscal" —
    // ele já "sabe", por este prompt, como o nosso sistema específico
    // funciona. Pedimos explicitamente "sem markdown" porque o chat do
    // Angular vai exibir a resposta como texto puro (sem processar
    // ** negrito **, listas numeradas, etc.) — se a Anthropic mandasse
    // markdown, o usuário veria os símbolos (asteriscos, hifens) soltos na
    // tela em vez de formatação de verdade.
    private const string SystemPrompt =
        "Você é um assistente do sistema KORP de notas fiscais. Você ajuda com dúvidas sobre: " +
        "cadastro de produtos (código, descrição, saldo), cadastro de notas fiscais (numeração " +
        "sequencial, status Aberta/Fechada, múltiplos itens), e impressão de notas (que abate o " +
        "saldo dos produtos e fecha a nota). Responda SEMPRE em texto simples, SEM markdown, sem " +
        "asteriscos, sem listas numeradas, de forma direta e curta.";

    public ChatController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _anthropicApiKey = configuration["ANTHROPIC_API_KEY"] ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
    }

    // [HttpPost] atende "POST /api/chat". Recebe { "mensagem": "..." } e
    // devolve { "resposta": "..." }, chamando a Anthropic com o system
    // prompt acima para manter as respostas relevantes ao sistema KORP.
    [HttpPost]
    public async Task<ActionResult<object>> Chat(ChatRequest request)
    {
        const string mensagemFallback =
            "Desculpe, não consegui responder agora. Tente novamente em instantes.";

        if (string.IsNullOrWhiteSpace(_anthropicApiKey))
        {
            return Ok(new { resposta = mensagemFallback });
        }

        var corpoRequisicao = new
        {
            model = "claude-haiku-4-5-20251001",
            max_tokens = 300,
            system = SystemPrompt,
            messages = new[]
            {
                new { role = "user", content = request.Mensagem }
            }
        };

        try
        {
            var httpClient = _httpClientFactory.CreateClient("AnthropicAPI");

            var mensagemRequisicao = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
            {
                Content = JsonContent.Create(corpoRequisicao)
            };
            mensagemRequisicao.Headers.Add("x-api-key", _anthropicApiKey);

            var resposta = await httpClient.SendAsync(mensagemRequisicao);

            if (!resposta.IsSuccessStatusCode)
            {
                // Falha da própria Anthropic (ex: 400, 429, 529 sobrecarregada):
                // devolvemos a mensagem de fallback amigável em vez de
                // propagar um erro técnico para o usuário do chat.
                return Ok(new { resposta = mensagemFallback });
            }

            var corpoResposta = await resposta.Content.ReadFromJsonAsync<AnthropicMessageResponse>();
            var textoResposta = corpoResposta?.Content?.FirstOrDefault()?.Text?.Trim();

            return Ok(new { resposta = string.IsNullOrWhiteSpace(textoResposta) ? mensagemFallback : textoResposta });
        }
        catch (HttpRequestException)
        {
            // Falha de rede/infraestrutura ao contatar a Anthropic (timeout,
            // serviço fora do ar etc.) — mesmo tratamento de fallback, para
            // o chat nunca quebrar a tela do usuário por causa disso.
            return Ok(new { resposta = mensagemFallback });
        }
    }
}

// DTO usado para receber o corpo do endpoint de chat: { "mensagem": "..." }.
public class ChatRequest
{
    public string Mensagem { get; set; } = string.Empty;
}
