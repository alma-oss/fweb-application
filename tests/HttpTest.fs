module Alma.WebApplication.HttpTest

open System.Net
open System.Text
open System.Net.Http
open System.Threading
open System.Threading.Tasks

open Expecto
open Feather.ErrorHandling
open Alma.WebApplication.Http

type CapturedRequest = {
    Method: HttpMethod
    Url: string
    Headers: (string * string list) list
    Body: string
}

type private CapturingHandler(statusCode: HttpStatusCode) =
    inherit HttpMessageHandler()

    let requests = ResizeArray<CapturedRequest>()

    member _.Requests = requests |> List.ofSeq

    override _.SendAsync(request: HttpRequestMessage, _: CancellationToken) = task {
        let! body =
            match request.Content with
            | null -> Task.FromResult ""
            | content -> content.ReadAsStringAsync()

        requests.Add {
            Method = request.Method
            Url = request.RequestUri.ToString()
            Headers = request.Headers |> Seq.map (fun kv -> kv.Key, kv.Value |> List.ofSeq) |> List.ofSeq
            Body = body
        }

        return new HttpResponseMessage(statusCode, Content = new StringContent("response-body"), RequestMessage = request)
    }

let private headerValues name (request: CapturedRequest) =
    request.Headers
    |> List.filter (fst >> (=) name)
    |> List.collect snd

let private okOrFail = function
    | Ok x -> x
    | Error e -> failtestf "%s" (HttpError.format e)

[<Tests>]
let httpTest =
    testList "WebApplication - Http.WithClient" [
        testCase "sets headers on the request and leaves the client untouched" <| fun _ ->
            let handler = new CapturingHandler(HttpStatusCode.OK)
            use client = new HttpClient(handler)

            let response =
                Http.WithClient.get client [ "X-Test", "first" ] (Url "http://localhost/test")
                |> Async.RunSynchronously
                |> okOrFail

            Expect.equal response "response-body" "Response body should be returned"
            Expect.isEmpty client.DefaultRequestHeaders "Client default headers should stay empty"

            let request = handler.Requests |> List.exactlyOne
            Expect.equal request.Method HttpMethod.Get "Method should be GET"
            Expect.equal (request |> headerValues "X-Test") [ "first" ] "Header should be on the request"

        testCase "reused client does not accumulate headers" <| fun _ ->
            let handler = new CapturingHandler(HttpStatusCode.OK)
            use client = new HttpClient(handler)
            let call value = Http.WithClient.get client [ "X-Test", value ] (Url "http://localhost/test") |> Async.RunSynchronously |> okOrFail

            call "first" |> ignore
            call "second" |> ignore

            let second = handler.Requests |> List.last
            Expect.equal (second |> headerValues "X-Test") [ "second" ] "Only the current call's header should be sent"

        testCase "postContent sends the body as given" <| fun _ ->
            let handler = new CapturingHandler(HttpStatusCode.OK)
            use client = new HttpClient(handler)
            let content = new StringContent("<add/>", Encoding.UTF8, "application/xml")

            Http.WithClient.postContent client [] (Url "http://localhost/update") content
            |> Async.RunSynchronously
            |> okOrFail
            |> ignore

            let request = handler.Requests |> List.exactlyOne
            Expect.equal request.Method HttpMethod.Post "Method should be POST"
            Expect.equal request.Body "<add/>" "Body should be sent unchanged"

        testCase "error status becomes ResponseError" <| fun _ ->
            let handler = new CapturingHandler(HttpStatusCode.NotFound)
            use client = new HttpClient(handler)

            match Http.WithClient.get client [] (Url "http://localhost/missing") |> Async.RunSynchronously with
            | Error (HttpError.ResponseError error) ->
                Expect.equal (error |> ResponseError.statusCode) HttpStatusCode.NotFound "Status code should be kept"
                Expect.equal (error |> ResponseError.responseContent) "response-body" "Response body should be kept"
            | other -> failtestf "Expected ResponseError, got %A" other
    ]
