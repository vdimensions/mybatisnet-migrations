﻿namespace MyBatis.Migrations

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
    class end

type ScriptRunner = 

    member this.RunScript (script : TextReader) =
        ignore()

    member this.Dispose (disposing : bool) =
        ()

    interface IDisposable with member this.Dispose() = this.Dispose(true)

module MigrationHook =

    [<Literal>]
    let HOOK_CONTEXT : string = "hookContext"

module Database =
    let changelogExists (cp : ConnectionProvider) (opt : DatabaseOperationOption) : bool =
        false

    let getChangelog (cp : ConnectionProvider) (opt : DatabaseOperationOption) : List<_> =
        List.empty

    let insertChangelog (cp : ConnectionProvider) (opt : DatabaseOperationOption) (change : Change) =
        ignore()

    let getScriptRunner (cp : ConnectionProvider) (opt : DatabaseOperationOption) (out : TextWriter) =
        Unchecked.defaultof<ScriptRunner>

    let checkSkippedOrMissing (changesInDb : List<_>) (migrations : Change list) (out : TextWriter) : unit =
        ignore()

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