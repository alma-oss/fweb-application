namespace Lmc.WebApplication.Http

open System
open System.Net
open System.Net.Http
open System.Xml.Serialization

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging

open Giraffe
open Lmc.ErrorHandling
open Lmc.Tracing
open Lmc.Tracing.Extension

type Url = Url of string

[<RequireQualifiedAccess>]
module Url =
    let asUri (Url url) = Uri url

type Method =
    | Get
    | Post of string
    | Put of string option
    | Unsupported of HttpMethod

type ResponseError = {
    Uri: Uri
    StatusCode: HttpStatusCode
    Request: Method
    Response: string
    ResponseMessage: HttpResponseMessage
}

[<RequireQualifiedAccess>]
module internal HttpContent =
    let asString (content: HttpContent) = asyncResult {
        return! content.ReadAsStringAsync()
    }

[<RequireQualifiedAccess>]
module ResponseError =
    let internal fromResponse (response: HttpResponseMessage) = asyncResult {
        let! responseString = response.Content |> HttpContent.asString

        let! method =
            match response.RequestMessage.Method with
            | get when get = HttpMethod.Get -> AsyncResult.ofSuccess Get
            | post when post = HttpMethod.Post ->
                response.RequestMessage.Content
                |> HttpContent.asString
                |> AsyncResult.map Post
            | put when put = HttpMethod.Put ->
                response.RequestMessage.Content
                |> HttpContent.asString
                |> AsyncResult.map Some
                |> AsyncResult.bindError (fun _ -> AsyncResult.ofSuccess None)
                |> AsyncResult.map Put
            | unsupported -> AsyncResult.ofSuccess (Unsupported unsupported)

        return {
            Uri = response.RequestMessage.RequestUri
            StatusCode = response.StatusCode
            Request = method
            Response = responseString
            ResponseMessage = response
        }
    }

    let response { ResponseMessage = response } = response
    let format: ResponseError -> string = sprintf "%A"

    let requestUri { Uri = uri } = uri
    let statusCode { StatusCode = code } = code

    let requestContent = function
        | { Request = Post request } -> Some request
        | { Request = _ } -> None

    let responseContent { Response = response } = response

[<RequireQualifiedAccess>]
type HttpError =
    /// Generic Api Error exception
    | ApiError of exn
    /// Generic Api Error message
    | ApiErrorMessage of string
    /// Specific 4xx or 5xx response error
    | ResponseError of ResponseError
    /// Api handles the request but there is an error with the Response
    | GenericResponseError of exn

[<RequireQualifiedAccess>]
module HttpError =
    let format = function
        | HttpError.ApiError e -> sprintf "Error: %A" e
        | HttpError.ApiErrorMessage e -> sprintf "Error: %A" e
        | HttpError.ResponseError e -> e |> ResponseError.format
        | HttpError.GenericResponseError e -> sprintf "Request was handled but there is a problem with the response: %A" e

    let internal statusCode = function
        | HttpError.ResponseError e -> e |> ResponseError.statusCode |> Some
        | _ -> None

[<RequireQualifiedAccess>]
module internal HttpStatusCode =
    let parseExn (e: exn) =
        match e with
        | :? WebException as webException when webException.Message.Contains "(400) Bad Request" -> Some HttpStatusCode.BadRequest
        | :? WebException as webException when webException.Message.Contains "(401) Unauthorized" -> Some HttpStatusCode.Unauthorized
        | :? WebException as webException when webException.Message.Contains "(403) Forbidden" -> Some HttpStatusCode.Forbidden
        | :? WebException as webException when webException.Message.Contains "(404) Not Found" -> Some HttpStatusCode.NotFound
        | :? WebException as webException when webException.Message.Contains "(406) Not Acceptable" -> Some HttpStatusCode.NotAcceptable
        | :? WebException as webException when webException.Message.Contains "(408) Request Timeout" -> Some HttpStatusCode.RequestTimeout
        | :? WebException as webException when webException.Message.Contains "(409) Conflict" -> Some HttpStatusCode.Conflict
        | :? WebException as webException when webException.Message.Contains "(422) Unprocessable Entity" -> Some HttpStatusCode.UnprocessableEntity
        | _ -> None

    let asInt (statusCode: HttpStatusCode) =
        int statusCode

    let asString = asInt >> string

    let isError = asInt >> fun code -> code >= 400

