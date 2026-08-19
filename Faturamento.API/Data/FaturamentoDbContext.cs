using Faturamento.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.API.Data;

// DbContext é a "ponte" entre as classes C# (Models) e o banco de dados.
// É através dele que o Entity Framework Core sabe quais tabelas existem
// e como ler/gravar os dados de cada uma.
public class FaturamentoDbContext : DbContext
{
    // Construtor que recebe as opções de configuração do DbContext
    // (ex: qual banco usar, connection string, etc). Essas opções são
    // montadas no Program.cs e injetadas aqui automaticamente pelo ASP.NET Core.
    public FaturamentoDbContext(DbContextOptions<FaturamentoDbContext> options)
        : base(options)
    {
    }

    // Representa a tabela de Notas Fiscais no banco de dados.
    // Usar esse DbSet é o que permite fazer consultas como
    // "context.NotasFiscais.Where(...)" ou "context.NotasFiscais.Add(...)".
    public DbSet<NotaFiscal> NotasFiscais { get; set; }

    // Representa a tabela de Itens de Nota Fiscal no banco de dados.
    public DbSet<ItemNotaFiscal> ItensNotaFiscal { get; set; }

    // Método usado pelo Entity Framework para configurar detalhes do modelo
    // que não são simples propriedades das classes (ex: schema, relacionamentos, índices).
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Define que todas as tabelas deste DbContext ficam dentro do schema
        // "faturamento" (em vez do schema padrão "public") no PostgreSQL.
        // Isso separa logicamente as tabelas do módulo de Faturamento das
        // tabelas do módulo de Estoque, mesmo que ambos compartilhem o mesmo banco.
        modelBuilder.HasDefaultSchema("faturamento");

        base.OnModelCreating(modelBuilder);
    }
}
