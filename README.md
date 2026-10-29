# OrderFlow

[![CI](https://github.com/qaweem321/orderflow/actions/workflows/ci.yml/badge.svg)](https://github.com/qaweem321/orderflow/actions/workflows/ci.yml)

A small online shop split into **.NET 10 microservices** with an **Angular 22** front end. Customers browse the
catalog and place orders; the Ordering service and the Catalog service agree on stock through **RabbitMQ** events,
each with its own **SQL Server** database. A **YARP** gateway is the single entry point and signs users in with JWTs.

## What it shows

| Area | How |
| --- | --- |
| Microservices | Catalog, Ordering and Gateway, each with its own database, deployable on its own |
| Messaging | RabbitMQ topic exchange, one queue per service, dead-letter queue for messages that keep failing |
| Reliable events | Transactional outbox (event saved with the change, published with publisher confirms) and inbox (each event handled once) |
| Data consistency | All-or-nothing stock reservation with `UPDLOCK` row locks; cancellation gives stock back |
| ASP.NET Core | Minimal APIs, built-in validation, problem details with stable error `code`s, OpenAPI + Scalar docs |
| EF Core + SQL Server | Migrations per service, owned collections, `rowversion`, check constraints, raw SQL where it matters |
| Service-to-service HTTP | Ordering reads prices from Catalog through `HttpClient` with retries, timeouts and a circuit breaker |
| Security | JWT (HMAC) issued by the gateway and checked by every service, roles (admin, customer), hashed passwords, login rate limit, CSP and security headers |
| Angular | Standalone components, signals, lazy routes, guards, HTTP interceptor, live order status, light/dark theme |
| Testing | xUnit v3 unit tests, end-to-end integration tests on real SQL Server and RabbitMQ (Testcontainers), Vitest for Angular |
| Delivery | Docker Compose for the whole stack, GitHub Actions CI, Conventional Commits |

## How an order flows

```text
Browser ──> nginx (web) ──> Gateway (YARP, sign-in) ──> Ordering ──HTTP prices──> Catalog
                                                           │                         ▲
                                         OrderPlaced       │      RabbitMQ           │
                                                           └──────> exchange ────────┘
                                                           ┌────── StockReserved / StockRejected
                                                           ▼
                                               order Confirmed or Rejected
```

1. The customer places an order. Ordering copies current names and prices from Catalog, saves the order as
   **Pending** and, in the same database transaction, an `OrderPlaced` event (outbox).
2. The outbox publisher sends it to RabbitMQ. Catalog locks the products, reserves **every** line or **none**,
   and answers `StockReserved` or `StockRejected` (with a reason such as "Only 2 of Mouse left.").
3. Ordering marks the order **Confirmed** or **Rejected**. The order page updates by itself.
4. Cancelling a confirmed order sends `OrderCancelled`, and Catalog puts the stock back.

Each consumer stores the event id in an inbox table in the same transaction as its changes, so a message
delivered twice changes nothing the second time.

## Quick start (Docker)

Needs Docker with Compose and about 3 GB of free RAM (SQL Server and RabbitMQ).

```bash
git clone https://github.com/qaweem321/orderflow.git && cd orderflow
```

```bash
printf 'MSSQL_SA_PASSWORD=Of-%s\nRABBITMQ_PASSWORD=%s\nJWT_SIGNING_KEY=%s\n' "$(openssl rand -hex 16)" "$(openssl rand -hex 24)" "$(openssl rand -base64 48)" > .env
```

```bash
docker compose up -d --build
```

Open <http://localhost:4300>. The first start creates the databases, a demo catalog and the demo logins.

| Email | Password | Role |
| --- | --- | --- |
| `admin@orderflow.test` | `orderflow-admin` | Admin: manage products and stock, see and cancel every order |
| `customer@orderflow.test` | `orderflow-customer` | Customer: place, view and cancel own orders |

These passwords are for local use only. Anyone can also create a customer account on the sign-up page.

Other addresses (local only):

- <http://localhost:5100/docs>: gateway API reference (sign-in endpoints); `/api/catalog/*` and `/api/orders/*` are forwarded from here too
- <http://localhost:15672>: RabbitMQ management (user `orderflow`, password `RABBITMQ_PASSWORD` from `.env`)

## Configuration

| Setting | Used by | Meaning |
| --- | --- | --- |
| `MSSQL_SA_PASSWORD` (`.env`) | Compose | SQL Server `sa` password: 8+ characters with three of upper, lower, digit, symbol. Generate: `echo "Of-$(openssl rand -hex 16)"` |
| `RABBITMQ_PASSWORD` (`.env`) | Compose | RabbitMQ password for user `orderflow`. Generate: `openssl rand -hex 24` |
| `JWT_SIGNING_KEY` (`.env`) | Gateway, services | Shared HMAC key, at least 32 characters (checked at start-up). Generate: `openssl rand -base64 48` |
| `ConnectionStrings__Default` | Each service | Its own SQL Server database |
| `RabbitMq__ConnectionString`, `RabbitMq__Queue` | Catalog, Ordering | Broker address and the service's queue name |
| `Services__Catalog` | Ordering | Catalog's internal address |
| `Seed__DemoData`, `Seed__DemoUsers` | Catalog, Gateway | Add the demo catalog / demo logins to an empty database |
| `RateLimits__LoginsPerMinute` | Gateway | Sign-in and sign-up attempts per IP (default 10) |
| `ReverseProxyNetworks__0` | Gateway | Network of the proxy in front (nginx). Only then is `X-Forwarded-For` trusted, last hop only |

## Develop without Docker for the code

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Node 24. Start only SQL Server and RabbitMQ
(the Development settings expect these passwords; stop other stacks that use port 1433 first):

```bash
docker run -d --name orderflow-sql -e ACCEPT_EULA=Y -e 'MSSQL_SA_PASSWORD=OrderFlow-Dev-2026!' -p 127.0.0.1:1433:1433 mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04
```

```bash
docker run -d --name orderflow-rabbit -e RABBITMQ_DEFAULT_USER=orderflow -e RABBITMQ_DEFAULT_PASS=orderflow-dev -p 127.0.0.1:5672:5672 -p 127.0.0.1:15672:15672 rabbitmq:4.3.6-management-alpine
```

Then run each in its own terminal: `dotnet run --project src/Services/Catalog/Catalog.Api` (port 5101),
`dotnet run --project src/Services/Ordering/Ordering.Api` (5102), `dotnet run --project src/Gateway/Gateway.Api`
(5100), and the front end:

```bash
cd web && npm ci && npm start
```

The Angular dev server (<http://localhost:4200>) forwards `/api` to the gateway.

## Tests

```bash
dotnet test
```

```bash
cd web && npm ci && npm test
```

The integration tests start SQL Server and RabbitMQ containers (Docker must be running) and host all three
services in memory. They cover the full order flow, all-or-nothing reservation, cancellation, twelve customers
racing for five units, parallel handlers (as with several Catalog instances), duplicate event delivery, access
rules between customers and admins, validation, and sign-in.

## Project structure

```text
src/
  Contracts/OrderFlow.Contracts/      Integration events shared by the services (OrderPlaced, StockReserved...)
  BuildingBlocks/OrderFlow.BuildingBlocks/
    Messaging/                        RabbitMQ connection, outbox publisher, inbox consumer, handler registry
    Persistence/                      Outbox and inbox tables
    Web/                              Shared service setup: JWT, problem details, health, OpenAPI
    Errors/                           Error types and their HTTP mapping
  Services/Catalog/Catalog.Api/       Products and stock; reserves and releases stock on events
  Services/Ordering/Ordering.Api/     Orders; reads prices from Catalog, reacts to stock events
  Gateway/Gateway.Api/                YARP routes, users, sign-in and JWT issuing
web/                                  Angular app (pages, core services, shared components) + nginx config
tests/
  OrderFlow.UnitTests/                Domain rules and event serialization
  OrderFlow.IntegrationTests/         Whole back end on SQL Server + RabbitMQ containers
docs/DECISIONS.md                     Why it is built this way, and the trade-offs
Dockerfile, compose.yaml              One image recipe for the .NET services; the full local stack
```

## Design decisions

See [docs/DECISIONS.md](docs/DECISIONS.md).

## License

[MIT](LICENSE)
