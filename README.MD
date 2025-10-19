# PagueVeloz - Processador de Transações

## Visão Geral

O projeto **PagueVeloz** é uma solução .NET 9 para processamento de transações financeiras, incluindo operações de crédito, débito, reserva, captura, estorno e transferência entre contas. O sistema foi desenhado para ser robusto, escalável e seguro, com foco em idempotência, concorrência e resiliência.

Na pasta DBParaTesteLocal tem criação do DB das tabelas utilizadas, com trigger e inserts para fazer as transações entre contas ACC-001 e ACC-002

---

## Decisões Técnicas e Arquiteturais

- **CQRS com MediatR**: Separação clara entre comandos (escrita) e queries (leitura), facilitando manutenção, testes e escalabilidade.
- **Polly**: Implementação de políticas de retry com backoff exponencial para garantir resiliência em operações de acesso ao banco de dados.
- **Idempotência**: Todas as operações financeiras utilizam `ReferenceId` único para evitar duplicidade de processamento. Foram utilizadas triggers no banco de dados também.
- **Locks por conta**: Uso de `SemaphoreSlim` para garantir consistência e evitar race conditions em operações concorrentes por conta.
- **DTOs e Handlers**: Cada operação possui seu próprio DTO de request/response e handler dedicado, facilitando extensão e clareza do fluxo.
- **Optou-se por um único controller** (`TransactionsController`) com uma chamada unificada para processar diversas operações financeiras (crédito, débito, reserva, captura, estorno e transferência) por motivos técnicos e de negócio:
	- **Simplicidade de Integração:** Um endpoint único reduz a complexidade para consumidores da API, que podem enviar múltiplas operações em lote sem precisar conhecer múltiplos endpoints ou contratos diferentes.
	- **Padronização do Fluxo:** Todas as operações seguem o mesmo pipeline de validação, autenticação, logging e tratamento de erros, facilitando manutenção e evolução do sistema.
	- **Flexibilidade para Novas Operações:** Novos tipos de operação podem ser adicionados ao contrato de request sem necessidade de criar novos controllers ou endpoints, mantendo a API extensível e estável.
	- **Testabilidade de várias chamadas:** Vários objetos podem ser adicionados para serem testados em uma única chamada inclusive para verificar riscos de concorrência.
-**Observabilidade**: Foi implementado logging's detalhado para auditoria e monitoramento das transações financeiras através do Microsoft.Extensions.Logging.
-**DI**: Injeção de dependência para facilitar testes(Retornos e Mocks) e modularidade. 

---

## Justificativa para Frameworks/Bibliotecas

- **MediatR versão 12.2.0 (a partir da 13 é monetizado)**: Facilita a implementação do padrão CQRS, desacoplando a lógica de negócio dos controladores e promovendo testabilidade.
- **Polly**: Permite políticas avançadas de retry, circuit breaker e fallback, essenciais para sistemas financeiros que dependem de alta disponibilidade.
- **AutoFixture, Moq, xUnit, FluentAssertions**: Conjunto moderno para testes unitários e de integração, promovendo produtividade, legibilidade e cobertura confiável.
- **FineCodeCoverage**: Extensão que garante coleta de cobertura de testes, importante para qualidade e auditoria do código.
- **ADO.NET(Abordagem)**: Maior controle e máxima performance sem o overhead de um ORM (EF).


---

## Compilação e Execução

- Pré-requisitos:
  - .NET 9 SDK
  - SQL Server (local)

- Execução atráves do Visual Studio 2022 ou via CLI .NET:
  dotnet build
  dotnet run --project PagueVeloz.WebApi

- Executar os comandos SQL na pasta DBParaTesteLocal para criar o banco de dados e tabelas necessárias para testes locais.

---

## Execução dos Testes

- Pode-se rodar todos os testes pelo Visual Studio 2022 (menu Test Explorer > Executar Run All Tests in View) ou via linha de comando:
dotnet test tests/PagueVeloz.UnitTests

- Usar a extensão FineCodeCoverage para visualizar a cobertura de testes no Visual Studio.

