namespace MyBatis.Migrations

open System.IO


type ConnectionProvider = class end
type MigrationsLoader = class end
type DatabaseOperationOption = class end

module Database =
    let changelogExists () : bool =
        false

type Operation =
    | Bootstrap of forced : bool
    | Up of steps : int
    | Down of steps : int
    | Version of version : decimal
    | Status
    | Pending

module Operations =

    let operate (cp : ConnectionProvider) (ml : MigrationsLoader) (opt : DatabaseOperationOption) (out : TextWriter) (op : Operation) : unit =
        match op with
        | Bootstrap forced ->
            if (Database.changelogExists() && not forced) then
                out.WriteLine("For your safety, the bootstrap SQL script will only run before migrations are applied (i.e. before the changelog exists).  If you're certain, you can run it using the --force option.")
            else
                ignore()





