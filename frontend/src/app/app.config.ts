import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';

import { routes } from './app.routes';
import { provideClientHydration, withEventReplay } from '@angular/platform-browser';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideClientHydration(withEventReplay()),
    // provideHttpClient() registra o HttpClient (e toda a infraestrutura de
    // interceptors/backend HTTP) no injetor global da aplicação. Sem isso,
    // qualquer service que peça "inject(HttpClient)" (como o ProdutoService)
    // falharia em tempo de execução com um erro de "no provider for
    // HttpClient" — em apps standalone (sem NgModules) esse provider não
    // existe por padrão, precisa ser habilitado explicitamente aqui.
    provideHttpClient()
  ]
};
