# KORP - Sistema de Emissão de Notas Fiscais

Projeto técnico desenvolvido para o teste prático da KORP ERP: um sistema de controle de estoque e emissão de notas fiscais, construído como dois microsserviços independentes em .NET 8 e um front-end em Angular.

## Stack utilizada

- **Front-end:** Angular 21 (standalone components, Signals, Reactive Forms) + Tailwind CSS v4
- **Back-end:** C# / .NET 8 (ASP.NET Core Web API com Controllers)
- **Banco de dados:** PostgreSQL 16
- **ORM:** Entity Framework Core 8 (provider Npgsql)
- **Infraestrutura:** Docker e Docker Compose
- **Documentação de API:** Swagger / OpenAPI (Swashbuckle.AspNetCore)
- **IA:** API da Anthropic (modelo `claude-haiku-4-5-20251001`), consumida via proxy seguro no back-end

## Arquitetura

O back-end é dividido em **dois microsserviços C# independentes**, cada um com seu próprio processo, seu próprio `Program.cs` e sua própria responsabilidade de domínio:

| Serviço | Porta (host) | Responsabilidade |
|---|---|---|
| `Estoque.API` | `5001` | Cadastro e saldo de produtos |
| `Faturamento.API` | `5002` | Cadastro e impressão de notas fiscais |

Os dois compartilham a **mesma instância** do PostgreSQL, mas cada um tem seu **próprio schema** dentro do banco `korp_notas`:

- `Estoque.API` → schema `estoque` (tabela `Produtos`)
- `Faturamento.API` → schema `faturamento` (tabelas `NotasFiscais` e `ItensNotaFiscal`)

Essa separação por schema é lógica, não física: os dois serviços rodam contra o mesmo servidor de banco, mas **nenhum dos dois acessa as tabelas do outro diretamente**. Toda comunicação entre eles acontece exclusivamente via **HTTP**: quando uma nota fiscal é impressa, o `Faturamento.API` chama o endpoint `PUT /api/produtos/{id}/baixar-saldo` do `Estoque.API` (via `IHttpClientFactory`, usando o hostname interno do Docker `estoque-api:8080`) para dar baixa no saldo de cada item — nunca conecta direto no schema `estoque` do banco.

## Como rodar o projeto

### Pré-requisitos

- Docker e Docker Compose
- Node.js + Angular CLI (para rodar o front-end fora do Docker)

### 1. Configurar a chave da Anthropic (opcional)

Na raiz do repositório, copie o arquivo de exemplo e preencha sua chave:

```bash
cp .env.example .env
```

Edite o `.env` e defina `ANTHROPIC_API_KEY=sua_chave_aqui`. Esse passo é **opcional**: sem a chave, as duas APIs sobem normalmente e todo o fluxo obrigatório (produtos, notas fiscais, impressão) funciona sem restrição — apenas os recursos de IA (gerar descrição, validar nota, chat) respondem com uma mensagem de fallback em vez de uma resposta gerada.

### 2. Subir o banco e as APIs

Na raiz do repositório:

```bash
docker-compose up --build
```

Isso sobe três containers:

- `korp_postgres` — PostgreSQL 16, exposto em `localhost:5433`
- `korp_estoque_api` — `Estoque.API`, exposto em `http://localhost:5001` (Swagger em `/swagger`)
- `korp_faturamento_api` — `Faturamento.API`, exposto em `http://localhost:5002` (Swagger em `/swagger`)

### 3. Rodar o front-end

Em outro terminal:

```bash
cd frontend
npm install
ng serve
```

O Angular sobe em `http://localhost:4200`. O CORS das duas APIs já está configurado para aceitar requisições dessa origem.

## Funcionalidades implementadas

### Obrigatórias

