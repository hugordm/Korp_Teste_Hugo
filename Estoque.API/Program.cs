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
        policy.WithOrigins(
                "http://localhost:4200",
                "https://korp-teste-hugo.vercel.app"
              )
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Registra um HttpClient "nomeado" (via IHttpClientFactory) para chamar a
// API da Anthropic (Claude), usada pelo endpoint de geração automática de
// descrição de produto. O ProdutosController atua como um "proxy seguro"
// entre o Angular e a Anthropic: o navegador nunca fala diretamente com
// api.anthropic.com nem vê a chave da API — ele só chama este backend, que
// é quem de fato inclui a chave (guardada aqui no servidor) na requisição.
// Se a chave fosse usada direto do Angular, qualquer usuário poderia abrir
// o DevTools do navegador e roubá-la.
builder.Services.AddHttpClient("AnthropicAPI", client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com/");
    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
});

// Lê a chave da API da Anthropic a partir da variável de ambiente
// ANTHROPIC_API_KEY. builder.Configuration já inclui automaticamente as
// variáveis de ambiente do processo como fonte de configuração — em
// produção/Docker, essa variável é injetada pelo docker-compose.yml a
// partir do arquivo ".env" na raiz do repositório (nunca commitado); o
// fallback com Environment.GetEnvironmentVariable cobre rodar a API fora
// do Docker (ex: "dotnet run" direto, com a variável exportada no shell).
// Só avisamos no log se estiver faltando (em vez de derrubar a API inteira
// na inicialização) porque essa integração é um recurso adicional — o
// cadastro de produtos continua funcionando normalmente sem ela; só o
// endpoint de gerar descrição por IA que vai falhar.
var anthropicApiKey = builder.Configuration["ANTHROPIC_API_KEY"]
    ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

if (string.IsNullOrWhiteSpace(anthropicApiKey))
{
    Console.WriteLine("[AVISO] ANTHROPIC_API_KEY não configurada — o endpoint de geração de descrição por IA vai falhar até isso ser definido no .env.");
}

var app = builder.Build();

// Configure the HTTP request pipeline.
// Decisão consciente: normalmente o Swagger fica restrito a
// app.Environment.IsDevelopment(), por segurança — expor a documentação
// completa da API (todas as rotas, modelos de request/response) publicamente
// em produção facilita o trabalho de quem for tentar explorar a API de forma
// indevida. Aqui removemos essa restrição de propósito, porque este projeto
// é um teste técnico/demonstração (deploy no Render): o objetivo é que
// qualquer avaliador consiga abrir o Swagger em produção sem precisar rodar
// a aplicação localmente.
app.UseSwagger();
app.UseSwaggerUI();

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
