namespace Tests

open System
open Xunit
open Xunit.Abstractions

type TestHarness(logger: ITestOutputHelper) =
    // write an F# mailbox processor that will reply to messages 
    
    [<Fact>]
    let ``Can Round Robin`` () =        
        let mailboxFactory (_: ITestOutputHelper) i =
            
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
                acc[i] <- resp
            do Assert.Equal<int []>(acc, [| 1; 2; 3; 4; 5; 1; 2; 3; 4; |])
        }
    [<Fact>]
    let ``Can Route`` () =
        
        let keys = Array.init 20 (fun _ -> System.Guid.NewGuid())
        let guidToInt modulus (guid: System.Guid) : uint16 =            
           BitConverter.ToUInt16(guid.ToByteArray(),0) &&& ((1us <<< modulus) - 1us)
        let mailboxFactory (_: ITestOutputHelper) i =    
            MailboxProcessor<System.Guid*AsyncReplyChannel<uint16>>.Start(
                fun inbox ->
                    let rec loop() =
                        async {
                            let! _,channel = inbox.Receive()
                            do channel.Reply(i)
                            return! loop()
                        }
                    loop ()
                )
        async {
            let mailboxes = [| for i in 0 .. 15 -> mailboxFactory logger (uint16 i) |]
            let roundRobin = Mailbox.Transformers.router fst mailboxes
            let acc = Array.init 20 (fun _ -> 0us)
            for i in 0 ..  19 do
                let! resp = roundRobin.PostAndAsyncReply(fun ch -> keys[i], ch)
                acc[i] <- resp
            do Assert.Equal<uint16[]>(keys |> Array.map (guidToInt 4) , acc)
        }