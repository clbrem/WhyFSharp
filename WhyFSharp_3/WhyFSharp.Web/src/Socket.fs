namespace WhyFSharp.Web

open Browser.Types

module Socket =        
    open Elmish    
    type SocketMessage<'S,'T> =
        | Connect of string
        | Close
        | Send of 'T
        | OnMessage of ('T )
    // let socketManager<'S,'T> path =        
    //     MailboxProcessor.Start (
    //         fun (inbox: MailboxProcessor<Dispatch<'S>*'T>) ->
    //                 let rec loop (socket: WebSocket option) =
    //                     async {
    //                         
    //                     }
    //                 loop None 
    //         )


