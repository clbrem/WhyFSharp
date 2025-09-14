namespace WhyFSharp


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
    let culture = System.Globalization.CultureInfo.CreateSpecificCulture("en-US") |> Some
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
    
    let stub =
        ServerErrors.notImplemented (text "Method Not Implemented")
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

    //    ---------------------------------    
    let webApp  database =
        choose [
            GET >=>
                choose [                
                    subRoute "/api" (
                        choose [
                                 routef "/get/%s" (parseToken (getHandler database ))   
                             ]
                        )                  
                ]
            POST >=>
                choose [
                    subRoute"/api" (
                        choose [
                           route "/set" >=> stub
                           ]
                     )
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