namespace JustFSharpThings.Test
open JustFSharpThings
open Xunit
open System
open System.IO
module DatabaseTest =   
   let fileWrite() = File.Open("Samples/database.txt", FileMode.OpenOrCreate, FileAccess.ReadWrite)
   
   [<Fact>]
   let ``Can Write To Database`` () =
       use db = File.Open("Samples/database.txt", FileMode.OpenOrCreate, FileAccess.ReadWrite)
       db.Seek(0, SeekOrigin.Begin) |> ignore
       let fstGuid = Guid.NewGuid()
       let sndGuid = Guid.NewGuid()
       let thirdGuid = Guid.NewGuid()
       let fourthGuid = Guid.NewGuid()
       let ln1 = Database.write 15 db (fstGuid) "Hello World"
       let ln2 = Database.write 15 db (sndGuid) "Hillo World"
       let ln3 = Database.write 15 db (thirdGuid) "Hallo World"
       let ln4 = Database.write 15 db (fourthGuid) "Hallo World"
       db.Flush()
       Assert.Equal (0L, ln1)
       Assert.Equal (1L, ln2)
       Assert.Equal (2L, ln3)
       Assert.Equal (3L, ln4)
       db.Seek(0, SeekOrigin.Begin) |> ignore
       let scan = Database.scan 15 db
       Assert.Equal(ln1, scan.[fstGuid])
       Assert.Equal(ln2, scan.[sndGuid])
       Assert.Equal(ln3, scan.[thirdGuid])
         
       Assert.Contains(fstGuid, scan)|> ignore
       Assert.Contains(sndGuid, scan)|> ignore
       Assert.Contains(thirdGuid, scan)|> ignore
       Assert.Contains(fourthGuid, scan)|> ignore
       Assert.Equal(Some "Hello World", Database.read 15 scan db fstGuid)       
       Assert.Equal(Some "Hillo World", Database.read 15 scan db sndGuid)
       Assert.Equal(Some "Hallo World", Database.read 15 scan db fourthGuid)
       
   
