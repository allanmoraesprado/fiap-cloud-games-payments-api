# FIAP Cloud Games — Payments API

Payments microservice for the FIAP Cloud Games Phase 2 platform. It **simulates**
payment processing (no real providers, SDKs, webhooks, checkout pages, or
callbacks — intentional for this academic MVP).

Stateless .NET 8 service that **simulates** payment processing (no real provider).
Consumes **`OrderPlacedEvent`** (`fcg.orders.placed`, group `payments-service`),
applies a deterministic rule, and publishes **`PaymentProcessedEvent`**
(`fcg.payments.processed`). Runs via Docker Compose and on local Kubernetes (see
`k8s/`); for the full system runbook, see the
**`fiap-cloud-games-orchestration`** repository.

Part of the five-repository solution (`users-api`, `catalog-api`,
`payments-api`, `notifications-api`, `orchestration`).

---

## Responsibilities

- Consume `OrderPlacedEvent` from `fcg.orders.placed` (group `payments-service`).
- Decide Approved/Rejected via a deterministic rule.
- Publish `PaymentProcessedEvent` to `fcg.payments.processed`.
- Expose `/health`.

Stateless — no database, no HTTP business endpoints, no JWT.

---

## Tech

.NET 8 · minimal ASP.NET Core `WebApplication` hosting a `BackgroundService`
consumer + producer · `Confluent.Kafka` · Serilog · xUnit/FluentAssertions.

---

## Events

| Direction | Event | Topic | Group / Key |
|---|---|---|---|
| Consume | `OrderPlacedEvent` | `fcg.orders.placed` | group `payments-service` |
| Produce | `PaymentProcessedEvent` | `fcg.payments.processed` | key `OrderId` |

Delivery is **at-least-once** (publish result, then commit the order offset).

---

## Payment simulation rule (deterministic)

```
price <= 0                -> Rejected   (invalid amount)
price > RejectAboveAmount -> Rejected   (configurable, default 1000)
otherwise                 -> Approved
```

To demo a **Rejection**: acquire a game priced above `RejectAboveAmount`, or set
`Payment__RejectAboveAmount` to a low value.

---

## Environment variables

| Variable | Meaning | Local default |
|---|---|---|
| `Kafka__BootstrapServers` | Kafka bootstrap (host dev / `kafka:9092` in containers) | `localhost:29092` |
| `Kafka__OrderPlacedTopic` | Topic to consume | `fcg.orders.placed` |
| `Kafka__PaymentProcessedTopic` | Topic to produce | `fcg.payments.processed` |
| `Kafka__ConsumerGroup` | Consumer group id | `payments-service` |
| `Payment__RejectAboveAmount` | Reject threshold | `1000` |

No secrets are used (local Kafka is PLAINTEXT).

---

## Run locally (uses the M0 Kafka)

1. Start the M0 infrastructure (orchestration repo): `docker compose up -d`.
2. Run the service:
   ```bash
   dotnet run --project src/PaymentsApi --urls http://localhost:8083
   ```
3. Trigger a purchase in CatalogAPI (`POST /api/library/acquire/{gameId}`) → this
   service logs the payment decision and publishes `PaymentProcessedEvent`.

## Test

```bash
dotnet test
```

## Docker

```bash
docker build -t fcg-payments-api .
docker run --rm -p 8083:8080 \
  -e Kafka__BootstrapServers="host.docker.internal:29092" \
  fcg-payments-api
```
