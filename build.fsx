// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------
#I "packages/FAKE/tools"
#r "NuGet.Core.dll"
#r "FakeLib.dll"
open System
open System.IO
open Fake 
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open Fake.Testing

// --------------------------------------------------------------------------------------
// Provide project-specific details below
// --------------------------------------------------------------------------------------

// Information about the project are used
//  - for version and project name in generated AssemblyInfo file
//  - by the generated NuGet package 
//  - to run tests and to publish documentation on GitHub gh-pages
//  - for documentation, you also need to edit info in "docs/tools/generate.fsx"

// The name of the project 
// (used by attributes in AssemblyInfo)
let project = "c4fsharp"

// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "Community for F# web application"

// Longer description of the project
// (used as a description for NuGet package; line breaks are automatically cleaned up)
let description = "Community for F# web application"
// List of author names (for NuGet package)
let authors = [ "Ryan Riley" ]
// Tags for your project (for NuGet package)
let tags = "F# fsharp web typescript webapi"

// File system information 
// (<solutionFile>.sln is built during the building process)
let solutionFile = "c4fsharp.sln"

// Pattern specifying assemblies to be tested using NUnit
let testAssemblies = "tests/**/bin/Release/*Tests*.dll"

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted 
let gitHome = "https://github.com/c4fsharp"
// The name of the project on GitHub
let gitName = "c4fsharp"
let gitRaw = environVarOrDefault "gitRaw" "https://raw.github.com/c4fsharp"

// --------------------------------------------------------------------------------------
// The rest of the file includes standard build steps 
// --------------------------------------------------------------------------------------

// Read additional information from the release notes document
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
let (!!) includes = (!! includes).SetBaseDirectory __SOURCE_DIRECTORY__
let release = parseReleaseNotes (IO.File.ReadAllLines "RELEASE_NOTES.md")
let isAppVeyorBuild = environVar "APPVEYOR" <> null
let nugetVersion = 
    if isAppVeyorBuild then
        let isTagged = Boolean.Parse(environVar "APPVEYOR_REPO_TAG")
        if isTagged then
            environVar "APPVEYOR_REPO_TAG_NAME"
        else
            sprintf "%s-b%03i" release.NugetVersion (int buildVersion)
    else release.NugetVersion

let fsharpAssemblyInfo proj =
    CreateFSharpAssemblyInfo (sprintf "%s/AssemblyInfo.fs" proj)
        [ Attribute.Title proj
          Attribute.Product proj
          Attribute.Description summary
          Attribute.Version release.AssemblyVersion
          Attribute.FileVersion release.AssemblyVersion ]

// Generate assembly info files with the right version & up-to-date information
Target "AssemblyInfo" <| fun _ ->
    fsharpAssemblyInfo "c4fsharp"

Target "BuildVersion" <| fun _ ->
    Shell.Exec("appveyor", sprintf "UpdateBuild -Version \"%s\"" nugetVersion) |> ignore

// --------------------------------------------------------------------------------------
// Clean build results & restore NuGet packages

Target "Clean" <| fun _ ->
    CleanDirs ["bin"]

// --------------------------------------------------------------------------------------
// Build library & test project

Target "Build" <| fun _ ->
    !!solutionFile
    |> MSBuildRelease "" "Rebuild"
    |> Log "AppBuild-Output: " 

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner

Target "RunTests" <| fun _ ->
    !! testAssemblies
    |> xUnit2 (fun p ->
        { p with
            TimeOut = TimeSpan.FromMinutes 20.
            XmlOutputPath = Some "bin/TestResults.xml" })

#if MONO
#else
// --------------------------------------------------------------------------------------
// SourceLink allows Source Indexing on the PDB generated by the compiler, this allows
// the ability to step through the source code of external libraries https://github.com/ctaggart/SourceLink
#load "packages/SourceLink.Fake/tools/SourceLink.fsx"
open SourceLink

Target "SourceLink" <| fun _ ->
    let baseUrl = sprintf "%s/%s/{0}/%%var2%%" gitRaw project
    !! "c4fsharp/*.??proj"
    |> Seq.iter (fun projFile ->
        let proj = VsProj.LoadRelease projFile 
        SourceLink.Index proj.CompilesNotLinked proj.OutputFilePdb __SOURCE_DIRECTORY__ baseUrl)
#endif

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target "All" DoNothing

"Clean"
  =?> ("BuildVersion", isAppVeyorBuild)
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "RunTests"
#if MONO
#else
  =?> ("SourceLink", Pdbstr.tryFind().IsSome )
#endif
  ==> "All"

RunTargetOrDefault "All"

