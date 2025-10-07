# Changelog

<!-- There is always Unreleased section on the top. Subsections (Add, Changed, Fix, Removed) should be Add as needed. -->
## Unreleased

- Fix Headers in HTTP functions

## 12.0.0 - 2025-03-18
- [**BC**] Use net9.0

## 11.0.0 - 2024-01-11
- [**BC**] Use net8.0
- Fix package metadata

## 10.2.0 - 2023-10-16
- Add `OAuth` module

## 10.1.0 - 2023-10-13
- Add `Http.head` function

## 10.0.0 - 2023-09-21
- Add `Instance.k8sLocalServiceUrl` function
- [**BC**] Rename `AlmaEnvironment` back to `LmcEnvironment`
- Add `ClientIpAddress.fromContext` function
- [**BC**] Remove functions
    - `AlmaEnvironment.clientIP`
    - `Handler.appRootStatus`
    - `Handler.healthCheck`
    - `Handler.metrics`
- [**BC**] Rename functions
    - `Handler.requireInternalRequest` -> `Handler.requiresLmcInternalRequest`
- Add modules
    - `Handler.Authorized`
    - `Handler.Lmc`
    - `Handler.Public`

## 9.0.0 - 2023-09-11
- [**BC**] Use `Alma` namespace

## 8.1.0 - 2023-08-31
- Add `Spot` module
- Add `Http.QueryParameters` module

## 8.0.0 - 2023-08-11
- [**BC**] Use net 7.0

## 7.5.0 - 2023-06-30
- Allow `health-check` on `GET` and `HEAD`
- Add `Http` module

## 7.4.0 - 2023-06-27
- Add `http_status_code` and `jsonrpc_method` to `total_request_duration_*` in `jsonrpc` handlers

## 7.3.0 - 2023-06-27
- Add `http_status_code` label to `total_request_duration_*` metrics

## 7.2.0 - 2023-06-27
- Add `Metrics.createDataSetKey` function

## 7.1.0 - 2023-06-27
- Add `Handler.handleRequestDuration` function

## 7.0.0 - 2023-06-27
- Add `Metrics` module
- Add `Http` module
- [**BC**] Change jsonrpc handlers to require current application instance
    - `JsonRpc.Handler.jsonRpc`
    - `JsonRpc.Handler.jsonRpcWithHttpContext`

## 6.1.0 - 2023-06-20
- Add `Jsonrpc` version to response

## 6.0.1 - 2023-06-20
- Fix `Handler.jsonRpc*` to serialize `RequestId` in response

## 6.0.0 - 2023-06-20
- Add `Handler.jsonRpcWithHttpContext` function
- Fix `RequestId` according to `JsonRpc specification`
- Update dependencies

## 5.0.0 - 2022-11-10
- Allow to parse `JsonRpc.Response` without an id (as explicit error)
- Add `RequestParameters` type
- [**BC**] Change `JsonRpc.Request.Parameters` to `RequestParameters`

## 4.4.0 - 2022-11-08
- Add `JsonRpc.methodNotAllowed` error and handler
- Update not found handlers to handle all not found requests
    - `JsonRpc.notFound`
    - `HttpHandler.notFound`
- Allow `JsonRpc.jsonRpc` handler with both trailing `/` and without it
- Show method not allowed if `/jsonrpc` endpoint is used with wrong method
- Add `HttpHandler.notFoundJson`

## 4.3.0 - 2022-11-03
- Update dependencies
- Remove forgotten print

## 4.2.0 - 2022-11-01
- Add JsonRpc
- Add method http handler to the base handlers

## 4.1.0 - 2022-06-29
- Update dependencies

## 4.0.0 - 2022-05-18
- Update dependencies
    - [**BC**] Use `OpenTelemetry` tracing

## 3.3.0 - 2022-04-27
- Update dependencies

## 3.2.0 - 2022-02-22
- Update dependencies

## 3.1.0 - 2022-02-22
- Update dependencies

## 3.0.0 - 2022-01-05
- [**BC**] Use net6.0

## 2.0.0 - 2021-12-09
- [**BC**] Use `accessDeniedXml` in `appRootStatus` handler
- Add `LmcEnvironment.clientIP` function

## 1.0.0 - 2021-11-22
- Initial implementation
