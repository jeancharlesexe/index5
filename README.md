# Documentação do Backend Index5

Este documento detalha a arquitetura, as dependências, os passos de instalação e como rodar o projeto e seus testes localmente. O backend é desenvolvido em **.NET 10** seguindo conceitos de **Clean Architecture** (Arquitetura Limpa).

---

## 🏗️ Arquitetura

O projeto utiliza **Clean Architecture**, dividindo suas responsabilidades em diferentes camadas e projetos para garantir manutenibilidade, testabilidade e baixo acoplamento:

1. **Index5.API:** A camada de Apresentação (Acesso Externo). É o ponto de entrada da aplicação, contendo os Controllers, middlewares configurados, e a documentação interativa das rotas via OpenAPI (Scalar).
2. **Index5.Application:** Camada de Casos de Uso. Processa as regras de negócio orquestrando as manipulações do banco e mensageria. Contém os Serviços, DTOs e Interfaces que a camada principal implementa.
3. **Index5.Domain:** O coração da aplicação. Contém as Entidades ricas, regras de negócio isoladas, Value Objects, Enums e Exceções de Domínio. Não possui nenhuma dependência externa (banco de dados ou web).
4. **Index5.Infrastructure:** A camada de Infraestrutura. Responsável por lidar com integrações externas. Aqui mora o contexto do Entity Framework Core e os repositórios (acesso a um banco de dados **MySQL**), bem como as configurações de produtor/consumidor do **Apache Kafka**.

Além dos projetos base, temos:
- **Index5.UnitTests:** Testes unitários para validar a lógica de domínio e regras de negócio de aplicação, sem integrações reais (mockando o que for externo).
- **Index5.IntegrationTests:** Testes de integração, garantindo que o ciclo completo de comunicação entre API, banco de dados e mensageria atue conforme o esperado.

### Tecnologias Principais
- **.NET 10.0** (ASP.NET Core Web API)
- **C#**
- **Entity Framework Core (EF Core)**
- **MySQL 8.0**
- **Apache Kafka** (mensageria em background)
- **BCrypt** (hashing de senhas)
- **JWT (JSON Web Token)** (autenticação e autorização)
- **Docker & Docker Compose**

---

## 🛠️ Pré-requisitos

Para rodar esta aplicação adequadamente nas configurações padrão (desenvolvimento), você precisará instalar:

