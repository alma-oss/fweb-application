---
name: fweb-application
description: Use whenever generating or reviewing F# code that builds a Giraffe web service on top of Alma.WebApplication — calling Http.get/post/put/head/postContent (traced AsyncResult HTTP client), composing Handler.Public / Handler.Lmc health-check, appRootStatus and metrics handlers, gating requests with LmcEnvironment.isInternalRequest / IPAddress.isInternal, parsing Spot via Spot.parseFromHttpContext, building k8s URLs with Instance.k8sLocalServiceUrl, sending JSON-RPC 2.0 via JsonRpcCall.send / JsonRpcCall.post, recording request-duration metrics with Metrics.startRequest / Metrics.finishRequest, or obtaining OAuth client-credentials tokens via OAuth.requestTokenFromURL / OAuth.requestTokenFromCognito. Trigger on HttpError, ResponseError, Url, Method, JsonRpcErrorDto, RequestMetric, OAuthCredentials, OAuthToken, anonimizeQueryParameters, allowAnyCORS.
---

# F-Web-Application

Library: [alma-oss/fweb-application](https://github.com/alma-oss/fweb-application)
NuGet: `Alma.WebApplication`

## Purpose

`Alma.WebApplication` is a shared F# foundation for building functional web services on Giraffe (ASP.NET Core). It supplies a traced HTTP client, ready-made Giraffe handlers (health check, status, metrics), internal-network request gating, request-duration metrics, service identification helpers, a JSON-RPC 2.0 implementation, and an OAuth client-credentials flow with token caching.

## When to Use

- Building or reviewing a Giraffe web service that consumes this library.
- Making outbound HTTP calls that must be traced and return typed errors.
- Adding health-check / status / metrics endpoints, optionally restricted to a trusted internal network.
- Recording per-request duration metrics, parsing a `Spot` from query parameters, or building a Kubernetes service URL.
- Implementing a JSON-RPC 2.0 client or server, or fetching OAuth tokens.

## When NOT to Use

- Non-Giraffe / non-ASP.NET hosts, or projects not already depending on the Alma ecosystem.
- Plain HTTP calls where tracing and the `HttpError` model add no value (use the raw client directly only then).
- Business/domain logic — this library is infrastructure only.

## Main Concepts

- `Url`, `Method` — typed request primitives; `Url.asUri` converts to `System.Uri`.
- `Http.get` / `post` / `put` / `head` / `postContent` — traced HTTP calls returning `AsyncResult<_, HttpError>`.
- `HttpError` — DU: `ApiError`, `ApiErrorMessage`, `ResponseError`, `GenericResponseError`; format with `HttpError.format`.
- `ResponseError` — captured failing response (uri, status, request method, body) with `ResponseError.format` / `formatWithBody`.
- `HeadResponse` — content, status code and headers returned by `Http.head`.
- `anonimizeQueryParameters` — masks `email`, `phone`, `id` and `*_id` query values for logging/metrics.
- `Header.(|RequestHeader|_|)`, `ClientIpAddress`, `IPAddress.isInternal` — request header / client-IP extraction and RFC1918/RFC3927 checks.
- `LmcEnvironment.isInternalRequest` — true when a request originates from the trusted internal network.
- `Setup.allowAnyCORS` — permissive CORS setup for `IApplicationBuilder`.
- `Handler.Public.*` / `Handler.Lmc.*` / `Handler.Authorized.*` — `HttpHandler`s for health check, status, metrics; `Lmc` variants require an internal request, `Public` variants are open.
- `Handler.Public.notFoundJson` / `resourceNotFound` / `handleRequestDuration` — JSON 404 responses and request-duration metrics middleware.
- `Metrics.startRequest` / `finishRequest`, `RequestMetric`, `SimpleDataSetKeys` — request-duration tracking bucketed ok/notice/warning/critical.
- `Spot.parseFromHttpContext` — reads a `Spot` from `?spot=(zone,bucket)` or `?zone=&bucket=`.
- `Instance.k8sLocalServiceUrl` — builds the in-cluster Kubernetes DNS URL for an `Instance`.
- `JsonRpc` types (`Request`, `Response`, `Method`, `RequestId`), `JsonRpcErrorDto`, `JsonRpcCall.send` / `post` — JSON-RPC 2.0 client/server primitives.
- `OAuthCredentials`, `OAuthToken`, `OAuth.requestTokenFromURL` / `requestTokenFromCognito` — cached client-credentials token flow.

## Related Libraries

- **Giraffe** — `HttpHandler` composition with `>=>`, `choose`, `route`, `warbler`.
- **Feather.ErrorHandling** — `result`/`asyncResult`/`maybe` computation expressions and `AsyncResult`.
- **Alma.Tracing** — outbound HTTP calls are automatically traced.
- **Alma.Metrics**, **Alma.State** (`TemporaryCache`, concurrent storage), **Alma.ServiceIdentification** (`Instance`, `Spot`, `Zone`, `Bucket`), **Alma.JsonApi** (JSON:API error DTOs), **Alma.Serializer** (`Serialize.toJson`).

## Keywords for Search

fweb-application, Alma.WebApplication, Giraffe, HttpHandler, Http.get, Http.post, Http.put, Http.head, postContent, HttpError, ResponseError, Url, Method, HeadResponse, anonimizeQueryParameters, LmcEnvironment, isInternalRequest, IPAddress.isInternal, ClientIpAddress, X-Forwarded-For, RequestHeader, Setup.allowAnyCORS, Handler.Public, Handler.Lmc, healthCheck, appRootStatus, metrics, notFoundJson, resourceNotFound, handleRequestDuration, Metrics.startRequest, finishRequest, RequestMetric, SimpleDataSetKeys, request duration, Spot.parseFromHttpContext, Instance.k8sLocalServiceUrl, JsonRpc, JsonRpcCall, JsonRpcErrorDto, RequestId, OAuth, OAuthCredentials, OAuthToken, requestTokenFromURL, requestTokenFromCognito, TemporaryCache, AsyncResult, Feather.ErrorHandling

## Reference Files

- For composition principles, recommended API usage, error handling, integration and testing, read `references/preferred-patterns.md`.
- For known pitfalls, incorrect assumptions and legacy usage, read `references/anti-patterns.md`.
- For worked, self-contained code examples, read `references/examples.md`.
