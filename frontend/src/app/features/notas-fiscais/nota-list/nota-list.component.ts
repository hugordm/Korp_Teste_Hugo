import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';

import { NotaFiscal, NotaFiscalService } from '../../../core/services/nota-fiscal.service';

@Component({
  selector: 'app-nota-list',
  standalone: true,
  imports: [RouterLink, DatePipe],
  templateUrl: './nota-list.component.html'
})
export class NotaListComponent implements OnInit {
  private readonly notaFiscalService = inject(NotaFiscalService);

  notas = signal<NotaFiscal[]>([]);

  // signal<string | null> guarda a mensagem do último erro (ex: ao tentar
  // imprimir uma nota) para exibir um banner/toast no template. Fica null
  // quando não há erro a mostrar. Guardar isso num signal (em vez de só
  // um "alert()" do navegador) permite renderizar o erro como parte da UI
  // normal da tela, com estilo próprio e um botão de fechar.
  erro = signal<string | null>(null);

  // Id do setTimeout usado para esconder o erro sozinho depois de alguns
  // segundos. Guardamos a referência para poder cancelar (clearTimeout) um
  // timeout anterior caso um novo erro apareça antes do primeiro sumir —
  // sem isso, dois erros seguidos poderiam brigar por qual limpa o signal
  // primeiro, e o segundo erro sumiria antes da hora.
  private erroTimeoutId: ReturnType<typeof setTimeout> | null = null;

  ngOnInit(): void {
    this.carregarNotas();
  }

  private carregarNotas(): void {
    this.notaFiscalService.listar().subscribe((notas) => {
      this.notas.set(notas);
    });
  }

  // Por que tratamos erro aqui com o objeto { next, error } em vez de passar
  // só uma função para subscribe(): um Observable pode terminar de duas
  // formas — com sucesso (next) ou com falha (error). Se passarmos só o
  // callback de sucesso, um erro HTTP (ex: 400 quando o backend recusa
  // imprimir uma nota já fechada, ou 502 se o Estoque.API estiver fora do
  // ar durante a baixa de saldo) simplesmente não seria tratado: o Angular
  // reportaria um "Unhandled Error" no console e a tela ficaria travada sem
  // nenhum feedback para quem clicou no botão. Informar o segundo callback
  // (error) é o que permite capturar essa falha e mostrar uma mensagem
  // amigável na tela, em vez de deixar o usuário sem saber o que aconteceu.
  imprimir(id: number): void {
    this.notaFiscalService.imprimir(id).subscribe({
      next: () => {
        // Após imprimir com sucesso, recarregamos a lista para que o status
        // da nota (agora "Fechada") reflita na tela imediatamente.
        this.carregarNotas();
      },
      error: (err: HttpErrorResponse) => {
        const mensagem =
          typeof err.error === 'string' && err.error.trim().length > 0
            ? err.error
            : 'Não foi possível imprimir a nota fiscal. Tente novamente.';
        this.mostrarErro(mensagem);
      }
    });
  }

  private mostrarErro(mensagem: string): void {
    if (this.erroTimeoutId !== null) {
      clearTimeout(this.erroTimeoutId);
    }

    this.erro.set(mensagem);

    this.erroTimeoutId = setTimeout(() => {
      this.erro.set(null);
      this.erroTimeoutId = null;
    }, 6000);
  }

  fecharErro(): void {
    if (this.erroTimeoutId !== null) {
      clearTimeout(this.erroTimeoutId);
      this.erroTimeoutId = null;
    }
    this.erro.set(null);
  }
}