- **Cadastro de produtos** — CRUD completo (`GET`/`POST`/`PUT`/`DELETE /api/produtos`) no `Estoque.API`.
- **Cadastro de notas fiscais** — criação com múltiplos itens (produto + quantidade) numa única requisição, com número sequencial gerado pelo servidor.
- **Impressão de notas fiscais** — fecha a nota, dá baixa no saldo de cada item via chamada ao `Estoque.API` e **bloqueia reimpressão**: só é possível imprimir uma nota com status `"Aberta"`; tentar imprimir de novo retorna `400 Bad Request`.
- **Arquitetura de microsserviços** — `Estoque.API` e `Faturamento.API` são processos, deploys e bancos (schemas) logicamente independentes, comunicando-se só por HTTP.
- **Tratamento de falhas** — regras de negócio violadas retornam `400` (saldo insuficiente, nota já fechada). Falha de comunicação entre microsserviços (`Estoque.API` indisponível durante a impressão) é capturada explicitamente: o `Faturamento.API` pega a `HttpRequestException`, cancela a operação sem alterar nada no banco (a nota não é fechada, o saldo não é baixado nem parcialmente) e retorna `502 Bad Gateway` com uma mensagem clara. **Importante:** isso não é um mecanismo de retry automático — não há retry configurado nem no Docker (o `restart: unless-stopped` do `docker-compose.yml` só reinicia um container que caiu, não reprocessa a requisição que falhou) nem no EF Core/Npgsql (`EnableRetryOnFailure` não está habilitado). A resiliência testada é outra: como a falha é interrompida ANTES de qualquer escrita no banco, a nota fica intacta como `"Aberta"`; assim que o `Estoque.API` volta a responder, basta tentar imprimir a mesma nota de novo (mesma ação do usuário, nenhuma mudança de código ou estado) que a operação é concluída normalmente. Ou seja: o sistema se recupera por não deixar estado corrompido para trás, não por reexecutar a chamada sozinho.
- **Conexão real com banco de dados** — PostgreSQL 16 via Entity Framework Core/Npgsql, com schemas separados por serviço e migrations versionadas.

### Opcionais implementadas

- **Geração de descrição de produto por IA** — botão "✨ Gerar com IA" no formulário de produto; chama `POST /api/produtos/gerar-descricao` no `Estoque.API`.
- **Validação de nota fiscal por IA** — endpoint `POST /api/notasfiscais/{id}/validar` implementado no `Faturamento.API` (analisa os itens e aponta se algo parece incomum). **Observação:** o endpoint está funcional e testável via Swagger, mas ainda não está conectado a nenhum botão/tela do Angular — a integração no front-end fica como próximo passo.
- **Chatbot flutuante** — widget de chat (bolinha no canto da tela, visível em todas as páginas) que conversa com um assistente com contexto do domínio KORP, via `POST /api/chat` no `Faturamento.API`.
- **Proxy seguro para a Anthropic** — em todos os três recursos de IA acima, a chave da API nunca é exposta ao navegador: o Angular só fala com o próprio back-end (`Estoque.API`/`Faturamento.API`), e é o back-end quem inclui a chave (lida do ambiente) na chamada para `api.anthropic.com`.
- **Geração de PDF client-side da nota fiscal** — botão "Baixar PDF", disponível para qualquer nota (aberta ou fechada), usando jsPDF inteiramente no navegador, sem chamada adicional ao servidor.

### Não implementadas

- **Tratamento de concorrência** — não há lock otimista nem transação distribuída entre os dois bancos/serviços. Se duas impressões de notas concorrentes envolverem o mesmo produto, é possível haver uma condição de corrida na baixa de saldo.
- **Idempotência** — não há chave de idempotência nas requisições; a única proteção contra reimpressão é a checagem do campo `Status` da nota (`"Aberta"`), não uma garantia formal de que a mesma requisição não seja processada duas vezes.

## Detalhamento técnico

