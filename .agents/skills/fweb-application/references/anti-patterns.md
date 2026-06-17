# Anti-Patterns

Each entry is **mistake → why it's wrong → fix**.

## HTTP client

- **Constructing your own `HttpClient` for outbound calls → loses distributed tracing and the typed `HttpError` model, so failures are invisible in traces and untyped → use `Http.get/post/put/head/postContent`, which open a client span and return `AsyncResult<_, HttpError>`.**
- **Passing a bare string where a `Url` is expected → won't compile / bypasses the intended type → wrap with `Url "http://..."` and convert via `Url.asUri` when a `System.Uri` is needed.**
- **Branching on raw HTTP status codes from the response object → the library already converts 4xx/5xx into `HttpError.ResponseError` → match the `HttpError` DU and read status via `ResponseError.statusCode`.**
- **Blocking on the async result with `.Result` / `.Wait()` → deadlocks and discards error short-circuiting → compose inside `asyncResult { ... }` and `let!` the calls.**
- **Logging full response bodies with `ResponseError.formatWithBody` everywhere → leaks large/sensitive payloads → default to `ResponseError.format` (truncates over 200 chars) and use `anonimizeQueryParameters` for query strings in logs/metrics.**

## Handlers & authorization

- **Assuming `Handler.Public.*` endpoints are access-controlled → `Public` variants are intentionally open; only `Handler.Lmc.*` (and `Authorized.*` with an authorizer) enforce internal-network access → use the `Lmc` variant, or pass your own authorizer.**
- **Re-implementing IP allow-listing per route → duplicates fragile logic → reuse `LmcEnvironment.isInternalRequest` / `IPAddress.isInternal`, or just the `Handler.Lmc.*` handlers.**
- **Placing the not-found handler before specific routes, or omitting `handleRequestDuration` → routes become unreachable / no duration metrics recorded → order the pipeline as duration-middleware → routes → `notFoundJson`/`resourceNotFound` last.**
- **Trusting `X-Forwarded-For` blindly for security decisions → the header is client-settable when not behind a controlled proxy → rely on the provided detection (which also checks `RemoteIpAddress`) and only treat requests as internal behind a trusted load balancer.**

## Metrics & service identification

- **Calling `Metrics.startRequest` without a matching `finishRequest` (or vice versa) → stopwatch entries leak in concurrent storage and counters never increment → use `Handler.Public.handleRequestDuration`, which pairs them correctly.**
- **Hardcoding a Kubernetes service URL string → drifts from the real `Instance` naming → build it with `Instance.k8sLocalServiceUrl`.**
- **Reading `?zone=`/`?bucket=` (or `?spot=`) by hand → misses the supported formats and trimming → use `Spot.parseFromHttpContext`, which accepts both `?spot=(zone,bucket)` and `?zone=&bucket=`.**

## JSON-RPC

- **Driving JSON-RPC success/failure off the HTTP status code → JSON-RPC reports errors inside a 200 response body → parse with `Response.parse` / `JsonRpcCall.send` and handle the `ResponseError` / `JsonRpcErrorDto` (codes -32700, -32600, -32601, -32602, -32603).**
- **Sending a request with a `Jsonrpc` value other than `"2.0"` → rejected as `invalidRequest` → always use the library's `JsonRpc.Version`; build requests via the provided `Request` type.**

## OAuth & caching

- **Requesting a fresh token on every call → hammers the auth endpoint → `OAuth.requestTokenFromURL`/`requestTokenFromCognito` cache via `TemporaryCache` keyed by `url + clientId`; reuse the cached token.**
- **Setting `cacheFor` equal to or longer than the token's real lifetime → serves expired tokens → choose a `cacheFor` safely shorter than the token TTL.**

## Build / project

- **Moving or renaming the JSON schema files under `src/schema/` or `src/JsonRPC/schema/` → `FSharp.Data.JsonProvider` reads them as compile-time samples, so the build breaks → keep the schema files in place when adapting code.**
- **Adding packages with the NuGet CLI → this repo uses Paket → manage dependencies via Paket (`paket.references` / `paket.dependencies`).**

## Legacy / do not use

- **Reaching for the commented-out `HttpDebug` block in the source → it is disabled, untraced debug scaffolding → ignore it; use the supported `Http` module.**
