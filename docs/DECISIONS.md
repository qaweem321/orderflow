# Design decisions

## Why split into services at all

A shop this size would be simpler as one application. The split is the point of the project: it shows the
problems microservices bring (no shared transaction, messages that arrive twice or late, one service being down)
and the usual answers to them. The boundaries follow ownership of data: Catalog owns products and stock, Ordering
owns orders, the Gateway owns users. No service reads another's database.

## Events with an outbox, not direct calls, for stock

Ordering could call Catalog's "reserve" endpoint while placing the order. Then a slow or failing Catalog would fail
the order, and a crash between "reserved" and "order saved" would leave stock reserved for an order that doesn't
exist. Instead the order is saved as **Pending** together with an `OrderPlaced` event in one database transaction
(outbox), and Catalog reacts when it can. The customer sees "Pending" for a moment; the page updates by itself.

The outbox publisher waits for RabbitMQ's publisher confirm before marking a row as published, so an event is
never lost; if the process dies after publishing but before marking, the event goes out twice. That is why every
consumer keeps an **inbox**: the event id is inserted in the same transaction as the consumer's changes, and a
second copy is skipped.

Publishing claims rows with `UPDATE TOP(n) ... WITH (READPAST, UPDLOCK) OUTPUT inserted.*`, so several instances
can publish without blocking each other; the claim moves `NextAttemptAt` forward as a lease, so rows of a crashed
instance are retried.

## Prices come from Catalog over HTTP

Prices must be what the customer saw and agreed to, so they are copied into the order at the moment it is placed.
That needs current data synchronously, so Ordering calls Catalog's lookup endpoint (through the standard resilience
pipeline: retries with jitter, timeouts, circuit breaker). If Catalog is down, placing an order fails fast with
`503 upstream_unavailable` and nothing is saved. A cached read model of prices in Ordering would remove that
dependency; it is the next step if Catalog availability became a problem.

## All-or-nothing reservation with row locks

Catalog locks every product of an order (`WITH (UPDLOCK, ROWLOCK)`, ids sorted so two orders can't deadlock),
checks all lines, then reserves all or none. One RabbitMQ consumer handles messages one at a time, so with a single
Catalog instance the lock is never contended; it matters when several instances run, and a test calls the handler
in parallel to prove it (without the lock that test fails with concurrency errors). `rowversion` on products also
protects an admin's stock edit from overwriting a reservation made in between (`409 concurrent_update`).

## Only confirmed orders can be cancelled

A pending order's stock may be reserved at any moment. If it could be cancelled while pending, the reservation
could arrive after the cancellation and the stock would stay reserved for good. Waiting a second for the decision is
the simpler, correct rule. Status changes from events only move a **Pending** order, so a late or repeated event
cannot reopen a decided one.

## Failure handling in consumers

A failing message is retried once (requeued); if it fails again it goes to the service's `.dead` queue, where it
can be inspected and replayed from the RabbitMQ UI. Endless requeueing would block the queue behind one bad message.

## Gateway and JWT

The gateway is the only public entry point. It signs users in (passwords hashed with ASP.NET Core Identity's
`PasswordHasher`) and issues a short-lived HMAC-SHA256 JWT with the user's id, email and role. Every service
validates the token itself, so a service reached from inside the network still enforces access. Unknown emails are
hashed against a dummy password so response times don't reveal which emails have accounts.

Customers who ask for someone else's order get **404**, not 403, so order ids can't be probed.

For a real deployment I would move to an identity provider with asymmetric keys (services only need the public key)
and refresh tokens; the token lives in `localStorage` here, which is fine for a demo but readable by any script that
runs on the page, so the strict CSP matters.

## Front end

Angular with standalone components and signals. Each page is a lazy-loaded chunk. Polish that matters to users:
sign-in returns you to the page you wanted (`?next=`, same-site paths only), sign-in and sign-out do a full page load
so Back can't show a signed-in page, pages restored from the back/forward cache reload, submit buttons lock while
saving, destructive actions ask first, dates are unambiguous and in UTC, and the theme (System/Light/Dark) is set
before the first paint by a small script file that a strict CSP allows.

nginx serves the app and forwards `/api` to the gateway. It resolves the gateway through Docker's DNS on each
request (cached 10 s): resolving it once at start broke after the gateway container was recreated, because its old
address then belonged to another service.

## What I'd add for production

- Payment step (for example through a payments API like PayLedger) between reservation and confirmation, as a saga.
- Expire reservations of orders that are never paid.
- OpenTelemetry tracing across HTTP and RabbitMQ, and dashboards per queue.
- Kubernetes or Azure Container Apps manifests built from CI, with the database migrations run as a separate job.
