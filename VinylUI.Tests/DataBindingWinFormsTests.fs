﻿module DataBindingWinFormsTests

open System
open System.Windows.Forms
open System.Reflection
open Microsoft.FSharp.Quotations
open NUnit.Framework
open FsUnitTyped
open VinylUI
open VinylUI.WinForms

// model stuff

type Book = {
    Id: int
    Name: string
}

type Model = {
    Id: int
    Name: string
    NickName: string option
    Age: int option
    Books: Book seq
}
with
    static member IdProperty = typedefof<Model>.GetProperty("Id")
    static member NameProperty = typedefof<Model>.GetProperty("Name")

let model = { Id = 2
              Name = "Dan"
              NickName = Some "D"
              Age = Some 30
              Books = [ { Id = 27; Name = "Programming For the Brave and True" }
                        { Id = 53; Name = "Something Like That" } ]
            }

// view stuff

type NumberBox() =
    inherit TextBox()

    let changedEvent = Event<EventHandler, EventArgs>()

    [<CLIEvent>]
    member this.ValueChanged = changedEvent.Publish

    member this.Value
        with get () =
            match System.Int32.TryParse(this.Text) with
            | true, v -> Nullable v
            | _ -> Nullable()
        and set (v: Nullable<int>) =
            this.Text <- string v
            changedEvent.Trigger(this, EventArgs.Empty)

type FakeForm() =
    let ctx = BindingContext()
    let init (ctl: 'c when 'c :> Control) =
        ctl.BindingContext <- ctx
        ctl.CreateControl()
        ctl

    member val TextBox = new TextBox() |> init
    member val ListBox = new ListBox() |> init
    member val NumberBox = new NumberBox() |> init

    interface IDisposable with
        member this.Dispose() =
            this.TextBox.Dispose()
            this.ListBox.Dispose()
            this.NumberBox.Dispose()

let validate (ctl: Control) =
    let notify = typedefof<Control>.GetMethod("NotifyValidating", BindingFlags.Instance ||| BindingFlags.NonPublic)
    notify.Invoke(ctl, null) |> ignore

// test utils

let controlGet cp = cp.ControlProperty.GetValue cp.Control
let controlSet x cp = cp.ControlProperty.SetValue(cp.Control, x)

let testModelToView (viewExpr: Expr<'v>) (startVal: 'v) newVal expectedVal binding =
    let cp = CommonBinding.controlPart viewExpr
    use s = binding.ViewChanged.Subscribe (fun _ -> failwith "view should not be updated here")
    controlGet cp :?> 'v |> shouldEqual startVal
    binding.SetView (box newVal)
    controlGet cp :?> 'v |> shouldEqual expectedVal

let testNonModelToView (viewExpr: Expr<'v>) (startVal: 'v) newVal binding =
    let cp = CommonBinding.controlPart viewExpr
    use s = binding.ViewChanged.Subscribe (fun _ -> failwith "view should not be updated here")
    controlGet cp :?> 'v |> shouldEqual startVal
    binding.SetView (box newVal)
    controlGet cp :?> 'v |> shouldEqual startVal

