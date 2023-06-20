module Lmc.WebApplication.JsonRpcTest

open Expecto
open NSubstitute

open FSharp.Data
open System.IO
open System.Net
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives
open Lmc.ErrorHandling
open Lmc.Serializer
open Lmc.WebApplication.JsonRpc

let okOrFail = function
    | Ok x -> x
    | Error e -> failwithf "%A" e

type TestCase = {
    Name: string
    Request: string
    ExpectedRequest: Request
}

let provideRequests =
    [
        {
            Name = "Request with no id"
            Request = """{"jsonrpc": "2.0", "method": "subtract", "params": [42, 23]}"""
            ExpectedRequest = {
                Id = RequestId.Null
                Method = Method "subtract"
                Parameters = RawJson (RawJsonData (JsonValue.Array [| JsonValue.Number (decimal 42); JsonValue.Number (decimal 23) |]))
            }
        }
        {
            Name = "Request with int id"
            Request = """{"jsonrpc": "2.0", "id": 42, "method": "subtract", "params": [42, 23]}"""
            ExpectedRequest = {
                Id = RequestId.Number 42
                Method = Method "subtract"
                Parameters = RawJson (RawJsonData (JsonValue.Array [| JsonValue.Number (decimal 42); JsonValue.Number (decimal 23) |]))
            }
        }
        {
            Name = "Request with string id"
            Request = """{"jsonrpc": "2.0", "id": "foo", "method": "subtract", "params": [42, 23]}"""
            ExpectedRequest = {
                Id = RequestId.String "foo"
                Method = Method "subtract"
                Parameters = RawJson (RawJsonData (JsonValue.Array [| JsonValue.Number (decimal 42); JsonValue.Number (decimal 23) |]))
            }
        }
        {
            Name = "Request with null id"
            Request = """{"jsonrpc": "2.0", "id": null, "method": "subtract", "params": [42, 23]}"""
            ExpectedRequest = {
                Id = RequestId.Null
                Method = Method "subtract"
                Parameters = RawJson (RawJsonData (JsonValue.Array [| JsonValue.Number (decimal 42); JsonValue.Number (decimal 23) |]))
            }
        }
    ]

[<Tests>]
let jsonRpcTest =
    testList "JsonRpc - Parse" [
        yield! provideRequests |> List.map (fun tc ->
            testCase tc.Name <| fun _ ->
                let request =
                    tc.Request
                    |> Request.parse
                    |> okOrFail

                Expect.equal request tc.ExpectedRequest ""
        )
    ]
