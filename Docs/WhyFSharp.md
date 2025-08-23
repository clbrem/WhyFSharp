# Eight reasons to try F#
1. **Basics**
    - Released (1.0) by Microsoft Research/Don Syme in 2005
    - Supported in Visual Studio 2010
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
4. **Union Types**/**Active Patterns**
5. **Computation Expressions**
6. **Tail Recursion**
7. **Mailbox Processor**
8. **Fable**
9. (Bonus) **Type Providers** 