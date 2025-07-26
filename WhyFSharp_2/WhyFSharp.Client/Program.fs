
open System
open System.Collections.Generic
open System.Net.Http

module Random =
    let string length (rnd: Random) =
            // random string generator for F#
            // Generates a random string of given length
            let chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
            let charArray = Array.init length (fun _ -> chars[rnd.Next(chars.Length)])
            String(charArray)
    let key (keySet: Set<Guid>) (rnd: Random) =
            // randomKey pulls a random key from a set of keys
            let count = Set.count keySet
            if count = 0 then
                None
            else
                // pick a random key from the set
                let index = rnd.Next(count)
                keySet |> Seq.item index |> Some            

type Action =
    | Message of string
    | Lookup of Guid         
module Action =                        
    let random keySet (rnd: Random) = 
        if rnd.Next(2) = 0 then
            Message (Random.string 1000 rnd)
        else
            match Random.key keySet rnd with
            | Some key -> Lookup key
            | None -> Message (Random.string 1000 rnd)

module Client =
                                
    [<Literal>]
    let BaseAddress = "http://localhost:8080/api/"
    let client() =
        new HttpClient(BaseAddress = Uri(BaseAddress), Timeout=TimeSpan.FromSeconds(10.0))
    
    let writeAsync (client: HttpClient) (message: string) =
        task {
            let! resp = client.PostAsync("set", new FormUrlEncodedContent([KeyValuePair("text", message)]))
            if resp.StatusCode <> System.Net.HttpStatusCode.OK then
                failwithf "Failed to write message: %A" resp.StatusCode            
            let! content = resp.Content.ReadAsStringAsync()
            return Guid.Parse(content)
        } |> Async.AwaitTask
    let lookupAsync (client: HttpClient) (key: Guid) =
        task {
            let! resp = client.GetAsync($"get/{key}")
            if resp.StatusCode = System.Net.HttpStatusCode.NotFound then
                printfn $"Miss! : {key}"
            return! resp.Content.ReadAsStringAsync()
        } |> Async.AwaitTask
    [<TailCall>]
    let rec loop (client: HttpClient)  (rnd: Random) (keySet: Set<Guid>) =
        async {
            let action = Action.random keySet rnd
            match action with
            | Message msg ->
                let! guid = writeAsync client msg
                printfn $"Wrote message with GUID: {guid}"
                return! loop client rnd (Set.add guid keySet) 
            | Lookup key ->
                let! resp = lookupAsync client key
                printfn $"Lookup response for {key}: {resp}"
                return! loop client rnd keySet 
        }
                               

[<EntryPoint>]
let main argv =
    
    let rnd = Random()
    let toDo =  
        async {
            use client = Client.client()
            let! _ = [for _ in 1..10 do Client.loop client rnd Set.empty] |> Async.Parallel
            return 0
        }
    (toDo
    |> Async.StartAsTask
    ).GetAwaiter().GetResult()
