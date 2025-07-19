namespace Mailbox

module Transformers =
    
    let roundRobin (mailboxes: MailboxProcessor<'T> seq) : MailboxProcessor<'T> =        
        let count = Seq.length mailboxes        
        MailboxProcessor<'T>.Start(
            fun inbox ->
                let rec loop index =
                    async {
                        let! message = inbox.Receive()
                        let mailbox = Seq.item index mailboxes
                        do mailbox.Post message
                        return! loop ((index + 1) % count)
                    }
                loop 0
                )
        