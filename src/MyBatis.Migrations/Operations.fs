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

module Operations =

    let operate (out : TextWriter) (opt : DatabaseOperationOption) (cp : ConnectionProvider) (ml : MigrationsLoader) (hook : MigrationHook option) (op : Operation) : unit =
        match op with
        | Bootstrap forced ->
            if (Database.changelogExists cp opt && not forced) then
                out.WriteLine("For your safety, the bootstrap SQL script will only run before migrations are applied (i.e. before the changelog exists).  If you're certain, you can run it using the --force option.")
            else
                match ml.GetBootstrapReader() with
                | Some bootstrapReader ->
                    out.WriteLine((Util.horizontalLine "Applying: bootstrap.sql" 80))
                    use runner = Database.getScriptRunner cp opt out
                    runner.RunScript(bootstrapReader)
                    out.WriteLine()
                | None ->
                    out.WriteLine("Error, could not run bootstrap.sql. The file does not exist.")

        | Up steps ->
            try
                let changesInDb =
                    if (Database.changelogExists cp opt) 
                    then Database.getChangelog cp opt
                    else List.empty              
                let migrations = ml.GetMigrations()
                //Collections.sort(migrations);
                Database.checkSkippedOrMissing changesInDb migrations out
                let mutable stepCount = 0
                let mutable hookBindings = Map.empty<String, HookContext>
                use runner = Database.getScriptRunner cp opt out
                let mutable changes = migrations
                try 
                    for _ in [0 .. steps-1] do
                        let change = changes.Head
                        changes <- changes.Tail
                        if (List.isEmpty changesInDb || (change > (List.last changesInDb))) then
                            match hook with
                            | Some hook ->
                                if stepCount = 0 then
                                    hookBindings <- hookBindings |> Map.add MigrationHook.HOOK_CONTEXT (HookContext(cp, runner, None))
                                    hook.Before hookBindings
                                hookBindings <- hookBindings |> Map.add MigrationHook.HOOK_CONTEXT (HookContext(cp, runner, change.Clone() |> Some))
                                hook.BeforeEach hookBindings
                            | None -> 
                                ignore()
                            
                            out.WriteLine((Util.horizontalLine ("Applying: " + change.Filename) 80))
                            use scriptReader = ml.GetScriptReader change false
                            runner.RunScript scriptReader
                            Database.insertChangelog cp opt change
                            out.WriteLine()

                            match hook with
                            | Some hook ->
                                hookBindings <-  hookBindings |> Map.add MigrationHook.HOOK_CONTEXT (HookContext(cp, runner, change.Clone() |> Some))
                                hook.AfterEach hookBindings
                            stepCount <- stepCount + 1

                    match hook with
                    | Some hook when stepCount > 0 ->
                        hookBindings <-  hookBindings |> Map.add MigrationHook.HOOK_CONTEXT (HookContext(cp, runner, None))
                        hook.After hookBindings
                    | _ -> ignore()

                // TODO: requires MyBatis.Common
                //with :? RuntimeSqlException as e
                //    use onAbortScriptReader = ml.GetOnAbortReader()
                //    out.WriteLine()
                //    out.WriteLine((Util.horizontalLine "Executing onabort.sql script." 80));
                //    runner.runScript onAbortScriptReader
                //    out.WriteLine()
                //    raise <| e
                finally 
                    ignore()
            with e ->
                let rec resolveEx (e : exn) : exn =
                    match e with
                    | :? MigrationException as me -> resolveEx me.InnerException
                    | _ -> e
                let e1 = resolveEx (e)
                raise <| MigrationException("Error executing command. Cause: " + e1.Message, e1);
