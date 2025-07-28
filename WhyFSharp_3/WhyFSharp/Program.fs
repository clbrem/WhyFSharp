namespace WhyFSharp

open System

//

type DatabaseMessage =
    | Set of Guid*string
    | Get of Guid*AsyncReplyChannel<string option>

module DatabaseMailbox =
    [<TailCall>]
    let rec loop databaseFactory (inbox: MailboxProcessor<DatabaseMessage>)  =
                async {
                    let! msg = inbox.Receive()
                    match msg with
                    | Set (key, value) ->
                        let db = databaseFactory true
                        Database.write db key value
                        do! db.DisposeAsync()
                        return! loop databaseFactory inbox                        
                    | Get (key, reply) ->
                        let db = databaseFactory false
                        Database.read db key
                        |> reply.Reply
                        do! db.DisposeAsync()
                        return! loop databaseFactory inbox
                }
    let create databaseFactory =
        MailboxProcessor<DatabaseMessage>.Start( loop databaseFactory )    
    
module App =
    
    open System.IO
    open Microsoft.AspNetCore.Builder
    open Microsoft.AspNetCore.Cors.Infrastructure
    open Microsoft.AspNetCore.Hosting
    open Microsoft.Extensions.Hosting
    open Microsoft.Extensions.Logging
    open Microsoft.Extensions.DependencyInjection
    open Giraffe

    let WIDTH =
        1000 // Width of the message in bytes, adjust as needed
    // ---------------------------------
    // Models
    // ---------------------------------
    [<CLIMutable>]
    type Message =
        {
            text : string
        }                     
    let culture = System.Globalization.CultureInfo.CreateSpecificCulture("en-US") |> Some
    let parseToken handler (s: string)=
        match Guid.TryParse(s) with
        | true, guid -> handler guid
        | false, _  -> RequestErrors.badRequest (text "Invalid token format. Expected a valid GUID.")
    let getHandler databaseFactory (token: Guid) =
        use database = databaseFactory false
        match Database.read database token with 
        | Some value -> 
            Successful.ok (text $"{value}") // Placeholder for get handler
        | None -> 
            RequestErrors.notFound (text "Token not found in the database.")
    // ---------------------------------
    let failOnNull msg next ctx=        
        if String.IsNullOrEmpty(msg) then
            RequestErrors.badRequest (text "Message text cannot be null or empty.") next ctx
        else
            next ctx            
    let writeHandler databaseFactory (value: string) (key: Guid) next ctx=
        use database = databaseFactory true
        Database.write database key value
        next ctx
    let asyncWriteHandler (mailbox: MailboxProcessor<DatabaseMessage>) (value: string) (key: Guid) next ctx =        
            do mailbox.Post(DatabaseMessage.Set(key, value))
            next ctx
    let asyncGetHandler (mailbox: MailboxProcessor<DatabaseMessage>) (token: Guid) next ctx =
        task {
            let! value = mailbox.PostAndAsyncReply (fun reply -> DatabaseMessage.Get(token, reply))
            match  value with
            | Some v -> 
                return! Successful.ok (text $"{v}") next ctx
            | None -> 
                return! RequestErrors.notFound (text "Token not found in the database.") next ctx
            }
    let warbler f a  = f a a         
    let createGuid handler =
        fun _  -> 
            let guid = Guid.NewGuid()
            handler guid
        |> warbler  
    //    ---------------------------------    
    let webApp mailbox =
        choose [
            GET >=>
                choose [                
                    subRoute "/api" (
                        choose [
                                 routef "/get/%s" (parseToken (asyncGetHandler mailbox ))                              
                             ]
                        )                  
                ]
            POST >=>
                choose [
                    subRoute"/api" (
                        choose [
                           route "/set" >=> (
                               createGuid (
                                   fun guid ->
                                       bindForm<Message> culture (
                                           fun message ->
                                              failOnNull message.text
                                              >=> asyncWriteHandler mailbox  message.text guid
                                              >=> Successful.ok (text $"{guid}")
                                       )
                               )
                           )
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
                "https://localhost:5001",
                "http://localhost:5173")
           .AllowAnyMethod()
           .AllowAnyHeader()
           |> ignore
    
    let configureApp (fileName, width) (app : IApplicationBuilder) =
        let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
        let logger = app.ApplicationServices.GetService<ILogger<Database>>()
        let database = Database.factoryWithLogger (fileName, width, logger)
        let mailbox = DatabaseMailbox.create database
        (match env.IsDevelopment() with
        | true  ->
            app.UseDeveloperExceptionPage()
        | false ->
            app .UseGiraffeErrorHandler(errorHandler)
                .UseHttpsRedirection())
            .UseCors(configureCors)
            .UseStaticFiles()
            .UseGiraffe(
                webApp mailbox
                )
    
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
                        .ConfigureServices(configureServices)
                        .ConfigureLogging(configureLogging)
                        .Configure(Action<IApplicationBuilder> (configureApp ("database.txt", WIDTH)))
                        |> ignore)
            .Build()
            .Run()
        0