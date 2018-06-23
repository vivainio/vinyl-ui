module ContactManager

open System.Windows
open VinylUI
open VinylUI.Wpf
open Domain

type Model = {
    Contacts: Contact list
    SelectedContact: Contact option
}

type Events =
    | SelectContact of Contact option
    | CreateContact
    | EditContact of Contact
    | DeleteContact of Contact

type View = FsXaml.XAML<"ContactManagerWindow.xaml">

let binder (view: View) model =
    view.ContactGrid.AddFSharpConverterToColumns()

    [ Bind.model(<@ model.Contacts @>).toViewOneWay(<@ view.ContactGrid.ItemsSource @>)
      Bind.model(<@ model.SelectedContact @>).toFunc(fun sel ->
        match sel with
        | Some c ->
            view.NameDisplay.Text <- sprintf "%s %s" c.FirstName c.LastName
            view.NumbersDisplay.Text <- c.Numbers |> List.map string |> String.concat "\n"
        | None ->
            view.NameDisplay.Text <- ""
            view.NumbersDisplay.Text <- ""
      )
    ]

let events (view: View) =
    let selected _ = view.ContactGrid.SelectedItem |> Option.ofObj |> Option.map unbox
    [ view.ContactGrid.SelectionChanged |> Observable.map (selected >> SelectContact)
      view.CreateButton.Click |> Observable.mapTo CreateContact
      view.EditButton.Click |> Observable.choose (selected >> Option.map EditContact)
      view.ContactGrid.MouseDoubleClick |> Observable.choose (selected >> Option.map EditContact)
      view.DeleteButton.Click |> Observable.choose (selected >> Option.map DeleteContact)
    ]

let edit (editContact: Contact option -> Contact option) contact model =
    match editContact contact with
    | Some newContact ->
        { model with
            Contacts =
                model.Contacts
                |> List.except (contact |> Option.toList)
                |> List.append [newContact]
                |> List.sortBy (fun c -> c.FirstName, c.LastName)
        }
    | None -> model

let delete (contact: Contact) model =
    let confirmed =
        MessageBox.Show(sprintf "Are you sure you want to delete %s from your contact list?" contact.FullName,
                        "Delete Contact?",
                        MessageBoxButton.YesNo) = MessageBoxResult.Yes
    if confirmed then
        { model with Contacts = model.Contacts |> List.except [contact] }
    else model

let dispatcher editContact = function
    | SelectContact c -> Sync (fun m -> { m with SelectedContact = c })
    | CreateContact -> Sync (edit editContact None)
    | EditContact c -> Sync (edit editContact (Some c))
    | DeleteContact c -> Sync (delete c)

let start editContact contacts (view: View) =
    let model = {
        Contacts = contacts
        SelectedContact = None
    }
    Framework.start binder events (dispatcher editContact) view model
