namespace WhyFSharp
//
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
    let warbler f a  = f a a         
    let createGuid handler =
        fun _  -> 
            let guid = Guid.NewGuid()
            handler guid
        |> warbler  
    //    ---------------------------------    
    let webApp  databaseFactory =
        choose [
            GET >=>
                choose [                
                    subRoute "/api" (
                        choose [
                                 routef "/get/%s" (parseToken (getHandler databaseFactory ))                              
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
                                              >=> writeHandler databaseFactory  message.text guid
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
                "https://localhost:5001")
           .AllowAnyMethod()
           .AllowAnyHeader()
           |> ignore
    
    let configureApp (fileName, width) (app : IApplicationBuilder) =
        let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
        let logger = app.ApplicationServices.GetService<ILogger<Database>>()
        let database = Database.factoryWithLogger (fileName, width, logger)            
        (match env.IsDevelopment() with
        | true  ->
            app.UseDeveloperExceptionPage()
        | false ->
            app .UseGiraffeErrorHandler(errorHandler)
                .UseHttpsRedirection())
            .UseCors(configureCors)
            .UseStaticFiles()
            .UseGiraffe(
                webApp database
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