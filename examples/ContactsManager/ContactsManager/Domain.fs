module Domain

let formatPhone (number: string) =
    let insert c i (s: string) =
        s.Substring(0, i) + c + s.Substring(i)
    if number.Length = 10 then
        number |> insert "-" 6 |> insert ") " 3 |> insert "(" 0
    else if number.Length = 7 then
        number |> insert "-" 3
    else number

type NumberType =
    | Mobile
    | Work
    | Home
    | Fax
    | Other of string
with
    override this.ToString () =
        match this with
        | Other t -> t
        | _ -> sprintf "%A" this

type ContactNumber = {
    Number: string
    Type: NumberType
} with
    override this.ToString () =
        sprintf "%s: %s" (string this.Type) (formatPhone this.Number)

type Contact = {
    FirstName: string
    LastName: string
    Group: string option
    Numbers: ContactNumber list
    Notes: string
} with
    member this.FullName =
        sprintf "%s %s" this.FirstName this.LastName

    member this.PrimaryNumber =
        this.Numbers |> List.tryHead |> Option.map (string)
