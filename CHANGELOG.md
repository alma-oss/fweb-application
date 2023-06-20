# Changelog

<!-- There is always Unreleased section on the top. Subsections (Add, Changed, Fix, Removed) should be Add as needed. -->
## Unreleased
- Fix `Handler.jsonRpc*` to serialize `RequestId` in response

## 6.0.0 - 2023-20-06
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
