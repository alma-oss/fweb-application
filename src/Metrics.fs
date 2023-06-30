namespace Lmc.WebApplication

open Lmc.ServiceIdentification

type SimpleDataSetKeys = SimpleDataSetKeys of (string * string) list

[<RequireQualifiedAccess>]
module Metrics =
    open System
    open System.Diagnostics
    open System.Threading
    open Microsoft.AspNetCore.Http

    open Lmc.Tracing
    open Lmc.State.ConcurrentStorage

    open Lmc.ServiceIdentification
    open Lmc.Metrics
    open Lmc.Metrics.ServiceStatus
    open Lmc.ErrorHandling

    let metricStatus (Context context) = context |> sprintf "%s_status" |> MetricName.createOrFail

    [<RequireQualifiedAccess>]
    module private SimpleDataSetKeys =
        let value (SimpleDataSetKeys dataSetKeys) = dataSetKeys

    type RequestMetric = {
        Request: string
        DurationMilliseconds: int64
        CustomLabels: (string * string) list
        StatusCode: int option
    }

    [<RequireQualifiedAccess>]
    module RequestMetric =
        let fromHttpContext (stopwatch: Stopwatch) (ctx: HttpContext) =
            {
                Request =
                    sprintf "/%s%s"
                        (if ctx.Request.Path.HasValue then ctx.Request.Path.Value.TrimStart '/' else "")
                        (ctx.Request.Query |> Http.Http.anonimizeQueryParameters)
                DurationMilliseconds = stopwatch.ElapsedMilliseconds
                CustomLabels = []
                StatusCode = try Some ctx.Response.StatusCode with _ -> None
            }

    [<AutoOpen>]
    module private InternalState =
        let createKey instance (spot: Spot option) labels =
            [
                match spot with
                | Some spot ->
                    yield "svc_zone", spot.Zone |> Zone.value
                    yield "svc_bucket", spot.Bucket |> Bucket.value
                | _ -> ()

                yield! labels |> SimpleDataSetKeys.value
            ]
            |> DataSetKey.createFromInstance instance
            |> Result.orFail

        let createKeyForStatus instance =
            createKey instance None (SimpleDataSetKeys [])

        let private createKeyForTotalRequestDuration instance request =
            SimpleDataSetKeys (
                [
                    "request", request.Request

                    match request.StatusCode with
                    | Some statusCode -> "http_status_code", string statusCode
                    | _ -> ()
                ]
                @ request.CustomLabels
            )
            |> createKey instance None

        let metricRequestDurationOk = MetricName.createOrFail "total_request_duration_ok"
        let metricRequestDurationNotice = MetricName.createOrFail "total_request_duration_notice"
        let metricRequestDurationWarning = MetricName.createOrFail "total_request_duration_warning"
        let metricRequestDurationCritical = MetricName.createOrFail "total_request_duration_critical"

        let incrementRequestDuration instance request =
            let metric =
                match request.DurationMilliseconds with
                | duration when duration <= 500L -> metricRequestDurationOk
                | duration when duration <= 1000L -> metricRequestDurationNotice
                | duration when duration <= 10000L -> metricRequestDurationWarning
                | _ -> metricRequestDurationCritical

            request
            |> createKeyForTotalRequestDuration instance
            |> State.incrementMetricSetValue (Int 1) metric
            |> ignore

    let createDataSetKey = InternalState.createKey

    let enableContextStatus (instance: Instance) =
        let metricName = metricStatus instance.Context
        let dataSetKey =
            instance
            |> createKeyForStatus

        State.enableStatusMetric metricName dataSetKey

    let serviceStatus instance = result {
        let! markAsEnabled =
            ServiceStatus.markAsEnabled instance Audience.Sys
        let! markAsDisabled =
            ServiceStatus.markAsDisabled instance Audience.Sys

        return { MarkAsEnabled = markAsEnabled; MarkAsDisabled = markAsDisabled }
    }

    let incrementRequestDuration = InternalState.incrementRequestDuration

    [<RequireQualifiedAccess>]
    module Format =
        let metric metricType description metricName =
            metricName
            |> State.getMetric
            |> function
                | Some metric ->
                    { metric with
                        Description = Some description
                        Type = Some metricType
                    }
                    |> Metric.format
                | _ -> ""

        let counter = metric MetricType.Counter

    let currentState (instance: Instance) applicationMetrics _ =
        [
            instance.Context |> metricStatus |> Format.metric MetricType.Gauge "Current instance status."
            ServiceStatus.getFormattedValue()

            metricRequestDurationOk |> Format.counter "Total requests with duration under 0.5s."
            metricRequestDurationNotice |> Format.counter "Total requests with duration 0.5s - 1s."
            metricRequestDurationWarning |> Format.counter "Total requests with duration 1s - 10s."
            metricRequestDurationCritical |> Format.counter "Total requests with duration over 10s."
        ]
        @ applicationMetrics
        |> String.concat ""

    let private requestStopwatchStorage: State<TraceIdentifier, Stopwatch> = State.empty()

    let startRequest (ctx: HttpContext) =
        let identifier = TraceIdentifier ctx.TraceIdentifier

        let stopwatch = Stopwatch()
        requestStopwatchStorage |> State.set (Key identifier) stopwatch
        stopwatch.Start()

    let finishRequest (incrementRequestDurationMetric: RequestMetric -> unit) (ctx: HttpContext) =
        let identifier = TraceIdentifier ctx.TraceIdentifier

        match requestStopwatchStorage |> State.tryFind (Key identifier) with
        | None -> ()
        | Some stopwatch ->
            stopwatch.Stop()
            requestStopwatchStorage |> State.tryRemove (Key identifier)

            ctx
            |> RequestMetric.fromHttpContext stopwatch
            |> incrementRequestDurationMetric
