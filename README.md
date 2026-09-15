# FIAP Cloud Games — Payments API

Payments microservice for the FIAP Cloud Games platform (Phase 2, extended in **Phase 3**).
It **simulates** payment processing (no real providers, SDKs, webhooks, checkout pages, or
callbacks — intentional for this academic MVP) and, since Phase 3, keeps the **payment
history in MongoDB** and exposes a **protected payment-status query**.

.NET 8 service that consumes **`OrderPlacedEvent`** (`fcg.orders.placed`, group
`payments-service`), applies a deterministic rule, **stores one payment document per order in
MongoDB** (`fcg_payments.payments`) and publishes **`PaymentProcessedEvent`**
(`fcg.payments.processed`). Runs via Docker Compose and on local Kubernetes (see `k8s/`); for
the full system runbook, see the **`fiap-cloud-games-orchestration`** repository.

Part of the six-repository solution (`users-api`, `catalog-api`, `payments-api`,
`notifications-api` [Phase 2 history], `notifications-function`, `orchestration`).

---

## Responsibilities

- Consume `OrderPlacedEvent` from `fcg.orders.placed` (group `payments-service`).
- Decide Approved/Rejected via a deterministic rule (with a human-readable reason).
- **Persist** the decision in MongoDB — one document per `OrderId`, idempotent upsert.
- Publish `PaymentProcessedEvent` to `fcg.payments.processed`.
- Expose `GET /api/payments/order/{orderId}` (JWT, owner or Admin) and `/health`.

PaymentsAPI owns `fcg_payments` (MongoDB) and is **independent from PostgreSQL**.

---

## Tech

.NET 8 · ASP.NET Core (Controllers) hosting a `BackgroundService` consumer + producer ·
`Confluent.Kafka` · **MongoDB.Driver 3** · JWT Bearer (validation only) · Swagger · Serilog ·
xUnit/Moq/FluentAssertions.

Layout: `Consumers` (Kafka loop), `Payments` (simulator, processor, query service, document),
`Infrastructure/Mongo` (settings, repository), `Controllers`, `Configuration`, `Contracts`, `Messaging`.

---

## Events

| Direction | Event | Topic | Group / Key |
|---|---|---|---|
| Consume | `OrderPlacedEvent` | `fcg.orders.placed` | group `payments-service` |
| Produce | `PaymentProcessedEvent` | `fcg.payments.processed` | key `OrderId` |

Delivery is **at-least-once** (persist + publish, then commit the order offset). A replayed
`OrderPlacedEvent` produces the same decision, **updates the same MongoDB document** (no
duplicate) and publishes the event again; CatalogAPI is idempotent on its side.

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

## MongoDB persistence (Phase 3)

**Why NoSQL here:** the payment history is a write-once, read-by-key document with no joins
and a shape that will grow (provider payloads, retries) — a natural fit for a document store,
and it gives PaymentsAPI its own database without touching PostgreSQL.

Database `fcg_payments`, collection `payments`, **unique index** on `orderId`. Writes use an
**idempotent upsert** keyed by `orderId` (`$setOnInsert` for `_id`/`createdAt`, `$set` for the
rest), so processing the same `OrderPlacedEvent` twice never creates a second document.

```json
{
  "_id": "3d0a…",                      "orderId": "5bd8…",
  "userId": "b963…",                   "gameId": "a4a2…",
  "price": NumberDecimal("49.90"),     "status": "Approved",
  "reason": "Approved by the payment simulation.",
  "orderPlacedEventId": "…",           "paymentProcessedEventId": "…",
  "orderOccurredAt": ISODate("…"),     "processedAt": ISODate("…"),
  "createdAt": ISODate("…"),           "updatedAt": ISODate("…")
}
```

Guids are stored as strings (readable in `mongosh`), `price` as `Decimal128`, dates as UTC.
If MongoDB is unavailable the failure is logged as an error and the event is **still
published** (no outbox in this MVP: the decision is deterministic and CatalogAPI needs the
event); the history for that order is then missing until a replay.

---

## Endpoints

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/api/payments/order/{orderId}` | JWT — **owner or Admin** | Payment status/details for an order → 200 `PaymentResponse`; **403** if another user's order; **404** if unknown |
| GET | `/health` | public | Liveness |
| GET | `/swagger` | public | Swagger UI (direct port only) |

`PaymentResponse`: `orderId`, `userId`, `gameId`, `price`, `status`, `reason`,
`orderOccurredAt`, `processedAt`.

### JWT and authorization

- Kong validates the JWT at the edge for `/api/payments/*`; PaymentsAPI **validates it again**
  (defense in depth) with the same shared `Jwt__SecretKey/Issuer/Audience` as UsersAPI/CatalogAPI.
- Ownership/role rules live **inside** PaymentsAPI (`PaymentQueryService`): a regular user
  only reads payments whose `userId` equals the token's user id; `Admin` reads any payment.

---

## Environment variables

| Variable | Meaning | Local default |
|---|---|---|
| `Kafka__BootstrapServers` | Kafka bootstrap (host dev / `kafka:9092` in containers) | `localhost:29092` |
| `Kafka__OrderPlacedTopic` | Topic to consume | `fcg.orders.placed` |
| `Kafka__PaymentProcessedTopic` | Topic to produce | `fcg.payments.processed` |
| `Kafka__ConsumerGroup` | Consumer group id | `payments-service` |
| `Payment__RejectAboveAmount` | Reject threshold | `1000` |
| `Mongo__ConnectionString` | MongoDB connection (`mongo:27017` in Compose) | `mongodb://fcg:fcg@localhost:27017/?authSource=admin` |
| `Mongo__DatabaseName` | Database | `fcg_payments` |
| `Mongo__PaymentsCollectionName` | Collection | `payments` |
| `JWT__SECRETKEY` | **Same** shared signing key as UsersAPI | dev placeholder |
| `JWT__ISSUER` / `JWT__AUDIENCE` | Token issuer / audience | `FiapCloudGames` |

Only local/development placeholders are committed (MongoDB root credentials `fcg/fcg` are
Compose dev values). **No real secrets are committed.**

---

## Run locally

1. Start the infrastructure (orchestration repo): `docker compose up -d` (Kafka + MongoDB).
2. Run the service:
   ```bash
   dotnet run --project src/PaymentsApi --urls http://localhost:8083
   ```
3. Trigger a purchase in CatalogAPI (`POST /api/library/acquire/{gameId}`) → this service logs
   the decision, stores the document and publishes `PaymentProcessedEvent`.
4. Query it: `GET http://localhost:8083/api/payments/order/{orderId}` with the buyer's (or an
   Admin) JWT — through the gateway: `http://localhost:8000/api/payments/order/{orderId}`.

Inspect MongoDB:
```bash
docker compose exec mongo mongosh -u fcg -p fcg --authenticationDatabase admin fcg_payments --eval 'db.payments.find().pretty()'
docker compose exec mongo mongosh -u fcg -p fcg --authenticationDatabase admin fcg_payments --eval 'db.payments.countDocuments({orderId:"<orderId>"})'
```

## Test

```bash
dotnet test
```

## Docker

```bash
docker build -t fcg-payments-api .
docker run --rm -p 8083:8080 \
  -e Kafka__BootstrapServers="host.docker.internal:29092" \
  -e Mongo__ConnectionString="mongodb://fcg:fcg@host.docker.internal:27017/?authSource=admin" \
  -e JWT__SECRETKEY="dev-only-change-me-please-min-32-characters-placeholder" \
  fcg-payments-api
```
