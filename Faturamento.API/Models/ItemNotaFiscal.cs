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
}
