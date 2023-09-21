namespace Alma.WebApplication

open System
open System.Net
open System.Xml.Serialization

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging

open Giraffe
open Alma.JsonApi

[<RequireQualifiedAccess>]
module LmcEnvironment =
    let publicIP = IPAddress.Parse "185.120.71.181"

    /// Check if request comes from internal "safe" network (ie. LMC offices, LMC proxy, vagrant, docker etc.).
    let isInternalRequest (httpContext: HttpContext) =
        match httpContext with
        // When request goes via loadbalancer, original client IP is stored in HTTP_X_FORWARDED_FOR
        | ClientIpAddress.HttpXForwardedFor (ClientIpAddress xForwardedFor) ->
            (xForwardedFor = publicIP) || (xForwardedFor |> IPAddress.isInternal)

        // Docker / vagrant requests goes directly, ie. HTTP_X_FORWARDED_FOR is not set and client IP is in REMOTE_ADDR
        | ClientIpAddress.RemoteIpAddress (ClientIpAddress remoteIp) ->
            (remoteIp |> IPAddress.isInternal)

        | _ -> false

[<RequireQualifiedAccess>]
module Setup =
    let allowAnyCORS (app: IApplicationBuilder) =
        app.UseCors(new Action<_>(fun (corsPolicy: Infrastructure.CorsPolicyBuilder) ->
            corsPolicy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod()
            |> ignore
        ))

[<CLIMutable>]
[<XmlRoot("response")>]
type Response = {
    [<XmlElement("title")>] Title: string
    [<XmlElement("code")>] Code: int
}

[<RequireQualifiedAccess>]
module Handler =
    let accessDeniedXml: HttpHandler =
        setStatusCode 403 >=> xml { Title = "Forbidden"; Code = 403 }

    let accessDeniedJson: HttpHandler =
        setStatusCode 403 >=> json { Title = "Forbidden"; Status = "403"; Detail = "Access denied." }

    let requiresLmcInternalRequest accessDenied: HttpHandler =
        authorizeRequest LmcEnvironment.isInternalRequest accessDenied

    [<RequireQualifiedAccess>]
    module Authorized =
        let healthCheck authorizeRequest: HttpHandler =
            match authorizeRequest with
            | Some authorizeRequest ->
                route "/health-check"
                    >=> authorizeRequest
                    >=> choose [
                        HEAD >=> text ""
                        GET >=> text "OK"
                    ]
            | None ->
                route "/health-check"
                    >=> choose [
                        HEAD >=> text ""
                        GET >=> text "OK"
                    ]

        let appRootStatus authorizeRequest status: HttpHandler =
            match authorizeRequest with
            | Some authorizeRequest ->
                route "/appRoot/status"
                    >=> GET
                    >=> authorizeRequest
                    >=> warbler (status >> xml)
            | None ->
                route "/appRoot/status"
                    >=> GET
                    >=> warbler (status >> xml)

        let metrics authorizeRequest metrics: HttpHandler =
            match authorizeRequest with
            | Some authorizeRequest ->
                route "/metrics"
                    >=> GET
                    >=> authorizeRequest
                    >=> warbler (metrics >> text)
            | None ->
                route "/metrics"
                    >=> GET
                    >=> warbler (metrics >> text)

    /// Lmc Environment where load-balancers add X-Forwarded-For header with LMC public IP.
    [<RequireQualifiedAccess>]
    module Lmc =
        let healthCheck accessDenied: HttpHandler =
            Authorized.healthCheck (Some (requiresLmcInternalRequest accessDenied))

        let appRootStatus status: HttpHandler =
            Authorized.appRootStatus (Some (requiresLmcInternalRequest accessDeniedXml)) status

        let metrics metrics: HttpHandler =
            Authorized.metrics (Some (requiresLmcInternalRequest accessDeniedJson)) metrics

    [<RequireQualifiedAccess>]
    module Public =
        let notFoundJson: HttpHandler =
            RequestErrors.notFound (fun next ctx -> task {
                return! json {|
                    Code = 404
                    Error = "Not Found"
                    Request = ctx |> HttpContext.requestPath
                |} next ctx
            })

        let resourceNotFound: HttpHandler =
            RequestErrors.notFound (fun next ctx -> task {
                let error =
                    ctx
                    |> HttpContext.requestPath
                    |> JsonApiErrorDto.notFound
                    |> JsonApiErrorResponseData.ofError
                return! json error next ctx
            })

        let healthCheck: HttpHandler =
            Authorized.healthCheck None

        let appRootStatus status: HttpHandler =
            Authorized.appRootStatus None status

        let metrics metrics: HttpHandler =
            Authorized.metrics None metrics

        let handleRequestDuration instance: HttpHandler =
            fun next ctx -> task {
                Metrics.startRequest ctx
                let! response = next ctx

                response
                |> Option.defaultValue ctx
                |> Metrics.finishRequest (Metrics.incrementRequestDuration instance)

                return response
            }
