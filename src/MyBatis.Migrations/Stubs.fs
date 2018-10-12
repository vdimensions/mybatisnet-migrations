namespace MyBatis.Migrations

open System
open System.IO

type ConnectionProvider = class end

type MigrationsLoader = 
    member this.GetBootstrapReader() : TextReader option =
        None

    member this.GetScriptReader(change : Change) (_ : bool) : TextReader =
        Unchecked.defaultof<TextReader>

    member this.GetOnAbortReader(change : Change) (_ : bool) : TextReader =
        Unchecked.defaultof<TextReader>

    member this.GetMigrations() : Change list =
        List.empty

type DatabaseOperationOption = 
    member this.ChangelogTable
        with get() : string =
            ""

type ScriptRunner = 

    member this.RunScript (script : TextReader) =
        ignore()

    member this.Dispose (disposing : bool) =
        ()

    interface IDisposable with member this.Dispose() = this.Dispose(true)

module MigrationHook =

    [<Literal>]
    let HOOK_CONTEXT : string = "hookContext"

type SqlRunner =
    member this.Delete(query : string, changeID : decimal) =
        ignore()

    member this.Dispose(disposing : bool) =
        ignore()

    interface IDisposable with member this.Dispose() = this.Dispose(true)



#if NETSTANDARD2_0_OR_NEWER || !NETSTANDARD
[<Serializable>]
#endif
type MigrationException =
    inherit Exception
    new (message : string, inner : exn) = { inherit Exception(message, inner) }
    new (message : string) = { inherit Exception(message) }
    #if NETSTANDARD2_0_OR_NEWER || !NETSTANDARD
    new (si : System.Runtime.Serialization.SerializationInfo, ctx : System.Runtime.Serialization.StreamingContext) = { inherit Exception(si, ctx) }
    #endif

module Database =
    let changelogExists (cp : ConnectionProvider) (opt : DatabaseOperationOption) : bool =
        false

    let getChangelog (cp : ConnectionProvider) (opt : DatabaseOperationOption) : _ list =
        List.empty

    let insertChangelog (cp : ConnectionProvider) (opt : DatabaseOperationOption) (change : Change) =
        ignore()

    let getSqlRunner (cp : ConnectionProvider) : SqlRunner =
        Unchecked.defaultof<SqlRunner>

    let deleteChange(cp : ConnectionProvider) (opt : DatabaseOperationOption) (change : Change) : unit =
        use runner = getSqlRunner cp
        try runner.Delete("delete from " + opt.ChangelogTable + " where ID = ?", change.ID)
        //TODO:
        //with | :? SQLException as e ->
        with e ->
            raise <| MigrationException("Error querying last applied migration.  Cause: " + e.Message, e)

    let getScriptRunner (cp : ConnectionProvider) (opt : DatabaseOperationOption) (out : TextWriter) =
        Unchecked.defaultof<ScriptRunner>

    let checkSkippedOrMissing (changesInDb : Change list) (migrations : Change list) (out : TextWriter) : unit =
        ignore()

    let removeLast (changesInDb : Change list) : Change list =
        let changeToRemove = List.last changesInDb
        // TODO: remove actually
        changesInDb |> List.except [changeToRemove]

module Util =
    let horizontalLine (str : string) (n : int) : string =
        ""


type HookContext(cp : ConnectionProvider, runner : ScriptRunner, change : Change option) =
    class end

type MigrationHook =
    member this.Before (hookBindings : Map<String, HookContext>) =
        ignore()

    member this.BeforeEach (hookBindings : Map<String, HookContext>) =
        ignore()

    member this.After (hookBindings : Map<String, HookContext>) =
        ignore()

    member this.AfterEach (hookBindings : Map<String, HookContext>) =
        ignore()