let testViewToModel sourceUpdate (viewExpr: Expr<'v>) startVal (newVal: 'v) expectedVal binding =
    let cp = CommonBinding.controlPart viewExpr
    let mutable fromView = startVal
    use s = binding.ViewChanged.Subscribe (fun n -> fromView <- n :?> 'm)
    controlSet newVal cp
    match sourceUpdate with
    | OnChange -> fromView |> shouldEqual expectedVal
    | OnValidation -> fromView |> shouldEqual startVal
    validate cp.Control
    fromView |> shouldEqual expectedVal

let testNonViewToModel (viewExpr: Expr<'v>) startVal (newVal: 'v) binding =
    let cp = CommonBinding.controlPart viewExpr
    let mutable fromView = startVal
    use s = binding.ViewChanged.Subscribe (fun n -> fromView <- n :?> 'm)
    controlSet newVal cp
    fromView |> shouldEqual startVal
    validate cp.Control
    fromView |> shouldEqual startVal

let sourceUpdateModes = [ OnValidation; OnChange ]

// finally, the tests

// two-way

[<TestCaseSource("sourceUpdateModes")>]
let ``Bind matching properties two-way`` sourceUpdate =
    use form = new FakeForm()
    let viewExpr = <@ form.TextBox.Text @>
    let binding = Bind.view(viewExpr).toModel(<@ model.Name @>, sourceUpdate)
    binding.ModelProperty |> shouldEqual Model.NameProperty
    binding |> testModelToView viewExpr model.Name "Bob" "Bob"
    binding |> testViewToModel sourceUpdate viewExpr model.Name "Cat" "Cat"

[<TestCaseSource("sourceUpdateModes")>]
let ``Bind nullable to option two-way`` sourceUpdate =
    use form = new FakeForm()
    let viewExpr = <@ form.NumberBox.Value @>
    let binding = Bind.view(viewExpr).toModel(<@ model.Age @>, sourceUpdate)
    binding |> testModelToView viewExpr (Option.toNullable model.Age) (Some 31) (Nullable 31)
    binding |> testViewToModel sourceUpdate viewExpr model.Age (Nullable 32) (Some 32)

[<TestCaseSource("sourceUpdateModes")>]
let ``Bind obj to val type two-way`` sourceUpdate =
    use form = new FakeForm()
    form.ListBox.DataSource <- [ 0 .. 100 ] |> List.toArray
    let viewExpr = <@ form.ListBox.SelectedItem @>
    let binding = Bind.view(viewExpr).toModel(<@ model.Id @>, sourceUpdate)
    binding.ModelProperty |> shouldEqual Model.IdProperty
    binding |> testModelToView viewExpr (box model.Id) 3 (box 3)
    binding |> testViewToModel sourceUpdate viewExpr model.Id (box 4) 4

[<TestCaseSource("sourceUpdateModes")>]
let ``Bind obj to ref type two-way`` sourceUpdate =
    use form = new FakeForm()
    form.ListBox.DataSource <- ",Dan,John,Matt".Split([|','|])
    let viewExpr = <@ form.ListBox.SelectedItem @>
    let binding = Bind.view(viewExpr).toModel(<@ model.Name @>, sourceUpdate)
    binding |> testModelToView viewExpr (box model.Name) "John" (box "John")
    binding |> testViewToModel sourceUpdate viewExpr model.Name (box "Matt") "Matt"

[<TestCaseSource("sourceUpdateModes")>]
let ``Bind obj to val type option two-way`` sourceUpdate =
    use form = new FakeForm()
    form.ListBox.DataSource <- [ 0 .. 100 ] |> List.toArray
    let viewExpr = <@ form.ListBox.SelectedItem @>
    let binding = Bind.view(viewExpr).toModel(<@ model.Age @>, sourceUpdate)
    binding |> testModelToView viewExpr (model.Age |> Option.toNullable |> box) (Some 31) (box 31)
    binding |> testViewToModel sourceUpdate viewExpr model.Age (box 32) (Some 32)

[<TestCaseSource("sourceUpdateModes")>]
let ``Bind obj to ref type option two-way`` sourceUpdate =
    use form = new FakeForm()
    form.ListBox.DataSource <- ",D,J,M".Split([|','|])
    let viewExpr = <@ form.ListBox.SelectedItem @>
    let binding = Bind.view(viewExpr).toModel(<@ model.NickName @>, sourceUpdate)
    binding |> testModelToView viewExpr (model.NickName |> Option.toObj |> box) (Some "J") (box "J")
    binding |> testViewToModel sourceUpdate viewExpr model.NickName (box "M") (Some "M")

// one way to model

[<TestCaseSource("sourceUpdateModes")>]
let ``Bind matching properties one way to model`` sourceUpdate =
    use form = new FakeForm()
    let viewExpr = <@ form.TextBox.Text @>
    let binding = Bind.view(viewExpr).toModelOneWay(<@ model.Name @>, sourceUpdate)
    binding.ModelProperty |> shouldEqual Model.NameProperty
    binding |> testNonModelToView viewExpr "" "Cat"
    binding |> testViewToModel sourceUpdate viewExpr model.Name "Bob" "Bob"

[<TestCaseSource("sourceUpdateModes")>]
let ``Bind nullable to option one way to model`` sourceUpdate =
    use form = new FakeForm()
    let viewExpr = <@ form.NumberBox.Value @>
    let binding = Bind.view(viewExpr).toModelOneWay(<@ model.Age @>, sourceUpdate)
    binding |> testNonModelToView viewExpr (Nullable()) (Some 31)
    binding |> testViewToModel sourceUpdate viewExpr model.Age (Nullable 32) (Some 32)

[<TestCaseSource("sourceUpdateModes")>]
let ``Bind obj to val type one way to model`` sourceUpdate =
    use form = new FakeForm()
    form.ListBox.DataSource <- [ 0 .. 100 ] |> List.toArray
    let viewExpr = <@ form.ListBox.SelectedItem @>
    let binding = Bind.view(viewExpr).toModelOneWay(<@ model.Id @>, sourceUpdate)
    binding.ModelProperty |> shouldEqual Model.IdProperty
    binding |> testNonModelToView viewExpr (box 0) 3
    binding |> testViewToModel sourceUpdate viewExpr model.Id (box 4) 4

[<TestCaseSource("sourceUpdateModes")>]
let ``Bind obj to ref type one way to model`` sourceUpdate =
    use form = new FakeForm()
    form.ListBox.DataSource <- ",Dan,John,Matt".Split([|','|])
    let viewExpr = <@ form.ListBox.SelectedItem @>
    let binding = Bind.view(viewExpr).toModelOneWay(<@ model.Name @>, sourceUpdate)
    binding |> testNonModelToView viewExpr (box "") "John"
    binding |> testViewToModel sourceUpdate viewExpr model.Name (box "Matt") "Matt"

[<TestCaseSource("sourceUpdateModes")>]
let ``Bind obj to val type option one way to model`` sourceUpdate =
    use form = new FakeForm()
    form.ListBox.DataSource <- [ 0 .. 100 ] |> List.toArray
    let viewExpr = <@ form.ListBox.SelectedItem @>
    let binding = Bind.view(viewExpr).toModelOneWay(<@ model.Age @>, sourceUpdate)
    binding |> testNonModelToView viewExpr (box 0) (Some 31)
    binding |> testViewToModel sourceUpdate viewExpr model.Age (box 32) (Some 32)

[<TestCaseSource("sourceUpdateModes")>]
let ``Bind obj to ref type option one way to model`` sourceUpdate =
    use form = new FakeForm()
    form.ListBox.DataSource <- ",D,J,M".Split([|','|])
    let viewExpr = <@ form.ListBox.SelectedItem @>
    let binding = Bind.view(viewExpr).toModelOneWay(<@ model.NickName @>, sourceUpdate)
    binding |> testNonModelToView viewExpr (box "") (Some "J")
    binding |> testViewToModel sourceUpdate viewExpr model.NickName (box "M") (Some "M")

// one way to view

[<Test>]
let ``Bind matching properties one way to view`` () =
    use form = new FakeForm()
    let viewExpr = <@ form.TextBox.Text @>
    let binding = Bind.model(<@ model.Name @>).toViewOneWay(viewExpr)
    binding.ModelProperty |> shouldEqual Model.NameProperty
    binding |> testModelToView viewExpr model.Name "Bob" "Bob"
    binding |> testNonViewToModel viewExpr model.Name "Cat"

[<Test>]
let ``Bind nullable to option one way to view`` () =
    use form = new FakeForm()
    let viewExpr = <@ form.NumberBox.Value @>
    let binding = Bind.model(<@ model.Age @>).toViewOneWay(viewExpr)
    binding |> testModelToView viewExpr (Option.toNullable model.Age) (Some 31) (Nullable 31)
    binding |> testNonViewToModel viewExpr model.Age (Nullable 32)

[<Test>]
let ``Bind obj to val type one way to view`` () =
    use form = new FakeForm()
    form.ListBox.DataSource <- [ 0 .. 100 ] |> List.toArray
    let viewExpr = <@ form.ListBox.SelectedItem @>
    let binding = Bind.model(<@ model.Id @>).toViewOneWay(viewExpr)
    binding.ModelProperty |> shouldEqual Model.IdProperty
    binding |> testModelToView viewExpr (box model.Id) 3 (box 3)
    binding |> testNonViewToModel viewExpr model.Id (box 4)

[<Test>]
let ``Bind obj to ref type one way to view`` () =
    use form = new FakeForm()
    form.ListBox.DataSource <- ",Dan,John,Matt".Split([|','|])
    let viewExpr = <@ form.ListBox.SelectedItem @>
    let binding = Bind.model(<@ model.Name @>).toViewOneWay(viewExpr)
    binding |> testModelToView viewExpr (box model.Name) "John" (box "John")
    binding |> testNonViewToModel viewExpr model.Name (box "Matt")

[<Test>]
let ``Bind obj to val type option one way to view`` () =
    use form = new FakeForm()
    form.ListBox.DataSource <- [ 0 .. 100 ] |> List.toArray
    let viewExpr = <@ form.ListBox.SelectedItem @>
    let binding = Bind.model(<@ model.Age @>).toViewOneWay(viewExpr)
    binding |> testModelToView viewExpr (model.Age |> Option.toNullable |> box) (Some 31) (box 31)
    binding |> testNonViewToModel viewExpr model.Age (box 32)

[<Test>]
let ``Bind obj to ref type option one way to view`` () =
    use form = new FakeForm()
    form.ListBox.DataSource <- ",D,J,M".Split([|','|])
    let viewExpr = <@ form.ListBox.SelectedItem @>
    let binding = Bind.model(<@ model.NickName @>).toViewOneWay(viewExpr)
    binding |> testModelToView viewExpr (model.NickName |> Option.toObj |> box) (Some "J") (box "J")
    binding |> testNonViewToModel viewExpr model.NickName (box "M")

// model to func

[<Test>]
let ``Bind model to func`` () =
    let mutable fVal = None
    let mutable fCount = 0
    let f n =
        fVal <- Some n
        fCount <- fCount + 1
    let binding = Bind.model(<@ model.Name @>).toFunc(f)
    binding.ModelProperty |> shouldEqual Model.NameProperty
    use s = binding.ViewChanged.Subscribe (fun _ -> failwith "view should not be updated here")
    (fVal, fCount) |> shouldEqual (Some model.Name, 1)
    binding.SetView (box "Bob")
    (fVal, fCount) |> shouldEqual (Some "Bob", 2)
