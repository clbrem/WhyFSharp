namespace WhyFSharp

open System
open System.Net.WebSockets
open Giraffe
open Microsoft.AspNetCore.Http
//

type DatabaseMessage =
    | Set of Guid*string
    | GetAsync of Guid*AsyncReplyChannel<string option>
    | Get of Guid
    | Subscribe of Guid*(string -> unit) // Placeholder for subscription logic

module Map =
    let append key value map =
        match Map.tryFind key map with
        | Some items -> Map.add key (value:: items) map
        | None -> Map.add key [value] map
    let pop key map =
        match Map.tryFind key map with
        | Some items ->
            Map.remove key map, items
        | None -> map, []
            

module DatabaseMailbox =
    [<TailCall>]
    let rec loop databaseFactory  subscribers (inbox: MailboxProcessor<DatabaseMessage>) =
                
                async {
                    let! msg = inbox.Receive()
                    match msg with
                    | Set (key, value) ->
                        let db = databaseFactory true
                        Database.write db key value
                        do! db.DisposeAsync()
                        return! loop databaseFactory subscribers inbox 
                    | GetAsync (key, reply) ->
                        let db = databaseFactory false
                        Database.read db key
                        |> reply.Reply
                        do! db.DisposeAsync()
                        return! loop databaseFactory subscribers inbox 
                    | Get key ->
                        let db = databaseFactory false
                        let value = Database.read db key
                        let map, subscribers =
                            Map.pop key subscribers
                        do! db.DisposeAsync()
                        return! loop databaseFactory Map.empty inbox
                    | Subscribe (guid, thunk) ->
                        return!
                            Map.append guid thunk subscribers
                            |> loop databaseFactory
                            <| inbox
                }
    let create databaseFactory =
        MailboxProcessor<DatabaseMessage>.Start( loop databaseFactory [])    
module Socket =
    
    let handShake (handler: WebSocket -> Async<unit>) (next: HttpFunc) (ctx: HttpContext) =
        task {
            if ctx.WebSockets.IsWebSocketRequest then
                let! webSocket = ctx.WebSockets.AcceptWebSocketAsync() 
                do! handler webSocket
                return! Successful.ok (text "WebSocket connection established.") next ctx
            else
                return! RequestErrors.badRequest (text "WebSocket request expected.") next ctx
        }
    
    let webSocketHandler (websocket: WebSocket)=
        let buffer = Array.zeroCreate<byte> (1024* 4)
        let rec loop () =
            async {
                let! result = websocket.ReceiveAsync(ArraySegment<byte>(buffer), System.Threading.CancellationToken.None) |> Async.AwaitTask
                if result.MessageType = WebSocketMessageType.Close then
                    do! websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", System.Threading.CancellationToken.None) |> Async.AwaitTask
                else
                    let message = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count)
                    printfn "Received message: %s" message
                    // Echo the message back
                    do! websocket.SendAsync(ArraySegment<byte>(buffer, 0, result.Count), WebSocketMessageType.Text, true, System.Threading.CancellationToken.None)|> Async.AwaitTask
                    return! loop ()
            }
        loop ()
        
module App =
    
    open System.IO
    open Microsoft.AspNetCore.Builder
    open Microsoft.AspNetCore.Cors.Infrastructure
    open Microsoft.AspNetCore.Hosting
    open Microsoft.Extensions.Hosting
    open Microsoft.Extensions.Logging
    open Microsoft.Extensions.DependencyInjection
    

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
            let! value = mailbox.PostAndAsyncReply (fun reply -> DatabaseMessage.GetAsync(token, reply))
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
                    route "/ws" >=> Socket.handShake Socket.webSocketHandler
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
            .UseWebSockets()
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