﻿namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Fredis")>]
[<assembly: AssemblyProductAttribute("Fredis")>]
[<assembly: AssemblyDescriptionAttribute("F# Redis client")>]
[<assembly: AssemblyVersionAttribute("0.0.3")>]
[<assembly: AssemblyFileVersionAttribute("0.0.3")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.0.3"
