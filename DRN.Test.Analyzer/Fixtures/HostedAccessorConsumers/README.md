# Hosted accessor consumer fixtures

These compile-only consumers exercise distribution outside the repository's shared build imports. They are deliberately outside the solution and analyzer test compilation. Their local `Directory.Build.props` and `Directory.Build.targets` stop discovery of the repository build files.

| Consumer | Distribution contract |
|---|---|
| DirectPackage | Hosting package supplies its generator and Razor input wiring directly. |
| TransitivePackage | PackageBridge references Hosting with private analyzer assets. Hosting's buildTransitive asset must still reach the Web host. |
| ExternalSource | SourceBridge references Hosting source. The Web host receives the generator through the documented explicit common-root import. |

Every Web consumer compiles the same ordinary controller, linked Razor page and references to both generated roots. `AppProgram` activates generation by convention without an assembly opt-in. Missing generator activation, missing page inputs or incorrect logical page paths must fail compilation. The bridge projects use the non-Web SDK and must not emit accessor roots.

Package consumers require `DrnHostingPackageVersion` set to the locally produced package version under test. Supply a package source containing that Hosting package and its matching DRN dependency packages. Use an isolated package cache so an existing published package cannot substitute for the local artifact. The fixtures intentionally have no fallback version.

The source consumer requires `DrnSourceRoot` set to the absolute repository root. Its common target explicitly imports Hosting's target for the Web host. Copy this fixture directory outside the repository when validating a truly external checkout. The local build files already isolate the nested fixture from ancestor imports.

Acceptance requires successful compilation of all three Web consumers and inspection that their generated endpoint and page roots come from the expected generator. Packaged contents and Rider compiler-worker loading are separate checks. No fixture builds, restores, package operations or IDE verification were performed when these fixtures were authored. Execution remains subject to repository and user authorization.

## SourceKnown convention distribution

Each consumer directory also contains a convention-named sibling project: `Consumer.Hosted.DirectPackage.csproj`, `Consumer.Hosted.TransitivePackage.csproj` or `Consumer.Hosted.ExternalSource.csproj`. Select that exact file to exercise SharedKernel conventions without an activation or identity property. The Hosted token identifies the role even though it is not the final segment. Standard `RootNamespace=Consumer` preserves the shared source namespace. Each sibling has an isolated intermediate-output directory.

The sibling's shared linked `QA/ConventionProbe.cs` deliberately declares a Dto in Hosted and must produce DRNCV016. It must not produce DRNCV001 for missing compiler inputs or DRNCV003 for a physical linked-file path. These minimal distribution fixtures deliberately omit Application and Infra projects, so DRNCV021 must also report the two missing direct layer references. Bridge projects and the original nonstandard host names remain outside application convention analysis. The external source fixtures add the explicit SharedKernel common-root import.

With `DrnConventionFixtureSuppress=true`, the exact probe type carries a reasoned standard suppression. DRNCV016 must disappear without DRNCV018; required-reference diagnostics remain independent. Compare the conventional sibling, its suppressed-probe state and the original nonstandard project across direct package, transitive package and external source consumers. Accessor generation must remain active in every state. Standard `.editorconfig` diagnostic severity settings can control intentional exceptions. Generated convention data and basic project metadata remain available for C# projects regardless of role recognition. Inspect their contents and count analyzer loads. Success requires actual diagnostic output, not merely successful compilation with warnings enabled.

These fixture extensions are written but unexecuted. Package extraction, excluded buildTransitive inputs, actual IDE support and atomic workspace application remain separate acceptance evidence. No package-version or dependency change is included.

## Convention logical-path matrix

For a convention-named sibling, vary `DrnConventionIntermediateLayout` and `DrnConventionPhysicalLayout` independently across `Relative`, `Rooted` and `Parent`. The former selects the intermediate-output root. The latter selects three physical spellings of the same in-project `PathProbe.cs`. The target assertions require exactly one project-relative `PathProbe.cs` record, preserve the explicit handwritten `QA/ConventionProbe.cs` link, exclude unlinked intermediate sources from path records and retain every original `Compile` item. These properties are fixture inputs for path tests, not production activation or identity settings.

Set `DrnConventionLinkCase=Traversal` or `Rooted` for negative Link cases. The target must preserve that invalid value and the analyzer must report DRNCV001. Omit the property for the valid link case. Run the matrix against each Web consumer when execution is authorized. These added assertions have not been executed.
