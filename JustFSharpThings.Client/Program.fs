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
        new HttpClient(BaseAddress = System.Uri(BaseAddress))
    
    let (|Post|_|) (input: string) =
        String.Equals(input.Trim(), "post", System.StringComparison.OrdinalIgnoreCase)
    
    let (|Get|_|) (input: string) =
        String.Equals(input.Trim(), "get", System.StringComparison.OrdinalIgnoreCase)
    let rec spam (client: HttpClient)  (rnd: Random) keyset =
        task {            
                match Action.random keyset rnd with
                | Message message ->
                    let! resp = client.PostAsync( "set", new FormUrlEncodedContent([KeyValuePair("text", message)] ))
                    let! content = resp.Content.ReadAsStringAsync()
                    printfn $"%s{content}"
                    return!
                        Set.add (Guid.Parse(content)) keyset                        
                        |> spam client rnd                     
                | Lookup key ->
                    let! resp = client.GetAsync ($"get/{key}")
                    let! content = resp.Content.ReadAsStringAsync()
                    printfn $"%s{content}" 
                    return! spam client rnd keyset
        }
        



[<EntryPoint>]
let main argv =
    let argList = 
        argv |> Array.toList
    let rnd = Random()
    let toDo =  
        task {
            use client = Client.client()
            match argList with
            | Client.Post :: str :: _ ->
                let! resp = client.PostAsync( "set", new FormUrlEncodedContent([KeyValuePair("text", str)] ))
                let! content = resp.Content.ReadAsStringAsync()
                do printfn $"%s{content}"
                return 0
            | Client.Get :: key :: _  ->                
                let! resp = client.GetAsync ($"get/{key.Trim()}")
                let! content = resp.Content.ReadAsStringAsync()
                do printfn $"%s{content}"
                return 0
            | _ ->
                do! Client.spam client rnd Set.empty<Guid> 
                return 0                
        }
    toDo.GetAwaiter().GetResult()
    