**Ciclos de vida do Angular utilizados**
Os quatro componentes que dependem de dados carregados do back-end (`ProdutoListComponent`, `ProdutoFormComponent`, `NotaListComponent`, `NotaFormComponent`) implementam `OnInit` e carregam seus dados no `ngOnInit()` — é o hook usado para disparar a primeira chamada HTTP assim que o componente termina de ser inicializado. Nenhum outro hook de ciclo de vida (`ngOnDestroy`, `ngAfterViewInit` etc.) é usado no projeto.

**Uso de RxJS**
RxJS é usado através dos `Observable`s retornados pelo `HttpClient` do Angular em cada método dos services (`ProdutoService`, `NotaFiscalService`, `ChatService`) — nenhuma chamada HTTP usa `fetch`/`Promise` diretamente. O padrão adotado em toda chamada que pode falhar de forma relevante para o usuário é `.subscribe({ next, error })` (em vez de passar só um callback de sucesso), para capturar explicitamente erros HTTP e exibi-los na tela — usado em `nota-list.component.ts` (ao imprimir) e `nota-form.component.ts` (ao salvar), por exemplo.

**Outras bibliotecas e para quê**
- **jsPDF** — geração do PDF da nota fiscal inteiramente no navegador (client-side), sem endpoint dedicado no back-end.
- **Reactive Forms** (`@angular/forms`) — `FormBuilder`, `FormGroup` e `Validators` nos formulários de produto e nota fiscal; `FormArray` especificamente no formulário de nota fiscal, para suportar um número variável de itens (o usuário adiciona/remove itens em tempo real).

**Bibliotecas para componentes visuais**
Tailwind CSS v4, usado via classes utilitárias diretamente nos templates (sem uma biblioteca de componentes prontos como Angular Material). O layout é responsivo com duas apresentações por tela: tabela tradicional em telas médias/grandes (`md:block`) e lista de cards empilhados em telas pequenas (`md:hidden`).

**Gerenciamento de dependências**
Como o back-end é C#/.NET (não Go), o gerenciamento de dependências é feito via **NuGet**, declarado nos arquivos `.csproj` de cada API. Pacotes principais (idênticos nas duas APIs):
- `Npgsql.EntityFrameworkCore.PostgreSQL` (8.0.11) — provider do Entity Framework Core para PostgreSQL.
- `Microsoft.EntityFrameworkCore.Design` (8.0.11) — ferramentas de design-time do EF Core, usadas para gerar as migrations.
- `Swashbuckle.AspNetCore` (6.6.2) — geração do Swagger/OpenAPI.

No front-end, o gerenciamento é via **npm** (`package.json`), com destaque para `jspdf` e `rxjs` além do próprio Angular.

**Frameworks utilizados no back-end**
ASP.NET Core 8 Web API, com **Controllers** (`[ApiController]`, `[Route("api/[controller]")]`) — não Minimal APIs. Persistência via **Entity Framework Core 8** (`DbContext`, `DbSet<T>`, migrations), com um `DbContext` próprio por serviço (`EstoqueDbContext`, `FaturamentoDbContext`), cada um fixando seu schema em `OnModelCreating` via `modelBuilder.HasDefaultSchema(...)`.

**Tratamento de erros e exceções no back-end**
- Validação de corpo de requisição: automática via `[ApiController]` (retorna `400` sozinho se um campo obrigatório do model binding estiver ausente).
- Regras de negócio explícitas retornando `400 Bad Request`: saldo insuficiente em `BaixarSaldo` (`Estoque.API`) e nota fiscal que não está `"Aberta"` em `ImprimirNotaFiscal` (`Faturamento.API`).
- Recurso não encontrado: `404 Not Found` nos `Get`/`Put`/`Delete` por Id, quando a entidade não existe.
- **Falha de comunicação entre microsserviços** (o ponto mais relevante): em `ImprimirNotaFiscal`, a chamada do `Faturamento.API` ao `Estoque.API` é envolvida em `try/catch`. Duas falhas diferentes são tratadas separadamente:
  - `HttpRequestException` (o `Estoque.API` está fora do ar, inacessível na rede Docker, ou não responde) → capturada no `catch`, retorna **`502 Bad Gateway`**.
  - Resposta HTTP recebida mas com status de erro (ex: `400` por saldo insuficiente naquele produto) → checada manualmente via `resposta.IsSuccessStatusCode`, sem lançar exceção, retorna `400 Bad Request` com o detalhe repassado do `Estoque.API`.
  - Em ambos os casos a nota **não é fechada** e a baixa é interrompida no primeiro item que falhar, para não deixar o sistema num estado parcialmente aplicado.
