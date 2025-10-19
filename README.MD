# PagueVeloz

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

---

## Justificativa para Frameworks/Bibliotecas

- **MediatR**: Facilita a implementação do padrão CQRS, desacoplando a lógica de negócio dos controladores e promovendo testabilidade.
- **Polly**: Permite políticas avançadas de retry, circuit breaker e fallback, essenciais para sistemas financeiros que dependem de alta disponibilidade.
- **AutoFixture, Moq, xUnit, FluentAssertions**: Conjunto moderno para testes unitários e de integração, promovendo produtividade, legibilidade e cobertura confiável.
- **FineCodeCoverage**: Extensão que garante coleta de cobertura de testes, importante para qualidade e auditoria do código.
- **ADO.NET**: Maior controle e máxima performance em paths críticos (bulk/low-latency), sem o overhead de um ORM.


---
