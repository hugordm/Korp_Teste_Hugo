using Estoque.API.Data;
using Estoque.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Estoque.API.Controllers;

// [ApiController] liga vários comportamentos automáticos de API REST nesta classe:
// por exemplo, validação automática do modelo recebido no corpo da requisição
// (se algo obrigatório estiver faltando, o ASP.NET Core já responde 400 Bad Request
// sozinho, sem precisarmos checar isso manualmente em cada método).
[ApiController]
// [Route("api/[controller]")] define o prefixo da URL para todos os endpoints
// desta classe. "[controller]" é substituído pelo nome da classe sem o sufixo
// "Controller" — ou seja, "ProdutosController" vira a rota "api/produtos".
[Route("api/[controller]")]
public class ProdutosController : ControllerBase
{
    // Campo privado e "readonly" (só pode ser definido no construtor) que guarda
    // a instância do DbContext que este controller vai usar para falar com o banco.
    private readonly EstoqueDbContext _context;

    // IHttpClientFactory é o serviço que sabe criar HttpClients "geridos"
    // pelo framework a partir dos registros feitos em Program.cs — aqui,
    // usado para pegar o HttpClient "AnthropicAPI" na hora de chamar a API
    // da Anthropic no endpoint de geração de descrição.
    private readonly IHttpClientFactory _httpClientFactory;

    // Chave da API da Anthropic, lida da configuração (que inclui variáveis
    // de ambiente automaticamente — ver comentário em Program.cs sobre
    // ANTHROPIC_API_KEY). Guardada aqui no construtor para não precisar
    // reler a configuração a cada requisição.
    private readonly string? _anthropicApiKey;

    // Construtor do controller. O ASP.NET Core usa Injeção de Dependência (DI):
    // como registramos "builder.Services.AddDbContext<EstoqueDbContext>(...)" no
    // Program.cs, o framework cria automaticamente um EstoqueDbContext e o passa
    // aqui sempre que uma requisição chega para este controller — não precisamos
    // instanciar o DbContext manualmente com "new". O mesmo vale para
    // IHttpClientFactory (registrado automaticamente por AddHttpClient) e
    // IConfiguration (sempre disponível para injeção no ASP.NET Core).
    public ProdutosController(EstoqueDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _anthropicApiKey = configuration["ANTHROPIC_API_KEY"] ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
    }

    // [HttpGet] marca este método para responder a requisições GET.
    // Sem parâmetro extra na rota, ele atende "GET /api/produtos".
    // O retorno é Task<ActionResult<...>> porque o método é assíncrono (async):
    // ele "promete" devolver o resultado no futuro, sem travar a thread do servidor
    // enquanto espera a resposta do banco de dados. Isso é importante porque libera
    // o servidor para atender outras requisições enquanto essa consulta acontece.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Produto>>> GetProdutos()
    {
        // ToListAsync() executa a consulta "SELECT * FROM estoque.\"Produtos\""
        // no banco de forma assíncrona (o "await" pausa este método até o banco
        // responder, sem bloquear a thread). O resultado já volta como uma lista
        // em memória com todos os produtos cadastrados.
        var produtos = await _context.Produtos.ToListAsync();

        // Retornar o objeto diretamente faz o ASP.NET Core devolver
        // HTTP 200 OK com a lista de produtos serializada em JSON.
        return Ok(produtos);
    }

    // [HttpGet("{id}")] atende "GET /api/produtos/5", por exemplo.
    // "{id}" na rota vira o parâmetro "id" do método, e o ASP.NET Core já converte
    // o texto da URL para "int" automaticamente (se não for um número válido,
    // ele responde 400 Bad Request antes mesmo de entrar no método).
    [HttpGet("{id}")]
    public async Task<ActionResult<Produto>> GetProduto(int id)
    {
        // FindAsync busca pela chave primária (Id) de forma assíncrona.
        // É a forma mais eficiente de buscar por Id, pois o EF Core primeiro
        // confere se a entidade já está em memória antes de ir ao banco.
        var produto = await _context.Produtos.FindAsync(id);

        // Se não encontrou nenhum produto com esse Id, "produto" fica null.
        // Nesse caso devolvemos 404 Not Found, como pedido.
        if (produto == null)
        {
            return NotFound();
        }

        // Encontrou: devolve 200 OK com o produto em JSON.
        return Ok(produto);
    }

