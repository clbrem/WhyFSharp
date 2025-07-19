namespace WhyFSharp

open System.Collections.Generic


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
    let fileWriter (file, width)=
        let fileStream = File.Open(file, FileMode.OpenOrCreate, FileAccess.Read)
        let index = Database.scan width fileStream
        fileStream.Dispose()        
        MailboxProcessor.Start(
            fun mailbox ->
                let rec loop (index: IDictionary<Guid,int64>)  =
                    async {
                        match! mailbox.Receive() with
                        | Set (key,msg) ->
                            use db = File.Open(file, FileMode.OpenOrCreate, FileAccess.ReadWrite)
                            try 
                                db.Seek(0L, SeekOrigin.End) |> ignore
                                let lineNumber = Database.write width db key msg
                                index.Add(key, lineNumber)
                                do! db.DisposeAsync().AsTask() |> Async.AwaitTask
                                return! loop index
                            with
                            | :? IOException ->
                                do! db.DisposeAsync().AsTask() |> Async.AwaitTask
                                return! loop index                                
                        | Get (key, reply) ->
                            use db = File.Open(file, FileMode.OpenOrCreate, FileAccess.Read)                            
                            try                                 
                                let msg = Database.read width index db key 
                                reply.Reply(msg)
                                do! db.DisposeAsync().AsTask() |> Async.AwaitTask
                                return! loop index
                            with
                            | :? IOException  ->
                                reply.Reply(None)
                                do! db.DisposeAsync().AsTask() |> Async.AwaitTask
                                return! loop index
                    }
                loop index
            )
    let getHandler (mailbox: MailboxProcessor<FileWriterMessage>) (key: string) next ctx=
        task {
            match Guid.TryParse(key) with
            | true, guid ->                
                    let! resp =  mailbox.PostAndTryAsyncReply(fun replyChannel -> Get (guid, replyChannel))  
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

            
    let setHandler mailbox  =
        bindForm<Message> culture (
            fun value ->
            createToken (
                fun token ->                
                    writeHandler mailbox value.text token >=>
                    Successful.created (text $"{token}")
            )                
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
                            route "/set" >=> setHandler mailbox 
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
    
    let configureApp mailbox (app : IApplicationBuilder) =
        let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
        (match env.IsDevelopment() with
        | true  ->
            app.UseDeveloperExceptionPage()
        | false ->
            app .UseGiraffeErrorHandler(errorHandler)
                .UseHttpsRedirection())
            .UseCors(configureCors)
            .UseStaticFiles()
            .UseGiraffe(webApp mailbox)
    
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
        let mailbox = fileWriter ("database.txt", WIDTH)
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(
                fun webHostBuilder ->
                    webHostBuilder
                        .UseContentRoot(contentRoot)
                        .UseWebRoot(webRoot)
                        .Configure(Action<IApplicationBuilder> (configureApp mailbox))
                        .ConfigureServices(configureServices)
                        .ConfigureLogging(configureLogging)
                        |> ignore)
            .Build()
            .Run()
        0