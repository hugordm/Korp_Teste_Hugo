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

// CORS (Cross-Origin Resource Sharing) é o mecanismo que os navegadores usam
// para decidir se uma página JavaScript de um domínio/porta pode ou não fazer
// requisições para uma API rodando em outro domínio/porta. Por padrão, os
// navegadores aplicam a política de "same-origin": um script servido a partir
// de "http://localhost:4200" (o Angular, em desenvolvimento) é bloqueado pelo
// próprio navegador de chamar "http://localhost:5002" (esta API), mesmo que
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

// Registra um HttpClient "nomeado" para chamar a API da Anthropic (Claude),
// usada pelo endpoint de validação informativa de notas fiscais e pelo
// chatbot de suporte. Assim como o "EstoqueAPI" registrado acima, este
// Faturamento.API atua como "proxy seguro" entre o Angular e a Anthropic —
// o navegador nunca vê a chave da API, só fala com este backend, que é
// quem de fato inclui a chave (lida do ambiente, nunca do código-fonte) em
// cada requisição a api.anthropic.com.
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
// do Docker. Só avisamos no log se estiver faltando: a validação por IA e
// o chat são recursos adicionais/opcionais, não regra de negócio
// obrigatória (imprimir nota continua funcionando sem isso).
var anthropicApiKey = builder.Configuration["ANTHROPIC_API_KEY"]
    ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

if (string.IsNullOrWhiteSpace(anthropicApiKey))
{
    Console.WriteLine("[AVISO] ANTHROPIC_API_KEY não configurada — os endpoints de validação por IA e chat vão usar as respostas de fallback até isso ser definido no .env.");
}

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