---

## Exemplos de uso da API

Pode-se usar para as chamadas o seguinte JSON, mudando as operações com as infos necessárias.
Com esse JSON foi capturado regras de concorrência não cumpridas que foram resolvidas posteriormente no desenvolvimento:

```json
[
  {"accountId":"ACC-001","amount":1000,"operation":"credit","referenceId":"ref-20251018-01","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 1"},
  {"accountId":"ACC-001","amount":2000,"operation":"debit","referenceId":"ref-20251018-02","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 2"},
  {"accountId":"ACC-001","amount":3000,"operation":"credit","referenceId":"ref-20251018-03","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 3"},
  {"accountId":"ACC-001","amount":4000,"operation":"debit","referenceId":"ref-20251018-04","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 4"},
  {"accountId":"ACC-001","amount":5000,"operation":"credit","referenceId":"ref-20251018-05","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 5"},
  {"accountId":"ACC-001","amount":6000,"operation":"reserve","referenceId":"ref-20251018-06","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 6"},
  {"accountId":"ACC-001","amount":7000,"operation":"credit","referenceId":"ref-20251018-07","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 7"},
  {"accountId":"ACC-001","amount":8000,"operation":"debit","referenceId":"ref-20251018-08","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 8"},
  {"accountId":"ACC-001","amount":9000,"operation":"credit","referenceId":"ref-20251018-09","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 9"},
  {"accountId":"ACC-001","amount":10000,"operation":"debit","referenceId":"ref-20251018-10","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 10"},
  {"accountId":"ACC-001","amount":11000,"operation":"credit","referenceId":"ref-20251018-11","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 11"},
  {"accountId":"ACC-001","amount":12000,"operation":"reserve","referenceId":"ref-20251018-12","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 12"},
  {"accountId":"ACC-001","amount":13000,"operation":"credit","referenceId":"ref-20251018-13","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 13"},
  {"accountId":"ACC-001","amount":14000,"operation":"debit","referenceId":"ref-20251018-14","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 14"},
  {"accountId":"ACC-001","amount":15000,"operation":"credit","referenceId":"ref-20251018-15","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 15"},
  {"accountId":"ACC-001","amount":16000,"operation":"debit","referenceId":"ref-20251018-16","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 16"},
  {"accountId":"ACC-001","amount":17000,"operation":"credit","referenceId":"ref-20251018-17","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 17"},
  {"accountId":"ACC-001","amount":18000,"operation":"reserve","referenceId":"ref-20251018-18","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 18"},
  {"accountId":"ACC-001","amount":19000,"operation":"credit","referenceId":"ref-20251018-19","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 19"},
  {"accountId":"ACC-001","amount":20000,"operation":"debit","referenceId":"ref-20251018-20","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 20"},
  {"accountId":"ACC-001","amount":21000,"operation":"credit","referenceId":"ref-20251018-21","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 21"},
  {"accountId":"ACC-001","amount":22000,"operation":"debit","referenceId":"ref-20251018-22","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 22"},
  {"accountId":"ACC-001","amount":23000,"operation":"credit","referenceId":"ref-20251018-23","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 23"},
  {"accountId":"ACC-001","amount":24000,"operation":"reserve","referenceId":"ref-20251018-24","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 24"},
  {"accountId":"ACC-001","amount":25000,"operation":"credit","referenceId":"ref-20251018-25","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 25"},
  {"accountId":"ACC-001","amount":26000,"operation":"debit","referenceId":"ref-20251018-26","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 26"},
  {"accountId":"ACC-001","amount":27000,"operation":"credit","referenceId":"ref-20251018-27","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 27"},
  {"accountId":"ACC-001","amount":28000,"operation":"debit","referenceId":"ref-20251018-28","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 28"},
  {"accountId":"ACC-001","amount":29000,"operation":"credit","referenceId":"ref-20251018-29","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 29"},
  {"accountId":"ACC-001","amount":30000,"operation":"reserve","referenceId":"ref-20251018-30","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 30"},
  {"accountId":"ACC-001","amount":31000,"operation":"credit","referenceId":"ref-20251018-31","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 31"},
  {"accountId":"ACC-001","amount":32000,"operation":"debit","referenceId":"ref-20251018-32","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 32"},
  {"accountId":"ACC-001","amount":33000,"operation":"credit","referenceId":"ref-20251018-33","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 33"},
  {"accountId":"ACC-001","amount":34000,"operation":"debit","referenceId":"ref-20251018-34","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 34"},
  {"accountId":"ACC-001","amount":35000,"operation":"credit","referenceId":"ref-20251018-35","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 35"},
  {"accountId":"ACC-001","amount":36000,"operation":"reserve","referenceId":"ref-20251018-36","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 36"},
  {"accountId":"ACC-001","amount":37000,"operation":"credit","referenceId":"ref-20251018-37","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 37"},
  {"accountId":"ACC-001","amount":38000,"operation":"debit","referenceId":"ref-20251018-38","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 38"},
  {"accountId":"ACC-001","amount":39000,"operation":"credit","referenceId":"ref-20251018-39","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 39"},
  {"accountId":"ACC-001","amount":40000,"operation":"debit","referenceId":"ref-20251018-40","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 40"},
  {"accountId":"ACC-001","amount":41000,"operation":"credit","referenceId":"ref-20251018-41","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 41"},
  {"accountId":"ACC-001","amount":42000,"operation":"reserve","referenceId":"ref-20251018-42","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 42"},
  {"accountId":"ACC-001","amount":43000,"operation":"credit","referenceId":"ref-20251018-43","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 43"},
  {"accountId":"ACC-001","amount":44000,"operation":"debit","referenceId":"ref-20251018-44","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 44"},
  {"accountId":"ACC-001","amount":45000,"operation":"credit","referenceId":"ref-20251018-45","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 45"},
  {"accountId":"ACC-001","amount":46000,"operation":"debit","referenceId":"ref-20251018-46","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 46"},
  {"accountId":"ACC-001","amount":47000,"operation":"credit","referenceId":"ref-20251018-47","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 47"},
  {"accountId":"ACC-001","amount":48000,"operation":"reserve","referenceId":"ref-20251018-48","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 48"},
  {"accountId":"ACC-001","amount":49000,"operation":"credit","referenceId":"ref-20251018-49","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 49"},
  {"accountId":"ACC-001","amount":50000,"operation":"debit","referenceId":"ref-20251018-50","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 50"},
  {"accountId":"ACC-001","amount":51000,"operation":"credit","referenceId":"ref-20251018-51","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 51"},
  {"accountId":"ACC-001","amount":52000,"operation":"debit","referenceId":"ref-20251018-52","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 52"},
  {"accountId":"ACC-001","amount":53000,"operation":"credit","referenceId":"ref-20251018-53","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 53"},
  {"accountId":"ACC-001","amount":54000,"operation":"reserve","referenceId":"ref-20251018-54","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 54"},
  {"accountId":"ACC-001","amount":55000,"operation":"credit","referenceId":"ref-20251018-55","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 55"},
  {"accountId":"ACC-001","amount":56000,"operation":"debit","referenceId":"ref-20251018-56","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 56"},
  {"accountId":"ACC-001","amount":57000,"operation":"credit","referenceId":"ref-20251018-57","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 57"},
  {"accountId":"ACC-001","amount":58000,"operation":"debit","referenceId":"ref-20251018-58","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 58"},
  {"accountId":"ACC-001","amount":59000,"operation":"credit","referenceId":"ref-20251018-59","targetAccountId":"1","sourceAccountId":"0","currency":"BRL","description":"Op 59"},
  {"accountId":"ACC-001","amount":60000,"operation":"reserve","referenceId":"ref-20251018-60","targetAccountId":"1","sourceAccountId":"1","currency":"BRL","description":"Op 60"}
]
```

Cada operação irá considerar as infos necessárias para isso. Por exemplo, para transferência, deve-se informar `sourceAccountId` e `targetAccountId`.
