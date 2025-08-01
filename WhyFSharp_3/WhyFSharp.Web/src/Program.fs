module App

open Feliz
open Elmish
open Elmish.HMR

type Model = {Text: string}

type Msg = | Submit | UpdateText of string

let init () = {Text = ""}, Cmd.none

let update msg model =
    match msg with
    | Submit -> 
        // Here you would typically send the model.Text to a server or process it
        printfn "Submitted text: %s" model.Text
        model, Cmd.none
    | UpdateText text -> 
        {model with Text = text}, Cmd.none

let render model dispatch =
            
    Html.div [
        prop.key "MainContainer"
        prop.className [
            "flex"; "flex-col"; "items-center"; "justify-center"; "gap-4"; "my-16";
        ]
        prop.children [
            Html.div [
                prop.key "InputContainer"
                prop.className ["w-xl"; ]
                prop.children [
                    Html.textarea [
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
Program.mkProgram init update render
|> Program.withReactSynchronous "elmish-app"
|> Program.run