# teto-toys-mobile-app-backend

Customer-facing API for the Teto Toys mobile app. Same Clean Architecture layout as
`admineTetoToys.api` (Domain → Application → Infrastructure → Api), serving the
storefront surface rather than the admin one.

Shares the existing MySQL and Redis on `infrastructure_network`, so it reads the same
products, categories and users as the web backends.

## Running

```bash
# Requires teto-toys-infrastructure to be up first (shared_mysql, shared_redis).
docker compose up --build -d
```

Listens on **8082** (8080 = storefront, 8081 = admin).

`TetoToysMobile.Api/.env` must supply at minimum:

```
JWT__SECRET=<a long random string>
MySQL__ConnectionString=Server=shared_mysql;Port=3306;Database=TetoToys;Uid=...;Pwd=...;
Redis__Password=<redis password>
```

There is no fallback secret: the service logs a critical error at boot and refuses to
issue or validate tokens when `JWT:SECRET` is missing.

## Auth model

Unlike the web backends, this API does **not** use cookies. Native clients have no
cookie jar, so both tokens are returned in the JSON body and the client stores the
refresh token in the Keychain/Keystore.

| Endpoint | Notes |
|---|---|
| `POST /api/auth/register` | Returns tokens immediately |
| `POST /api/auth/login` | `{ access_token, refresh_token, expires_in, user }` |
| `POST /api/auth/refresh` | Body `{ refresh_token }`. **Rotating** — the old token is revoked |
| `POST /api/auth/logout` | Body `{ refresh_token }` |
| `GET /api/auth/me` | `Authorization: Bearer <access_token>` |

Access tokens last 15 minutes; refresh tokens 30 days (mobile users expect to stay
signed in). Refresh tokens are tracked in Redis under `refresh:{token}`, the same key
format the other backends use, so revocation is immediate.

Access tokens carry `token_type=access` and it is checked on validation — otherwise a
refresh token, signed with the same key, would be accepted as a bearer credential.

## Public endpoints

| Endpoint | Notes |
|---|---|
| `GET /api/products` | `?page&pageSize&search&category&lang`. Displayed, non-deleted only |
| `GET /api/products/{id}` | `?lang` |
| `GET /api/categories` | `?lang`. Only categories with active products |
| `GET /api/languages` | Drives the client language picker |
| `GET /api/store-hours` | Weekly schedule + `is_open_now`, 1h Redis cache |
| `GET /health` | Liveness probe |

Favourites require a bearer token: `GET /api/favorites`, `GET /api/favorites/ids`,
`POST|DELETE /api/favorites/{productId}`.

## Carried over from the admin API

- **Token-bucket rate limiting** in Redis — 120/min globally, 10/min on `/api/auth`,
  scoped under service name `mobile` so it has its own budget.
- **Security headers** — nosniff, `X-Frame-Options`, `Referrer-Policy`,
  `Permissions-Policy`, plus HSTS outside development.

Not carried over (not requested): password reset / email, per-account login lockout.
