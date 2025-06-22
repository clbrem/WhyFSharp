// For more information see https://aka.ms/fsharp-console-apps


open System.Net.Http

module Client =
    [<Literal>]
    let BaseAddress = "http://localhost:5000/api/"
    let client() =
        new HttpClient(BaseAddress = System.Uri(BaseAddress))


[<EntryPoint>]
let main argv =
    