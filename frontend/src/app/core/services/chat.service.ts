import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

// Service do chat flutuante (funcionalidade opcional/bônus, ver
// ChatWidgetComponent). O backend responsável por esse endpoint atua como
// proxy seguro para a API da Anthropic: o front só envia a mensagem digitada
// pelo usuário e recebe de volta o texto da resposta, sem nunca ter acesso à
// chave de API.
@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly http = inject(HttpClient);

  private readonly apiUrl = 'https://korp-faturamento-api-r0gh.onrender.com/api/chat';

  // POST /api/chat — envia a mensagem do usuário e recebe a resposta do bot.
  enviarMensagem(mensagem: string): Observable<{ resposta: string }> {
    return this.http.post<{ resposta: string }>(this.apiUrl, { mensagem });
  }
}
