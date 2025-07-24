namespace WhyFSharp

open System.Collections.Generic
open Mailbox

module App =
    open System
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
    
    let warbler f a=
        f a a
    
    let createToken handler =    
       fun _ ->
            let guid = Guid.NewGuid()
            handler guid
       |> warbler         
    let culture = System.Globalization.CultureInfo.CreateSpecificCulture("en-US") |> Some
    type FileWriterMessage =
        | Set of Guid * string
        | Get of Guid * AsyncReplyChannel<string option>

    let fileWriter factory =        
        MailboxProcessor.Start(
            fun mailbox ->
                let rec loop factory =
                    async {
                        match! mailbox.Receive() with
                        | Set (key,msg) ->
                            use db = factory true
                            try                                 
                                Database.write db key msg                                
                                do! db.DisposeAsync()
                                return! loop factory
                            with
                            | :? IOException ->
                                do! db.DisposeAsync()
                                return! loop factory
                        | Get (key, reply) ->
                            use db = factory false
                            try                                 
                                let msg = Database.read db key 
                                reply.Reply(msg)
                                do! db.DisposeAsync()
                                return! loop factory
                            with
                            | :? IOException  ->
                                reply.Reply(None)
                                do! db.DisposeAsync()
                                return! loop factory
                    }
                loop factory
            )
    let getHandler (mailbox: MailboxProcessor<FileWriterMessage>) (key: string) next ctx=
        task {
            match Guid.TryParse(key) with
            | true, guid ->                
                    let! resp =  mailbox.PostAndTryAsyncReply((fun replyChannel -> Get (guid, replyChannel)), timeout=10000)  
                    match Option.flatten resp with 
                        | Some value ->
                            return! Successful.ok (text value) next ctx
                        | None ->
                            return! RequestErrors.notFound (text $"No value found for key {key}") next ctx                                
            | _ ->
                return! RequestErrors.badRequest (text $"Invalid key format: {key}") next ctx
        }
        
    let writeHandler (mailbox: MailboxProcessor<FileWriterMessage>) value (key: Guid): HttpHandler =
        fun next ctx ->
            mailbox.Post (Set (key, value))
            next ctx

    let failOnNull (value: string) =
        fun next ctx ->
            if String.IsNullOrEmpty(value) then
                RequestErrors.badRequest (text "Value cannot be null or empty") next ctx
            else
                next ctx
            
    let setHandler mailbox token  =        
        bindForm<Message> culture (
                    fun value ->
                        failOnNull value.text
                        >=>                                
                        writeHandler mailbox value.text token >=>
                        Successful.created (text $"{token}")
                )                
        
    
    let webApp mailbox =
        choose [
            GET >=>
                choose [                
                    subRoute "/api" (
                        choose [
                                 routef "/get/%s" (getHandler mailbox)                             
                             ]
                        )                  
                ]
            POST >=>
                choose [
                    subRoute"/api" (
                        choose [
                           route "/set" >=> createToken (setHandler mailbox)  
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
    
    let configureApp (fileName, width) (app : IApplicationBuilder) =
        let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
        let logger = app.ApplicationServices.GetService<ILogger<Database>>()
        let mailbox = 
            fileWriter (Database.factoryWithLogger (fileName, width, logger))               
        (match env.IsDevelopment() with
        | true  ->
            app.UseDeveloperExceptionPage()
        | false ->
            app .UseGiraffeErrorHandler(errorHandler)
                .UseHttpsRedirection())
            .UseCors(configureCors)
            .UseStaticFiles()
            .UseGiraffe(
                mailbox
                |> webApp
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