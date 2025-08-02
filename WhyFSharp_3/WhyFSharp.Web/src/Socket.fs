namespace WhyFSharp.Web

module Socket =
    open Browser.WebSocket
    open Elmish
    let socketManager<'S,'T> (config: Config) =
        MailboxProcessor.Start (
            fun (inbox: MailboxProcessor<Dispatch<'S>*'T>) ->
                    let rec loop socket =
                        async {

                        }
                    WebSocket.Create(config.server) |> loop
            )


