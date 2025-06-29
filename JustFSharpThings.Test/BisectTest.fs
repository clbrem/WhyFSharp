namespace JustFSharpThings.Test
open JustFSharpThings
open Xunit
module BisectTest =
    
    [<Fact>]
    let ``Can Sort Guid``() =
        let a = Array.init 100 (fun _ -> System.Guid.NewGuid()) |> Array.sort
        let b = System.Guid.NewGuid()
        let predicate test i =
            let t = a.[i]
            if t < test then -1y
            elif t > test then 1y
            else 0y
        let j = Bisect.bisect (predicate b) (0, 99)        
        Assert.True(a[j] <= b )
        Assert.True(a[j+1] > b)
        
        let m = a[65]
        let k = Bisect.bisect (predicate m) (0, 99)
        Assert.Equal(65, k)
        
        
        
        