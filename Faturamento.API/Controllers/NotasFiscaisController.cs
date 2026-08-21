using Faturamento.API.Data;
using Faturamento.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Faturamento.API.Controllers;

// [ApiController] liga comportamentos automáticos de API REST nesta classe,
// como a validação automática do modelo recebido no corpo da requisição
// (se algo obrigatório estiver faltando, o ASP.NET Core já responde
// 400 Bad Request sozinho, sem precisarmos checar isso manualmente).
[ApiController]
// [Route("api/[controller]")] define o prefixo da URL para todos os endpoints
// desta classe. "[controller]" é substituído pelo nome da classe sem o sufixo
// "Controller" — ou seja, "NotasFiscaisController" vira a rota "api/notasfiscais".
[Route("api/[controller]")]
public class NotasFiscaisController : ControllerBase
{
    // Campo privado e "readonly" (só pode ser definido no construtor) que guarda
    // a instância do DbContext que este controller vai usar para falar com o banco.
    private readonly FaturamentoDbContext _context;

    // IHttpClientFactory é o serviço que sabe criar HttpClients "geridos" pelo
    // framework, a partir dos registros feitos em Program.cs (como o
    // "EstoqueAPI" configurado com AddHttpClient). Guardamos só a factory
    // aqui, e não um HttpClient já pronto, porque é a própria factory quem
    // deve criar o client no momento do uso (CreateClient("EstoqueAPI")) —
    // é assim que ela consegue reciclar/reutilizar conexões por baixo dos
    // panos e evitar o esgotamento de sockets mencionado em Program.cs.
    private readonly IHttpClientFactory _httpClientFactory;

    // Chave da API da Anthropic, lida da configuração (que inclui variáveis
    // de ambiente automaticamente — ver comentário em Program.cs sobre
    // ANTHROPIC_API_KEY). Usada pelo endpoint "validar" abaixo.
    private readonly string? _anthropicApiKey;

    // Construtor do controller. O ASP.NET Core usa Injeção de Dependência (DI):
    // como o FaturamentoDbContext já está registrado em Program.cs
    // ("builder.Services.AddDbContext<FaturamentoDbContext>(...)"), o framework
    // cria automaticamente uma instância dele e a passa aqui sempre que uma
    // requisição chega para este controller — não precisamos instanciar o
    // DbContext manualmente com "new". O mesmo vale para o IHttpClientFactory,
    // que é registrado automaticamente pelo framework assim que chamamos
    // "builder.Services.AddHttpClient(...)" em Program.cs, e para
    // IConfiguration, sempre disponível para injeção no ASP.NET Core.
    public NotasFiscaisController(FaturamentoDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _anthropicApiKey = configuration["ANTHROPIC_API_KEY"] ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
    }

    // [HttpGet] marca este método para responder a requisições GET.
    // Sem parâmetro extra na rota, ele atende "GET /api/notasfiscais".
    // O retorno é Task<ActionResult<...>> porque o método é assíncrono (async):
    // ele "promete" devolver o resultado no futuro sem travar a thread do
    // servidor enquanto espera a resposta do banco, liberando-a para atender
    // outras requisições nesse meio tempo.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotaFiscal>>> GetNotasFiscais()
    {
        // Include(n => n.Itens) faz o EF Core trazer, na mesma consulta, os itens
        // de cada nota fiscal (via JOIN), em vez de deixar "Itens" vazio.
        // Sem o Include, a propriedade de navegação "Itens" viria como uma lista
        // vazia (não é carregada automaticamente), e para preenchê-la depois
        // precisaríamos fazer uma consulta extra para cada nota — o chamado
        // problema N+1 (1 consulta para as notas + N consultas, uma por nota,
        // para buscar os itens de cada uma). Usar Include resolve tudo em uma
        // única ida ao banco, o que é bem mais eficiente.
        var notasFiscais = await _context.NotasFiscais
            .Include(n => n.Itens)
            .ToListAsync();

        // Coleta todos os ProdutoId únicos de todos os itens de todas as
        // notas (SelectMany "achata" a lista de listas de itens em uma única
        // lista de itens; Distinct evita pedir o mesmo produto mais de uma
        // vez ao Estoque.API, mesmo que ele apareça em vários itens/notas
        // diferentes) e busca todos eles de uma vez no Estoque.API.
        var produtoIds = notasFiscais
            .SelectMany(n => n.Itens)
            .Select(item => item.ProdutoId)
            .Distinct();

        var produtosPorId = await BuscarProdutosAsync(produtoIds);

        // Preenche Código e Descrição de cada item com o que veio do
        // Estoque.API. Se o Id não estiver no dicionário (produto excluído
        // no Estoque, ou o Estoque.API estava indisponível e o dicionário
        // veio vazio), marcamos como "Produto não encontrado" em vez de
        // deixar null, para deixar claro na tela que a informação não pôde
        // ser obtida.
        foreach (var item in notasFiscais.SelectMany(n => n.Itens))
        {
            if (produtosPorId.TryGetValue(item.ProdutoId, out var produto))
            {
                item.CodigoProduto = produto.Codigo;
                item.DescricaoProduto = produto.Descricao;
            }
            else
            {
                item.CodigoProduto = "Produto não encontrado";
                item.DescricaoProduto = "Produto não encontrado";
            }
        }

        // Retornar o objeto diretamente faz o ASP.NET Core devolver
        // HTTP 200 OK com a lista de notas fiscais (e seus itens) em JSON.
        return Ok(notasFiscais);
    }

