namespace Test
open Browser.WebSocket
open Fable.SimpleHttp
open Fable.Core
open Expect

open WebTestRunner

module ResponseContent =
    let (|Ok|_|) (resp: HttpResponse) =
        match resp.statusCode with
        | 200 -> Some resp.content
        | _ -> None
    let (|Guid|_|) (content: ResponseContent) =
        match content with
        | ResponseContent.Text text ->
            match System.Guid.TryParse(text) with
            | true, guid -> Some guid
            | false, _ -> None
        | _ -> None

module Testing =
    describe "Execute a test "<| fun () ->
        it "should pass" <| fun () ->
            promise {
                let! resp =                    
                        Http.request("http://localhost:5000/api/set")
                        |> Http.method POST
                        |> Http.content (FormData.create() |> FormData.append "text" "Hello from F# test" |> BodyContent.Form)
                        |> Http.send |> Async.StartAsPromise                    
                let guid =
                    match resp with
                    | ResponseContent.Ok (ResponseContent.Guid guid)-> guid
                    | _ -> failwith "Failed to parse GUID from response"
                                
                let socket  = WebSocket.Create("http://localhost:5000/ws")
                let! p = Promise.create(
                    fun resolve reject ->
                        socket.onopen <- fun _ ->
                            socket.onmessage <- fun msg ->
                                resolve msg
                            socket.send("Hello from F# WebSocket test")                            
                        socket.onerror <- fun e ->
                            
                            reject (System.Exception("WebSocket error: " + e.``type``))                
                    )
                Expect.equal p.data "Hello from F# WebSocket test" 
            }
    
            
            
    