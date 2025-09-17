# Eight reasons to try F#
[![F#](f-seeklogo.svg)](https://fsharp.org)
1. **Basics**
    - Released (1.0) by Microsoft Research/Don Syme in 2005
    - Supported in Visual Studio 2010
    - .NET language (compiles to IL) 
    - ML family (OCaml, Haskell, Rust, etc.)
    - Functional (First class functions, immutability, etc.)
    - Object-oriented
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
2. **Type Inference** (Hindley-Milner type system)
3. **Railway Oriented Programming** 
4. **Computation Expressions**
5. **Union Types**/**Active Patterns**
6. **Tail Recursion**
7. **Mailbox Processor**
8. **Fable**
9. (Bonus) **Type Providers** 