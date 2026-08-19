import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

// Interface que descreve um item de nota fiscal como ele volta do backend
// (Faturamento.API), já com o "id" gerado pelo banco de dados.
export interface ItemNotaFiscal {
  id: number;
  produtoId: number;
  quantidade: number;
}

// Interface que descreve a nota fiscal completa, como ela volta do backend
// (ex: no GET /api/notasfiscais ou no POST de criação). Ela espelha a classe
// C# "NotaFiscal": "numero" e "status" já vêm preenchidos pelo servidor, e
// "itens" já vem com os ItemNotaFiscal completos (com id), porque o backend
// usa Include(n => n.Itens) para trazer tudo junto.
export interface NotaFiscal {
  id: number;
  numero: number;
  status: string;
  dataCriacao: string;
  itens: ItemNotaFiscal[];
}

// Interface usada apenas para ENVIAR um item novo ao criar uma nota fiscal.
// Não tem "id" porque quem gera o Id de cada item é o banco de dados no
// momento da gravação — o cliente não escolhe (e nem tem como saber) esse
// valor antes de a nota existir.
export interface NovoItemNotaFiscal {
  produtoId: number;
  quantidade: number;
}

// Interface usada apenas para ENVIAR os dados de uma nota fiscal nova
// (POST /api/notasfiscais).
//
// Por que ela é diferente de "NotaFiscal" (usada para o que recebemos de
// volta): o que mandamos para o servidor e o que o servidor devolve não são
// simetricos. Ao criar uma nota, o cliente só sabe (e só precisa informar)
// a lista de itens — quais produtos e quantidades compõem a nota. Campos
// como "id" (gerado pelo banco), "numero" (calculado no servidor como
// sequencial) e "status" (sempre forçado para "Aberta" na criação, ignorando
// qualquer valor enviado) são decididos pelo backend, não pelo cliente — o
// próprio Faturamento.API ignora/sobrescreve esses campos mesmo que o
// cliente tente mandá-los. Se usássemos a mesma interface "NotaFiscal" para
// o envio, o TypeScript exigiria (ou pelo menos permitiria) que o
// componente preenchesse id/numero/status/dataCriacao com valores
// inventados, o que é enganoso: passaria a falsa impressão de que esses
// valores importam ou são respeitados pelo servidor. Ter uma interface de
// envio enxuta, só com o que de fato é usado (itens), deixa o contrato da
// API explícito só de olhar para o tipo.
export interface NovaNotaFiscal {
  itens: NovoItemNotaFiscal[];
}

@Injectable({ providedIn: 'root' })
export class NotaFiscalService {
  private readonly http = inject(HttpClient);

  private readonly apiUrl = 'http://localhost:5002/api/notasfiscais';

  // GET /api/notasfiscais — lista todas as notas fiscais (já com seus itens).
  listar(): Observable<NotaFiscal[]> {
    return this.http.get<NotaFiscal[]>(this.apiUrl);
  }

  // GET /api/notasfiscais/{id} — busca uma nota fiscal específica.
  buscarPorId(id: number): Observable<NotaFiscal> {
    return this.http.get<NotaFiscal>(`${this.apiUrl}/${id}`);
  }

  // POST /api/notasfiscais — cria uma nova nota fiscal com seus itens.
  // Recebe "NovaNotaFiscal" (só o que o cliente de fato informa) e devolve
  // "NotaFiscal" (o recurso completo, já com id/numero/status/dataCriacao
  // preenchidos pelo servidor) — é assim que o backend responde no 201
  // Created.
  criar(nota: NovaNotaFiscal): Observable<NotaFiscal> {
    return this.http.post<NotaFiscal>(this.apiUrl, nota);
  }

  // POST /api/notasfiscais/{id}/imprimir — fecha a nota fiscal, disparando
  // no backend a baixa de saldo de cada item junto ao Estoque.API. Não há
  // corpo para enviar (a nota já existe, só referenciamos pelo id na URL);
  // o retorno é a nota fiscal atualizada, já com status "Fechada".
  imprimir(id: number): Observable<NotaFiscal> {
    return this.http.post<NotaFiscal>(`${this.apiUrl}/${id}/imprimir`, {});
  }
}
