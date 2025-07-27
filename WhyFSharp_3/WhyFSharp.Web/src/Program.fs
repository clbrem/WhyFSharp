open Browser

let div = document.createElement "div"
let h1 = document.createElement "h1"
h1.innerHTML <- "Hello World!"
div.appendChild h1
|> document.body.appendChild
|> ignore




