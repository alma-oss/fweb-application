module Alma.WebApplication.AlmaEnvironmentTest

open Expecto
open NSubstitute

open System.IO
open System.Net
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives
open Alma.WebApplication

type TestCase = {
    Name: string
    HttpContext: HttpContext
    ShouldBeInternal: bool
}

type HttpHeader =
    | RemoteIp of string
    | XForwardedFor of string

let prepareHttpContext headers: HttpContext =
    let headersDict: IHeaderDictionary =
        let dict = HeaderDictionary()

        headers
        |> List.iter (function
            | XForwardedFor ip -> dict.Add("X-Forwarded-For", StringValues(ip))
            | _ -> ()
        )

        dict :> IHeaderDictionary

    let remoteIp: IPAddress =
        headers
        |> List.tryPick (function
            | RemoteIp ip -> IPAddress.Parse ip |> Some
            | _ -> None
        )
        |> Option.defaultValue IPAddress.None

    let ctx = Substitute.For<HttpContext>()
    ctx.Request.Headers.Returns(headersDict) |> ignore
    ctx.Request.HttpContext.Connection.RemoteIpAddress.Returns(remoteIp) |> ignore

    ctx

/// see https://bitbucket.lmc.cz/projects/PRA/repos/lmcenvbundle/browse/tests/Environment/AbstractEnvironmentTestCase.php
let provideRequests: TestCase list =
    let lmcPublicIp = string AlmaEnvironment.publicIP
    let publicIp = "8.8.8.8"
    let publicIpViaVarnish = $"{publicIp}, 10.4.48.24, 10.4.48.7"

    [
        {
            Name = "Vagrant Request"
            HttpContext = prepareHttpContext [
                RemoteIp "172.18.0.1"
            ]
            ShouldBeInternal = true
        }
        {
            Name = "Docker Request"
            HttpContext = prepareHttpContext [
                RemoteIp "192.168.100.50"
            ]
            ShouldBeInternal = true
        }
        {
            Name = "Internal request from LMC offices to a service behind loadbalancer"
            HttpContext = prepareHttpContext [
                RemoteIp publicIp
                XForwardedFor lmcPublicIp
            ]
            ShouldBeInternal = true
        }
        {
            Name = "Internal request from LMC offices, directly to the service - ie. not via loadbalancer"
            HttpContext = prepareHttpContext [
                RemoteIp lmcPublicIp
            ]
            ShouldBeInternal = false
        }
        {
            Name = "Internal request via proxy"
            HttpContext = prepareHttpContext [
                RemoteIp "10.2.220.142"
                XForwardedFor "10.2.220.50"
            ]
            ShouldBeInternal = true
        }
        {
            Name = "Internal request via proxy to a service behind Varnish"
            HttpContext = prepareHttpContext [
                RemoteIp "10.4.48.24"
                XForwardedFor "10.7.9.45, 10.4.48.7"
            ]
            ShouldBeInternal = true
        }
        {
            Name = "Internal request via proxies with whitespace"
            HttpContext = prepareHttpContext [
                RemoteIp "10.4.48.24"
                XForwardedFor "10.7.9.45 , 10.4.48.7"
            ]
            ShouldBeInternal = true
        }
        {
            Name = "External request (directly to a service, without loadbalancer)"
            HttpContext = prepareHttpContext [
                RemoteIp publicIp
            ]
            ShouldBeInternal = false
        }
        {
            Name = "External request (with sniffed X-Forwarded-For)"
            HttpContext = prepareHttpContext [
                RemoteIp publicIp
                XForwardedFor publicIp
            ]
            ShouldBeInternal = false
        }
        {
            Name = "External request (with loadbalancer on IP 10.*)"
            HttpContext = prepareHttpContext [
                RemoteIp "10.2.220.142"
                XForwardedFor publicIp
            ]
            ShouldBeInternal = false
        }
        {
            Name = "External request (with loadbalancer on IP 192.*)"
            HttpContext = prepareHttpContext [
                RemoteIp "192.168.3.3"
                XForwardedFor publicIp
            ]
            ShouldBeInternal = false
        }
        {
            Name = "External request (via both loadbalancer and varnish)"
            HttpContext = prepareHttpContext [
                RemoteIp "10.2.220.142"
                XForwardedFor publicIpViaVarnish
            ]
            ShouldBeInternal = false
        }
    ]

[<Tests>]
let lmcEnvironmentTest =
    testList "WebApplication - AlmaEnvironment" [
        yield! provideRequests |> List.map (fun tc ->
            testCase tc.Name <| fun _ ->
                let actual = AlmaEnvironment.isInternalRequest tc.HttpContext

                Expect.equal actual tc.ShouldBeInternal ""
        )
    ]
