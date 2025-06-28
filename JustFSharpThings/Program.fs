module JustFSharpThings.App

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe

// ---------------------------------
// Models
// ---------------------------------
[<CLIMutable>]
type Message =
    {
        text : string
    }

// ---------------------------------
// Views
// ---------------------------------

module Database =    
    let (|KeyValue|_|) key (input: string) =
        let parts = input.Split([|':'|], 2)
        if parts.Length = 2 then
            match Guid.TryParse(parts.[0]) with
            | true, guid when guid = key -> Some parts.[1]
            | _ -> None
        else
            None            
    let read db (key: Guid) =        
        let rec readLines (db: StreamReader) =            
            match db.ReadLine() with
            | null -> None
            | KeyValue key value -> Some value
            | _ -> readLines db
        readLines db
    let write (db: StreamWriter) (key: Guid) (value: string) =
            do db.WriteLine $"{key}: {value}"

// ---------------------------------
// Web app
// ---------------------------------

let warbler f a=
    f a a

let createToken handler =    
    fun _ ->
        let guid = Guid.NewGuid()
        handler guid
   |> warbler 

let getHandler (key: string) =
    match Guid.TryParse(key) with
    | true, guid ->
        use db = new StreamReader("database.txt")
        match Database.read db guid with
        | Some value ->
            Successful.ok (text value)
        | None ->
            RequestErrors.notFound (text $"No value found for key {key}")
    | _ ->
        RequestErrors.badRequest (text $"Invalid key format: {key}")
let culture = System.Globalization.CultureInfo.CreateSpecificCulture("en-US") |> Some
let writeHandler value (key: Guid): HttpHandler =
    fun next ctx ->
        use db = new StreamWriter("database.txt", true)
        do Database.write db key value    
        next ctx
        
let setHandler  =
    bindForm<Message> culture (
        fun value ->
        createToken (
            fun token ->                
                writeHandler value.text token >=>
                Successful.created (text $"{token}")
        )                
    )

let webApp =
    choose [
        GET >=>
            choose [                
                subRoute "/api" (
                    choose [
                             routef "/get/%s" getHandler                             
                         ]
                    )                  
            ]
        POST >=>
            choose [
                subRoute"/api" (
                    choose [
                        route "/set" >=> setHandler 
                 ])
            ]

        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder : CorsPolicyBuilder) =
    builder
        .WithOrigins(
            "http://localhost:5000",
            "https://localhost:5001")
       .AllowAnyMethod()
       .AllowAnyHeader()
       |> ignore

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    (match env.IsDevelopment() with
    | true  ->
        app.UseDeveloperExceptionPage()
    | false ->
        app .UseGiraffeErrorHandler(errorHandler)
            .UseHttpsRedirection())
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore

let configureLogging (builder : ILoggingBuilder) =
    builder.AddConsole()
           .AddDebug() |> ignore

[<EntryPoint>]
let main args =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .UseContentRoot(contentRoot)
                    .UseWebRoot(webRoot)
                    .Configure(Action<IApplicationBuilder> configureApp)
                    .ConfigureServices(configureServices)
                    .ConfigureLogging(configureLogging)
                    |> ignore)
        .Build()
        .Run()
    0