namespace JustFSharpThings
open System
open System.Buffers
open System.Collections.Generic
open System.IO
open System.Runtime.InteropServices
open System.Text

module Database =
    
    let private GUID_SIZE = 16
    
    let private newLine = Encoding.UTF8.GetBytes(Environment.NewLine)
    let private NEWLINE_SIZE = Array.length newLine // \r\n in UTF-8
    let private PADDING = GUID_SIZE + NEWLINE_SIZE
    let (|Key|_|) (buffer: byte[]) (db: #Stream) =
        try
            db.Read(buffer, 0, GUID_SIZE) |> ignore
            Guid(buffer.AsSpan(0,16)) |> Some
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
                let loc = db.Position / (int64 (width + GUID_SIZE + NEWLINE_SIZE))
                let pos = db.Seek(int64 (width + 1), SeekOrigin.Current)
                if pos >= db.Length then
                    (key, loc) :: acc |> List.map KeyValuePair |> Dictionary
                else                                        
                    (key, loc) :: acc |> loop
            | _ -> acc |> List.map KeyValuePair |> Dictionary
        loop []
        
    
    let write width (db: FileStream) (key: Guid) (value: string) =        
        let shared = ArrayPool.Shared.Rent(width + PADDING)
        let pos = db.Position / (int64 (width + PADDING))
        try        
            let asBytes = Encoding.UTF8.GetBytes(value)
            let keySlice = shared.AsSpan(0, GUID_SIZE)
            let msgSlice = shared.AsSpan(GUID_SIZE, width)
            let endSlice = shared.AsSpan(width+GUID_SIZE, NEWLINE_SIZE)
            key.ToByteArray().CopyTo(keySlice)
            asBytes.CopyTo(msgSlice)
            newLine.CopyTo(endSlice)
            db.Write(shared,0,width + PADDING) 
            pos
        finally
            ArrayPool.Shared.Return(shared)
            
    let read width (index: IDictionary<Guid, int64>)  (db: FileStream) (key: Guid) =
        let buffer = ArrayPool.Shared.Rent(width + GUID_SIZE)        
        try 
            match index.TryGetValue(key) with
            | true, line ->
                let pos = line * (int64 (width + PADDING))
                db.Seek(pos, SeekOrigin.Begin) |> ignore // Go to correct position
                match db with
                | Key buffer k when k = key ->
                    db.Read(buffer, GUID_SIZE, width) |> ignore // Read the message
                    let msgSlice = buffer.AsSpan(GUID_SIZE, width)
                    let msg = Encoding.UTF8.GetString(msgSlice).TrimEnd(char(0uy)) // Trim null characters
                    Some msg
                | _ -> None
            | _ -> None
            
        finally
            ArrayPool.Shared.Return(buffer)
            
            
            


        