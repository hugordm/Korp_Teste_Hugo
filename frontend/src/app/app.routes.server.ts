import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  {
    // 'produtos/:id/editar' tem um parâmetro dinâmico (:id) cujos valores só
    // existem em tempo de execução, vindos da API de estoque — não há como o
    // Angular pré-renderizar (gerar HTML estático em build) essa rota sem
    // saber de antemão todos os ids possíveis. Por isso ela usa
    // RenderMode.Client: essa tela é renderizada só no navegador, e não no
    // servidor/build.
    path: 'produtos/:id/editar',
    renderMode: RenderMode.Client
  },
  {
    path: '**',
    renderMode: RenderMode.Prerender
  }
];
