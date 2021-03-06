module BuildStats.App

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Serilog
open Serilog.Events
open Serilog.Sinks.Elasticsearch
open Elasticsearch.Net
open Giraffe
open BuildStats.Common

[<EntryPoint>]
let main args =
    let parseLogLevel =
        function
        | "verbose" -> LogEventLevel.Verbose
        | "debug"   -> LogEventLevel.Debug
        | "info"    -> LogEventLevel.Information
        | "warning" -> LogEventLevel.Warning
        | "error"   -> LogEventLevel.Error
        | "fatal"   -> LogEventLevel.Fatal
        | _         -> LogEventLevel.Warning

    let logLevelConsole =
        match isNotNull args && args.Length > 0 with
        | true  -> args.[0]
        | false -> Config.logLevelConsole
        |> parseLogLevel

    let logLevelElastic =
        Config.logLevelElastic
        |> parseLogLevel

    let elasticOptions =
        new ElasticsearchSinkOptions(
            new Uri(Config.elasticUrl),
            AutoRegisterTemplate = true,
            AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv6,
            MinimumLogEventLevel = new Nullable<LogEventLevel>(logLevelElastic),
            ModifyConnectionSettings =
                fun (config : ConnectionConfiguration) ->
                    config.BasicAuthentication(
                        Config.elasticUser,
                        Config.elasticPassword))

    Log.Logger <-
        (new LoggerConfiguration())
            .MinimumLevel.Information()
            .Enrich.WithProperty("Environment", Config.environmentName)
            .Enrich.WithProperty("Application", "CI-BuildStats")
            .WriteTo.Console(logLevelConsole)
            .WriteTo.Elasticsearch(elasticOptions)
            .CreateLogger()

    try
        try
            Log.Information "Starting BuildStats.info..."

            WebHostBuilder()
                .UseSerilog()
                .UseKestrel(fun k -> k.AddServerHeader <- false)
                .UseContentRoot(Directory.GetCurrentDirectory())
                .Configure(Action<IApplicationBuilder> Web.configureApp)
                .ConfigureServices(Web.configureServices)
                .Build()
                .Run()
            0
        with ex ->
            Log.Fatal(ex, "Host terminated unexpectedly.")
            1
    finally
        Log.CloseAndFlush()