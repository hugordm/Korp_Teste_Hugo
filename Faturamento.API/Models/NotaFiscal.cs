namespace Faturamento.API.Models;

// Representa uma Nota Fiscal, o documento que registra a venda de um
// ou mais produtos. Cada instância dessa classe vira uma linha na tabela
// "NotasFiscais" dentro do schema "faturamento" do banco de dados.
public class NotaFiscal
{
    // Identificador único da nota fiscal (chave primária).
    // O banco de dados gera esse valor automaticamente a cada nova nota.
    public int Id { get; set; }

    // Número da nota fiscal, usado para identificação/exibição
    // (diferente do Id, que é só uma chave interna do banco).
    public int Numero { get; set; }

    // Situação atual da nota fiscal (ex: "Aberta", "Faturada", "Cancelada").
    // Começa como "Aberta" até que o processo de faturamento a feche.
    public string Status { get; set; } = "Aberta";

    // Data e hora em que a nota fiscal foi criada.
    // Usa UtcNow (horário universal) para evitar problemas de fuso horário
    // quando o sistema for usado em locais diferentes.
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

    // Lista dos itens (produtos e quantidades) que compõem esta nota fiscal.
    // Já começa vazia para nunca ser nula, evitando checagens de null
    // toda vez que formos adicionar ou percorrer os itens.
    public List<ItemNotaFiscal> Itens { get; set; } = new();
}
