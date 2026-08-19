using Estoque.API.Data;
using Estoque.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    // Construtor do controller. O ASP.NET Core usa Injeção de Dependência (DI):
    // como registramos "builder.Services.AddDbContext<EstoqueDbContext>(...)" no
    // Program.cs, o framework cria automaticamente um EstoqueDbContext e o passa
    // aqui sempre que uma requisição chega para este controller — não precisamos
    // instanciar o DbContext manualmente com "new".
    public ProdutosController(EstoqueDbContext context)
    {
        _context = context;
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
}
