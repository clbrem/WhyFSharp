// For more information see https://aka.ms/fsharp-console-apps

open System
open System.Collections.Generic
open System.Net.Http



module Client =
    type Action =
        | Message of string
        | Lookup of Guid
    
    // random string generator for F#
    // Generates a random string of given length    
            
    module Action =
            
        let private randomString length (rnd: Random) =
            let chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
            let charArray = Array.init length (fun _ -> chars[rnd.Next(chars.Length)])
            String(charArray) 
        // randomKey pulls a random key from a set of keys
        let randomKey (keySet: Set<Guid>) (rnd: Random) =
            let count = Set.count keySet
            if count = 0 then
                None
            else
                // pick a random key from the set
                let index = rnd.Next(count)
                keySet |> Seq.item index |> Some        
        let random keySet (rnd: Random) =
            if rnd.Next(2) = 0 then
                Message (randomString 1000 rnd)
            else
                match randomKey keySet rnd with
                | Some key -> Lookup key
                | None -> Message (randomString 1000 rnd)
                
        
                
    [<Literal>]
    let BaseAddress = "http://localhost:8080/api/"
    let client() =
        new HttpClient(BaseAddress = Uri(BaseAddress), Timeout=TimeSpan.FromSeconds(10.0))
    
    let (|Post|_|) (input: string) =
        String.Equals(input.Trim(), "post", StringComparison.OrdinalIgnoreCase)
    
    let (|Get|_|) (input: string) =
        String.Equals(input.Trim(), "get", StringComparison.OrdinalIgnoreCase)
    let send (client: HttpClient) (action: Action) =
        task {
            match action with
            | Message message ->
                let! resp = client.PostAsync("set", new FormUrlEncodedContent([KeyValuePair("text", message)]))
                let! content = resp.Content.ReadAsStringAsync()
                return Guid.Parse(content)
            | Lookup key ->
                let! resp = client.GetAsync($"get/{key}")
                if resp.StatusCode = System.Net.HttpStatusCode.NotFound then
                    printfn $"Cache Miss! : {key}"                
                return key
        }
    let rec spam (mailbox: MailboxProcessor<Action>)  (rnd: Random) keyset =
        let action = Action.random keyset rnd
        mailbox.Post action
        spam mailbox rnd keyset
    let processor (client: HttpClient) =
        MailboxProcessor.Start(
            fun inbox ->
                let rec loop client keyset =
                    async {
                        let! action = inbox.Receive()
                        try
                            let! key = send client action |> Async.AwaitTask
                            return! loop client (Set.add key keyset)
                        with
                        | ex  ->
                            printfn $"Timeout occurred: %s{ex.Message}"
                            return! loop client keyset                        
                    }
                loop client Set.empty
            )        
        


[<EntryPoint>]
let main argv =
    
    let rnd = Random()
    let rec loop client keyset =
                    async {
                        let action = Client.Action.random keyset rnd 
                        try
                            let! key = Client.send client action |> Async.AwaitTask  
                            return! loop client (Set.add key keyset)
                        with
                        | ex  ->
                            printfn $"Timeout occurred: %s{ex.Message}"
                            return! loop client keyset                        
                    }
    let toDo =  
        async {
            use client = Client.client()                        
            let! _ = [|for _ in 1..100 do loop client Set.empty|] |> Async.Parallel                 
            return 0
        }
    (toDo
    |> Async.StartAsTask
    ).GetAwaiter().GetResult()
    