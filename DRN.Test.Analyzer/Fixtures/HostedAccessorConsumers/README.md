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
