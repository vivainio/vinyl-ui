﻿module DataBindingWinFormsTests

open System
open System.Windows.Forms
open System.ComponentModel
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

[<AllowNullLiteralAttribute>]
type BookObj(id: int, name: string) =
    member this.Id = id
    member this.Name = name

type Model = {
    Id: int
    Name: string
    NickName: string option
    Age: int option
    Books: Book list
    BookObjs: BookObj list
    BookIndex: int
    BookSelection: BookObj option
    BookValue: int option
}
with
    static member IdProperty = typedefof<Model>.GetProperty("Id")
    static member NameProperty = typedefof<Model>.GetProperty("Name")
    static member BooksProperty = typedefof<Model>.GetProperty("Books")

let books = [ 
    { Id = 27; Name = "Programming For the Brave and True" }
    { Id = 53; Name = "Something Like That" } 
]

let bookObjs = books |> List.map (fun b -> BookObj(b.Id, b.Name))
let model = { Id = 2
              Name = "Dan"
              NickName = Some "D"
              Age = Some 30
              Books = books
              BookObjs = bookObjs
              BookIndex = -1
              BookSelection = None
              BookValue = None
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

type InpcControl<'a when 'a: equality>(initVal: 'a) =
    let mutable value = initVal
    let propChanged = Event<_,_>()

    member this.Value
        with get () = value
        and set v =
            if value <> v then
                value <- v
                propChanged.Trigger(this, PropertyChangedEventArgs("Value"))

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member this.PropertyChanged = propChanged.Publish

type FakeForm() =
    let ctx = BindingContext()
    let init (ctl: 'c when 'c :> Control) =
        ctl.BindingContext <- ctx
        ctl.CreateControl()
        ctl

    member val TextBox = new TextBox() |> init
    member val ListBox = new ListBox() |> init
    member val NumberBox = new NumberBox() |> init
    member val ComboBox = new ComboBox() |> init
    member val CustomTextControl = InpcControl("")
    member val CustomIntControl = InpcControl(Nullable<int>())

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
    binding.SetView (box startVal)
    use s = binding.ViewChanged.Subscribe (fun n -> fromView <- n :?> 'm)
    controlSet newVal cp
    match sourceUpdate with
    | OnChange -> fromView |> shouldEqual expectedVal
    | OnValidation -> fromView |> shouldEqual startVal
    validate cp.Control
    fromView |> shouldEqual expectedVal

let testViewInpcToModel (viewExpr: Expr<'v>) startVal (newVal: 'v) expectedVal binding =
    let cp = CommonBinding.controlPart viewExpr
    let mutable fromView = startVal
    binding.SetView (box startVal)
    use s = binding.ViewChanged.Subscribe (fun n -> fromView <- n :?> 'm)
    controlSet newVal cp
    fromView |> shouldEqual expectedVal

let testNonViewToModel (viewExpr: Expr<'v>) startVal (newVal: 'v) binding =
    let cp = CommonBinding.controlPart viewExpr
    let mutable fromView = startVal
    binding.SetView (box startVal)
    use s = binding.ViewChanged.Subscribe (fun n -> fromView <- n :?> 'm)
    controlSet newVal cp
    fromView |> shouldEqual startVal
    validate cp.Control
    fromView |> shouldEqual startVal

let testNonViewInpcToModel (viewExpr: Expr<'v>) startVal (newVal: 'v) binding =
    let cp = CommonBinding.controlPart viewExpr
    binding.SetView (box startVal)
    let mutable fromView = startVal
    use s = binding.ViewChanged.Subscribe (fun n -> fromView <- n :?> 'm)
    controlSet newVal cp
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

[<Test>]
let ``Bind matching properties two-way for custom control`` () =
    use form = new FakeForm()
    let viewExpr = <@ form.CustomTextControl.Value @>
    let binding = Bind.viewInpc(viewExpr).toModel(<@ model.Name @>)
    binding |> testModelToView viewExpr model.Name "Bob" "Bob"
    binding |> testViewInpcToModel viewExpr model.Name "Cat" "Cat"

[<TestCaseSource("sourceUpdateModes")>]
let ``Bind nullable to option two-way`` sourceUpdate =
    use form = new FakeForm()
    let viewExpr = <@ form.NumberBox.Value @>
    let binding = Bind.view(viewExpr).toModel(<@ model.Age @>, sourceUpdate)
    binding |> testModelToView viewExpr (Option.toNullable model.Age) (Some 31) (Nullable 31)
    binding |> testViewToModel sourceUpdate viewExpr model.Age (Nullable 32) (Some 32)

[<Test>]
let ``Bind nullable to option two-way for custom control`` () =
    use form = new FakeForm()
    let viewExpr = <@ form.CustomIntControl.Value @>
    let binding = Bind.viewInpc(viewExpr).toModel(<@ model.Age @>)
    binding |> testModelToView viewExpr (Option.toNullable model.Age) (Some 31) (Nullable 31)
    binding |> testViewInpcToModel viewExpr model.Age (Nullable 32) (Some 32)

[<TestCaseSource("sourceUpdateModes")>]
let ``Bind string to string option two-way`` sourceUpdate =
    use form = new FakeForm()
    let viewExpr = <@ form.TextBox.Text @>
    let binding = Bind.view(viewExpr).toModel(<@ model.NickName@>, sourceUpdate)
    binding |> testModelToView viewExpr "D" (None) ("")
    binding |> testViewToModel sourceUpdate viewExpr model.NickName (null) (None)

[<Test>]
let ``Bind string to string option two-way for custom control`` () =
    use form = new FakeForm()
    let viewExpr = <@ form.CustomTextControl.Value @>
    let binding = Bind.viewInpc(viewExpr).toModel(<@ model.NickName @>)
    binding |> testModelToView viewExpr "D" (Some "Chip Jiggins") ("Chip Jiggins")
    binding |> testViewInpcToModel viewExpr model.NickName (" ") (None)

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

[<Test>]
let ``Bind matching properties one way to model for custom control`` () =
    use form = new FakeForm()
    let viewExpr = <@ form.CustomTextControl.Value @>
    let binding = Bind.viewInpc(viewExpr).toModelOneWay(<@ model.Name @>)
    binding.ModelProperty |> shouldEqual Model.NameProperty
    binding |> testNonModelToView viewExpr "" "Cat"
    binding |> testViewInpcToModel viewExpr model.Name "Bob" "Bob"

[<TestCaseSource("sourceUpdateModes")>]
let ``Bind nullable to option one way to model`` sourceUpdate =
    use form = new FakeForm()
    let viewExpr = <@ form.NumberBox.Value @>
    let binding = Bind.view(viewExpr).toModelOneWay(<@ model.Age @>, sourceUpdate)
    binding |> testNonModelToView viewExpr (Nullable()) (Some 31)
    binding |> testViewToModel sourceUpdate viewExpr model.Age (Nullable 32) (Some 32)

[<Test>]
let ``Bind nullable to option one way to model for custom control`` () =
    use form = new FakeForm()
    let viewExpr = <@ form.CustomIntControl.Value @>
    let binding = Bind.viewInpc(viewExpr).toModelOneWay(<@ model.Age @>)
    binding |> testNonModelToView viewExpr (Nullable()) (Some 31)
    binding |> testViewInpcToModel viewExpr model.Age (Nullable 32) (Some 32)

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
let ``Bind matching properties one way to view for custom control`` () =
    use form = new FakeForm()
    let viewExpr = <@ form.CustomTextControl.Value @>
    let binding = Bind.model(<@ model.Name @>).toViewInpcOneWay(viewExpr)
    binding.ModelProperty |> shouldEqual Model.NameProperty
    binding |> testModelToView viewExpr model.Name "Bob" "Bob"
    binding |> testNonViewInpcToModel viewExpr model.Name "Cat"

[<Test>]
let ``Bind nullable to option one way to view`` () =
    use form = new FakeForm()
    let viewExpr = <@ form.NumberBox.Value @>
    let binding = Bind.model(<@ model.Age @>).toViewOneWay(viewExpr)
    binding |> testModelToView viewExpr (Option.toNullable model.Age) (Some 31) (Nullable 31)
    binding |> testNonViewToModel viewExpr model.Age (Nullable 32)

[<Test>]
let ``Bind nullable to option one way to view for custom control`` () =
    use form = new FakeForm()
    let viewExpr = <@ form.CustomIntControl.Value @>
    let binding = Bind.model(<@ model.Age @>).toViewInpcOneWay(viewExpr)
    binding |> testModelToView viewExpr (Option.toNullable model.Age) (Some 31) (Nullable 31)
    binding |> testNonViewInpcToModel viewExpr model.Age (Nullable 32)

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

// model to data source

[<Test>]
let ``Bind model to data source`` () =
    use form = new FakeForm()
    let getList () = form.ListBox.DataSource :?> Book seq |> Seq.toList
    let binding = Bind.model(<@ model.Books @>).toDataSource(form.ListBox, <@ fun b -> b.Id, b.Name @>)
    binding.ModelProperty |> shouldEqual Model.BooksProperty
    getList () |> shouldEqual model.Books
    form.ListBox.SelectedIndex <- 0
    form.ListBox.SelectedItem |> unbox |> shouldEqual model.Books.[0]
    form.ListBox.SelectedValue |> unbox |> shouldEqual model.Books.[0].Id
    form.ListBox.Text |> shouldEqual model.Books.[0].Name

    let newList = [ { Id = 99; Name = "Dependency Injection" } ]
    binding.SetView (box newList)
    getList () |> shouldEqual newList

// helper tests

[<Test>]
let ``getObjConverter for record option type handles nulls`` () =
    let converter = BindPartExtensions.getObjConverter<Book option> ()
    converter.ToSource null |> shouldEqual None
    converter.ToControl None |> shouldEqual null

type ListControls = ListType | ComboType

let listControls = [ ListType; ComboType ]

[<TestCaseSource("listControls")>]
let ``Model to view correctly updates SelectedIndex to -1`` controlType =
    use form = new FakeForm()
    let ctrl =
        match controlType with
        | ComboType -> form.ComboBox :> ListControl
        | ListType -> form.ListBox :> ListControl

    Bind.model(<@ model.Books @>).toDataSource(ctrl, <@ fun b -> b.Id, b.Name @>) |> ignore

    let viewExpr = <@ ctrl.SelectedIndex @>
    let binding = Bind.view(viewExpr).toModel(<@ model.BookIndex @>)
    binding |> testModelToView viewExpr model.BookIndex 1 1
    binding |> testModelToView viewExpr 1 -1 -1

[<Test>]
let ``Model to view correctly updates SelectedItem to null`` () =
    use form = new FakeForm()
    let ctrl = form.ComboBox

    Bind.model(<@ model.BookObjs @>).toDataSource(ctrl, <@ fun b -> b.Id, b.Name @>) |> ignore

    let viewExpr = <@ ctrl.SelectedItem @>
    let binding = Bind.view(viewExpr).toModel(<@ model.BookSelection @>)

    binding |> testModelToView viewExpr (model.BookSelection |> Option.toObj |> box) (Some bookObjs.[1]) (bookObjs.[1] |> box)
    binding |> testModelToView viewExpr (bookObjs.[1] |> box) None (null |> box)
    
[<TestCaseSource("listControls")>]
let ``Model to view correctly updates SelectedValue to null`` controlType =
    use form = new FakeForm()
    let ctrl =
        match controlType with
        | ComboType -> form.ComboBox :> ListControl
        | ListType -> form.ListBox :> ListControl

    Bind.model(<@ model.Books @>).toDataSource(ctrl, <@ fun b -> b.Id, b.Name @>) |> ignore

    let viewExpr = <@ ctrl.SelectedValue @>
    let binding = Bind.view(viewExpr).toModel(<@ model.BookValue @>)

    binding |> testModelToView viewExpr (model.BookValue |> Option.toNullable |> box) (Some bookObjs.[1].Id) (bookObjs.[1].Id |> box)
    binding |> testModelToView viewExpr (bookObjs.[1].Id |> box) None (null |> box)
