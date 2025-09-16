namespace WhyFSharp
open System
open System.Buffers
open System.Collections.Generic
open System.IO
open System.Text
open Microsoft.Extensions.Logging

type Database =
    // Record type represents a database
    private
        {
            index: IDictionary<Guid, int64>
            logger: ILogger<Database> option
            readOnly: bool
            stream: Stream
            width: int                        
        }
    with
    member this.DisposeAsync() =
        this.stream.DisposeAsync().AsTask() |> Async.AwaitTask
    interface IDisposable
       with 
         member this.Dispose() =
            this.stream.Dispose()                     
module Database =

    // Database consists of two parts: a file with w a bunch of key value pairs and an index. Must also have a width
    // Constants
    let private GUID_SIZE = 16
    let private newLine = Encoding.UTF8.GetBytes(Environment.NewLine)
    let private NEWLINE_SIZE = Array.length newLine // \r\n in UTF-8
    let private PADDING = GUID_SIZE + NEWLINE_SIZE
    
    // Read first 16 bytes as a Guid
    // NOTE: #Stream requires some type hints because it comes from C#
    let (|Key|_|) buffer (db: #Stream) =
        try
            db.Read(buffer, 0, GUID_SIZE) |> ignore
            Guid(buffer.AsSpan(0, GUID_SIZE)) |> Some
        with
        | :? EndOfStreamException ->
            None
        | :? ArgumentException ->
            None
    [<TailCall>]
    let rec private scanLoop width db buffer acc =
        // Create the index by scanning the database
        match db with
        | Key buffer key->
            let loc = db.Position / (int64 (width + GUID_SIZE + NEWLINE_SIZE))
            let pos = db.Seek(int64 (width + 1), SeekOrigin.Current)
            if pos >= db.Length then
                KeyValuePair(key, loc) :: acc |> Dictionary
            else                                        
                scanLoop width db buffer (KeyValuePair(key, loc) :: acc)
        | _ -> acc |> Dictionary
    // Scan a database to create the index
    
    let private scan width (db: #Stream) =
        let buffer = ArrayPool.Shared.Rent(16)        
        try
            scanLoop width db buffer []
        finally
            ArrayPool.Shared.Return(buffer)
            
    // A factory function to create temporary database instances
    let factory (fileName, width)  =
        let index =
          // Scan index from the file if it exists
          use file = File.Open(fileName, FileMode.OpenOrCreate, FileAccess.Read)
          scan width file
        fun (canWrite: bool) ->
            let db =
                if canWrite then
                    File.Open(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite)                                
                else
                    File.Open(fileName, FileMode.Open, FileAccess.Read)
            // Create a file stream for the database 
            { index = index; width = width; stream = db; readOnly = not canWrite ; logger = None}
    let factoryWithLogger (fileName, width, logger: ILogger<Database>) =
        let index =
          // Scan index from the file if it exists
          use file = File.Open(fileName, FileMode.OpenOrCreate, FileAccess.Read)
          scan width file
        fun (canWrite: bool) ->
            let db =
                if canWrite then
                    File.Open(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite)                                
                else
                    File.Open(fileName, FileMode.Open, FileAccess.Read)
            // Create a file stream for the database            
            { index = index; width = width; stream = db; readOnly = not canWrite ; logger = Some logger}
    // Write a key-value pair to the database
    let write (database: Database) (key: Guid) (value: string) =
        // Write a value to the database, update the index
        if database.readOnly then
            raise (InvalidOperationException("Cannot write to a read-only database."))
        if database.width < value.Length then
            raise (ArgumentException($"Value must have length less than {database.width} or it will be truncated."))
        database.logger
        |> Option.iter _.LogInformation("Writing to key {key} message of length {}",key, value.Length )
        let db, width, index = database.stream, database.width, database.index        
        db.Seek(0L, SeekOrigin.End) |> ignore
        let shared = ArrayPool.Shared.Rent(width + PADDING)        
        let pos = db.Position / (int64 (width + PADDING))
        try        
            let asBytes = if String.IsNullOrEmpty(value) then [||] else Encoding.UTF8.GetBytes(value)
            let keySlice = shared.AsSpan(0, GUID_SIZE)
            let msgSlice = shared.AsSpan(GUID_SIZE, width)
            let endSlice = shared.AsSpan(width+GUID_SIZE, NEWLINE_SIZE)
            msgSlice.Fill(0uy)
            key.ToByteArray().CopyTo(keySlice)
            asBytes.CopyTo(msgSlice)            
            newLine.CopyTo(endSlice)
            db.Write(shared,0,width + PADDING) 
            index.Add(KeyValuePair(key, pos))
        finally
            ArrayPool.Shared.Return(shared)
    
    // Read an entry from the database    
    let read database (key: Guid) =
        let db, width, index = database.stream, database.width, database.index        
        database.logger
        |> Option.iter _.LogInformation("Reading key: {key}",key)        
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
