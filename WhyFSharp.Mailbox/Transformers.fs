namespace Mailbox

open System
open System.Buffers
open System.Numerics

module Transformers =
    let private guidToInt modulus (guid: Guid) : uint16 =            
        BitConverter.ToUInt16(guid.ToByteArray(),0) &&& ((1us <<< modulus) - 1us)   
    
    let router (key: 'T -> Guid) (mailboxes: MailboxProcessor<'T> seq) : MailboxProcessor<'T> =
        let length = Seq.length mailboxes |> uint32
        let modulus = BitOperations.Log2(length) 
        MailboxProcessor.Start(
            fun inbox ->
                let rec loop () =
                    async {
                        let! message = inbox.Receive()
                        let mailboxIndex = guidToInt modulus (key message)
                        let mailbox = Seq.item (int mailboxIndex) mailboxes
                        do mailbox.Post message
                        return! loop() 
                    }
                loop ()
            )
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
        