[<RequireQualifiedAccess>]
module Http =
    open Lmc.Serializer

    [<RequireQualifiedAccess>]
    module QueryParameters =
        let (|Has|_|) key (query: IQueryCollection) =
            match query.TryGetValue key with
            | true, values -> Some (values |> Seq.toList)
            | _ -> None

    let anonimizeQueryParameters (query: IQueryCollection) =
        let anonymize = Set [ "email"; "phone"; "id" ]

        if query.Count <= 0 then ""
        else
            query
            |> Seq.map (function
                | kvPair when kvPair.Key |> anonymize.Contains -> sprintf "%s=***" kvPair.Key
                | kvPair when kvPair.Key.EndsWith "_id" -> sprintf "%s=***" kvPair.Key
                | kvPair -> sprintf "%s=%s" kvPair.Key (kvPair.Value.ToString())
            )
            |> String.concat "&"
            |> (+) "?"

    let private handleResponseTracedError (trace, error) =
        use trace = trace |> Trace.addError (TracedError.ofError HttpError.format error)

        error
        |> HttpError.statusCode
        |> Option.iter (fun statusCode ->
            trace
            |> Trace.addTags [ "http.status_code", statusCode |> HttpStatusCode.asString ]
            |> ignore
        )

        error

    let private handleResponseTracedSuccess (trace, response: HttpResponseMessage) =
        use trace = trace |> Trace.addTags [ "http.status_code", response.StatusCode |> HttpStatusCode.asString ]

        response.Content
        |> HttpContent.asString
        |> AsyncResult.mapError (fun e ->
            trace
            |> Trace.addError (TracedError.ofExn e)
            |> ignore

            HttpError.ApiError e
        )

    let private handleResponse (result: AsyncResult<Trace * HttpResponseMessage, Trace * HttpError>): AsyncResult<string, HttpError> =
        result
        |> AsyncResult.mapError handleResponseTracedError
        |> AsyncResult.bind handleResponseTracedSuccess

    let private assertSuccessfulResponse (response: HttpResponseMessage): AsyncResult<unit, HttpError> = asyncResult {
        if response.StatusCode |> HttpStatusCode.isError then
            let! responseError =
                response
                |> ResponseError.fromResponse
                |> AsyncResult.mapError HttpError.GenericResponseError

            return! AsyncResult.ofError (HttpError.ResponseError responseError)
    }

    let get headers (Url url): AsyncResult<string, HttpError> =
        asyncResult {
            let trace =
                "[HTTP] Get response"
                |> Trace.ChildOf.continueOrStart Trace.Active.current
                |> Trace.addTags [
                    "component", (sprintf "fWebApplication (%s)" AssemblyVersionInformation.AssemblyVersion)
                    "http.method", "GET"
                    "span.kind", "client"
                ]

            let trace = trace |> Trace.addTags [ "http.url", url ]

            use client = new HttpClient()

            headers
            |> Http.inject trace
            |> List.iter (fun (key, value) ->
                client.DefaultRequestHeaders.TryAddWithoutValidation(key, value) |> ignore
            )
            let tracedError error = trace, error

            let! (response: HttpResponseMessage) =
                client.GetAsync(url)
                |> AsyncResult.ofTaskCatch (HttpError.ApiError >> tracedError)

            do! assertSuccessfulResponse response |> AsyncResult.mapError tracedError

            return trace, response
        }
        |> handleResponse

    let post<'Request> headers (Url url) (request: 'Request): AsyncResult<string, HttpError> =
        asyncResult {
            let trace =
                "[HTTP] Post response"
                |> Trace.ChildOf.continueOrStart Trace.Active.current
                |> Trace.addTags [
                    "component", (sprintf "fWebApplication (%s)" AssemblyVersionInformation.AssemblyVersion)
                    "http.method", "POST"
                    "span.kind", "client"
                ]

            let trace = trace |> Trace.addTags [ "http.url", url ]

            let requestBody =
                request
                |> Serialize.toJson

            use client = new HttpClient()
            use requestBodyContent = new StringContent(requestBody, Text.Encoding.UTF8)

            headers
            |> Http.inject trace
            |> List.iter (fun (key, value) ->
                try requestBodyContent.Headers.Remove(key) |> ignore with _ -> ()
                requestBodyContent.Headers.TryAddWithoutValidation(key, value) |> ignore
            )
            let tracedError error = trace, error

            let! (response: HttpResponseMessage) =
                client.PostAsync(url, requestBodyContent)
                |> AsyncResult.ofTaskCatch (HttpError.ApiError >> tracedError)

            do! assertSuccessfulResponse response |> AsyncResult.mapError tracedError

            return trace, response
        }
        |> handleResponse

    let put<'Request> headers (Url url) (request: 'Request): AsyncResult<string, HttpError> =
        asyncResult {
            let trace =
                "[HTTP] Put response"
                |> Trace.ChildOf.continueOrStart Trace.Active.current
                |> Trace.addTags [
                    "component", (sprintf "fWebApplication (%s)" AssemblyVersionInformation.AssemblyVersion)
                    "http.method", "PUT"
                    "span.kind", "client"
                ]

            let trace = trace |> Trace.addTags [ "http.url", url ]

            let requestBody =
                request
                |> Serialize.toJson

            use client = new HttpClient()
            use requestBodyContent = new StringContent(requestBody, Text.Encoding.UTF8)

            headers
            |> Http.inject trace
            |> List.iter (fun (key, value) ->
                try requestBodyContent.Headers.Remove(key) |> ignore with _ -> ()
                requestBodyContent.Headers.TryAddWithoutValidation(key, value) |> ignore
            )
            let tracedError error = trace, error

            let! (response: HttpResponseMessage) =
                client.PutAsync(url, requestBodyContent)
                |> AsyncResult.ofTaskCatch (HttpError.ApiError >> tracedError)

            do! assertSuccessfulResponse response |> AsyncResult.mapError tracedError

            return trace, response
        }
        |> handleResponse
