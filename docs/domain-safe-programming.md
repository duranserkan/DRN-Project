# Domain-Safe Programming

**Domain safety** means preserving explicit domain invariants and architectural boundaries. Duran Serkan Kılıç calls the practice of expressing these as enforceable constraints **domain-safe programming**. DRN checks the subset that static analysis can establish.

## Architecture level

Roles come from recognized project-name tokens. References need no matching prefix or application-root setting. Unknown roles are outside role checks; missing-reference checks need complete, valid inventory.

| Area | Analyzer checks |
|---|---|
| Required references | DRNARCHITECTURE006 requires Hosted → Application and Infra, Infra → Domain, and Domain → Contract. At least one direct project per required role suffices. Shared and multiple dependencies are allowed. |
| Dependency direction | DRNARCHITECTURE003 reports forbidden direct references. DRNARCHITECTURE005 reports bound uses of forbidden APIs, such as a Domain class using DbContext. |
| Domain subdomains | DRNOWNERSHIP003 matches recognized concrete, closed Infra contexts and repositories against directly referenced Domain owners. It compares `(Domain, Subdomain)` pairs when either side uses `Folders` layout or an explicit domain declaration. Otherwise, unannotated `Project`-layout owners match by subdomain only. DRNOWNERSHIP004 requires every project carrying the Domain role to contain at least one populated subdomain folder. |
| Ownership and placement | DRNCONVENTIONINPUT001 validates generated convention inputs and `DrnDomainLayout`/`DrnDomain` metadata. DRNOWNERSHIP001, DRNSTRUCTURE001, DRNARCHITECTURE004, DRNOWNERSHIP002, DRNSTRUCTURE004, DRNSTRUCTURE005, DRNSTRUCTURE006, and DRNSTRUCTURE008 check semantic naming, folders-only Domain grouping folders, `_Shared` value-object boundaries, dedicated directories, and cross-subdomain entity uses. |
| Entity DTOs and public contracts | DRNDTO001 through DRNDTO007 check entity-owned `override ToDto()`, Contract DTO placement, mapped classifications on DTOs and Contract `*Response` projections, entity-prefixed message placement, prohibition of DTO exposure on public contracts/endpoints, explicit Contract `*Request`/`*Response` endpoint boundaries, and `SourceKnownService<TEntity, TDto>` default `ToDto` alignment. |
| Enum conventions | DRNENUM001 through DRNENUM005 enforce single-definition Contract enums, `[DomainOnly]` opt-in for internal Domain enums, prohibition of `[DomainOnly]` enum exposure on public shapes, and `[AllowZero]` opt-in for zero-valued enum members at compile time. |
| Public contracts and transport | DRNARCHITECTURE001 names a member whose public model exceeded the inspection limit. DRNARCHITECTURE002 reports a recognized entity or persistence type exposed by the contract. |

See [architecture conventions](../DRN.Framework.SharedKernel/CONVENTIONS.md) (including [terminology](../DRN.Framework.SharedKernel/CONVENTIONS.md#terminology)) for allowed directions, exception targets, and DRNCONVENTIONEXCEPTION001 review-expiry rules.

## Code level

| Area | Analyzer checks |
|---|---|
| Entity identity | Source-Known analyzers check metadata, duplicate entity type values per application partition, constants and concrete entity bindings. |
| Persistence | DRNPERSISTENCE001 through DRNPERSISTENCE009 check context contracts, construction, provider setup, options, model configuration, DbSets and migrations. DRNSTRUCTURE rules cover naming and destinations. DRNREPOSITORY rules cover aggregate repository contracts, implementations and context bindings. |
| Services and DI modules | DRNDEPENDENCYINJECTION001, DRNREPOSITORY002, DRNDEPENDENCYINJECTION002, and DRNDEPENDENCYINJECTION003 check paired interface lifetime bindings, project `*Module` assembly scans, observed registration paths, and aggregate/repository bindings. |
| Identity operations | DRNENTITY014 reports provably uninitialized uses within its supported analysis. |
| Exceptions | DRNCONVENTIONEXCEPTION001 rejects invalid, wildcarded, blank-reason, or due-review convention exceptions. |
| SourceKnown HTTP actions | DRNAUTHORIZATION001 reports missing named policies and anonymous exposure on recognized actions. It does not verify policy registration or resource access. |

Details: [identity rules](../DRN.Framework.SharedKernel/README.md) and [convention diagnostics](../DRN.Framework.SharedKernel/CONVENTION-DIAGNOSTICS.md).

## Analyzers, code fixes and code generation

Unsuppressed analyzer errors fail the build. Convention rules default to warnings; `.editorconfig` severity or warnings-as-errors can make them build gates.

The guarded DRNPERSISTENCE001 fix adds a forwarding context options constructor when eligible. Classification fixes offer explicit annotation choices for DRNCLASSIFICATION001/DRNCLASSIFICATION004 and repair invalid exemptions for DRNCLASSIFICATION002.

Optional IDE refactorings generate empty entity-configuration starting points, repository pairs, selected scalar-property DTOs with mapping, and services forwarding a selected repository method. They require supported selections, existing references where needed, and conflict checks. Aggregate and controller actions create Markdown review plans only.

Generation does not invent business invariants, entity IDs or authorization policies. See [code fixes and scaffolding](../DRN.Framework.SharedKernel/SCAFFOLDING.md) for supported actions.

Unknown or incomplete evidence can defer checks. A clean result does not prove domain correctness or initialization. Runtime validation and tests remain necessary for business behavior, EF models and resource access.
