namespace Faturamento.API.Models;

// Representa apenas os dados mínimos de um Produto que o Faturamento.API
// precisa "importar" do Estoque.API via HTTP (GET /api/produtos/por-ids)
// para exibir Código e Descrição nos itens de uma nota fiscal.
//
// Não é uma entidade do Faturamento.API — não existe uma tabela para ela
// no banco do Faturamento, e ela não é gerenciada pelo EF Core. É só um
// DTO usado para desserializar a resposta HTTP vinda do Estoque.API, que é
// o dono real dessa informação.
public class ProdutoResumo
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
}
