using Faturamento.API.Data;
using Faturamento.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    // Construtor do controller. O ASP.NET Core usa Injeção de Dependência (DI):
    // como o FaturamentoDbContext já está registrado em Program.cs
    // ("builder.Services.AddDbContext<FaturamentoDbContext>(...)"), o framework
    // cria automaticamente uma instância dele e a passa aqui sempre que uma
    // requisição chega para este controller — não precisamos instanciar o
    // DbContext manualmente com "new".
    public NotasFiscaisController(FaturamentoDbContext context)
    {
        _context = context;
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

        // Retornar o objeto diretamente faz o ASP.NET Core devolver
        // HTTP 200 OK com a lista de notas fiscais (e seus itens) em JSON.
        return Ok(notasFiscais);
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
}
