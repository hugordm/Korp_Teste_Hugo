import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { Produto, ProdutoService } from '../../../core/services/produto.service';

@Component({
  selector: 'app-produto-list',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './produto-list.component.html'
})
export class ProdutoListComponent implements OnInit {
  private readonly produtoService = inject(ProdutoService);

  // signal<Produto[]>([]) cria um pedaço de estado reativo local ao
  // componente, começando como lista vazia. Um signal é a forma moderna do
  // Angular de guardar e expor estado: diferente de um Observable (que
  // representa um "fluxo" de valores ao longo do tempo e só entrega algo
  // depois de alguém se inscrever com .subscribe()), um signal já contém o
  // valor atual pronto para leitura a qualquer momento (chamando
  // "produtos()") e, quando o valor muda (com .set() ou .update()), o
  // Angular sabe exatamente quais partes do template dependem dele e
  // atualiza só isso — sem precisar do "| async" no template nem de
  // gerenciar inscrição/desinscrição manualmente. Para estado simples como
  // "a lista de produtos que a tela está exibindo agora", um signal é mais
  // direto que manter um Observable exposto e usar async pipe.
  produtos = signal<Produto[]>([]);

  // ngOnInit é o hook do ciclo de vida do Angular chamado uma vez, logo
  // depois que o componente é criado e suas propriedades de entrada (@Input,
  // se houvesse) já foram atribuídas. É o lugar recomendado para disparar a
  // busca inicial de dados — diferente do construtor, que deve só configurar
  // injeção de dependências e não fazer trabalho pesado/assíncrono, o
  // ngOnInit já roda num momento em que o componente está totalmente
  // inicializado e pronto para reagir à chegada dos dados.
  ngOnInit(): void {
    this.carregarProdutos();
  }

  private carregarProdutos(): void {
    // produtoService.listar() devolve um Observable, que é "preguiçoso": ele
    // só descreve a requisição HTTP, mas não a dispara sozinho. É a chamada
    // a ".subscribe()" que efetivamente "ativa"/executa o Observable — nesse
    // momento a requisição GET é enviada ao Estoque.API. Quando a resposta
    // chega, a função passada para subscribe() é executada com a lista de
    // produtos, e é aí que atualizamos o signal com ".set(...)" para o
    // template refletir os dados na tela.
    this.produtoService.listar().subscribe((produtos) => {
      this.produtos.set(produtos);
    });
  }

  excluir(id: number): void {
    this.produtoService.excluir(id).subscribe(() => {
      // Após excluir com sucesso no backend, recarregamos a lista para que
      // o produto removido também desapareça da tela, refletindo o novo
      // estado real do servidor.
      this.carregarProdutos();
    });
  }
}
