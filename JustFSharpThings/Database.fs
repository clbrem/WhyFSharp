namespace JustFSharpThings
open System
open System.Buffers
open System.Collections.Generic
open System.IO
open System.Runtime.InteropServices
open System.Text

module Database =
    let private newLine = Encoding.UTF8.GetBytes(Environment.NewLine)
    let (|Key|_|) (buffer: byte[]) (db: #Stream) =
        try
            db.Read(buffer, 0, 16) |> ignore
            Guid(buffer) |> Some
        with
        | :? EndOfStreamException ->
            None
        | :? ArgumentException ->
            None
    let scan width (db: FileStream) =        
        let buffer = ArrayPool.Shared.Rent(16)
        let rec loop (acc: (Guid * int64) list) =
            match db with
            | Key buffer key->
                let loc = db.Position / (int64 (width + 1 + 16))                
                let pos = db.Seek(int64 (width + 1), SeekOrigin.Current)
                if pos >= db.Length then
                    (key, loc) :: acc |> List.map KeyValuePair |> Dictionary
                else                                        
                    (key, loc) :: acc |> loop
            | _ -> acc |> List.map KeyValuePair |> Dictionary
        loop []
        
    
    let write width (db: FileStream) (key: Guid) (value: string) =                
        let shared = ArrayPool.Shared.Rent(width + 1 + 16)
        let pos = db.Position / (int64 (width + 1 + 16))
        try        
            let asBytes = Encoding.UTF8.GetBytes(value)
            let keySlice = shared.AsSpan(0, 16)
            let msgSlice = shared.AsSpan(16, width)
            let endSlice = shared.AsSpan(width+16, 1)
            key.ToByteArray().CopyTo(keySlice)
            asBytes.CopyTo(msgSlice)
            newLine.CopyTo(endSlice)
            db.Write(shared,0,width + 1 + 16) 
            pos
        finally
            ArrayPool.Shared.Return(shared)
            
    let read width index (db: StreamReader) (key: Guid) =
        failwith "todo"

        