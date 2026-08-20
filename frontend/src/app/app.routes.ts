import { Routes } from '@angular/router';

// Lazy loading (loadComponent): em vez de importar o componente no topo do
// arquivo (o que faria o Angular incluí-lo no bundle inicial, carregado já
// na abertura do app), cada rota abaixo recebe uma função que só executa o
// import() quando o usuário efetivamente navega até aquela rota. Isso quebra
// o JavaScript da aplicação em pedaços menores (code splitting) e deixa o
// carregamento inicial mais rápido, já que a tela de produtos só baixa o
// código de "novo/editar produto" se o usuário realmente for até lá, e
// vice-versa.
export const routes: Routes = [
  {
    // Rota padrão: quando o usuário abre o app em '' (a raiz), ele é
    // redirecionado para '/produtos'. pathMatch: 'full' garante que esse
    // redirect só se aplica quando a URL é exatamente vazia, e não sempre
    // que '' aparece como prefixo de outra rota.
    path: '',
    redirectTo: '/produtos',
    pathMatch: 'full'
  },
  {
    path: 'produtos',
    loadComponent: () =>
      import('./features/produtos/produto-list/produto-list.component').then(
        (m) => m.ProdutoListComponent
      )
  },
  {
    path: 'produtos/novo',
    loadComponent: () =>
      import('./features/produtos/produto-form/produto-form.component').then(
        (m) => m.ProdutoFormComponent
      )
  },
  {
    // ':id' é um parâmetro de rota — o Angular captura o trecho da URL nessa
    // posição (ex: 'produtos/5/editar' -> id = '5') e o componente lê esse
    // valor via ActivatedRoute para saber qual produto carregar e editar.
    path: 'produtos/:id/editar',
    loadComponent: () =>
      import('./features/produtos/produto-form/produto-form.component').then(
        (m) => m.ProdutoFormComponent
      )
  },
  {
    // Rota curinga (wildcard): casa com qualquer URL que não bateu com
    // nenhuma rota acima (ex: erro de digitação, link quebrado). Precisa ser
    // sempre a última rota da lista, já que o Angular avalia as rotas em
    // ordem e '**' casaria com tudo se viesse antes.
    path: '**',
    redirectTo: '/produtos'
  }
];
