using Estoque.API.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registra o EstoqueDbContext no container de injeção de dependência do ASP.NET Core.
// Isso permite que Controllers e outros serviços recebam um EstoqueDbContext
// pronto para uso apenas declarando-o no construtor.
// UseNpgsql configura o Entity Framework Core para falar com um banco PostgreSQL,
// usando a connection string "DefaultConnection" definida em appsettings.json.
builder.Services.AddDbContext<EstoqueDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// CORS (Cross-Origin Resource Sharing) é o mecanismo que os navegadores usam
// para decidir se uma página JavaScript de um domínio/porta pode ou não fazer
// requisições para uma API rodando em outro domínio/porta. Por padrão, os
// navegadores aplicam a política de "same-origin": um script servido a partir
// de "http://localhost:4200" (o Angular, em desenvolvimento) é bloqueado pelo
// próprio navegador de chamar "http://localhost:5001" (esta API), mesmo que
// ambos estejam rodando na mesma máquina — para o navegador, portas
// diferentes já contam como origens diferentes. Essa restrição existe para
// proteger o usuário: sem ela, qualquer site malicioso poderia usar o
// navegador da vítima (já autenticado em outros serviços) para fazer
// requisições silenciosas a APIs de terceiros em nome dela.
// Como o Angular e esta API são, de fato, partes do mesmo sistema (só que
// servidas de origens diferentes durante o desenvolvimento), precisamos
// dizer explicitamente ao navegador que confiamos nessa origem específica.
// É isso que AddCors + a política abaixo fazem: declaram que requisições
// vindas de "http://localhost:4200", com qualquer método HTTP (GET, POST,
// PUT, DELETE...) e qualquer cabeçalho, são permitidas.
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Ativa o middleware de CORS usando a política registrada acima. Precisa
// vir antes de UseAuthorization()/MapControllers() porque é este middleware
// quem inspeciona o cabeçalho "Origin" de cada requisição recebida e decide
// se deve adicionar os cabeçalhos de resposta (Access-Control-Allow-Origin
// etc.) que autorizam o navegador a aceitar a resposta — se ele rodasse
// depois, os controllers já teriam processado a requisição sem essa
// liberação estar em vigor.
app.UseCors("PermitirAngular");

app.UseAuthorization();

app.MapControllers();

app.Run();
