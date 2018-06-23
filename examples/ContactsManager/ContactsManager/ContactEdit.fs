module ContactEdit

open System.Windows
open VinylUI
open VinylUI.Wpf
open Domain

type Model = {
    FirstName: string option
    LastName: string option
    Groups: string list
    GroupEntry: string option
    Numbers: ContactNumber list
    Notes: string
    Result: Contact option
}

type Events =
    | Save
    | Cancel

type View = FsXaml.XAML<"ContactEditWindow.xaml">

let binder (view: View) model =
    [ Bind.view(<@ view.FirstNameBox.Text @>).toModel(<@ model.FirstName @>)
      Bind.view(<@ view.LastNameBox.Text @>).toModel(<@ model.LastName @>)
      Bind.view(<@ view.NotesBox.Text @>).toModel(<@ model.Notes @>)
    ]

let events (view: View) =
    [ view.SaveButton.Click |> Observable.mapTo Save
      view.CancelButton.Click |> Observable.mapTo Cancel
    ]

let save close model =
    close ()
    { model with
        Result = Some
            { FirstName = model.FirstName |> Option.defaultValue ""
              LastName = model.LastName |> Option.defaultValue ""
              Group = None
              Numbers = []
              Notes = model.Notes
            }
    }

let dispatcher (close: unit -> unit) = function
    | Save -> Sync (save close)
    | Cancel -> Sync (fun m -> close(); m)

let start (contact: Contact option) (view: View) =
    let model =
        match contact with
        | Some c ->
            { FirstName = Some c.FirstName
              LastName = Some c.LastName
              Groups = []
              GroupEntry = None
              Numbers = c.Numbers
              Notes = c.Notes
              Result = None
            }
        | None ->
            { FirstName = None
              LastName = None
              Groups = []
              GroupEntry = None
              Numbers = []
              Notes = ""
              Result = None
            }
    Framework.start binder events (dispatcher view.Close) view model
