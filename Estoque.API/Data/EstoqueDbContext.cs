using Estoque.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Estoque.API.Data;

// DbContext é a "ponte" entre as classes C# (Models) e o banco de dados.
// É através dele que o Entity Framework Core sabe quais tabelas existem
// e como ler/gravar os dados de cada uma.
public class EstoqueDbContext : DbContext
{
    // Construtor que recebe as opções de configuração do DbContext
    // (ex: qual banco usar, connection string, etc). Essas opções são
    // montadas no Program.cs e injetadas aqui automaticamente pelo ASP.NET Core.
    public EstoqueDbContext(DbContextOptions<EstoqueDbContext> options)
        : base(options)
    {
    }

    // Representa a tabela de Produtos no banco de dados.
    // Usar esse DbSet é o que permite fazer consultas como
    // "context.Produtos.Where(...)" ou "context.Produtos.Add(...)".
    public DbSet<Produto> Produtos { get; set; }

    // Método usado pelo Entity Framework para configurar detalhes do modelo
    // que não são simples propriedades das classes (ex: schema, relacionamentos, índices).
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Define que todas as tabelas deste DbContext ficam dentro do schema
        // "estoque" (em vez do schema padrão "public") no PostgreSQL.
        // Isso separa logicamente as tabelas do módulo de Estoque das tabelas
        // do módulo de Faturamento, mesmo que ambos compartilhem o mesmo banco.
        modelBuilder.HasDefaultSchema("estoque");

        base.OnModelCreating(modelBuilder);
    }
}
