namespace JustFSharpThings

module Bisect =
    [<TailCall>]
    let rec bisect (predicate: int -> sbyte) =
        function
        | (st, en)  ->
            if st > en then
                en
            else
                let mid = (st + en) / 2
                if predicate mid = 0y then
                    mid
                elif predicate mid < 0y then
                    bisect predicate (mid + 1, en)
                else
                    bisect predicate (st, mid - 1)
    