- Erros de IA (Anthropic fora do ar, chave ausente, resposta vazia) são tratados como não-críticos nos recursos opcionais: `ChatController` e o endpoint `validar` devolvem `200 OK` com uma mensagem de fallback em vez de propagar erro, já que são funcionalidades extras que não podem travar o fluxo principal; já `gerar-descricao` (que tem um botão dedicado no front, com tratamento de erro próprio) retorna `500` com o detalhe.

**Uso de LINQ** (exemplos reais do código)
- `_context.Produtos.ToListAsync()` — lista todos os produtos (`ProdutosController.GetProdutos`).
- `_context.NotasFiscais.Include(n => n.Itens).ToListAsync()` — lista notas fiscais já trazendo os itens relacionados numa única consulta, evitando o problema N+1 (`NotasFiscaisController.GetNotasFiscais`).
- `_context.NotasFiscais.Include(n => n.Itens).FirstOrDefaultAsync(n => n.Id == id)` — busca uma nota específica com os itens, usado em `GetNotaFiscal`, `ImprimirNotaFiscal` e `ValidarNotaFiscal`.
- `_context.NotasFiscais.Select(n => (int?)n.Numero).MaxAsync()` — calcula o maior número de nota já existente, para gerar o próximo número sequencial em `PostNotaFiscal`.
- `notaFiscal.Itens.Select(item => $"produtoId {item.ProdutoId}, quantidade {item.Quantidade}")` combinado com `string.Join("; ", ...)` — monta o resumo textual dos itens enviado no prompt do endpoint `validar`.
- `corpoResposta?.Content?.FirstOrDefault()?.Text?.Trim()` — extrai o texto gerado da resposta da Anthropic, usado nos três endpoints de IA.

## Estrutura do repositório

```
Korp_Teste_Hugo/
├── docker-compose.yml
├── .env.example
├── Estoque.API/
│   ├── Controllers/
│   │   └── ProdutosController.cs
│   ├── Data/
│   │   └── EstoqueDbContext.cs
│   ├── Models/
│   │   └── Produto.cs
│   ├── Migrations/
│   ├── Program.cs
│   ├── Dockerfile
│   └── appsettings.json
├── Faturamento.API/
│   ├── Controllers/
│   │   ├── NotasFiscaisController.cs
│   │   ├── ChatController.cs
│   │   └── AnthropicModels.cs
│   ├── Data/
│   │   └── FaturamentoDbContext.cs
│   ├── Models/
│   │   ├── NotaFiscal.cs
│   │   └── ItemNotaFiscal.cs
│   ├── Migrations/
│   ├── Program.cs
│   ├── Dockerfile
│   └── appsettings.json
└── frontend/
    ├── src/app/
    │   ├── core/services/       # ProdutoService, NotaFiscalService, ChatService
    │   ├── features/
    │   │   ├── produtos/        # produto-list, produto-form
    │   │   └── notas-fiscais/   # nota-list, nota-form
    │   ├── shared/chat-widget/  # widget de chat flutuante
    │   ├── app.ts / app.html    # shell da aplicação (header + router-outlet)
    │   └── app.routes.ts
    ├── angular.json
    └── package.json
```

## Vídeo de apresentação

[Link do vídeo aqui]
