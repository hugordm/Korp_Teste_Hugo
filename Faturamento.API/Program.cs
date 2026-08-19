using Faturamento.API.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// AddJsonOptions ajusta o serializador JSON usado pelos Controllers.
// É necessário aqui porque NotaFiscal tem uma lista de Itens
// (List<ItemNotaFiscal>) e, ao mesmo tempo, cada ItemNotaFiscal tem uma
// propriedade de navegação "NotaFiscal" apontando de volta para a nota dona
// dele. Isso forma um ciclo de referências: para serializar a nota em JSON,
// o serializador entra em cada item; para serializar cada item, ele tentaria
// entrar de novo na propriedade "NotaFiscal" do item, que aponta para a
// mesma nota original, que aponta de novo para os itens... e assim por
// diante, infinitamente. O serializador padrão do System.Text.Json não
// consegue resolver esse ciclo sozinho e lança
// "JsonException: A possible object cycle was detected".
// ReferenceHandler.IgnoreCycles resolve isso fazendo o serializador "lembrar"
// quais objetos ele já visitou durante a serialização atual; quando ele
// percorre o ciclo e chega de novo num objeto já visitado (o
// ItemNotaFiscal.NotaFiscal apontando para a nota que já estava sendo
// serializada), ele simplesmente omite essa referência de volta (o campo
// some do JSON) em vez de tentar serializá-la de novo e entrar em loop.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
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

// Registra um HttpClient "nomeado" (via IHttpClientFactory) para chamar a API
// de Estoque. Usamos AddHttpClient em vez de fazer "new HttpClient()" direto
// no controller porque instanciar HttpClient manualmente (principalmente
// dentro de um "using") é uma prática desaconselhada pela própria Microsoft:
// cada HttpClient novo abre seus próprios sockets, e descartá-lo logo em
// seguida pode deixar sockets em estado TIME_WAIT, esgotando as portas
// disponíveis do sistema sob carga (o chamado "socket exhaustion").
// O IHttpClientFactory resolve isso reciclando e reutilizando o
// HttpMessageHandler por baixo dos panos, mesmo quando pedimos um HttpClient
// "novo" a cada chamada via factory.CreateClient("EstoqueAPI").
//
// "estoque-api" (usado tanto como nome lógico do client aqui quanto no host
// da BaseAddress abaixo) é exatamente o nome do serviço "estoque-api"
// definido no docker-compose.yml. Dentro da rede interna que o Docker Compose
// cria automaticamente, cada container enxerga os outros pelo nome do
// serviço (como se fosse um hostname de DNS), e não por "localhost" — afinal,
// "localhost" de dentro do container do Faturamento.API apontaria para o
// próprio container do Faturamento.API, não para o do Estoque.API. Por isso a
// URL usada aqui é "http://estoque-api:8080/" (porta interna do container,
// definida no Dockerfile/compose), e não "http://localhost:5001/" (que é só a
// porta exposta para acesso de fora do Docker, da sua máquina).
builder.Services.AddHttpClient("EstoqueAPI", client =>
{
    client.BaseAddress = new Uri("http://estoque-api:8080/");
});

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
