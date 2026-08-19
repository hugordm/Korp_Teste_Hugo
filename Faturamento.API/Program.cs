using Faturamento.API.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registra o FaturamentoDbContext no container de injeção de dependência do ASP.NET Core.
// Isso permite que Controllers e outros serviços recebam um FaturamentoDbContext
// pronto para uso apenas declarando-o no construtor.
// UseNpgsql configura o Entity Framework Core para falar com um banco PostgreSQL,
// usando a connection string "DefaultConnection" definida em appsettings.json.
builder.Services.AddDbContext<FaturamentoDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