    // Busca, numa única chamada HTTP ao Estoque.API, os dados resumidos
    // (Id, Código, Descrição) de todos os produtos cujos Ids são passados em
    // "produtoIds". O resultado vem como um Dictionary indexado por Id para
    // permitir busca O(1) na hora de preencher cada item da nota fiscal, em
    // vez de varrer a lista inteira de produtos a cada item.
    //
    // Por que buscar em lote (uma query string com todos os Ids) em vez de
    // uma chamada por item: se uma nota tem 10 itens, uma chamada por item
    // significaria 10 requisições HTTP separadas ao Estoque.API só para
    // montar UMA listagem — e isso multiplica pela quantidade de notas na
    // tela. Cada requisição HTTP entre microsserviços tem um custo de
    // latência de rede bem maior que uma chamada de método local, então
    // esse padrão (N+1 aplicado a chamadas entre serviços) ficaria lento e
    // sobrecarregaria o Estoque.API rapidamente. Buscando todos os Ids
    // únicos de uma vez, o custo de rede é pago uma única vez,
    // independentemente de quantos itens/notas estamos exibindo.
    //
    // Por que a falha aqui NÃO lança exceção (diferente do endpoint
    // "imprimir", que propaga o erro com 502 Bad Gateway): imprimir uma nota
    // é uma operação crítica que dá baixa real no estoque, então faz sentido
    // travá-la se o Estoque.API estiver fora do ar (senão o dado ficaria
    // inconsistente). Já listar ou visualizar notas fiscais é uma operação
    // de LEITURA, sem efeito colateral nenhum — é só exibição de dados. Se o
    // Estoque.API estiver indisponível nesse momento, é uma escolha de
    // design melhorar a experiência do usuário deixando a listagem de notas
    // continuar funcionando normalmente (só sem Código/Descrição do produto,
    // que ficam como "Produto não encontrado") em vez de quebrar a tela
    // inteira por causa de uma informação complementar.
    private async Task<Dictionary<int, ProdutoResumo>> BuscarProdutosAsync(IEnumerable<int> produtoIds)
    {
        // Ids únicos, já sem duplicatas (o chamador também já faz Distinct,
        // mas repetimos aqui para o método ser seguro mesmo se chamado com
        // uma lista não tratada) e transformados em texto para montar a
        // query string "1,2,3".
        var idsUnicos = produtoIds.Distinct().ToList();

        // Sem nenhum Id para buscar, não há motivo para chamar o
        // Estoque.API — devolve um dicionário vazio direto.
        if (idsUnicos.Count == 0)
        {
            return new Dictionary<int, ProdutoResumo>();
        }

        var queryString = string.Join(",", idsUnicos);

        try
        {
            var httpClient = _httpClientFactory.CreateClient("EstoqueAPI");

            var produtos = await httpClient.GetFromJsonAsync<List<ProdutoResumo>>(
                $"api/produtos/por-ids?ids={queryString}");

            // GetFromJsonAsync pode devolver null se o corpo da resposta for
            // literalmente "null" (não deveria acontecer aqui, mas o
            // compilador exige tratarmos esse caso). Usamos "?? new()" para
            // nunca devolver um dicionário nulo.
            return (produtos ?? new List<ProdutoResumo>())
                .ToDictionary(produto => produto.Id);
        }
        catch (HttpRequestException)
        {
            // Falha de rede/infraestrutura ao contatar o Estoque.API
            // (serviço fora do ar, timeout, DNS etc.). Não propagamos a
            // exceção: devolvemos um dicionário vazio para que a listagem de
            // notas fiscais continue funcionando mesmo sem os dados de
            // produto (ver explicação completa no comentário acima do
            // método).
            return new Dictionary<int, ProdutoResumo>();
        }
    }

