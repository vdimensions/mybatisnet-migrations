namespace MyBatis.Migrations

open System


type Change() = 
    static member cmp (this : Change,  other : Change) : int =
        0

    static member eq (this : Change,  other : Change) : bool =
        false

    member this.Clone () =
        Change()

    override this.GetHashCode() =
        0

    override this.Equals (o : obj) : bool =
        match o with
        | :? Change as c -> Change.eq(this, c)
        | :? IEquatable<Change> as eq -> (eq.Equals(this))
        | _ -> false

    member this.Filename
        with get() : string  =
            ""

    interface IComparable with
        member this.CompareTo (o : obj) =
            match o with
            | :? Change as c -> Change.cmp(this, c)
            | :? IComparable as comparable -> -1 * (comparable.CompareTo(o))
            | _ -> invalidOp "Value cannot be compared"

    interface IComparable<Change> with member this.CompareTo(other : Change) = Change.cmp(this, other)

    interface IEquatable<Change> with member this.Equals(other : Change) = Change.eq(this, other)

    interface ICloneable with member this.Clone() = upcast this.Clone()