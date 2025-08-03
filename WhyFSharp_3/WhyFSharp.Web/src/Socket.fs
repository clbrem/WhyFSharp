namespace WhyFSharp.Web

open Browser.Types

module Socket =
    open Browser.WebSocket
    open Elmish
    let socketManager<'S,'T> path =
        
        MailboxProcessor.Start (
            fun (inbox: MailboxProcessor<Dispatch<'S>*'T>) ->
                    let rec loop (socket: WebSocket ) =
                        async {
                            match socket.readyState with
                            | WebSocketState.OPEN ->
                                return! loop  socket
                            | WebSocketState.CLOSED
                            | WebSocketState.CLOSING ->
                                return! Async.Sleep 1000 
                            | WebSocketState.CONNECTING -> failwith "todo"
                            | _ -> System.ArgumentOutOfRangeException() |> raise                                                        
                        }
                    loop (WebSocket.Create(path)) 
            )