    // [HttpPost] atende "POST /api/produtos". O parâmetro "produto" vem do corpo
    // (body) da requisição em JSON; o ASP.NET Core desserializa automaticamente
    // o JSON recebido para um objeto Produto (isso é o "model binding").
    [HttpPost]
    public async Task<ActionResult<Produto>> PostProduto(Produto produto)
    {
        // Add() apenas marca o objeto como "novo" na memória do EF Core
        // (rastreado como Added); ele ainda não foi enviado ao banco.
        _context.Produtos.Add(produto);

        // SaveChangesAsync() é quem realmente gera e executa o INSERT no banco,
        // de forma assíncrona. É só depois dessa chamada que "produto.Id" recebe
        // o valor gerado automaticamente pelo Postgres (IDENTITY).
        await _context.SaveChangesAsync();

        // CreatedAtAction devolve 201 Created — o código correto para indicar que
        // um novo recurso foi criado — e inclui no cabeçalho "Location" a URL para
        // buscar esse novo produto (aponta para o método GetProduto, passando o
        // Id gerado), além do próprio produto criado no corpo da resposta.
        return CreatedAtAction(nameof(GetProduto), new { id = produto.Id }, produto);
    }

    // [HttpPut("{id}")] atende "PUT /api/produtos/5". PUT representa uma
    // atualização completa do recurso: o cliente manda o objeto inteiro
    // com os novos valores.
    [HttpPut("{id}")]
    public async Task<IActionResult> PutProduto(int id, Produto produto)
    {
        // Busca o produto atualmente salvo no banco para confirmar que ele existe
        // antes de tentar atualizar (evita atualizar algo que não existe).
        var produtoExistente = await _context.Produtos.FindAsync(id);

        if (produtoExistente == null)
        {
            return NotFound();
        }

        // Copia os novos valores recebidos no corpo da requisição para a entidade
        // que o EF Core já está rastreando (produtoExistente). Como essa entidade
        // veio do FindAsync, o EF Core sabe que ela existe no banco e vai gerar
        // um UPDATE (não um INSERT) ao salvar.
        produtoExistente.Codigo = produto.Codigo;
        produtoExistente.Descricao = produto.Descricao;
        produtoExistente.Saldo = produto.Saldo;

        // Executa o UPDATE no banco de forma assíncrona.
        await _context.SaveChangesAsync();

        // 204 No Content é o código convencional para "atualizei com sucesso e
        // não tenho nada a mais para te devolver no corpo da resposta".
        return NoContent();
    }

    // [HttpDelete("{id}")] atende "DELETE /api/produtos/5".
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduto(int id)
    {
        var produto = await _context.Produtos.FindAsync(id);

        if (produto == null)
        {
            return NotFound();
        }

        // Remove() marca a entidade como "a ser excluída" (rastreada como Deleted).
        _context.Produtos.Remove(produto);

        // Executa o DELETE no banco de forma assíncrona.
        await _context.SaveChangesAsync();

        // 204 No Content também é o código padrão para exclusão bem-sucedida.
        return NoContent();
    }

    // [HttpPut("{id}/baixar-saldo")] atende "PUT /api/produtos/5/baixar-saldo".
    // Esse endpoint dedicado existe para representar explicitamente a operação
    // de negócio "dar baixa no estoque" (ex: quando uma nota fiscal é fechada),
    // em vez de depender do PUT genérico (que exigiria mandar o objeto Produto
    // inteiro e calcular o novo saldo no cliente, o que é menos seguro e mais
    // sujeito a erro).
    [HttpPut("{id}/baixar-saldo")]
    public async Task<IActionResult> BaixarSaldo(int id, BaixarSaldoRequest request)
    {
        var produto = await _context.Produtos.FindAsync(id);

        // Produto não existe: nada a baixar, devolve 404 Not Found.
        if (produto == null)
        {
            return NotFound();
        }

        // Validação de saldo insuficiente: antes de subtrair, conferimos se o
        // Saldo atual do produto é maior ou igual à Quantidade pedida.
        // Sem essa checagem, o Saldo poderia ficar negativo, o que não faz
        // sentido para uma quantidade em estoque (não é possível vender mais
        // do que existe fisicamente disponível). Se a quantidade solicitada
        // for maior que o saldo disponível, rejeitamos a operação com 400 Bad
        // Request e uma mensagem explicando o motivo, sem alterar nada no banco.
        if (produto.Saldo < request.Quantidade)
        {
            return BadRequest($"Saldo insuficiente. Saldo atual: {produto.Saldo}, quantidade solicitada: {request.Quantidade}.");
        }

        // Saldo suficiente: subtrai a quantidade baixada do saldo atual.
        produto.Saldo -= request.Quantidade;

        // Executa o UPDATE no banco de forma assíncrona.
        await _context.SaveChangesAsync();

        // 204 No Content indica que a baixa foi aplicada com sucesso e não há
        // corpo de resposta a devolver.
        return NoContent();
    }

