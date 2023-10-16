namespace Alma.WebApplication

module OAuth =
    open System
    open System.Collections.Generic
    open System.Net
    open System.Net.Http
    open Alma.ServiceIdentification
    open Alma.State
    open Alma.ErrorHandling
    open Alma.WebApplication
    open Alma.WebApplication.Http

    type OAuthCredentials = {
        ClientId: string
        ClientSecret: string
    }
    type OAuthToken = {
        Type: string
        Token: string
        Expires: int
    }

    [<RequireQualifiedAccess>]
    module OAuthToken =
        open FSharp.Data

        type private ResponseSchema = JsonProvider<"src/schema/oauth-response.json", SampleIsList=true>

        let parse response =
            try
                let parsed = ResponseSchema.Parse response

                Ok {
                    Type = parsed.TokenType
                    Token = parsed.AccessToken
                    Expires = parsed.ExpiresIn
                }
            with e -> Error e

        let asAuthorizationHeader token =
            "Authorization", sprintf "%s %s" token.Type token.Token

    [<RequireQualifiedAccess>]
    module OAuthCredentials =
        let asAuthorizationHeader credentials =
            let encoded =
                sprintf "%s:%s" credentials.ClientId credentials.ClientSecret
                |> System.Text.Encoding.UTF8.GetBytes
                |> Convert.ToBase64String

            "Authorization", sprintf "Basic %s" encoded

    let requestTokenFromCognito region instance (cacheFor: TimeSpan) credentials = asyncResult {
        let url = sprintf "https://%s.auth.%s.amazoncognito.com/oauth2/token" ((instance |> Instance.concat "-").ToLower()) region
        let key = sprintf "%s--%s" url credentials.ClientId

        let requestToken = fun () -> asyncResult {
            use client = new HttpClient()

            let data = Dictionary<string, string>()
            data.Add("grant_type", "client_credentials")

            use requestBodyContent = new HttpRequestMessage(HttpMethod.Post, url)
            requestBodyContent.Content <- new FormUrlEncodedContent(data)

            [
                "Accept", "application/json"
                "Content-Type", "application/x-www-form-urlencoded"
                credentials |> OAuthCredentials.asAuthorizationHeader
            ]
            |> List.iter (fun (key, value) -> requestBodyContent.Headers.TryAddWithoutValidation(key, value) |> ignore)

            let! (response: HttpResponseMessage) =
                client.SendAsync(requestBodyContent)
                |> AsyncResult.ofTaskCatch HttpError.ApiError

            if response.StatusCode <> HttpStatusCode.OK then
                return! AsyncResult.ofError (HttpError.ApiErrorMessage "Invalid response")

            let! content = response.Content.ReadAsStringAsync() |> AsyncResult.ofTaskCatch HttpError.GenericResponseError

            return! content |> OAuthToken.parse |> Result.mapError HttpError.GenericResponseError
        }

        let ttl = (cacheFor.TotalMilliseconds |> int) * 1<TemporaryCache.Millisecond>

        return!
            TemporaryCache.load key requestToken ttl
            |> AsyncResult.mapError (function
                | InvalidTypeOfData -> HttpError.ApiErrorMessage "Invalid cache"
                | LoadFreshDataError error -> error
            )
    }
