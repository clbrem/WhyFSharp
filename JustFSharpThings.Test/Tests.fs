namespace Tests

open System
open System.Threading
open Xunit
open Xunit.Abstractions

type TestHarness(logger: ITestOutputHelper) =
    // write an F# mailbox processor that will reply to messages 
    
    
        
    [<Fact>]
    let ``Can Round Robin`` () =        
        let mailboxFactory (logger: ITestOutputHelper) i =
            
            MailboxProcessor<AsyncReplyChannel<int>>.Start(
                fun inbox ->
                    let rec loop() =
                        async {
                            let! channel = inbox.Receive()
                            do channel.Reply(i)
                            return! loop()
                        }
                    loop ()
                )
        async {
            let mailboxes = [| for i in 1 .. 5 -> mailboxFactory logger i |]
            let roundRobin = Mailbox.Transformers.roundRobin mailboxes
            let acc = Array.init 9 (fun _ -> 0)
            for i in 0 ..  8 do
                let! resp = roundRobin.PostAndAsyncReply(id)
                acc.[i] <- resp
            do Assert.Equal<int []>(acc, [| 1; 2; 3; 4; 5; 1; 2; 3; 4; |])
        }
        
    
        
    
    