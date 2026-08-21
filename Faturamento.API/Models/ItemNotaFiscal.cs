using System.ComponentModel.DataAnnotations.Schema;

namespace Faturamento.API.Models;

// Representa um item (uma linha) dentro de uma Nota Fiscal: um produto
// específico e a quantidade vendida dele. Cada instância dessa classe
// vira uma linha na tabela "ItensNotaFiscal" no schema "faturamento".
public class ItemNotaFiscal
{
    // Identificador único do item (chave primária).
    // O banco de dados gera esse valor automaticamente a cada novo item.
    public int Id { get; set; }

    // Chave estrangeira: indica a qual Nota Fiscal este item pertence.
    public int NotaFiscalId { get; set; }

    // Propriedade de navegação para a Nota Fiscal dona deste item.
    // É "nullable" porque o Entity Framework só a preenche quando
    // explicitamente pedimos para carregar os dados relacionados (Include);
    // caso contrário ela fica null em memória, mesmo a chave estrangeira existindo no banco.
    public NotaFiscal? NotaFiscal { get; set; }

    // Identificador do produto vendido (Produto pertence ao Estoque.API,
    // um projeto/domínio diferente). Por isso guardamos só o Id, sem uma
    // propriedade de navegação para um objeto Produto.
    public int ProdutoId { get; set; }

    // Quantidade do produto vendida nesse item da nota fiscal.
    public int Quantidade { get; set; }

    // Código e Descrição do produto, buscados em tempo real no Estoque.API
    // (via NotasFiscaisController.BuscarProdutosAsync) só para exibição na
    // listagem/consulta de notas fiscais.
    //
    // [NotMapped] diz ao EF Core para IGNORAR essas duas propriedades ao
    // mapear a classe para a tabela do banco — elas não viram colunas em
    // "ItensNotaFiscal" e nunca são lidas/escritas em SELECT/INSERT/UPDATE.
    // Isso é proposital: são dados que pertencem ao Estoque.API (é ele quem
    // é "dono" do cadastro de produtos, com Código e Descrição podendo mudar
    // a qualquer momento), não ao Faturamento.API. Persistir uma cópia deles
    // aqui duplicaria a informação entre os dois bancos e criaria o risco de
    // ela ficar desatualizada (ex: se o produto for renomeado no Estoque, a
    // cópia salva aqui ficaria com o nome antigo). Por isso preenchemos essas
    // propriedades apenas em memória, a cada requisição, com o valor mais
    // recente vindo do outro serviço.
    [NotMapped]
    public string? CodigoProduto { get; set; }

    [NotMapped]
    public string? DescricaoProduto { get; set; }
}