    // [HttpPost("gerar-descricao")] atende "POST /api/produtos/gerar-descricao".
    //
    // Este endpoint existe para o Angular NUNCA precisar falar diretamente
    // com a API da Anthropic (api.anthropic.com). O fluxo é:
    //   Angular --(código + nomeBase)--> Estoque.API --(chave da API)--> Anthropic
    // O Estoque.API funciona como um "proxy seguro": é ele quem guarda a
    // chave da Anthropic (lida do ambiente/servidor, nunca enviada ao
    // navegador) e quem efetivamente inclui essa chave na requisição. Se o
    // Angular chamasse a Anthropic diretamente, a chave teria que estar em
    // algum lugar do código JavaScript entregue ao navegador — e qualquer
    // usuário poderia abrir o DevTools e copiá-la, o que exporia a chave
    // publicamente (e o consumo/custo associado a ela).
    [HttpPost("gerar-descricao")]
    public async Task<IActionResult> GerarDescricao(GerarDescricaoRequest request)
    {
        // Sem a chave configurada, nem tentamos chamar a Anthropic — devolvemos
        // logo um 500 com uma mensagem clara, em vez de deixar a requisição
        // falhar de forma confusa mais adiante.
        if (string.IsNullOrWhiteSpace(_anthropicApiKey))
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                "A geração de descrição por IA não está disponível: a chave da API da Anthropic (ANTHROPIC_API_KEY) não foi configurada no servidor.");
        }

        var httpClient = _httpClientFactory.CreateClient("AnthropicAPI");

        var prompt = $"Gere uma descrição curta e comercial (max 15 palavras) para um produto de código {request.Codigo} e nome base '{request.NomeBase}'. Responda APENAS com a descrição, sem explicações.";

        // Corpo da requisição no formato esperado pela API "Messages" da
        // Anthropic (POST /v1/messages). "model", "max_tokens" e "messages"
        // são os campos exigidos pela API; "role: user" indica que este é o
        // conteúdo enviado pelo usuário (e não uma resposta do assistente).
        var corpoRequisicao = new
        {
            model = "claude-haiku-4-5-20251001",
            max_tokens = 100,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        try
        {
            // Montamos a requisição manualmente (em vez de PostAsJsonAsync)
            // porque precisamos adicionar o header "x-api-key" com a chave —
            // esse header não faz parte da configuração padrão do
            // HttpClient "AnthropicAPI" em Program.cs porque a chave só é
            // conhecida aqui, via configuração/injeção de dependência.
            var mensagemRequisicao = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
            {
                Content = JsonContent.Create(corpoRequisicao)
            };
            mensagemRequisicao.Headers.Add("x-api-key", _anthropicApiKey);

            var resposta = await httpClient.SendAsync(mensagemRequisicao);

            if (!resposta.IsSuccessStatusCode)
            {
                var detalhe = await resposta.Content.ReadAsStringAsync();
                return StatusCode(StatusCodes.Status500InternalServerError,
                    $"A API da Anthropic recusou a requisição (status {(int)resposta.StatusCode}). Detalhe: {detalhe}");
            }

            var corpoResposta = await resposta.Content.ReadFromJsonAsync<AnthropicMessageResponse>();

            // A resposta da Anthropic traz o texto gerado em
            // "content[0].text". Usamos FirstOrDefault por segurança: se a
            // lista vier vazia por algum motivo, evitamos uma exceção de
            // índice fora do intervalo e caímos no tratamento de erro
            // abaixo.
            var descricao = corpoResposta?.Content?.FirstOrDefault()?.Text?.Trim();

            if (string.IsNullOrWhiteSpace(descricao))
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "A API da Anthropic respondeu, mas sem nenhum texto de descrição.");
            }

            return Ok(new { descricao });
        }
        catch (HttpRequestException ex)
        {
            // Falha de rede/infraestrutura ao tentar contatar a Anthropic
            // (timeout, DNS, serviço fora do ar etc.) — nenhuma resposta
            // HTTP válida chegou a ser recebida.
            return StatusCode(StatusCodes.Status500InternalServerError,
                $"Não foi possível contatar a API da Anthropic no momento. Detalhe técnico: {ex.Message}");
        }
    }
}

// DTO usado para receber o corpo do endpoint "gerar-descricao":
// { "codigo": "...", "nomeBase": "..." }.
public class GerarDescricaoRequest
{
    public string Codigo { get; set; } = string.Empty;
    public string NomeBase { get; set; } = string.Empty;
}

// Classes usadas só para desserializar a resposta da API "Messages" da
// Anthropic. Modelamos apenas os campos que realmente usamos (o texto
// gerado) — a resposta completa da Anthropic tem outros campos (id, model,
// usage, stop_reason etc.) que não precisamos ler aqui.
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

// DTO (Data Transfer Object) simples usado só para representar o corpo (body)
// esperado pelo endpoint de baixa de saldo: um JSON como { "quantidade": 5 }.
// Usar uma classe própria em vez de receber um "int" solto no body deixa o
// contrato da API explícito e evita ambiguidade na hora do model binding.
public class BaixarSaldoRequest
{
    public int Quantidade { get; set; }
}
