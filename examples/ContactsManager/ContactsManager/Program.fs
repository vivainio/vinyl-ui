open System.Windows
open VinylUI.Wpf
open Domain

let exampleContacts = [
    { FirstName = "Alice"
      LastName = "Henderson"
      Group = Some "Coworkers"
      Numbers =
        [ { Number = "5553331234"; Type = Work }
          { Number = "5551236789"; Type = Mobile }
          { Number = "5553339999"; Type = Fax }
        ]
      Notes = "Project Manager"
    }
    { FirstName = "Brett"
      LastName = "Foreman"
      Group = Some "Coworkers"
      Numbers =
        [ { Number = "5553451111"; Type = Mobile }
          { Number = "5553339876"; Type = Work }
        ]
      Notes = "Server Admin"
    }
    { FirstName = "Chuck"
      LastName = "North"
      Group = Some "Friends"
      Numbers =
        [ { Number = "2345558899"; Type = Mobile }
        ]
      Notes = "Met at the local festival in 2012"
    }
]

[<EntryPoint>]
[<System.STAThread>]
let main args = 
    let app = Application()
    let editContact c = ContactEdit.View().ShowDialog(ContactEdit.start c).Result
    app.Run(ContactManager.View(), ContactManager.start editContact exampleContacts) |> ignore
    0
