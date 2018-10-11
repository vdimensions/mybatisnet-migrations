//   Apache Notice
//
//   Copyright 2010-2018 the original author or authors.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//
namespace MyBatis.Migrations

open System


type [<Interface>] ISimpleScript =
    abstract member Script : string with get

type [<Interface>] IBootstrapScript =
    inherit ISimpleScript

type [<Interface>] IOnAbortScript =
    inherit ISimpleScript

type [<Interface>] IMigrationScript =
    /// <summary>
    /// Gets the ID of this migration script
    /// <para>
    /// Newer scripts should have a larger ID number.
    /// </para>
    /// </summary>
    abstract member ID : decimal with get

    /// <summary>
    /// Gets a short description of the current migration script.
    /// </summary>
    abstract member Description : string with get

    /// <summary>
    /// Gets the SQL statement(s) executed at runtime schema upgrade.
    /// </summary>
    abstract member UpScript : string with get

    /// <summary>
    /// Gets the SQL statement(s) executed at runtime schema downgrade.
    /// </summary>
    abstract member DownScript : string with get
