import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { ProdutoService } from '../../../core/services/produto.service';

// Este componente é usado tanto para CRIAR quanto para EDITAR um produto.
// A tela, os campos e as validações são idênticos nos dois casos — o único
// que muda é: (a) se o formulário começa vazio ou já preenchido com os
// dados de um produto existente, e (b) se ao salvar chamamos
// produtoService.criar() ou produtoService.atualizar(). Ter um único
// componente para os dois casos evita duplicar o HTML do formulário (campos,
// labels, mensagens de erro, layout) e a lógica de validação em dois lugares
// diferentes — se amanhã precisar adicionar um campo novo, é uma mudança só,
// em um só arquivo, em vez de lembrar de replicar em "produto-criar" e
// "produto-editar" separados.
@Component({
  selector: 'app-produto-form',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './produto-form.component.html'
})
export class ProdutoFormComponent implements OnInit {
  private readonly produtoService = inject(ProdutoService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  // FormBuilder é um serviço "de conveniência" do Angular Reactive Forms:
  // em vez de montar cada FormControl manualmente ("new FormControl('', ...)"
  // para cada campo), o FormBuilder oferece uma sintaxe mais enxuta
  // (fb.group({...})) para descrever a estrutura inteira do formulário de
  // uma vez.
  private readonly fb = inject(FormBuilder);

  // Guarda o Id do produto sendo editado (null em modo criação). Usado no
  // método salvar() para saber qual chamada fazer e qual Id passar.
  private produtoId: number | null = null;

  // signal<boolean> exposto ao template para saber se a tela está em modo
  // edição ou criação — usado para trocar o título ("Novo Produto" /
  // "Editar Produto") e o texto do botão sem duplicar o template inteiro
  // num @if/@else.
  editando = signal<boolean>(false);

  // Reactive Forms x Template-Driven Forms: no Angular existem duas formas
  // de trabalhar com formulários. No Template-Driven, o formulário é
  // definido majoritariamente no HTML (com [(ngModel)]), e o Angular cria a
  // estrutura de controles "por trás das cortinas" de forma implícita. No
  // Reactive Forms (usado aqui), a estrutura do formulário — quais campos
  // existem, seus valores iniciais e suas validações — é definida
  // explicitamente aqui no TypeScript, como um objeto (FormGroup) que o
  // template apenas "liga" via [formGroup]/formControlName. Isso torna o
  // formulário mais fácil de testar (dá para validar/preencher via código,
  // sem precisar renderizar o template) e o fluxo de dados mais previsível
  // (o TypeScript é a fonte da verdade do estado do formulário, não o DOM).
  //
  // FormGroup agrupa vários FormControls (um por campo) num único objeto,
  // permitindo checar validade do formulário inteiro de uma vez
  // (form.valid) e ler/escrever todos os valores juntos (form.value,
  // form.patchValue(...)).
  //
  // Validators são funções prontas do Angular que checam uma regra sobre o
  // valor do campo e devolvem um erro (ou null se estiver válido). Cada
  // FormControl recebe um array de validators, que rodam automaticamente
  // toda vez que o valor do campo muda:
  // - Validators.required: o campo não pode ficar vazio.
  // - Validators.min(0): o valor numérico não pode ser menor que 0 (usado
  //   em "saldo", já que não faz sentido estoque negativo).
  form = this.fb.group({
    codigo: ['', Validators.required],
    descricao: ['', Validators.required],
    saldo: [0, [Validators.required, Validators.min(0)]]
  });

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');

    if (idParam) {
      // Existe um "id" na rota (ex: /produtos/5/editar): modo edição.
      this.produtoId = Number(idParam);
      this.editando.set(true);

      this.produtoService.buscarPorId(this.produtoId).subscribe((produto) => {
        // patchValue preenche só os campos informados do formulário com os
        // dados do produto encontrado (aqui coincide com todos os campos,
        // mas patchValue não exige bater 1:1 com a estrutura do FormGroup
        // como setValue exigiria).
        this.form.patchValue({
          codigo: produto.codigo,
          descricao: produto.descricao,
          saldo: produto.saldo
        });
      });
    }
    // Se não há "id" na rota, é modo criação: o formulário permanece com os
    // valores iniciais vazios já definidos acima.
  }

  salvar(): void {
    if (this.form.invalid) {
      // markAllAsTouched faz cada FormControl do grupo passar a ser
      // considerado "touched", que é a condição que o template usa para
      // decidir se mostra a mensagem de erro de cada campo. Sem isso, se o
      // usuário nunca clicou/saiu de um campo obrigatório vazio, a mensagem
      // de erro correspondente nunca apareceria, mesmo com o clique em
      // "Salvar".
      this.form.markAllAsTouched();
      return;
    }

    // O "!" abaixo é seguro porque os três campos têm Validators.required
    // (ou min), logo se o form é válido eles não podem ser null/undefined.
    const valores = {
      codigo: this.form.value.codigo!,
      descricao: this.form.value.descricao!,
      saldo: this.form.value.saldo!
    };

    if (this.editando() && this.produtoId !== null) {
      this.produtoService
        .atualizar(this.produtoId, { id: this.produtoId, ...valores })
        .subscribe(() => this.router.navigate(['/produtos']));
    } else {
      this.produtoService.criar(valores).subscribe(() => this.router.navigate(['/produtos']));
    }
  }

  cancelar(): void {
    this.router.navigate(['/produtos']);
  }
}
