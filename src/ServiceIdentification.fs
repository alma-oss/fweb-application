namespace Alma.WebApplication

open System.Net
open Microsoft.AspNetCore.Http
open Alma.ServiceIdentification
open Alma.ErrorHandling
open Alma.WebApplication.Http

[<RequireQualifiedAccess>]
module Spot =
    let private tryParseSpot (zone: string, bucket: string) =
        Create.Spot(zone, bucket)
        |> Result.toOption

    let private tryParseSpotFromQuery (query: IQueryCollection) = maybe {
        let! spotValue =
            match query with
            | Http.QueryParameters.Has "spot" [ value ] -> Some value
            | _ -> None

        return!
            match spotValue.Trim('(').Trim(')').Split(',') |> Seq.map String.trim |> Seq.toList with
            | [ zone; bucket ] -> (zone, bucket) |> tryParseSpot
            | _ -> None
    }

    let private tryParseZoneAndBucketFromQuery (query: IQueryCollection) = maybe {
        let! zoneValue =
            match query with
            | Http.QueryParameters.Has "zone" [ value ] -> Some value
            | _ -> None

        let! bucketValue =
            match query with
            | Http.QueryParameters.Has "bucket" [ value ] -> Some value
            | _ -> None

        return! (zoneValue.Trim(), bucketValue.Trim()) |> tryParseSpot
    }

    let parseFromHttpContext (ctx: HttpContext) =
        let query = ctx.Request.Query

        match query |> tryParseSpotFromQuery with
        | Some spot -> Some spot
        | _ ->
        match query |> tryParseZoneAndBucketFromQuery with
        | Some spot -> Some spot
        | _ -> None

[<RequireQualifiedAccess>]
module Instance =
    let k8sLocalServiceUrl (instance: Instance) =
        sprintf "http://%s-%s-%s.%s.svc.cluster.local"
            (instance.Context |> Context.lower |> Context.value)
            (instance.Purpose |> Purpose.lower |> Purpose.value)
            (instance.Version |> Version.lower |> Version.value)
            (instance.Domain |> Domain.lower |> Domain.value)
