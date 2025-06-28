# Ten Things I Like about the F#
1. **Table Stakes**
   - Functional (First class functions, immutability, etc.)
   - Object-oriented  
   - ML family (OCaml, Haskell, Rust, etc.)
   - .NET language (compiles to IL)      
   - Indentation based syntax (No braces, no semicolons)
   - Linear compiler
   - Partial application 
     ```fsharp
     let add x y = x + y
     let f = add 2
     f 3 // 5
     ```
   - Pipe operator
     ```fsharp
     3 |> add 2 |> add 5 // 10        
     ```
2. **Type Inference** 
3. **Railway Oriented Programming** 
4. **Union Types**
5. **Pattern Matching**
6. **Computation Expressions**
7. **Tail Recursion**
8. **Mailbox Processor**
9. **Fable**
10. **Type Providers** (NOT)