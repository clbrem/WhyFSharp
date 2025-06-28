// For more information see https://aka.ms/fsharp-console-apps

open System
open System.Collections.Generic
open System.Net.Http
open System.Threading.Tasks


module Client =
    type Action =
        | Message of string
        | Lookup of Guid
    
    // random string generator for F#
    // Generates a random string of given length    
            
    module Action =
            
        let private randomString length (rnd: Random) =
            let chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
            let charArray = Array.init length (fun _ -> chars.[rnd.Next(chars.Length)])
            String(charArray) 
        // randomKey pulls a random key from a set of keys
        let randomKey (keySet: Set<Guid>) (rnd: Random) =
            let count = Set.count keySet
            if count = 0 then
                Guid.NewGuid() 
            else
                let index = rnd.Next(count)
                keySet |> Seq.item index        
        let random keySet (rnd: Random) =
            if rnd.Next(2) = 0 then
                Message (randomString 1000 rnd)
            else
                randomKey keySet rnd |> Lookup
        
                
    [<Literal>]
    let BaseAddress = "http://localhost:8080/api/"
    let client() =
        new HttpClient(BaseAddress = System.Uri(BaseAddress), Timeout=TimeSpan.FromSeconds(10.0))
    
    let (|Post|_|) (input: string) =
        String.Equals(input.Trim(), "post", System.StringComparison.OrdinalIgnoreCase)
    
    let (|Get|_|) (input: string) =
        String.Equals(input.Trim(), "get", System.StringComparison.OrdinalIgnoreCase)
    let send (client: HttpClient) (action: Action) =
        task {
            match action with
            | Message message ->
                let! resp = client.PostAsync("set", new FormUrlEncodedContent([KeyValuePair("text", message)]))
                let! content = resp.Content.ReadAsStringAsync()
                return Guid.Parse(content)
            | Lookup key ->
                let! resp = client.GetAsync($"get/{key}")
                let! content = resp.Content.ReadAsStringAsync()
                printfn $"%s{content}"
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
    let argList = 
        argv |> Array.toList
    let rnd = Random()
    let rec loop client keyset : Task=
                    task {
                        let action = Client.Action.random keyset rnd 
                        try
                            let! key = Client.send client action 
                            return! loop client (Set.add key keyset)
                        with
                        | ex  ->
                            printfn $"Timeout occurred: %s{ex.Message}"
                            return! loop client keyset                        
                    }    
    
    let toDo =  
        task {
            use client = Client.client()            
            match argList with
            | _ ->
                [|for _ in 1..500 do loop client Set.empty|] |> Task.WaitAll 
                return 0                
        }
    toDo.GetAwaiter().GetResult()
    