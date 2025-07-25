namespace WhyFSharp.Test
open WhyFSharp
open Xunit
open System
open System.IO
type DatabaseTest () =   
   let factory = ("Samples/database.txt", 1000) |> Database.factory
   
   let db = factory true
   let fstGuid = Guid.NewGuid()
   let sndGuid = Guid.NewGuid()
   let thirdGuid = Guid.NewGuid()
   let fourthGuid = Guid.NewGuid()
   do Database.write db fstGuid "Hello World"
   do Database.write db sndGuid "Hillo World"
   do Database.write db thirdGuid "Hallo World"
   do Database.write db fourthGuid "Hell World"
   do (db:> IDisposable).Dispose()

   [<Fact>]
   let ``Can Write To Database`` () =

       use db = factory(true)
                                            
       Assert.Equal(Some "Hello World", Database.read db fstGuid)       
       Assert.Equal(Some "Hillo World", Database.read  db sndGuid)
       Assert.Equal(Some "Hell World", Database.read  db fourthGuid)
       
   
            
       
   [<Fact>]
   let ``Can not write value that is too long``() =
        let tinyFactory = ("Samples/database_tiny.txt", 15) |> Database.factory   
        use db = tinyFactory true                
        let fstGuid = Guid.NewGuid()
        let longValue = String.replicate 100 "A" // Value longer than 15 characters
        Assert.Throws<ArgumentException>(
            fun () -> Database.write db fstGuid longValue
        )
   [<Fact>]
   let ``What happens when key does not exist ``() =
       use db = factory(false) // Open in read-only mode
       let guid = Guid.NewGuid()
       Assert.Equal(None, Database.read db guid) // Should return None for non-existent key
   [<Fact>]
   let ``What happens when value is null``() =       
           use db = factory true
           let guid = Guid.NewGuid()
           do Database.write db guid null // Write a null value
           Database.read db guid |> Option.map Assert.Empty
    
       
       
       
         



       

        
       