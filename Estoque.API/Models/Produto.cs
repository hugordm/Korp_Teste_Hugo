namespace Estoque.API.Models;

// Representa um Produto que pode ser cadastrado e usado em Notas Fiscais.
// Cada instância dessa classe vira uma linha na tabela "Produtos" no schema "estoque".
public class Produto
{
    // Identificador único do produto (chave primária).
    // O banco de dados gera esse valor automaticamente a cada novo produto.
    public int Id { get; set; }

    // Código do produto, usado para identificação (campo obrigatório do teste).
    public string Codigo { get; set; } = string.Empty;

    // Descrição/nome do produto (campo obrigatório do teste).
    public string Descricao { get; set; } = string.Empty;

    // Saldo em estoque: quantidade disponível desse produto (campo obrigatório do teste).
    public int Saldo { get; set; }
}
