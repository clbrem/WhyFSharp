namespace WhyFSharp.Test
open WhyFSharp
open Xunit
open System
open System.IO
module DatabaseTest =   
   let fileWrite() = File.Open("Samples/database.txt", FileMode.OpenOrCreate, FileAccess.ReadWrite)
   
   [<Fact>]
   let ``Can Write To Database`` () =
       File.Delete("Samples/database.txt")
       use db = File.Open("Samples/database.txt", FileMode.OpenOrCreate, FileAccess.ReadWrite)       
       db.Seek(0, SeekOrigin.Begin) |> ignore
       let fstGuid = Guid.NewGuid()
       let sndGuid = Guid.NewGuid()
       let thirdGuid = Guid.NewGuid()
       let fourthGuid = Guid.NewGuid()
       let ln1 = Database.write 15 db fstGuid "Hello World"
       let ln2 = Database.write 15 db sndGuid "Hillo World"
       let ln3 = Database.write 15 db thirdGuid "Hallo World"
       let ln4 = Database.write 15 db fourthGuid "Hollo World"
       db.Flush()
       Assert.Equal (0L, ln1)
       Assert.Equal (1L, ln2)
       Assert.Equal (2L, ln3)
       Assert.Equal (3L, ln4)
       db.Seek(0, SeekOrigin.Begin) |> ignore
       let scan = Database.scan 15 db
       Assert.Equal(ln1, scan[fstGuid])
       Assert.Equal(ln2, scan[sndGuid])
       Assert.Equal(ln3, scan[thirdGuid])
         
       Assert.Contains(fstGuid, scan)|> ignore
       Assert.Contains(sndGuid, scan)|> ignore
       Assert.Contains(thirdGuid, scan)|> ignore
       Assert.Contains(fourthGuid, scan)|> ignore
       Assert.Equal(Some "Hello World", Database.read 15 scan db fstGuid)       
       Assert.Equal(Some "Hillo World", Database.read 15 scan db sndGuid)
       Assert.Equal(Some "Hollo World", Database.read 15 scan db fourthGuid)
       
   
   [<Fact>]
   let ``Can Write Using Mailbox``() =
       task {
           let writer = App.fileWriter ("Samples/database.txt", 15)
           let guid = Guid.NewGuid()
           let guid2 = Guid.NewGuid()
           let msg = "Hello World"
           writer.Post(App.Set (guid, msg))           
           match! writer.PostAndAsyncReply(fun replyChannel -> App.Get (guid, replyChannel)) with
           | Some msg ->Assert.Equal(msg, "Hello World")
           | _ -> Assert.Fail("Expected Some (Some msg)")
           match! writer.PostAndAsyncReply(fun replyChannel -> App.Get (guid2, replyChannel)) with
           | None -> Assert.True(true) // Guid2 not found
           | _ -> Assert.Fail("Expected Some None")
       }
       
         
       
   [<Fact>]
   let ``Can write value that is too long``() = 
        File.Delete("Samples/database.txt")
        use db = File.Open("Samples/database.txt", FileMode.OpenOrCreate, FileAccess.ReadWrite)       
        db.Seek(0, SeekOrigin.Begin) |> ignore
        let fstGuid = Guid.NewGuid()
        let longValue = String.replicate 100 "A" // Value longer than 15 characters
        Assert.Throws<ArgumentException>(
            fun () -> Database.write 15 db fstGuid longValue |> ignore
        )
        

        

        
       