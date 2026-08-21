import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import jsPDF from 'jspdf';

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

  // Mesma ideia do "erro" acima, mas para o toast verde de sucesso mostrado
  // após imprimir uma nota fiscal com êxito. Guardar num signal (em vez de
  // um alert()) deixa o feedback como parte da UI normal da tela, com
  // transição suave de entrada/saída controlada via CSS.
  mensagemSucesso = signal<string | null>(null);

  // Id do setTimeout que esconde o toast de sucesso sozinho depois de
  // alguns segundos — mesmo motivo do erroTimeoutId: cancelar um timeout
  // anterior se um novo sucesso aparecer antes do primeiro sumir.
  private sucessoTimeoutId: ReturnType<typeof setTimeout> | null = null;

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
        this.mostrarSucesso('Nota fiscal impressa com sucesso! Saldo dos produtos atualizado.');
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

  private mostrarSucesso(mensagem: string): void {
    if (this.sucessoTimeoutId !== null) {
      clearTimeout(this.sucessoTimeoutId);
    }

    this.mensagemSucesso.set(mensagem);

    this.sucessoTimeoutId = setTimeout(() => {
      this.mensagemSucesso.set(null);
      this.sucessoTimeoutId = null;
    }, 4000);
  }

  fecharSucesso(): void {
    if (this.sucessoTimeoutId !== null) {
      clearTimeout(this.sucessoTimeoutId);
      this.sucessoTimeoutId = null;
    }
    this.mensagemSucesso.set(null);
  }

  // Geração de PDF 100% client-side: o jsPDF monta o arquivo inteiro dentro
  // do navegador, a partir dos dados da nota que já estão carregados na tela
  // (o array "notas()" veio do listar() no ngOnInit). Não há nenhuma
  // requisição adicional ao backend aqui — nem para gerar o PDF, nem para
  // buscar dados extras — o download acontece só com o que já temos em mãos.
  //
  // Código e Descrição do produto vêm prontos em cada item (campos
  // "codigoProduto"/"descricaoProduto", preenchidos pelo Faturamento.API a
  // partir de uma consulta ao Estoque.API). O frontend não precisa fazer
  // nenhuma chamada extra nem cruzar essa informação com uma lista de
  // produtos carregada à parte — só exibe o que já veio na nota.
  baixarPdf(nota: NotaFiscal): void {
    const doc = new jsPDF();

    // Cabeçalho em destaque: fonte maior e em negrito.
    doc.setFontSize(18);
    doc.setFont('helvetica', 'bold');
    doc.text('NOTA FISCAL', 14, 20);

    // Dados principais da nota, em fonte normal, um pouco menor.
    doc.setFontSize(11);
    doc.setFont('helvetica', 'normal');
    doc.text(`Número: ${nota.numero}`, 14, 32);
    doc.text(`Status: ${nota.status}`, 14, 39);
    doc.text(`Data de Criação: ${new Date(nota.dataCriacao).toLocaleString('pt-BR')}`, 14, 46);

    // Lista de itens em formato de "tabela simples": um cabeçalho de colunas
    // seguido de uma linha por item, usando espaçamento fixo entre colunas
    // (em vez de uma tabela sofisticada, que exigiria um plugin extra como
    // jspdf-autotable).
    doc.setFont('helvetica', 'bold');
    doc.text('Itens', 14, 58);
    doc.text('Código', 14, 65);
    doc.text('Descrição', 60, 65);
    doc.text('Quantidade', 160, 65);

    doc.setFont('helvetica', 'normal');
    let y = 72;
    for (const item of nota.itens) {
      // Quando o produto não foi encontrado no Estoque.API (excluído, ou o
      // serviço estava indisponível na hora da consulta), o backend manda
      // "codigoProduto"/"descricaoProduto" como null — exibimos um texto
      // explicativo no lugar em vez de deixar a célula em branco ou
      // imprimir "null".
      const codigo = item.codigoProduto ?? 'Produto não encontrado';
      const descricao = item.descricaoProduto ?? 'Produto não encontrado';

      doc.text(codigo, 14, y);
      doc.text(descricao, 60, y);
      doc.text(String(item.quantidade), 160, y);
      y += 7;
    }

    doc.save(`nota-fiscal-${nota.numero}.pdf`);
  }

  // Controla quais notas estão "expandidas" na listagem, mostrando a lista
  // de itens (Código, Descrição, Quantidade) diretamente na tela — sem essa
  // expansão, a tabela principal só mostra a quantidade de itens, sem
  // detalhar quais produtos compõem a nota. Um Set guarda os ids das notas
  // expandidas no momento; alternamos (expande/recolhe) um id por vez.
  private readonly notasExpandidas = signal<ReadonlySet<number>>(new Set());

  estaExpandida(notaId: number): boolean {
    return this.notasExpandidas().has(notaId);
  }

  alternarExpansao(notaId: number): void {
    const atual = new Set(this.notasExpandidas());

    if (atual.has(notaId)) {
      atual.delete(notaId);
    } else {
      atual.add(notaId);
    }

    this.notasExpandidas.set(atual);
  }
}
