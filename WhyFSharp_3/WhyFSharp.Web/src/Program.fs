module App
open System
open WhyFSharp.Web
open Feliz
open Elmish
open Elmish.HMR
open Fable.SimpleHttp



type History = {
    id: Guid
    text: string
}

type Model = {
    text: string
    previous: History list
}

type Msg = | Submit | UpdateText of string | AppendKey of History | Handle of Exception  | Fetch of Guid

let init () = {text = ""; previous = []}, Cmd.none


let config: Config =
    {
        server = "http://localhost:5000"
    }
let truncate (s: string) =
    if s.Length > 40 then
        s.Substring(0, 40) + "..."
    else
        s



    
let post config message=
    async {
        let req =
            Http.request($"{config.server}/api/set")
            |> Http.method POST
            |> Http.content (FormData.create() |> FormData.append "text" message |> BodyContent.Form)            
        let! resp =  Http.send req
        printf $"%A{resp}"
        match resp.content with
        | ResponseContent.Text text ->
            match Guid.TryParse(text) with
            | true, guid -> return {id= guid; text= message |> truncate}
            | false, _ -> return failwith "Failed to parse GUID from response"
        | _ -> return failwith "Unexpected response content"        
    }
let fetch config guid =
    async {
        let req =
            Http.request($"{config.server}/api/get/{guid}")
            |> Http.method GET
        let! resp =  Http.send req
        printf $"%A{resp}"
        match resp.content with
        | ResponseContent.Text text ->
            return text
        | _ -> return failwith "Unexpected response content"
    }


let update config msg model =
    match msg with
    | Submit -> 
        // Here you would typically send the model.Text to a server or process it
        printfn "Submitted text: %s" model.text
        model, Cmd.OfAsync.either 
            (post config ) 
            (model.text) 
            AppendKey
            Handle
    | UpdateText text -> 
        {model with text = text}, Cmd.none
    | AppendKey guid ->
        printf $"Appending %A{guid}"
        {model with previous = guid :: model.previous}, Cmd.none
    | Handle exn -> model, Cmd.none
    | Fetch guid ->
        model,
        Cmd.OfAsync.either 
            (fetch config) 
            guid 
            UpdateText
            Handle

let render model dispatch =
            
    Html.div [
        prop.key "ParentWindow"
        prop.className ["flex"; "flex-row"; "justify-center"; "items-center"; "gap-4"; "w-full"; "max-w-4xl"; "mx-auto"; ]
        prop.children [
            Html.div [
                prop.key "MainContainer"
                prop.className [
                    "flex"; "flex-col"; "items-center"; "justify-center"; "gap-4";"my-16"
                ]
                prop.children [
                    Html.div [
                        prop.key "InputContainer"
                        prop.className ["w-xl"; ]
                        prop.children [
                            Html.textarea [
                                prop.value model.text                                
                                prop.className [ "resize"; "block"; "p-2.5"; "w-full"; "text-sm"; "text-gray-900"; "bg-gray-50"; "rounded-lg"; "border"; "border-gray-300"; "focus:ring-blue-500"; "focus:border-blue-500"; "dark:bg-gray-700"; "dark:border-gray-600"; "dark:placeholder-gray-400"; "dark:text-white"; "dark:focus:ring-blue-500"; "dark:focus:border-blue-500"]
                                prop.key "Input"
                                prop.placeholder "Please enter some text"
                                prop.onChange (UpdateText >> dispatch)            
                            ]
                            Html.div [
                                    prop.key "ButtonContainer"
                                    prop.className ["flex"; "flex-wrap";"justify-end";"gap-4";"w-full";"mt-2"]
                                    prop.children [
                                        Html.button [
                                            prop.name "SubmitButton"
                                            prop.className ["cursor-pointer"; "select-none"; "px-8"; "py-4"; "bg-gradient-to-r"; "from-blue-500"; "to-purple-500"; "text-white"; "font-bold"; "rounded-full"; "transition-transform"; "transform-gpu"; "hover:-translate-y-1"; "hover:shadow-lg"]
                                            prop.key "Submit"
                                            prop.onClick (fun _ -> dispatch Submit)
                                            prop.text "Send"
                                            ]
                                        ]                
                                ]                    
                            ]
                    ]
                    
                ]
            ]
            Html.div [
                prop.key "Sidebar"
                prop.className ["justify-end"; "flex"; "flex-col"; "gap-4"; "bg-gray-100"; "rounded-lg"]
                prop.children [
                    Html.ul [
                        for history in List.rev model.previous do
                            yield Html.li [
                                prop.onClick (fun _ -> dispatch (Fetch history.id))
                                prop.key $"Key-%A{history.id}"
                                prop.className ["p-2"; "bg-white"; "rounded-lg"; "shadow-md"; "mb-2"; "cursor-pointer"]
                                prop.text $"%A{history.text}"
                            ]
                    ]
                ]
            
                
            ]
        ]
    ]
        
Program.mkProgram init (update config) render
|> Program.withReactSynchronous "elmish-app"
|> Program.run