namespace Lmc.WebApplication

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
open Lmc.JsonApi

[<RequireQualifiedAccess>]
module LmcEnvironment =
    let publicIP = IPAddress.Parse "185.120.71.181"

    let clientIP = function
        // When request goes via loadbalancer, original client IP is stored in HTTP_X_FORWARDED_FOR
        | ClientIpAddress.HttpXForwardedFor xForwardedFor -> Some xForwardedFor

        // Docker / vagrant requests goes directly, ie. HTTP_X_FORWARDED_FOR is not set and client IP is in REMOTE_ADDR
        | ClientIpAddress.RemoteIpAddress remoteIp -> Some remoteIp

        // This is just a fallback, one of above methods should always return an IP
        | _ -> None

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

    let requiresInternalRequest accessDenied: HttpHandler =
        authorizeRequest LmcEnvironment.isInternalRequest accessDenied

    let healthCheck accessDenied: HttpHandler =
        route "/health-check"
            >=> HEAD
            >=> requiresInternalRequest accessDenied
            >=> text ""

    let appRootStatus status: HttpHandler =
        route "/appRoot/status"
            >=> GET
            >=> requiresInternalRequest accessDeniedXml
            >=> warbler (status >> xml)

    let resourceNotFound: HttpHandler =
        routef "/%s"
            (JsonApiErrorDto.notFound >> JsonApiErrorResponseData.ofError >> json)
            >=> setStatusCode 404

    let metrics metrics: HttpHandler =
        route "/metrics"
            >=> GET
            >=> requiresInternalRequest accessDeniedJson
            >=> warbler (metrics >> text)
