# Alma.WebApplication (fweb-application)

Open-source F# library (`Alma.WebApplication` NuGet package) providing common utilities, types, HTTP handlers, metrics, service identification helpers, JSON-RPC support, and OAuth integration for Alma platform web applications built on Giraffe (ASP.NET Core). Used by downstream microservices as a shared foundation for building web APIs.

## Tech Stack

- **Language:** F# (.NET 10.0)
- **Web framework:** Giraffe (functional ASP.NET Core) ~> 8.0
- **Build system:** FAKE (F# Make) via `build.sh` wrapper
- **Package management:** Paket (`paket.dependencies` / `paket.references`)
- **Test framework:** Expecto + NSubstitute ~> 5.3
- **Linter:** FSharpLint (`fsharplint.json`)
- **CI/CD:** GitHub Actions
- **Key dependencies:**
  - `FSharp.Core` ~> 10.0
  - `FSharp.Data` ~> 6.0 (JSON type providers)
  - `FsHttp` ~> 15.0 (HTTP client DSL)
  - `Giraffe` ~> 8.0 (functional web framework on ASP.NET Core)
  - `Alma.JsonApi` ~> 11.0 (JSON:API error response types)
  - `Alma.Metrics` ~> 12.0 (metrics collection and reporting)
  - (Transitive) `Alma.Tracing` (distributed tracing), `Alma.ServiceIdentification` (Box model), `Alma.State` (concurrent caching), `Alma.Serializer`

## Commands

```bash
# Restore tools + packages and build
./build.sh build

# Run tests
./build.sh -t tests

# Lint
./build.sh -t lint

# Pack NuGet package
./build.sh -t release

# Publish to NuGet.org (requires NUGET_API_KEY)
./build.sh -t publish
```

`build.sh` runs: `dotnet tool restore` → `dotnet tool run paket restore` → FAKE build pipeline.

## Project Structure

```
WebApplication.fsproj    # Library project (OutputType: Library)
AssemblyInfo.fs          # Auto-generated assembly metadata

src/
  Common.fs              # Shared utilities:
                         #   Header active pattern (RequestHeader)
                         #   ClientIpAddress parsing (X-Forwarded-For, RemoteIpAddress)
                         #   IPAddress.isInternal (RFC1918, RFC3927)
  Http.fs                # HTTP client module (traced requests):
                         #   Url, Method types
                         #   ResponseError handling
                         #   HttpError discriminated union
                         #   Http.head / Http.get / Http.post / Http.postContent / Http.put
                         #   All requests are traced via Alma.Tracing
                         #   Query parameter anonymization
  Metrics.fs             # Request duration metrics:
                         #   RequestMetric, startRequest/finishRequest
                         #   Duration buckets (ok/notice/warning/critical)
                         #   DataSetKey creation, context status
  ServiceIdentification.fs  # Spot parsing from HTTP query parameters
                         #   (?spot=(zone,bucket) or ?zone=X&bucket=Y)
                         #   Instance.k8sLocalServiceUrl (Kubernetes DNS)
  JsonRPC/
    JsonRPC.fs           # JSON-RPC 2.0 implementation:
                         #   Request/Response types, parsing, serialization
                         #   Error codes (-32700, -32600, -32601, -32602, -32603)
                         #   Method routing, batch support
                         #   Schema-based parsing via FSharp.Data type provider
  WebApplication.fs      # Core web application handlers:
                         #   LmcEnvironment.isInternalRequest (IP-based auth)
                         #   Setup.allowAnyCORS
                         #   Handler.healthCheck, Handler.appRootStatus, Handler.metrics
                         #   Handler.Lmc.* (internal-only endpoints)
                         #   Handler.Public.* (public endpoints, notFoundJson, resourceNotFound)
                         #   Handler.Public.handleRequestDuration (metrics middleware)
  OAuth.fs               # OAuth 2.0 client credentials flow:
                         #   OAuthCredentials, OAuthToken
                         #   requestTokenFromURL (generic OAuth endpoint)
                         #   requestTokenFromCognito (AWS Cognito)
                         #   Token caching via TemporaryCache

  schema/                # JSON schema files for FSharp.Data type providers
  JsonRPC/schema/        # JSON-RPC request schema

tests/
  Tests.fs               # Expecto test entry point
  JsonRpcTest.fs         # JSON-RPC parsing/serialization tests
  LmcEnvironmentTest.fs  # Internal request detection tests
  ServiceIdentificationTest.fs  # Spot parsing tests

build/
  Build.fs               # FAKE build project definition
  Targets.fs             # FAKE targets (Clean, Build, Lint, Tests, Release, Publish)
  Utils.fs               # Build utility functions
  SafeBuildHelpers.fs    # SAFE Stack build helpers (not used in this library)
```

## Architecture & Key Modules

### HTTP Client (`Http` module)
All HTTP methods (`head`, `get`, `post`, `postContent`, `put`) are traced via `Alma.Tracing`. Each request:
1. Creates a child span with method, URL, and component tags
2. Sends the request with injected tracing headers
3. Handles response — success returns content, error returns `HttpError`
4. `ResponseError` captures URI, status code, request method, and response body for debugging

### Web Handlers (Giraffe `HttpHandler`)
Handlers follow Giraffe's `HttpHandler` composition pattern (`>=>` operator):
- **Health check**: `GET /health-check` → "OK", `HEAD /health-check` → empty
- **App status**: `GET /appRoot/status` → XML status response
- **Metrics**: `GET /metrics` → text metrics output
- **Authorization**: `Handler.Lmc.*` variants restrict to internal IPs (LMC offices, Docker, proxy)
- **Public**: `Handler.Public.*` variants are unrestricted

### JSON-RPC 2.0
Full implementation of JSON-RPC 2.0 spec:
- Request parsing with schema validation
- Standard error codes (Parse Error, Invalid Request, Method Not Found, etc.)
- `RequestId` supports Number, String, or Null
- `Method` routing via active patterns

### Service Identification
- `Spot.parseFromHttpContext` — extracts Zone/Bucket from query parameters
- `Instance.k8sLocalServiceUrl` — generates Kubernetes service DNS name from Instance

### OAuth
- Client credentials flow with token caching
- Generic URL support + AWS Cognito shortcut
- `TemporaryCache` prevents redundant token requests

### Metrics
- Per-request duration tracking with severity buckets:
  - ≤500ms → ok, ≤1000ms → notice, ≤10000ms → warning, >10s → critical
- Labels include request path, HTTP status code, and custom labels

## Conventions

- **Giraffe `HttpHandler`** composition — use `>=>` operator, `choose`, `route`, `warbler`
- **`[<RequireQualifiedAccess>]`** on all public modules
- **Result-based error handling** — `Feather.ErrorHandling` (`result { }`, `asyncResult { }`)
- **`<@>` operator** for `Result.mapError`
- **JSON type providers** (`FSharp.Data.JsonProvider`) for schema-based parsing — schemas in `src/schema/`
- **DTO pattern**: Domain type → DTO type → serialized JSON
- **Active patterns** extensively used for matching (headers, query params, IP addresses)
- **Tracing**: all outbound HTTP calls are traced — never make untraced HTTP requests
- **IP-based authorization**: `LmcEnvironment.isInternalRequest` checks X-Forwarded-For and RemoteIpAddress
- **No mutable state** — all types are immutable records or DUs (except metrics counters)

## CI/CD Workflows

| Workflow | Trigger | What it does |
|----------|---------|-------------|
| `tests.yaml` | PR, daily cron (3 AM) | Runs `./build.sh -t tests` on ubuntu-latest with .NET 10.x |
| `pr-check.yaml` | PR | Blocks fixup commits, runs ShellCheck on shell scripts |
| `publish.yaml` | Tag push (`X.Y.Z`) | Runs `./build.sh -t publish` to publish NuGet package |

## Release Process

1. Increment `<Version>` in `WebApplication.fsproj`
2. Update `CHANGELOG.md` (move items from Unreleased to new version section)
3. Commit and push
4. Create a git tag matching the version (e.g., `14.0.0`) — this triggers the publish workflow

## Pitfalls

- **JSON schema files are required at compile time** — `FSharp.Data.JsonProvider` uses files in `src/schema/` and `src/JsonRPC/schema/` as compile-time samples. Do not move or rename without updating type provider paths.
- **No docker-compose** — this is a library, not a service. No local environment to spin up.
- **Paket, not NuGet CLI** — always use `dotnet paket install` to add packages, not `dotnet add package`.
- **FAKE build system** — entry point is `build.sh`, not `dotnet build` directly (though `dotnet build` works for compilation).
- **LMC public IP hardcoded** — `LmcEnvironment.publicIP` is hardcoded to `185.120.71.181`. If the office IP changes, this must be updated.
- **Transitive dependencies** — this library depends on `Alma.Tracing`, `Alma.State`, and `Alma.ServiceIdentification` transitively through other Alma packages. Check `paket.lock` for resolved versions.
- **`Alma.*` packages** are internal/OSS Alma ecosystem packages — check their repos for API docs.
- **OAuth token caching** — `TemporaryCache` caches tokens by URL+clientId key. Be aware of cache TTL when debugging auth issues.
