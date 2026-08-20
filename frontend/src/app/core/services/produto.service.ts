import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

// Interface que descreve o formato de um Produto vindo/indo para a API
// (Estoque.API). Ela espelha a classe C# "Produto" do backend: mesmos
// campos, mesmos tipos (o "id" numérico do C# vira "number" aqui, e assim
// por diante). Usar uma interface em vez de "any" dá autocomplete e faz o
// TypeScript avisar em tempo de compilação se algum componente tentar
// acessar um campo que não existe, ou esquecer de passar um campo obrigatório.
export interface Produto {
  id: number;
  codigo: string;
  descricao: string;
  saldo: number;
}

// @Injectable({ providedIn: 'root' }) registra este service como um único
// "singleton" disponível para toda a aplicação, sem precisar declará-lo
// manualmente em nenhum array de "providers" — o Angular cria a instância
// sozinho na primeira vez que algum componente pedir um ProdutoService, e
// reutiliza essa mesma instância em todo o app.
@Injectable({ providedIn: 'root' })
export class ProdutoService {
  // inject(HttpClient) é a forma moderna de obter uma dependência dentro de
  // um service, usada em vez do tradicional "constructor(private http:
  // HttpClient)". O resultado final é o mesmo (o Angular injeta a mesma
  // instância de HttpClient), mas inject() pode ser chamado em qualquer
  // contexto de injeção (inclusive em inicializadores de campo, como aqui),
  // o que evita a "poluição" de parâmetros no construtor quando o service
  // cresce e passa a depender de vários serviços.
  private readonly http = inject(HttpClient);

  // URL base da API de Estoque. Fica centralizada aqui, numa constante
  // privada, para não repetir a string em cada método abaixo — se a porta
  // ou o caminho mudar, só precisa ajustar em um lugar.
  private readonly apiUrl = 'https://korp-teste-hugo.onrender.com/api/produtos';

  // Por que centralizar as chamadas HTTP num Service, e não fazer o
  // HttpClient direto dentro de cada componente:
  // 1) Reuso: várias telas podem precisar "listar produtos" ou "baixar
  //    saldo"; sem um service, cada componente reimplementaria a mesma
  //    chamada HTTP e a mesma URL, duplicando código e criando risco de
  //    inconsistência (ex: um componente aponta para a porta errada).
  // 2) Separação de responsabilidades: o componente deve cuidar da tela
  //    (o que exibir, como reagir a clique), não de detalhes de como uma
  //    requisição HTTP é montada. Isso deixa os componentes mais simples
  //    de ler e os services mais fáceis de testar isoladamente (dá para
  //    simular/mockar o ProdutoService inteiro num teste de componente).
  // 3) Ponto único de mudança: se amanhã a API mudar de endpoint, adicionar
  //    autenticação, cache, tratamento de erro padrão etc., isso é feito
  //    uma vez aqui, e todo componente que usa o service ganha o benefício
  //    automaticamente.

  // O que é HttpClient: é o serviço do Angular usado para fazer requisições
  // HTTP (GET, POST, PUT, DELETE...) a partir do navegador. Cada método dele
  // (get, post, put, delete) NÃO devolve o dado diretamente nem uma Promise —
  // devolve um Observable.

  // O que é um Observable (e por que não é a mesma coisa que uma Promise):
  // uma Promise, assim que criada, já começa a executar a operação
  // assíncrona (ex: a requisição já foi disparada), e representa um único
  // valor que vai chegar (ou um erro) no futuro. Um Observable, por outro
  // lado, é "preguiçoso" (lazy): ele só descreve COMO obter o valor, mas não
  // faz nada sozinho. A requisição HTTP só é efetivamente disparada quando
  // alguém "se inscreve" nele chamando ".subscribe()" — se ninguém chamar
  // subscribe(), a chamada HTTP simplesmente nunca acontece. Isso dá mais
  // controle: o mesmo Observable pode ser configurado com operadores (RxJS)
  // para cancelar, repetir, combinar com outras chamadas, etc., antes de
  // decidir "agora sim, dispare".

  // GET /api/produtos — lista todos os produtos.
  listar(): Observable<Produto[]> {
    return this.http.get<Produto[]>(this.apiUrl);
  }

  // GET /api/produtos/{id} — busca um produto específico pelo Id.
  buscarPorId(id: number): Observable<Produto> {
    return this.http.get<Produto>(`${this.apiUrl}/${id}`);
  }

  // POST /api/produtos — cria um novo produto.
  // "Omit<Produto, 'id'>" reaproveita a interface Produto, mas removendo o
  // campo "id": ao criar um produto novo o cliente não escolhe o Id (quem
  // gera é o banco de dados no backend), então nem faz sentido o tipo exigir
  // esse campo aqui — o TypeScript já barra em tempo de compilação qualquer
  // tentativa de mandar um "id" nessa chamada.
  criar(produto: Omit<Produto, 'id'>): Observable<Produto> {
    return this.http.post<Produto>(this.apiUrl, produto);
  }

  // PUT /api/produtos/{id} — atualização completa de um produto existente.
  // O retorno é Observable<void> porque o endpoint PUT do backend responde
  // 204 No Content (sem corpo) quando a atualização dá certo.
  atualizar(id: number, produto: Produto): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, produto);
  }

  // DELETE /api/produtos/{id} — remove um produto. Também sem corpo de
  // resposta (204 No Content), por isso Observable<void>.
  excluir(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  // PUT /api/produtos/{id}/baixar-saldo — dá baixa de "quantidade" unidades
  // no saldo do produto. O corpo enviado é "{ quantidade }" (forma reduzida
  // de "{ quantidade: quantidade }" em JavaScript/TypeScript), no mesmo
  // formato que o endpoint BaixarSaldo do Estoque.API espera receber.
  baixarSaldo(id: number, quantidade: number): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}/baixar-saldo`, { quantidade });
  }

  // POST /api/produtos/gerar-descricao — funcionalidade opcional/bônus: pede
  // para o backend gerar uma sugestão de descrição a partir do código e (se
  // houver) de um nome-base já digitado. O backend (Estoque.API) é quem
  // efetivamente fala com a API da Anthropic — o front nunca vê nem manuseia
  // a chave de API, só chama esse endpoint como um proxy seguro.
  gerarDescricao(codigo: string, nomeBase: string): Observable<{ descricao: string }> {
    return this.http.post<{ descricao: string }>(`${this.apiUrl}/gerar-descricao`, {
      codigo,
      nomeBase
    });
  }
}
