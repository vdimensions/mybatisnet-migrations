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

    let upDown (out : TextWriter) (opt : DatabaseOperationOption) (cp : ConnectionProvider) (ml : MigrationsLoader) (hook : MigrationHook option) (steps : int) (isDown : bool) : unit =
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
                                //Database.insertChangelog cp opt change
                                if (Database.changelogExists cp opt) then
                                    Database.deleteChange cp opt head
                                else
                                    out.WriteLine("Changelog doesn't exist. No further migrations will be undone (normal for the last migration).")
                                    br <- true
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
                let rec resolveEx (e : exn) : exn =
                    match e with
                    | :? MigrationException as me -> resolveEx me.InnerException
                    | _ -> e
                let e1 = resolveEx (e)
                raise <| MigrationException("Error executing command. Cause: " + e1.Message, e1)

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
            upDown out opt cp ml hook steps false

        | Down steps ->
            upDown out opt cp ml hook steps true
            //List<Change> changesInDb = Collections.emptyList();
            //if (changelogExists(connectionProvider, option)) {
            //  changesInDb = getChangelog(connectionProvider, option);
            //}
            //if (changesInDb.isEmpty()) {
            //  println(printStream, "Changelog exist, but no migration found.");
            //} else {
            //  List<Change> migrations = migrationsLoader.getMigrations();
            //  Collections.sort(migrations);
            //  checkSkippedOrMissing(changesInDb, migrations, printStream);
            //  Collections.reverse(migrations);
            //  int stepCount = 0;
            //  ScriptRunner runner = getScriptRunner(connectionProvider, option, printStream);
            //
            //  Map<String, Object> hookBindings = new HashMap<String, Object>();
            //---------------------------
            //
            //  try {
            //    for (Change change : migrations) {
            //      if (change.equals(changesInDb.get(changesInDb.size() - 1))) {
            //        if (stepCount == 0 && hook != null) {
            //          hookBindings.put(MigrationHook.HOOK_CONTEXT, new HookContext(connectionProvider, runner, null));
            //          hook.before(hookBindings);
            //        }
            //        if (hook != null) {
            //          hookBindings.put(MigrationHook.HOOK_CONTEXT,
            //              new HookContext(connectionProvider, runner, change.clone()));
            //          hook.beforeEach(hookBindings);
            //        }
            //        println(printStream, Util.horizontalLine("Undoing: " + change.getFilename(), 80));
            //        runner.runScript(migrationsLoader.getScriptReader(change, true));
            //        if (changelogExists(connectionProvider, option)) {
            //          deleteChange(connectionProvider, change, option);
            //        } else {
            //          println(printStream,
            //              "Changelog doesn't exist. No further migrations will be undone (normal for the last migration).");
            //          stepCount = steps;
            //        }
            //        println(printStream);
            //        if (hook != null) {
            //          hookBindings.put(MigrationHook.HOOK_CONTEXT,
            //              new HookContext(connectionProvider, runner, change.clone()));
            //          hook.afterEach(hookBindings);
            //        }
            //        stepCount++;
            //        if (steps == null || stepCount >= steps) {
            //          break;
            //        }
            //        changesInDb.remove(changesInDb.size() - 1);
            //      }
            //    }
            //    if (stepCount > 0 && hook != null) {
            //      hookBindings.put(MigrationHook.HOOK_CONTEXT, new HookContext(connectionProvider, runner, null));
            //      hook.after(hookBindings);
            //    }
            //  } finally {
            //    runner.closeConnection();
            //  }
            //}

        | Version id ->
            ignore()

        | Status ->
            ignore()

        | Pending ->
            ignore()