    // [HttpGet("{id}")] atende "GET /api/notasfiscais/5", por exemplo.
    // "{id}" na rota vira o parâmetro "id" do método, e o ASP.NET Core já
    // converte o texto da URL para "int" automaticamente.
    [HttpGet("{id}")]
    public async Task<ActionResult<NotaFiscal>> GetNotaFiscal(int id)
    {
        // Aqui não dá para usar FindAsync (que só busca pela chave primária)
        // porque também precisamos do Include para trazer os itens junto.
        // Por isso usamos Where + FirstOrDefaultAsync: filtra pelo Id e traz,
        // na mesma consulta ao banco, a nota e seus itens relacionados —
        // de novo evitando uma segunda consulta separada só para os itens.
        var notaFiscal = await _context.NotasFiscais
            .Include(n => n.Itens)
            .FirstOrDefaultAsync(n => n.Id == id);

        // Se não encontrou nenhuma nota com esse Id, "notaFiscal" fica null.
        // Nesse caso devolvemos 404 Not Found, como pedido.
        if (notaFiscal == null)
        {
            return NotFound();
        }

        // Mesmo enriquecimento com Código/Descrição do produto feito em
        // GetNotasFiscais (ver comentários detalhados em BuscarProdutosAsync
        // logo abaixo), aqui só para os itens desta única nota.
        var produtoIds = notaFiscal.Itens.Select(item => item.ProdutoId).Distinct();
        var produtosPorId = await BuscarProdutosAsync(produtoIds);

        foreach (var item in notaFiscal.Itens)
        {
            if (produtosPorId.TryGetValue(item.ProdutoId, out var produto))
            {
                item.CodigoProduto = produto.Codigo;
                item.DescricaoProduto = produto.Descricao;
            }
            else
            {
                item.CodigoProduto = "Produto não encontrado";
                item.DescricaoProduto = "Produto não encontrado";
            }
        }

        // Encontrou: devolve 200 OK com a nota fiscal (e seus itens) em JSON.
        return Ok(notaFiscal);
    }

