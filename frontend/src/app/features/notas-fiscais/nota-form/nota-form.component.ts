import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';

import { NotaFiscalService, NovaNotaFiscal } from '../../../core/services/nota-fiscal.service';
import { Produto, ProdutoService } from '../../../core/services/produto.service';

@Component({
  selector: 'app-nota-form',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './nota-form.component.html'
})
export class NotaFormComponent implements OnInit {
  private readonly notaFiscalService = inject(NotaFiscalService);
  private readonly produtoService = inject(ProdutoService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  // Lista de produtos disponíveis para escolher em cada item da nota,
  // carregada uma vez no ngOnInit e usada para preencher as opções de cada
  // <select> no template.
  produtos = signal<Produto[]>([]);

  erro = signal<string | null>(null);

  // FormGroup comum (como o de produto-form) descreve uma quantidade FIXA
  // de campos, conhecida em tempo de compilação (ex: sempre "codigo",
  // "descricao" e "saldo"). Uma nota fiscal, porém, pode ter 1, 3 ou 20
  // itens — esse número só é decidido pelo usuário, clicando em "Adicionar
  // Item" quantas vezes quiser, ENQUANTO a tela já está aberta. Não dá para
  // declarar "item1, item2, item3..." como campos fixos do FormGroup sem
  // saber de antemão quantos existirão.
  //
  // FormArray resolve exatamente isso: é uma LISTA de controles do mesmo
  // "formato" (aqui, cada posição é um FormGroup com produtoId +
  // quantidade), que pode crescer (push) ou encolher (removeAt) em tempo de
  // execução. O array "itens" abaixo fica aninhado dentro do FormGroup
  // principal do formulário, então o form inteiro (título + itens) ainda é
  // validado e lido como um único objeto.
  form = this.fb.group({
    itens: this.fb.array([this.criarItem()])
  });

  // Getter de conveniência para acessar o FormArray a partir do template
  // (tanto no .ts quanto no .html) sem precisar escrever
  // "form.controls.itens" toda vez.
  get itens() {
    return this.form.controls.itens;
  }

  ngOnInit(): void {
    this.produtoService.listar().subscribe((produtos) => {
      this.produtos.set(produtos);
    });
  }

  // Cria um novo FormGroup "molde" para um item da nota: produtoId
  // (obrigatório — o usuário precisa escolher um produto da lista) e
  // quantidade (obrigatória e não pode ser menor que 1, já que não faz
  // sentido uma nota com "0 unidades" de um item).
  private criarItem() {
    return this.fb.group({
      produtoId: ['', Validators.required],
      quantidade: [1, [Validators.required, Validators.min(1)]]
    });
  }

  adicionarItem(): void {
    this.itens.push(this.criarItem());
  }

  removerItem(index: number): void {
    this.itens.removeAt(index);
  }

  salvar(): void {
    if (this.form.invalid) {
      // markAllAsTouched percorre o FormGroup inteiro, incluindo cada
      // FormGroup dentro do FormArray, marcando todos os controles como
      // "touched" — é isso que faz as mensagens de erro de cada item
      // aparecerem mesmo que o usuário nunca tenha clicado neles.
      this.form.markAllAsTouched();
      return;
    }

    const nota: NovaNotaFiscal = {
      itens: this.itens.controls.map((item) => ({
        produtoId: Number(item.value.produtoId),
        quantidade: Number(item.value.quantidade)
      }))
    };

    // Assim como em nota-list, usamos o objeto { next, error } para tratar
    // explicitamente uma falha do backend ao criar a nota (ex: produtoId
    // inexistente) e mostrar isso na tela, em vez de deixar o erro sem
    // tratamento.
    this.notaFiscalService.criar(nota).subscribe({
      next: () => {
        this.router.navigate(['/notas']);
      },
      error: (err: HttpErrorResponse) => {
        const mensagem =
          typeof err.error === 'string' && err.error.trim().length > 0
            ? err.error
            : 'Não foi possível salvar a nota fiscal. Tente novamente.';
        this.erro.set(mensagem);
      }
    });
  }

  cancelar(): void {
    this.router.navigate(['/notas']);
  }
}
