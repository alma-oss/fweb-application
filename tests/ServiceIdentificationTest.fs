module Alma.WebApplication.ServiceIdentificationTest

open Expecto
open NSubstitute

open System.IO
open System.Net
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives
open Alma.ServiceIdentification
open Alma.WebApplication

let okOrFail = function
    | Ok x -> x
    | Error e -> failtestf "%A" e

[<Tests>]
let serviceIdentificationTest =
    testList "WebApplication - ServiceIdentification" [
        testCase "Instance - k8sLocalServiceUrl" <| fun _ ->
            let instance = Create.Instance("domain-contextWithSuffix-purpose-version") |> okOrFail
            let url = Instance.k8sLocalServiceUrl instance

            Expect.equal url "http://contextwithsuffix-purpose-version.domain.svc.cluster.local" "Instance should be used as a service name and domain as a namespace"
    ]
