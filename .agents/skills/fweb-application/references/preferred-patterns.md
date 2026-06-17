# Preferred Patterns

## Core Principles

- **Everything is `AsyncResult` / `Result`.** All `Http.*` calls and OAuth/JSON-RPC calls return `AsyncResult<_, _>`. Compose them inside `asyncResult { ... }` and let errors short-circuit; never `.Result`/`.Wait()` to block.
- **All public modules use `[<RequireQualifiedAccess>]`.** Always qualify: `Http.get`, `Handler.Public.healthCheck`, `Metrics.startRequest`, `Spot.parseFromHttpContext`. Do not `open` to drop the qualifier.
- **Outbound HTTP is traced for you.** `Http.head/get/post/postContent/put` each open a client span (component/method/url tags) tied to the active trace. Use them instead of constructing `HttpClient` yourself so calls appear in distributed traces.
- **Handlers compose with Giraffe.** Build endpoints with `>=>`, `choose`, `route`, `warbler`; the library's handlers are ordinary `HttpHandler` values you slot into a `choose [ ... ]` pipeline.

## Recommended API Usage

- **HTTP client.** Wrap the target address in `Url`, pass a `(string * string) list` of headers (often `[]`). `Http.get`/`head` take headers + `Url`; `Http.post`/`put` take headers + `Url` + a value serialized to JSON automatically; `Http.postContent` takes a pre-built `HttpContent` for non-JSON bodies. See `examples.md` → "Basic GET" and "POST a typed request".
- **Typed errors.** Match on the `HttpError` DU to branch on failure; use `HttpError.format` for log/trace messages and `ResponseError.statusCode` / `requestUri` / `responseContent` to inspect a failing response. See `examples.md` → "Handling HttpError".
- **Endpoint handlers.** Use `Handler.Public.*` for open endpoints and `Handler.Lmc.*` to require an internal-network request. `appRootStatus` takes a `unit -> Response` status function, `metrics` takes a `unit -> string` metrics function. Add `Handler.Public.notFoundJson` or `resourceNotFound` as the fall-through. See `examples.md` → "Wiring endpoints".
- **Request-duration metrics.** Place `Handler.Public.handleRequestDuration instance` early in the pipeline; it calls `Metrics.startRequest`/`finishRequest` around the rest of the pipeline and buckets duration into ok/notice/warning/critical counters. See `examples.md` → "Request-duration middleware".
- **Internal-network gating.** Use `LmcEnvironment.isInternalRequest ctx` (or the lower-level `IPAddress.isInternal` / `ClientIpAddress.fromContext`) when you need custom authorization beyond the ready-made `Lmc` handlers.
- **Service identification.** `Spot.parseFromHttpContext ctx` returns `Spot option`; `Instance.k8sLocalServiceUrl instance` yields the in-cluster URL to call sibling services — combine with `Http.get`.
- **JSON-RPC.** Build a `Request` and call `JsonRpcCall.send postJson request`, supplying your own JSON POST function, or use `JsonRpcCall.post url` for the default implementation. Inspect results with `Response.tryParseResultAsJsonString`. See `examples.md` → "JSON-RPC call".
- **OAuth.** Build `OAuthCredentials`, call `OAuth.requestTokenFromURL url cacheFor credentials` (or `requestTokenFromCognito region instance cacheFor credentials`), then `OAuthToken.asAuthorizationHeader` to attach the token to subsequent `Http.*` calls. See `examples.md` → "OAuth token then call".

## Error Handling

- Convert and propagate with the `<@>` (`Result.mapError`) operator and `AsyncResult.mapError` rather than throwing.
- Keep the `HttpError` type at boundaries; map it into your own domain error only at the edge, after logging with `HttpError.format`.
- `ResponseError.format` truncates bodies over 200 chars; use `formatWithBody` only when you intentionally want the full payload.

## Composition

- Order matters in `choose [ ... ]`: put `handleRequestDuration`, then specific routes, then the not-found fall-through last.
- Reuse `Handler.Lmc.*` for any endpoint that should be internal-only instead of re-implementing IP checks per route.

## Integration with Other Libraries

- **Giraffe:** these handlers are drop-in `HttpHandler`s; register CORS with `Setup.allowAnyCORS` during app building.
- **Alma.ServiceIdentification:** `Instance`, `Spot`, `Zone`, `Bucket` flow through metrics keys and URL building.
- **Alma.State / TemporaryCache:** OAuth tokens are cached by `url + clientId`; pick `cacheFor` shorter than the token's real TTL.
- **Alma.Serializer:** `Http.post`/`put` and `JsonRpcCall.post` serialize via `Serialize.toJson` — your request type must be serializable.

## Naming Conventions

- Domain type → DTO type → serialized JSON (e.g. `Request` → `RequestDto`).
- Active patterns for matching headers/IPs/query params (`|RequestHeader|_|`, `|HttpXForwardedFor|_|`, `|Has|_|`).
- Immutable records and DUs throughout; no mutable shared state except metric counters.

## Testing Recommendations

- Tests use **Expecto** with **NSubstitute**. Substitute an `HttpContext` and stub `Request.Headers` / `Connection.RemoteIpAddress` to drive `isInternalRequest` and IP logic. See `examples.md` → "Unit-testing internal-request detection".
- Prefer table-driven test cases (a list of records) over many near-duplicate tests.