    // [HttpPost] atende "POST /api/notasfiscais". O parâmetro "notaFiscal" vem
    // do corpo (body) da requisição em JSON; o ASP.NET Core desserializa
    // automaticamente o JSON recebido para um objeto NotaFiscal, incluindo a
    // lista de itens dentro dele (model binding também preenche listas
    // aninhadas), permitindo criar a nota já com seus itens numa única
    // requisição.
    [HttpPost]
    public async Task<ActionResult<NotaFiscal>> PostNotaFiscal(NotaFiscal notaFiscal)
    {
        // Geramos o Numero no servidor, e não confiamos no valor que o cliente
        // eventualmente mande, por dois motivos principais:
        // 1) Concorrência/consistência: se dois clientes decidissem o próprio
        //    número, poderiam escolher o mesmo valor ao mesmo tempo, gerando
        //    números duplicados. O servidor é a única fonte de verdade sobre
        //    qual foi o último número usado.
        // 2) Segurança/integridade: o número da nota fiscal é uma regra de
        //    negócio (deve ser sequencial), não uma escolha do cliente. Se
        //    confiássemos no valor enviado, um cliente mal-intencionado (ou só
        //    um bug no front-end) poderia mandar números fora de ordem,
        //    repetidos ou até negativos.
        //
        // MaxAsync retorna o maior valor de "Numero" já existente na tabela.
        // Como a tabela pode estar vazia, usamos a versão que aceita nulo
        // (MaxAsync sobre um Select de int?) para não lançar exceção nesse caso.
        var maiorNumeroExistente = await _context.NotasFiscais
            .Select(n => (int?)n.Numero)
            .MaxAsync();

        // Se não houver nenhuma nota ainda (maiorNumeroExistente == null),
        // começamos em 1. Caso contrário, é o maior número existente mais 1.
        notaFiscal.Numero = (maiorNumeroExistente ?? 0) + 1;

        // O Status também é definido pelo servidor, ignorando qualquer valor
        // que o cliente tenha enviado no JSON: toda nota nova nasce "Aberta".
        // Isso garante que ninguém consiga criar uma nota já "Fechada" ou em
        // qualquer outro status pulando as regras de negócio do fechamento.
        notaFiscal.Status = "Aberta";

        // Add() marca a nota fiscal (e, em cascata, os itens dentro de
        // notaFiscal.Itens) como "novos" na memória do EF Core. Como cada item
        // já referencia a nota fiscal pai (relacionamento de navegação), o EF
        // Core entende sozinho que deve gravar a nota e todos os seus itens
        // juntos, numa única unidade de trabalho.
        _context.NotasFiscais.Add(notaFiscal);

        // SaveChangesAsync() é quem realmente gera e executa os INSERTs no
        // banco (um para a nota fiscal, um para cada item), de forma
        // assíncrona e dentro de uma mesma transação implícita. É só depois
        // dessa chamada que "notaFiscal.Id" (e o Id de cada item) recebe o
        // valor gerado automaticamente pelo Postgres.
        await _context.SaveChangesAsync();

        // CreatedAtAction devolve 201 Created — o código correto para indicar
        // que um novo recurso foi criado — e inclui no cabeçalho "Location" a
        // URL para buscar essa nova nota (aponta para o método
        // GetNotaFiscal, passando o Id gerado), além da própria nota criada
        // (já com Numero e Status definidos pelo servidor) no corpo da resposta.
        return CreatedAtAction(nameof(GetNotaFiscal), new { id = notaFiscal.Id }, notaFiscal);
    }

