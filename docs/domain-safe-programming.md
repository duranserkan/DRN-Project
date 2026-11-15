# Domain-Safe Programming

**Domain safety** means preserving explicit domain invariants and architectural boundaries. Duran Serkan Kılıç calls the practice of expressing these as enforceable constraints **domain-safe programming**. DRN checks the subset that static analysis can establish.

## Architecture level

Roles come from recognized project-name tokens. References need no matching prefix or application-root setting. Unknown roles are outside role checks; missing-reference checks need complete, valid inventory.

| Area | Analyzer checks |
|---|---|
| Required references | DRNCV021 requires Hosted → Application and Infra, Infra → Domain, and Domain → Contract. At least one direct project per required role suffices. Shared and multiple dependencies are allowed. |
| Dependency direction | DRNCV015 reports forbidden direct role references and restricted framework dependencies, including additional edges. |
| Domain subdomains | DRNCV022 checks populated namespaces across directly referenced Domains and matches recognized concrete, closed Infra contexts/repositories by subdomain name. |
| Ownership and placement | DRNCV002, DRNCV003, DRNCV016 and DRNCV019 check recognized implementations and supported cross-subdomain entity uses. |
| Public contracts and transport | DRNCV014 reports recognized entity/persistence exposure through bounded type traversal. It does not validate serialization. |

See [architecture conventions](../DRN.Framework.SharedKernel/CONVENTIONS.md) for allowed directions and exceptions.

## Code level

| Area | Analyzer checks |
|---|---|
| Entity identity | Source-Known analyzers check metadata, duplicate entity type values per application partition, constants and concrete entity bindings. |
| Identity operations | DRN0014 reports provably uninitialized uses within its supported analysis. |
| Persistence | DRNCV004 through DRNCV011, DRNCV017 and DRNCV020 check recognized contexts, construction, discarded provider options, omitted base setup, declared membership/discovery, repository bindings and migration identity. |
| Services | DRNCV012 and DRNCV013 check registration on supported composition paths and aggregate/repository bindings. |
| SourceKnown HTTP actions | DRN0026 reports missing named policies and anonymous exposure on recognized actions. It does not verify policy registration or resource access. |

Details: [identity rules](../DRN.Framework.SharedKernel/README.md) and [convention diagnostics](../DRN.Framework.SharedKernel/CONVENTION-DIAGNOSTICS.md).

## Analyzers, code fixes and code generation

Unsuppressed analyzer errors fail the build. Convention rules default to warnings; `.editorconfig` severity or warnings-as-errors can make them build gates.

The guarded DRNCV005 fix adds a forwarding context options constructor when eligible. Classification fixes offer explicit annotation choices for DRN0015/DRN0018 and repair invalid exemptions for DRN0016.

Optional IDE refactorings generate empty entity-configuration starting points, repository pairs, selected scalar-property DTOs with mapping, and services forwarding a selected repository method. They require supported selections, existing references where needed, and conflict checks. Aggregate and controller actions create Markdown review plans only.

Generation does not invent business invariants, entity IDs or authorization policies. See [code fixes and scaffolding](../DRN.Framework.SharedKernel/SCAFFOLDING.md) for supported actions.

Unknown or incomplete evidence can defer checks. A clean result does not prove domain correctness or initialization. Runtime validation and tests remain necessary for business behavior, EF models and resource access.
