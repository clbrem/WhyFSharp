
open System
open System.Collections.Generic
open System.Net.Http

module Client =
    type Action =
        | Message of string
        | Lookup of Guid 
        
    module Action =
        
        let private randomString length (rnd: Random) =
            // random string generator for F#
            // Generates a random string of given length
            let chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
            let charArray = Array.init length (fun _ -> chars[rnd.Next(chars.Length)])
            String(charArray) 
        
        let private randomKey (keySet: Set<Guid>) (rnd: Random) =
            // randomKey pulls a random key from a set of keys
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
    
    let writeAsync (client: HttpClient) (message: string) =
        task {
            let! resp = client.PostAsync("set", new FormUrlEncodedContent([KeyValuePair("text", message)]))
            let! content = resp.Content.ReadAsStringAsync()
            return Guid.Parse(content)
        }
    let lookupAsync (client: HttpClient) (key: Guid) =
        task {
            let! resp = client.GetAsync($"get/{key}")
            if resp.StatusCode = System.Net.HttpStatusCode.NotFound then
                printfn $"Miss! : {key}"                
            return key
        }
            
    let send (client: HttpClient) (action: Action) =        
            match action with
            | Message message ->
                writeAsync client message
            | Lookup key ->
                lookupAsync client key                    

[<EntryPoint>]
let main argv =
    
    let rnd = Random()
    let rec loop client keyset : Async<unit>=
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
            let! _ = [ for _ in 1 .. 100 do loop client Set.empty] |> Async.Parallel
            return 0
        }
    (toDo
    |> Async.StartAsTask
    ).GetAwaiter().GetResult()
    