    // [HttpPost("{id}/imprimir")] atende "POST /api/notasfiscais/5/imprimir".
    // "Imprimir" aqui representa o processo de fechamento da nota: antes de
    // considerar a nota pronta/fechada, o Faturamento.API precisa avisar o
    // Estoque.API (outro microsserviço, outro processo, outro banco) para
    // dar baixa no saldo de cada produto vendido. É justamente essa
    // comunicação entre serviços — e o que fazer quando ela falha — que este
    // endpoint existe para demonstrar.
    [HttpPost("{id}/imprimir")]
    public async Task<ActionResult<NotaFiscal>> ImprimirNotaFiscal(int id)
    {
        // (a) Busca a nota com os itens incluídos (precisamos deles para saber
        // quais produtos e quantidades baixar no estoque) e confere se existe.
        var notaFiscal = await _context.NotasFiscais
            .Include(n => n.Itens)
            .FirstOrDefaultAsync(n => n.Id == id);

        if (notaFiscal == null)
        {
            return NotFound();
        }

        // (b) Regra de negócio: só faz sentido "imprimir"/fechar uma nota que
        // ainda está "Aberta". Isso evita imprimir a mesma nota duas vezes
        // (o que baixaria o estoque em dobro) ou imprimir uma nota já
        // cancelada, por exemplo.
        if (notaFiscal.Status != "Aberta")
        {
            return BadRequest("Nota fiscal não pode ser impressa pois não está com status Aberta.");
        }

        // Cria o HttpClient "EstoqueAPI" através da factory (ver Program.cs e
        // o comentário no campo _httpClientFactory acima para o motivo de
        // usarmos a factory em vez de "new HttpClient()").
        var httpClient = _httpClientFactory.CreateClient("EstoqueAPI");

        // (c) e (d) Para cada item da nota, chamamos o endpoint de baixa de
        // saldo do Estoque.API. Envolvemos tudo em um único try/catch porque
        // aqui é onde a comunicação entre os dois microsserviços pode falhar
        // de duas formas bem diferentes:
        //
        // 1) Falha de rede/infraestrutura: o Estoque.API está fora do ar, o
        //    container não subiu, houve timeout, DNS não resolveu "estoque-api",
        //    etc. Isso lança uma exceção (ex: HttpRequestException) ANTES de
        //    recebermos qualquer resposta HTTP.
        // 2) Falha de regra de negócio: o Estoque.API respondeu normalmente,
        //    mas com um status de erro (ex: 400 Bad Request, porque o saldo
        //    daquele produto é insuficiente). Aqui NÃO é lançada exceção — a
        //    chamada "deu certo" do ponto de vista de rede, só que o
        //    resultado foi negativo. Por isso conferimos
        //    "resposta.IsSuccessStatusCode" manualmente, e não confiamos
        //    apenas no try/catch para detectar esse caso.
        //
        // Em ambos os casos, a operação inteira precisa ser CANCELADA — não
        // continuamos para os próximos itens, e a nota NÃO é fechada. O
        // motivo é consistência de dados: se a nota tem 3 itens e o item 2
        // falha, não queremos deixar a nota "Fechada" com o saldo baixado só
        // dos itens 1 e (parcialmente) 2, mas não do item 3 — isso deixaria o
        // sistema em um estado inconsistente, difícil de auditar e corrigir
        // depois. Preferimos parar assim que o primeiro problema aparece e
        // devolver um erro claro para quem chamou, para que a operação possa
        // ser tentada novamente mais tarde (com o Estoque.API já de volta, ou
        // com o saldo já reposto).
        //
        // OBSERVAÇÃO IMPORTANTE (limitação conhecida): se o item 1 já tiver
        // sido baixado com sucesso no Estoque.API e o item 2 falhar, o saldo
        // do item 1 já foi decrementado lá e não é desfeito automaticamente
        // aqui — não há uma "transação distribuída" entre os dois bancos de
        // dados (cada microsserviço tem o seu). Resolver isso completamente
        // exigiria um mecanismo de compensação (padrão Saga, por exemplo: uma
        // chamada de "estorno" ao Estoque.API para cada baixa já aplicada).
        // Isso está fora do escopo deste teste, mas é importante saber que
        // esse é o próximo passo natural em um cenário de produção real.
        try
        {
            foreach (var item in notaFiscal.Itens)
            {
                // PutAsJsonAsync serializa o objeto anônimo para JSON
                // automaticamente (equivalente a { "quantidade": item.Quantidade })
                // e faz o PUT para "api/produtos/{ProdutoId}/baixar-saldo",
                // resolvido contra a BaseAddress configurada em Program.cs
                // ("http://estoque-api:8080/").
                var resposta = await httpClient.PutAsJsonAsync(
                    $"api/produtos/{item.ProdutoId}/baixar-saldo",
                    new { quantidade = item.Quantidade });

                // Se o Estoque.API respondeu com um status de erro (4xx/5xx),
                // não lançamos exceção — precisamos checar isso explicitamente.
                // O caso mais comum aqui é 400 Bad Request por saldo
                // insuficiente daquele produto.
                if (!resposta.IsSuccessStatusCode)
                {
                    var detalhe = await resposta.Content.ReadAsStringAsync();

                    return BadRequest(
                        $"Não foi possível processar a nota fiscal: o Estoque.API recusou a baixa " +
                        $"do produto {item.ProdutoId} (status {(int)resposta.StatusCode}). Detalhe: {detalhe}");
                }
            }
        }
        catch (HttpRequestException ex)
        {
            // Aqui caímos quando a chamada nem chegou a receber uma resposta
            // HTTP válida — tipicamente porque o Estoque.API está fora do ar,
            // inacessível na rede do Docker, ou demorou demais para responder.
            // 502 Bad Gateway é o código correto nesse cenário: ele significa
            // "eu (Faturamento.API), agindo como intermediário, tentei falar
            // com outro serviço para completar seu pedido, e esse outro
            // serviço não respondeu corretamente" — diferente de um erro
            // interno do próprio Faturamento.API (que seria 500).
            return StatusCode(StatusCodes.Status502BadGateway,
                $"Não foi possível processar a nota fiscal porque o serviço de estoque está indisponível no momento. Detalhe técnico: {ex.Message}");
        }

        // (e) Só chegamos até aqui se TODAS as baixas de saldo foram
        // bem-sucedidas (nenhum item causou "return" ou exceção lá em cima).
        // Nesse ponto é seguro fechar a nota fiscal.
        notaFiscal.Status = "Fechada";

        await _context.SaveChangesAsync();

        return Ok(notaFiscal);
    }

