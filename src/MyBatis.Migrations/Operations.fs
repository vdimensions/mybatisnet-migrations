namespace MyBatis.Migrations

open System
open System.IO


type Operation =
    | Bootstrap of forced : bool
    | Up of steps : int
    | Down of steps : int
    | Version of version : decimal
    | Status
    | Pending

type OperationResult =
    | BootstrapComplete
    | BoostrapSkipped
    | MigrationResult
    | Error
    | VersionResult
    | StatusResult of pending : int * applied : int * changes : Change list
    | PendingResult

module Operations =

    let rec private resolveEx (e : exn) : exn =
        match e with
        | :? MigrationException as me -> resolveEx me.InnerException
        | _ -> e

    let private boostrap(out : TextWriter) (opt : DatabaseOperationOption) (cp : ConnectionProvider) (ml : MigrationsLoader) (hook : MigrationHook option) (forced : bool) : OperationResult =
        if (Database.changelogExists cp opt && not forced) then
            out.WriteLine("For your safety, the bootstrap SQL script will only run before migrations are applied (i.e. before the changelog exists).  If you're certain, you can run it using the --force option.")
            BoostrapSkipped
        else
            match ml.GetBootstrapReader() with
            | Some bootstrapReader ->
                out.WriteLine((Util.horizontalLine "Applying: bootstrap.sql" 80))
                use runner = Database.getScriptRunner cp opt out
                runner.RunScript(bootstrapReader)
                out.WriteLine()
                BootstrapComplete
            | None ->
                out.WriteLine("Error, could not run bootstrap.sql. The file does not exist.")
                Error

    let private upDown (out : TextWriter) (opt : DatabaseOperationOption) (cp : ConnectionProvider) (ml : MigrationsLoader) (hook : MigrationHook option) (steps : int) (isDown : bool) : OperationResult =
        try
            let changesInDb =
                if (Database.changelogExists cp opt) 
                then Database.getChangelog cp opt
                else List.empty
            if List.isEmpty changesInDb && isDown then
                out.WriteLine("Changelog exist, but no migration found.")
            else
                let migrations = ml.GetMigrations()
                //
                // NB: migrations are expected to be sorted starting from the most recent
                // This will nicely fit with how F# lists work
                //
                //Collections.sort(migrations);
                //                
                //if isDown then Collections.reverse(migrations)
                //
                Database.checkSkippedOrMissing changesInDb migrations out
                let mutable hookBindings = Map.empty<String, HookContext>
                let mutable changes = migrations
                use runner = Database.getScriptRunner cp opt out

                let rec traverseChanges (ml : MigrationsLoader) (cp : ConnectionProvider) (acc : int) (persistedChanges : Change list) (changes : Change list) =
                    match changes with
                    | [] -> acc
                    | head::tail ->
                        if (List.isEmpty changesInDb || (head < (List.last persistedChanges))) then
                            let mutable br = false
                            match hook with
                            | Some h ->
                                hookBindings <- hookBindings |> Map.add MigrationHook.HOOK_CONTEXT (HookContext(cp, runner, head.Clone() |> Some))
                                h.BeforeEach hookBindings
                            | None -> ignore()

                            use scriptReader = ml.GetScriptReader head isDown
                            if (isDown) then
                                out.WriteLine((Util.horizontalLine ("Undoing: " + head.Filename) 80))
                                runner.RunScript scriptReader
                                br <- 
                                    if (Database.changelogExists cp opt) then
                                        Database.deleteChange cp opt head
                                        false
                                    else
                                        out.WriteLine("Changelog doesn't exist. No further migrations will be undone (normal for the last migration).")
                                        true
                            else
                                out.WriteLine((Util.horizontalLine ("Applying: " + head.Filename) 80))
                                runner.RunScript scriptReader
                                Database.insertChangelog cp opt head
                            out.WriteLine()

                            match hook with
                            | Some h ->
                                hookBindings <-  hookBindings |> Map.add MigrationHook.HOOK_CONTEXT (HookContext(cp, runner, head.Clone() |> Some))
                                h.AfterEach hookBindings
                            | None -> ignore()

                            if not br && steps > 0 && acc < steps then
                                if isDown then
                                    traverseChanges ml cp (acc + 1) (persistedChanges |> List.except [head]) tail
                                else
                                    traverseChanges ml cp (acc + 1) persistedChanges tail
                            else acc + 1
                        else acc
                try 
                    match hook with
                    | Some h ->
                        hookBindings <- hookBindings |> Map.add MigrationHook.HOOK_CONTEXT (HookContext(cp, runner, None))
                        h.Before hookBindings
                        if traverseChanges ml cp 0 changesInDb changes > 0 then
                            hookBindings <-  hookBindings |> Map.add MigrationHook.HOOK_CONTEXT (HookContext(cp, runner, None))
                            h.After hookBindings
                    | _ -> traverseChanges ml cp 0 changesInDb changes |> ignore
                // TODO: requires MyBatis.Common
                //with :? RuntimeSqlException as e
                //    use onAbortScriptReader = ml.GetOnAbortReader()
                //    out.WriteLine()
                //    out.WriteLine((Util.horizontalLine "Executing onabort.sql script." 80))
                //    runner.runScript onAbortScriptReader
                //    out.WriteLine()
                //    raise <| e
                finally 
                    ignore()
            with e ->
                let e1 = resolveEx (e)
                raise <| MigrationException("Error executing command. Cause: " + e1.Message, e1)
        MigrationResult

    let private version (out : TextWriter) (opt : DatabaseOperationOption) (cp : ConnectionProvider) (ml : MigrationsLoader) (hook : MigrationHook option) (id : decimal) =
        VersionResult

    let private status (out : TextWriter) (opt : DatabaseOperationOption) (cp : ConnectionProvider) (ml : MigrationsLoader) (hook : MigrationHook option) : OperationResult =
        out.WriteLine("ID             Applied At          Description")
        out.WriteLine((Util.horizontalLine ""  80))
        let migrations = ml.GetMigrations()
        let mutable (pending, applied, changes) = (0, 0, System.Collections.Generic.List<Change>()) 
        if (Database.changelogExists cp opt) then
            let changelog = Database.getChangelog cp opt
            for change in migrations do
                match changelog |> List.tryFind (fun e -> e.Equals(change))  with
                | Some c ->
                    changes.Add(c)
                    applied <- applied + 1
                | None ->
                    changes.Add(change)
                    pending <- pending + 1
        else
            changes.AddRange(migrations)
            pending <- List.length migrations
        //Collections.sort(changes);
        for change in changes do
            out.WriteLine(change.ToString())
        out.WriteLine()
        StatusResult (pending, applied, changes |> List.ofSeq)

    let private pending (out : TextWriter) (opt : DatabaseOperationOption) (cp : ConnectionProvider) (ml : MigrationsLoader) (hook : MigrationHook option) =
        try
            if not (Database.changelogExists(cp opt)) then
                raise <| MigrationException("Change log doesn't exist, no migrations applied.  Try running 'up' instead.")
            let pending = getPendingChanges(connectionProvider, migrationsLoader, option)
            let stepCount = 0
            let hookBindings = System.Collections.Generic.Dictionary<String, Object>()
            out.WriteLine("WARNING: Running pending migrations out of order can create unexpected results.")
            use runner = Database.getScriptRunner(cp, opt, out)
            let scriptReader: Reader = null
            try
                for change in pending do
                    if (stepCount == 0 && hook != null) then
                        hookBindings.Add(MigrationHook.HOOK_CONTEXT, HookContext(connectionProvider, runner, null))
                        hook.Before hookBindings
                    if (hook != null) then
                        hookBindings.Add(MigrationHook.HOOK_CONTEXT, HookContext(connectionProvider, runner, change.clone()))
                        hook.BeforeEach hookBindings
                    out.WriteLine (Util.horizontalLine ("Applying: " + change.getFilename()) 80)
                    use scriptReader = migrationsLoader.getScriptReader(change, false)
                    runner.runScript(scriptReader)
                    Database.insertChangelog cp opt change
                    println(printStream)
                    if (hook != null) then
                        hookBindings.put(MigrationHook.HOOK_CONTEXT, new HookContext(connectionProvider, runner, change.clone()))
                        hook.afterEach(hookBindings)
                    stepCount++
                if (stepCount > 0 && hook != null) then
                    hookBindings.put(MigrationHook.HOOK_CONTEXT, new HookContext(connectionProvider, runner, null))
                    hook.after(hookBindings)
            with e ->
                raise <| MigrationException("Error executing command.  Cause: " + e, e)
        with e ->
            let e1 = resolveEx e
            raise <| MigrationException("Error executing command.  Cause: " + e1.Message, e1)
        PendingResult

    let exec (out : TextWriter) (opt : DatabaseOperationOption) (cp : ConnectionProvider) (ml : MigrationsLoader) (hook : MigrationHook option) (op : Operation) : OperationResult =
        match op with
        | Bootstrap forced -> boostrap out opt cp ml hook forced
        | Up steps -> upDown out opt cp ml hook steps false
        | Down steps -> upDown out opt cp ml hook steps true
        | Version id -> version out opt cp ml hook id
        | Status -> status out opt cp ml hook
        | Pending -> pending out opt cp ml hook
