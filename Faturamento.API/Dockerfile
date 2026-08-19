# ===== Etapa 1: BUILD =====
# Imagem do SDK do .NET 8 — contém o compilador e as ferramentas necessárias
# para restaurar pacotes NuGet e compilar o projeto. É "pesada" (várias
# centenas de MB), por isso ela só existe durante o build e não vai
# para a imagem final que roda em produção.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Define a pasta de trabalho dentro do container onde os comandos seguintes
# serão executados (equivale a um "cd /src" dentro do container).
WORKDIR /src

# Copia primeiro só o arquivo .csproj (e não o código todo) para dentro do
# container. Isso é um truque de cache do Docker: se só o código mudar mas
# as dependências (pacotes NuGet) continuarem as mesmas, o Docker reaproveita
# a camada do "dotnet restore" e não baixa tudo de novo a cada build.
COPY Faturamento.API.csproj .

# Baixa todos os pacotes NuGet referenciados no .csproj (Npgsql,
# EntityFrameworkCore, Swashbuckle, etc).
RUN dotnet restore Faturamento.API.csproj

# Agora sim copia o restante do código-fonte do projeto para dentro do container.
COPY . .

# Compila o projeto em modo Release (otimizado para produção) e publica o
# resultado (DLLs prontas para rodar) na pasta /app/publish dentro do container.
RUN dotnet publish Faturamento.API.csproj -c Release -o /app/publish

# ===== Etapa 2: RUNTIME =====
# Imagem enxuta, com apenas o runtime do ASP.NET Core 8 (sem SDK/compilador).
# É essa imagem, bem menor, que efetivamente roda em produção.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# Pasta de trabalho dentro do container final.
WORKDIR /app

# Copia da etapa "build" apenas os arquivos já publicados (DLLs, configs),
# sem levar código-fonte nem ferramentas de compilação para a imagem final.
COPY --from=build /app/publish .

# Informa ao Docker que a aplicação escuta na porta 8080 dentro do container
# (é só documentação/metadado da imagem; quem realmente expõe a porta para
# fora é o mapeamento de portas feito no docker-compose.yml).
EXPOSE 8080

# Diz ao Kestrel (servidor web do ASP.NET Core) para escutar em todas as
# interfaces de rede do container na porta 8080, em vez do padrão https/5000-5001.
# Isso é necessário porque dentro do container não configuramos certificado HTTPS.
ENV ASPNETCORE_URLS=http://+:8080

# Comando executado quando o container inicia: roda a aplicação compilada.
ENTRYPOINT ["dotnet", "Faturamento.API.dll"]