    // [HttpPost("{id}/validar")] atende "POST /api/notasfiscais/5/validar".
    //
    // IMPORTANTE: esta validação é só INFORMATIVA/opcional — um recurso
    // extra que dá um alerta rápido usando IA (ex: "essa quantidade parece
    // alta"), mas NÃO faz parte da regra de negócio obrigatória para
    // imprimir/fechar uma nota. A regra de negócio real (nota precisa estar
    // "Aberta", saldo suficiente no Estoque.API etc.) continua inteiramente
    // no endpoint "imprimir" acima, e não é afetada por este endpoint de
    // forma alguma — o usuário pode chamar "imprimir" sem nunca ter chamado
    // "validar", e o front-end não deve bloquear a impressão esperando ou
    // exigindo uma resposta daqui.
    [HttpPost("{id}/validar")]
    public async Task<ActionResult<object>> ValidarNotaFiscal(int id)
    {
        var notaFiscal = await _context.NotasFiscais
            .Include(n => n.Itens)
            .FirstOrDefaultAsync(n => n.Id == id);

        if (notaFiscal == null)
        {
            return NotFound();
        }

        // Sem chave configurada, nem tentamos chamar a Anthropic. Como este
        // é um recurso não crítico, devolvemos uma análise de fallback com
        // 200 OK em vez de um erro — o front-end pode exibir essa mensagem
        // normalmente, sem tratar isso como falha.
        if (string.IsNullOrWhiteSpace(_anthropicApiKey))
        {
            return Ok(new { analise = "Não foi possível validar com IA no momento." });
        }

        // Monta um resumo simples dos itens (produtoId e quantidade de cada
        // um) para mandar no prompt — não precisamos enviar a nota inteira,
        // só o suficiente para a IA avaliar se algo parece fora do comum.
        var resumo = string.Join(
            "; ",
            notaFiscal.Itens.Select(item => $"produtoId {item.ProdutoId}, quantidade {item.Quantidade}"));

        var prompt = $"Analise esta nota fiscal com os seguintes itens: {resumo}. Aponte de forma BREVE (max 20 palavras) se algo parece incomum (ex: quantidade muito alta) ou responda 'Nota fiscal parece consistente' se estiver tudo normal. Responda APENAS com a análise.";

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
            var httpClient = _httpClientFactory.CreateClient("AnthropicAPI");

            var mensagemRequisicao = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
            {
                Content = JsonContent.Create(corpoRequisicao)
            };
            mensagemRequisicao.Headers.Add("x-api-key", _anthropicApiKey);

            var resposta = await httpClient.SendAsync(mensagemRequisicao);

            if (!resposta.IsSuccessStatusCode)
            {
                // Falha de regra/limite na própria Anthropic (ex: 400, 429).
                // Como este recurso é opcional, não propagamos o erro para o
                // cliente — só devolvemos a mensagem de fallback, para não
                // atrapalhar quem só queria ver a nota na tela.
                return Ok(new { analise = "Não foi possível validar com IA no momento." });
            }

            var corpoResposta = await resposta.Content.ReadFromJsonAsync<AnthropicMessageResponse>();
            var analise = corpoResposta?.Content?.FirstOrDefault()?.Text?.Trim();

            if (string.IsNullOrWhiteSpace(analise))
            {
                return Ok(new { analise = "Não foi possível validar com IA no momento." });
            }

            return Ok(new { analise });
        }
        catch (HttpRequestException)
        {
            // Falha de rede/infraestrutura ao contatar a Anthropic (timeout,
            // serviço fora do ar etc.) — de novo, tratada como fallback e
            // não como erro, pelo mesmo motivo: isso é só um alerta extra,
            // não deve derrubar nem bloquear o fluxo de imprimir a nota.
            return Ok(new { analise = "Não foi possível validar com IA no momento." });
        }
    }
}
