import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ChatService } from '../../core/services/chat.service';

// Uma mensagem do histórico do chat: 'usuario' é o que a pessoa digitou,
// 'bot' é a resposta vinda da IA (via ChatService/backend).
interface MensagemChat {
  autor: 'usuario' | 'bot';
  texto: string;
}

// Componente opcional/bônus: bolinha de chat flutuante, fixa no canto
// inferior direito da tela, adicionada uma única vez em app.html (fora do
// router-outlet) para ficar visível em todas as páginas. O backend por trás
// do ChatService atua como proxy seguro para a API da Anthropic — o front
// nunca vê a chave de API, só troca mensagens de texto.
@Component({
  selector: 'app-chat-widget',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './chat-widget.component.html'
})
export class ChatWidgetComponent {
  private readonly chatService = inject(ChatService);

  // Controla se a janela do chat está aberta ou só a bolinha está visível.
  aberto = signal<boolean>(false);

  // Histórico de mensagens trocadas nesta sessão (perdido ao recarregar a
  // página — não há persistência, é só estado em memória do componente).
  mensagens = signal<MensagemChat[]>([]);

  // true enquanto aguardamos a resposta do backend, usado no template para
  // mostrar o indicador de "digitando..." e desabilitar o envio.
  carregando = signal<boolean>(false);

  // Texto atual do input, ligado via [(ngModel)] no template.
  textoInput = '';

  alternarAberto(): void {
    this.aberto.set(!this.aberto());
  }

  enviar(): void {
    const texto = this.textoInput.trim();

    if (!texto || this.carregando()) {
      return;
    }

    this.mensagens.update((atual) => [...atual, { autor: 'usuario', texto }]);
    this.textoInput = '';
    this.carregando.set(true);

    this.chatService.enviarMensagem(texto).subscribe({
      next: (resultado) => {
        this.mensagens.update((atual) => [...atual, { autor: 'bot', texto: resultado.resposta }]);
        this.carregando.set(false);
      },
      error: () => {
        this.mensagens.update((atual) => [
          ...atual,
          { autor: 'bot', texto: 'Desculpe, não consegui responder agora. Tente novamente.' }
        ]);
        this.carregando.set(false);
      }
    });
  }
}
