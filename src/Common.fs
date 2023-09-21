namespace Alma.WebApplication

open Microsoft.AspNetCore.Http
open System.Net
open System.Xml.Serialization
open Giraffe

[<AutoOpen>]
module internal Utils =
    [<RequireQualifiedAccess>]
    module String =
        let trim (string: string) = string.Trim(' ')

    [<RequireQualifiedAccess>]
    module HttpContext =
        let requestPath (ctx: HttpContext) =
            if ctx.Request.Path.HasValue then ctx.Request.Path.Value else ""

[<RequireQualifiedAccess>]
module Header =
    let (|RequestHeader|_|) header (httpContext: HttpContext) =
        match httpContext.Request.Headers.TryGetValue(header) with
        | true, headerValues -> Some (headerValues |> Seq.toList)
        | _ -> None

type ClientIpAddress = ClientIpAddress of IPAddress

[<RequireQualifiedAccess>]
module ClientIpAddress =
    let parse = function
        | null | "" -> None
        | ip ->
            match ip |> IPAddress.TryParse with
            | true, ip -> Some (ClientIpAddress ip)
            | _ -> None

    /// Parse IP of the original client from the value of X-Forwarded-For header.
    ///
    /// The value could consist of more comma-separated IPs, the left-most being the original client,
    /// and each successive proxy that passed the request adding the IP address where it received the request from.
    ///
    /// @see https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/X-Forwarded-For#syntax
    let parseClientIpFromXForwardedFor = function
        | null | "" -> None
        | xForwardedFor -> xForwardedFor.Split ',' |> Array.map String.trim |> Array.tryHead |> Option.bind parse

    /// Parse IP of the original client from the value of X-Forwarded-For header.
    ///
    /// The value could consist of more comma-separated IPs, the left-most being the original client,
    /// and each successive proxy that passed the request adding the IP address where it received the request from.
    ///
    /// @see https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/X-Forwarded-For#syntax
    let (|HttpXForwardedFor|_|) = function
        | Header.RequestHeader "X-Forwarded-For" xForwardedFor -> xForwardedFor |> List.tryPick parseClientIpFromXForwardedFor
        | _ -> None

    let (|RemoteIpAddress|_|) (httpContext: HttpContext) =
        match httpContext.Request.HttpContext.Connection.RemoteIpAddress with
        | null -> None
        | remoteIp -> Some (ClientIpAddress remoteIp)

    let fromContext = function
        // When request goes via loadbalancer, original client IP is stored in HTTP_X_FORWARDED_FOR
        | HttpXForwardedFor xForwardedFor -> Some xForwardedFor

        // Docker / vagrant requests goes directly, ie. HTTP_X_FORWARDED_FOR is not set and client IP is in REMOTE_ADDR
        | RemoteIpAddress remoteIp -> Some remoteIp

        // This is just a fallback, one of above methods should always return an IP
        | _ -> None

[<RequireQualifiedAccess>]
module IPAddress =
    let isInternal (ip: IPAddress) =
        match ip.GetAddressBytes() |> Array.map int with
        | [| 127; _; _; _ |]

        // RFC1918
        | [| 10; _; _; _ |]
        | [| 192; 168; _; _ |] -> true
        | [| 172; i; _; _ |] when i >= 16 && i <= 31 -> true

        // RFC3927
        | [| 169; 254; _; _ |] -> true

        | _ -> false
