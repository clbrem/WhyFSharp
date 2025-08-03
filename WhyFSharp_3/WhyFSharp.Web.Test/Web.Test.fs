namespace Test
open Browser.WebSocket
open Expect
open Expect.Dom
open WebTestRunner

module Testing =
    describe "Execute a test "<| fun () ->
        it "should pass" <| fun () ->
            promise {
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
            
            
    