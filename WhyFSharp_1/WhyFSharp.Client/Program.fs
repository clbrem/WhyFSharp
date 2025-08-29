
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


module Client =
                                
    [<Literal>]
    let BaseAddress = "http://localhost:8080/api/"
    let client() =
        new HttpClient(BaseAddress = Uri(BaseAddress), Timeout=TimeSpan.FromSeconds(10.0))
    
    let writeAsync (client: HttpClient) (message: string) =
        task {
            let! resp = client.PostAsync("set", new FormUrlEncodedContent([KeyValuePair("text", message)]))
            if not resp.IsSuccessStatusCode then
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


[<EntryPoint>]
let main _ =
    let rnd = Random()
    let toDo =  
        async {
            use client = Client.client()
            let! guid = Client.writeAsync client (Random.string 1000 rnd) 
            let! found = Client.lookupAsync client guid
            printfn $"Found: %s{found}"
            return 0
        }
    (toDo |> Async.StartAsTask
    ).GetAwaiter().GetResult()
    