1. [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
2. [Docker Desktop](https://www.docker.com/products/docker-desktop/) (ou Docker Compose standalone) e garantir que o daemon esteja executando.
3. [Git](https://git-scm.com/) (opcional, apenas para clonagem do projeto).

---

## 🚀 Como Executar o Backend Passo a Passo

Siga este procedimento na raiz do diretório do projeto `index5`.

### Passo 1: Subir os Containers Docker (Infraestrutura)
O projeto depende do MySQL e Kafka para rodar. Existe um arquivo `docker-compose.yml` pré-configurado.
No seu terminal, na pasta raiz (onde fica o `docker-compose.yml`), execute o comando:

```bash
docker-compose up -d
```
*Isto irá preparar e iniciar:*
- Servidor **MySQL 8.0** respondendo na porta `3307`.
- Servidor **Zookeeper** usado internamente pelo Kafka.
- Corretor **Apache Kafka** em `localhost:9092`.

### Passo 2: Validar a Configuração Local do Ambiente
Confirme no arquivo `Index5/Index5.API/appsettings.json` se os caminhos e senhas condizem com o local.
- O campo `Cotacoes:Folder` deve apontar no seu disco local para a pasta `cotacoes` (Ex: `C:\Users\jcalbuquerque\Desktop\it-test\index5\cotacoes`). Esta pasta fornece os dados (arquivos locais) na qual o backend depende para algumas rotinas do negócio.

### Passo 3: Criar / Atualizar o Banco de Dados (Entity Framework)
O EF Core pode precisar que você aplique no banco de dados todas as migrações criadas no projeto de Infraestrutura. No terminal:

```bash
cd Index5

# Certifique-se de ter a ferramenta do EF (caso não tenha instalado globalmente)
dotnet tool install --global dotnet-ef

# Aplicar o migration no MySQL criado pelo Docker
dotnet ef database update --project Index5.Infrastructure/Index5.Infrastructure.csproj --startup-project Index5.API/Index5.API.csproj
```

*Nota: Em alguns casos o método `context.Database.EnsureCreated()` ou `context.Database.Migrate()` já é engatilhado no `Program.cs` para subir o banco automaticamente se este for recém criado. Verifique se o console apontou essa transação se não quiser aplicar os comandos EF Core na mão.*

### Passo 4: Executar o Projeto API
Ainda dentro da pasta `Index5`, inicie a aplicação web hospedada pela CLI do `dotnet`:

```bash
dotnet run --project Index5.API/Index5.API.csproj
```

### Passo 5: Acessar a Documentação no Navegador
Quando executado o comando do passo interior, seu console indicará a URL onde está operando, geralmente `http://localhost:5246` ou porta análoga (`https://localhost:xxxx`).
Use o navegador para ir nos endpoints documentados pelo `Scalar OpenApi`:
**URL Padrão de Doc:** `http://localhost:<porta>/scalar/v1` (ou análoga gerada no log do console).

---

## 🖥️ Executando via Visual Studio Community (Interface Gráfica)

Se você prefere evitar comandos no terminal para a execução e utilizar a IDE oficial da Microsoft, siga estes passos:

1. **Suba os Containers (Dependências):** O Visual Studio por si só não sobe o banco de dados e o mensageiro neste projeto (a não ser que você tenha configurado a orquestração do Docker no VS). Você ainda precisará executar `docker-compose up -d` na raiz do projeto (`index5`) pelo seu terminal.
2. **Abra a Solução:** Navegue até a pasta `index5/Index5` e dê um duplo-clique no arquivo de solução `Index5.slnx` (formato de solução mais recente do .NET). O Visual Studio Community abrirá e carregará os diversos subprojetos.
3. **Defina o Projeto de Inicialização:** No **Gerenciador de Soluções** (Solution Explorer), encontre o projeto **`Index5.API`**, clique nele com o botão direito e selecione **"Definir como Projeto de Inicialização"** (Set as Startup Project). Ele ficará negrito.
4. **Verifique os "appsettings":** Da mesma forma que na CLI, valide se o arquivo `appsettings.json` na API aponta `Cotacoes:Folder` para um caminho existente na sua máquina.
5. **Executar a API:** Pressione `F5` ou clique no botão **"Iniciar"** (Play verde) na barra superior. O Visual Studio irá baixar as dependências do NuGet automaticamente, compilar a solução (Build) e abrir a janela do navegador em seu endpoint inicial.
6. **Rodar os Testes (Opcional):** Para rodar os testes via IDE, acesse o menu superior **"Teste" > "Gerenciador de Testes"** (Test Explorer). Clique no botão "Executar Todos os Testes na Exibição" (Run All). Uma árvore será gerada mostrando os métodos que passaram (ícone verde) e os que falharam (ícone vermelho).

---

## 🧪 Como Rodar os Testes

O ambiente também engloba validações automatizadas implementadas com xUnit.

### Executando Todos os Testes Juntos
Para rodar tanto os unitários quanto os de integração, abra o terminal na subpasta raiz da solução (`Index5`) e execute:

```bash
dotnet test
```

### Executando Camadas Específicas
Você pode indicar em qual camada focar, caso deseje um log menor ou depurar coisas de maneira separada:

**Apenas Testes Unitários:**
```bash
dotnet test Index5.UnitTests/Index5.UnitTests.csproj
```

**Apenas Testes de Integração:**
```bash
dotnet test Index5.IntegrationTests/Index5.IntegrationTests.csproj
```
*(Para os testes de integração, você possivelmente precisa garantir que seu Docker Compose esteja de pé, pois o EF/Kafka geralmente apontam ou simulam bancos e tópicos nestas rotinas mais densas).*

---

## 🛑 Como Parar os Serviços

Ao terminar os trabalhos localizados, é uma boa ideia desligar e limpar seus utilitários base (MySQL e Kafka).

Na raiz repositório `index5`, digite:

```bash
docker-compose down
```
Isto limpa seus containers para não congestionarem seu Windows local.

---

## 📡 API e Padronização de Retornos (JSON)

Para manter a consistência entre o Backend e o código cliente (Frontend Web ou Mobile), todas as respostas da API são envelopadas em uma classe comum chamada `ApiResponse<T>`. 

Isso significa que, independentemente do endpoint (e sabendo se deu sucesso ou erro), você sempre encontrará uma estrutura previsível no body da requisição:

### Estrutura Padrão (Sucesso)
```json
{
  "status": 200,
  "message": "Login successful.",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5...",
    "user": {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "João Silva",
      "role": "CLIENT"
    }
  },
  "timestamp": "2026-02-28T20:34:27"
}
```

### Estrutura Padrão (Erro)
No caso de um erro de validação ou regra de negócio (como usuário não encontrado, ou CPF inválido), a API retornará o status HTTP correspondente (e.g., `400 BadRequest` ou `401 Unauthorized`), porém também vai devolver um JSON bem formatado detalhando o código do erro do domínio:
```json
{
  "status": 401,
  "message": "Invalid CPF or password.",
  "data": {
    "code": "INVALID_CREDENTIALS"
  },
  "timestamp": "2026-02-28T20:35:10"
}
```

---

## 🔗 Exemplos de Endpoints

Aqui estão exemplos comuns para entender a separação de responsabilidades nas rotas do projeto:

### 1. Autenticação (Auth)
Usado tanto por Administradores quanto por Clientes finais.

- **`POST /api/v1/auth/register`**
  Cria um novo usuário na plataforma (cliente ou admin).
- **`POST /api/v1/auth/login/client`**
  Autentica um cliente através do CPF e Senha, retornando seu JWT *Bearer Token*.
- **`POST /api/v1/auth/login/admin`**
  Autentica um admin através da sua chave corporativa (JKey) e Senha.

### 2. Administrativo (Admin)
Rotas protegidas que só podem ser invocadas se o token JWT recebido contiver o papel `ADMIN`.

- **`GET /api/v1/admin/clients/pending`**
  Lista todos os clientes que se registraram e aguardam aprovação de cadastro por um funcionário.
- **`POST /api/v1/admin/clients/{clientId}/approve`**
  Aprova o cadastro de um cliente permitindo-o utilizar a plataforma em sua totalidade.
- **`GET /api/v1/admin/basket/current`**
  Visualiza quais as ações e percentuais da carteira/cesta de investimentos do mês vigente recomendados pela corretora.

### 3. Área do Cliente (Client)
Rotas protegidas exclusivas para usuários `CLIENT`.

- **`GET /api/v1/client/dashboard`**
  Retorna um resumo de tela principal do Mobile ou Home do site (dados de investimentos consolidados, saldo e dados pessoais).
- **`POST /api/v1/client/investment/update-value`**
  Altera o valor recorrente do investimento mensal deste cliente.
- **`POST /api/v1/client/investment/exit`**
  Inicia o fluxo do cliente solicitar pausa/saída do fundo de investimento em questão.

---

## 📖 Documentação Interativa com Scalar (OpenAPI)

Para explorar todos os endpoints de forma interativa, visualizar os **Schemas de requisição e resposta**, e testar chamadas diretamente do seu navegador, o projeto expõe uma interface gráfica baseada em OpenAPI gerada com **Scalar**.

### Como acessar

1. Certifique-se de que a API (`Index5.API`) esteja em execução.
2. Abra o seu navegador e acesse a URL base onde a API está operando (ex: `http://localhost:5246` ou porta análoga).
3. Navegue para o caminho `/scalar/v1`:

```
http://localhost:5246/scalar/v1
```

Lá você encontrará a listagem detalhada de todos os controllers (Auth, Admin, Client e Engine), os modelos de JSON esperados para todas as requisições (`Request`) e o padrão detalhado do `ApiResponse` retornado em cada caso. Esta é a ferramenta ideal para uma rápida análise técnica e documentação viva das funcionalidades sem precisar ler o código.
