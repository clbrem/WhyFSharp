namespace Mailbox

module Transformers =
    let private truncate max ix =
        if ix < max then
            ix
        else
            0
    let roundRobin (mailboxes: MailboxProcessor<'T> seq) : MailboxProcessor<'T> =        
        let count = Seq.length mailboxes        
        MailboxProcessor<'T>.Start(
            fun inbox ->
                let rec loop index =
                    async {
                        let! message = inbox.Receive()
                        let mailbox = Seq.item index mailboxes
                        do mailbox.Post message
                        return! loop (truncate count (index + 1))
                    }
                loop 0
                )
        