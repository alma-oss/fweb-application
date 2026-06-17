# Examples

Self-contained snippets, ordered from basic to full workflow. Names like `WebApi`,
`ServiceA`, `CacheInstance` are neutral placeholders. This file is the only place in
the skill that contains code.

## Basic GET

```fsharp
open Alma.WebApplication.Http
open Feather.ErrorHandling

let fetchStatus () = asyncResult {
    let! body = Http.get [] (Url "http://service-a.local/status")
    return body
}
```

## POST a typed request

```fsharp
open Alma.WebApplication.Http
open Feather.ErrorHandling

type CreateRequest = { Name: string; Size: int }

let create () = asyncResult {
    let payload = { Name = "demo"; Size = 3 }
    // Http.post serializes the value to JSON and sets the proper headers.
    let! response = Http.post [] (Url "http://service-a.local/items") payload
    return response
}
```

## Handling HttpError

```fsharp
open System.Net
open Alma.WebApplication.Http
open Feather.ErrorHandling

let callWithFallback () = async {
    match! Http.get [] (Url "http://service-a.local/data") with
    | Ok body -> return Some body
    | Error (HttpError.ResponseError responseError) ->
        match responseError |> ResponseError.statusCode with
        | HttpStatusCode.NotFound -> return None            // treat 404 as "no data"
        | _ ->
            eprintfn "%s" (HttpError.format (HttpError.ResponseError responseError))
            return None
    | Error other ->
        eprintfn "%s" (HttpError.format other)
        return None
}
```

## HEAD request

```fsharp
open Alma.WebApplication.Http
open Feather.ErrorHandling

let probe () = asyncResult {
    let! head = Http.head [] (Url "http://service-a.local/health-check")
    // head : HeadResponse with Content, StatusCode and Headers
    return head.StatusCode, head.Headers
}
```

## Wiring endpoints

```fsharp
open Giraffe
open Alma.WebApplication

// status: unit -> Response ; metrics: unit -> string
let webApp (instance) (status) (metrics) : HttpHandler =
    choose [
        Handler.Public.handleRequestDuration instance      // duration middleware first
        Handler.Lmc.healthCheck Handler.accessDeniedJson    // internal-only health check
        Handler.Lmc.appRootStatus status                    // internal-only status (XML)
        Handler.Lmc.metrics metrics                         // internal-only metrics (text)
        Handler.Public.notFoundJson                         // fall-through last
    ]
```

## CORS setup

```fsharp
open Microsoft.AspNetCore.Builder
open Alma.WebApplication

let configureApp (app: IApplicationBuilder) =
    app
    |> Setup.allowAnyCORS
    |> ignore
```

## Internal-network gating with a custom handler

```fsharp
open Giraffe
open Alma.WebApplication

let internalOnly: HttpHandler =
    Handler.requiresLmcInternalRequest Handler.accessDeniedJson
    >=> route "/admin"
    >=> text "ok"

// Or check directly inside a handler:
let custom: HttpHandler =
    fun next ctx ->
        if LmcEnvironment.isInternalRequest ctx then text "internal" next ctx
        else Handler.accessDeniedJson next ctx
```

## Parse a Spot and call a sibling service

```fsharp
open Alma.WebApplication
open Alma.WebApplication.Http
open Alma.ServiceIdentification
open Feather.ErrorHandling

let callSibling (ctx) (instance: Instance) = asyncResult {
    let spot = Spot.parseFromHttpContext ctx            // Spot option
    let url = Instance.k8sLocalServiceUrl instance       // in-cluster DNS URL
    let! body = Http.get [] (Url (sprintf "%s/info" url))
    return spot, body
}
```

## Request-duration middleware (standalone)

```fsharp
open Giraffe
open Alma.WebApplication

// Equivalent to including handleRequestDuration: it times the inner pipeline
// and increments the ok/notice/warning/critical counters.
let timed (instance) (inner: HttpHandler) : HttpHandler =
    Handler.Public.handleRequestDuration instance >=> inner
```

## JSON-RPC call

```fsharp
open Alma.WebApplication.JsonRpc
open Feather.ErrorHandling

let callRpc () = asyncResult {
    let request = {
        Id = RequestId.Number 1
        Method = Method "demo.ping"
        Parameters = Dto {| value = 42 |}
    }

    // JsonRpcCall.post url returns a postJson function using the default HTTP impl.
    let! response = JsonRpcCall.send (JsonRpcCall.post "http://service-a.local/rpc") request

    return response |> Response.tryParseResultAsJsonString   // string option
}
```

## OAuth token, then authenticated call

```fsharp
open System
open Alma.WebApplication
open Alma.WebApplication.Http
open Alma.WebApplication.OAuth
open Feather.ErrorHandling

let callAuthenticated () = asyncResult {
    let credentials = { ClientId = "client-a"; ClientSecret = "secret" }

    let! token =
        requestTokenFromURL
            "https://auth.example.local/oauth2/token"
            (TimeSpan.FromMinutes 5.0)        // cacheFor: shorter than real TTL
            credentials

    let authHeader = token |> OAuthToken.asAuthorizationHeader
    let! body = Http.get [ authHeader ] (Url "http://service-a.local/secure")
    return body
}
```

## Unit-testing internal-request detection

```fsharp
open Expecto
open NSubstitute
open System.Net
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives
open Alma.WebApplication

let makeContext (xForwardedFor: string option) (remoteIp: string) : HttpContext =
    let headers = HeaderDictionary()
    xForwardedFor |> Option.iter (fun ip -> headers.Add("X-Forwarded-For", StringValues ip))

    let ctx = Substitute.For<HttpContext>()
    ctx.Request.Headers.Returns(headers :> IHeaderDictionary) |> ignore
    ctx.Request.HttpContext.Connection.RemoteIpAddress.Returns(IPAddress.Parse remoteIp) |> ignore
    ctx

[<Tests>]
let tests =
    testList "isInternalRequest" [
        test "private remote IP is internal" {
            let ctx = makeContext None "10.0.0.5"
            Expect.isTrue (LmcEnvironment.isInternalRequest ctx) "RFC1918 address"
        }
        test "public remote IP is external" {
            let ctx = makeContext None "8.8.8.8"
            Expect.isFalse (LmcEnvironment.isInternalRequest ctx) "public address"
        }
    ]
```

## Full workflow: fetch token, call sibling, expose endpoints

```fsharp
open System
open Giraffe
open Alma.WebApplication
open Alma.WebApplication.Http
open Alma.WebApplication.OAuth
open Alma.ServiceIdentification
open Feather.ErrorHandling

let fetchFromSibling (instance: Instance) (credentials: OAuthCredentials) = asyncResult {
    let! token =
        requestTokenFromURL "https://auth.example.local/oauth2/token"
            (TimeSpan.FromMinutes 5.0) credentials

    let url = Instance.k8sLocalServiceUrl instance
    let! body =
        Http.get [ token |> OAuthToken.asAuthorizationHeader ] (Url (sprintf "%s/items" url))
    return body
}

let webApp (instance) (status) (metrics) : HttpHandler =
    choose [
        Handler.Public.handleRequestDuration instance
        Handler.Public.healthCheck
        Handler.Lmc.metrics metrics
        Handler.Lmc.appRootStatus status
        Handler.Public.resourceNotFound
    ]
```
