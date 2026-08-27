# DRN Project SOC 2 Preparedness Checklist (Draft)

> **Preparedness notice:** This checklist is an open self-assessment draft, is not legally binding, is not legal or audit advice, and does not represent a SOC 2 examination, certification, or assurance opinion. It is a point-in-time preparedness assessment. Management should agree the scope with an independent licensed CPA firm, which performs the examination.

| Assessment field | Value |
|---|---|
| Document status | **Draft** (Work in progress / pre-audit assessment) |
| Assessment date | 2026-08-26 |
| Application repository | `DRN-Project`, reviewed at `a69dcf8693c9`; working tree was clean before this document was added |
| Deployment repository | `DRN-Project-Argo-CD-Gitops`, reviewed at committed base `7a8a340f6d53` plus existing uncommitted work |
| Intended platform | DRN Sample and DRN Nexus on Kubernetes with Argo CD, Linkerd, cert-manager, trust-manager, Sealed Secrets, CloudNativePG/PostgreSQL, Traefik Gateway API, and Graylog/MongoDB |
| Evidence level | Static repository review only. No live cluster, GitHub ruleset, cloud account, backup, alert, incident, access review, or control-operation evidence was inspected |
| Readiness decision | **Not ready for a SOC 2 examination.** Technical foundations exist, but governance, production hardening, resilience, evidence collection, and operating effectiveness remain incomplete |

## Contents

- [1. SOC 2 overview](#1-soc-2-overview)
- [2. How to use this checklist](#2-how-to-use-this-checklist)
- [3. Scope and assumptions](#3-scope-and-assumptions)
- [4. Current posture](#4-current-posture)
- [5. Criteria traceability](#5-criteria-traceability)
- [6. Common Criteria checklist](#6-common-criteria-checklist)
- [7. Additional Trust Services Criteria](#7-additional-trust-services-criteria)
- [8. Lean implementation for a two-to-three-person company](#8-lean-implementation-for-a-two-to-three-person-company)
- [9. Evidence and operating cadence](#9-evidence-and-operating-cadence)
- [10. Actions after checklist completion](#10-actions-after-checklist-completion)
- [Appendix A. Architecture, limitations, and alternatives](#appendix-a-architecture-limitations-and-alternatives)
- [Appendix B. Minimum evidence package](#appendix-b-minimum-evidence-package)
- [Appendix C. Reviewed evidence](#appendix-c-reviewed-evidence)
- [Appendix D. Fictional end-to-end example for a three-person SaaS](#appendix-d-fictional-end-to-end-example-for-a-three-person-saas)

## 1. SOC 2 overview

SOC 2 is an independent attestation report on controls at a service organization. It is intended for customers, partners, auditors, procurement teams, and regulators that need assurance about systems used to process customer data. It is not a product certification and it does not replace contractual, privacy, cybersecurity, or sector-specific legal obligations.

The AICPA Trust Services Criteria cover five categories:

| Category | Purpose | Recommended DRN scope |
|---|---|---|
| Security | Protect systems and information against unauthorized access, use, or damage | Required baseline |
| Availability | Operate systems in line with committed availability and recovery objectives | Recommended before production customer commitments |
| Processing Integrity | Process data completely, accurately, timely, and as authorized | Contextual. Include when DRN makes material processing commitments |
| Confidentiality | Protect information designated as confidential | Recommended because DRN handles credentials, identity data, configuration, and logs |
| Privacy | Govern personal information across collection, use, retention, disclosure, and disposal | Contextual for the SOC 2 report, but likely operationally relevant because Sample contains user identity and profile data |

### Report types

| Report | What it evaluates | Suitable use |
|---|---|---|
| Type I | Suitability of control design at a specified date | First DRN milestone after the critical and high gaps in this checklist are closed |
| Type II | Control design and operating effectiveness over a defined period | Target after controls operate consistently and evidence is retained for the auditor-agreed observation period |

Organizations generally pursue SOC 2 when they provide SaaS, cloud, data processing, platform, managed, or infrastructure services and customers require independent assurance. The practical benefits are stronger control ownership, repeatable operations, better enterprise sales due diligence, and evidence that security claims are performed rather than merely documented.

Primary framework references:

- [AICPA SOC 2 overview](https://www.aicpa-cima.com/topic/audit-assurance/audit-and-assurance-greater-than-soc-2/)
- [AICPA 2017 Trust Services Criteria with revised points of focus, 2022](https://www.aicpa-cima.com/resources/download/2017-trust-services-criteria-with-revised-points-of-focus-2022)
- [AICPA 2018 SOC 2 Description Criteria with revised implementation guidance, 2022](https://www.aicpa-cima.com/resources/download/get-description-criteria-for-your-organizations-soc-2-r-report)
- [AICPA SOC 2 reporting guide](https://www.aicpa-cima.com/cpe-learning/publication/soc-2-reporting-on-an-examination-of-controls-at-a-service-organization-relevant-to-security-availability-processing-integrity-confidentiality-or-privacy)

## 2. How to use this checklist

### Intended audience and adaptability

This checklist is provided as an open-access preparation and self-assessment resource:

- **Who can use this:** Any team, project, or organization preparing for SOC 2 compliance can use or adapt this framework to evaluate technical and operational controls.
- **Reference material only:** The criteria, status marks, and recommendations reflect point-in-time gap analysis and reference patterns; they are not exhaustive and may require tailoring for your specific system boundary, cloud providers, and business commitments.
- **Auditor alignment:** Final audit criteria, testing procedures, and evidence packages must be established in collaboration with your independent licensed CPA firm.

### Status marks

| Mark | Status | Meaning |
|---|---|---|
| `[x]` | Done | Implementation was verified in reviewed source or configuration. This does not prove production deployment or operating effectiveness |
| `[/]` | Progress | Partially implemented, explicitly under development, or present only in the GitOps repository's uncommitted work |
| `[ ]` | Undone | Required implementation or evidence was not found |
| `[A]` | Assumed | Based on the requested future architecture or another explicit assumption, not verified implementation |
| `[C]` | Contextual | Applicability depends on scope, customer commitments, data, deployment model, or evidence held outside the repositories |

`[/]`, `[A]`, and `[C]` are visual status marks, not GitHub interactive checkboxes.

### Severity

| Severity | Meaning |
|---|---|
| Critical | Blocks a credible audit scope, creates material security exposure, or prevents recovery |
| High | Likely control exception or significant production risk |
| Medium | Required supporting control or material maturity gap |
| Info | Scope decision, documentation, or improvement that does not alone establish a control failure |

### Completion rule

An item is not audit-ready until its control owner, implementation, approval, frequency, evidence source, exception process, and retention period are defined. Source code alone is design evidence. Type II readiness requires dated evidence that the control operated throughout the examination period.

## 3. Scope and assumptions

### Proposed system boundary

- DRN Sample and DRN Nexus application services.
- DRN Framework components used by those services.
- Source control, pull-request, CI, release, package, and container publishing workflows.
- Argo CD and the GitOps repository used to deploy the services.
- Kubernetes cluster, Gateway API, Traefik, Linkerd, cert-manager, trust-manager, and Sealed Secrets.
- CloudNativePG and PostgreSQL, plus Graylog, Data Node, and MongoDB when Graylog is enabled.
- Supporting third parties, including GitHub, SonarQube Cloud or SonarCloud, CodeRabbit, Docker Hub, NuGet, the Kubernetes or cloud provider, DNS or certificate providers, and alerting or support systems.
- Personnel and processes that administer, develop, approve, support, monitor, or recover the service.

### Explicit assumptions

- [A] **INFO | Platform adoption.** Kubernetes, Argo CD, Linkerd, cert-manager, PostgreSQL operators, and Graylog are assumed to be the intended production architecture. Current GitOps documentation defines a development-only, fresh-install topology.
- [A] **INFO | GitOps improvement.** The existing GitOps repository will be hardened and promoted through reviewed commits before it becomes audit evidence.
- [A] **HIGH | Production environment.** A production environment, production configuration, cloud boundary, and production operating team will exist. None was evidenced in the reviewed repositories.
- [A] **HIGH | Organizational control plane.** Personnel records, policies, training records, vendor contracts, access reviews, risk registers, and incident records may be held outside Git. They must be supplied before changing related items from contextual or undone.

### Scope decisions required

- [ ] **SCP-01 | CRITICAL | Define the service organization and in-scope legal entity.** Name the entity, products, locations, personnel, subprocessors, infrastructure accounts, customer populations, and excluded systems. Obtain executive and auditor approval.
- [ ] **SCP-02 | CRITICAL | Define production services and boundaries.** Decide whether Sample is a reference application, a customer-facing service, or both. Define Nexus responsibilities for configuration, discovery, identity, and internal trust. Exclude unfinished features from customer commitments.
- [ ] **SCP-03 | HIGH | Select Trust Services Categories.** Use Security as the baseline. Include Availability and Confidentiality unless contracts explicitly avoid those commitments. Include Processing Integrity and Privacy only after mapping commitments and data flows.
- [ ] **SCP-04 | HIGH | Select Type I or Type II.** Use Type I as the first target. Do not start a Type II period until controls have owners, evidence automation, tested recovery, stable production configuration, and no unresolved critical gaps.
- [ ] **SCP-05 | HIGH | Define complementary controls.** Document controls customers, cloud providers, GitHub, registries, and other subservice organizations must perform. Decide whether each subservice organization uses the carve-out or inclusive method with the auditor.
- [ ] **SCP-06 | CRITICAL | Document criterion applicability.** For every official criterion in each selected Trust Services Category, map the implemented controls and evidence or document the DC8 rationale for why the criterion is not relevant. A prohibitory policy or an outsourced component alone does not make a criterion irrelevant. Obtain auditor agreement before relying on an exclusion.

## 4. Current posture

| Area | Status | Evidence-based conclusion |
|---|---|---|
| Governance and policy | Undone | The repositories do not contain an approved security program, risk register, control owner matrix, incident plan, business continuity plan, vendor program, or evidence policy |
| Application security | Progress | Strong framework defaults exist for MFA, authorization, CSP, CSRF, host filtering, rate limiting, secure serialization, scoped logging, and Data Protection. Nexus and deployment settings remain incomplete |
| Secure development and supply chain | Progress | CI uses secretless PR jobs, immutable action SHAs, CodeQL, dependency review, SonarCloud, vulnerability scanning, attestations, and staged image scanning. Live branch-rule enforcement was not verified |
| GitOps and cluster security | Progress | Least-privilege Argo projects, validation contracts, Linkerd injection, certificate automation, isolated service accounts, and database roles exist. Current changes are uncommitted and production hardening is incomplete |
| Observability | Progress | Structured logs and Graylog manifests exist. Graylog requires manual input creation, has no evidenced retention or alert policy, and is not a complete metrics or tracing solution |
| Availability and recovery | Undone | PostgreSQL backups are disabled, stateful and application services use one replica, PDBs are disabled or absent, and no restore or disaster-recovery evidence exists |
| Audit evidence | Undone | No control matrix, evidence repository, review record, access recertification, incident exercise, backup test, penetration test, or observation-period evidence was reviewed |

### Recommended closure order

1. Approve scope, owners, policies, risk assessment, data classification, and customer commitments.
2. Build a separate production baseline with managed secrets, edge TLS, NetworkPolicies, hardened Pods, supported platform versions, immutable images, backups, and tested recovery.
3. Operate access reviews, monitoring, alerts, incidents, vulnerability remediation, vendor reviews, and continuity exercises with retained evidence.
4. Complete an independent readiness review, then pursue Type I. Start a Type II observation period only after the controls operate reliably.

## 5. Criteria traceability

### Reference and identifier convention

The AICPA criteria and the DRN actions are separate identifiers:

- **AICPA references use dots**, such as `CC1.1`, `A1.1`, and `P6.4`. These identify the authoritative criteria.
- **DRN action IDs use hyphens and zero-padded suffixes**, such as `CC1-01`, `A1-01`, and `P1-06`. These are internal remediation and evidence actions; their numbers do not correspond one-to-one with AICPA references.
- The focus labels below are short paraphrases for navigation, not replacements for the authoritative wording. Management and the CPA firm must use the linked AICPA Trust Services Criteria and Description Criteria when determining scope, control suitability, and evidence.
- Security requires the complete Common Criteria set. Each additional category requires the Common Criteria plus every relevant criterion for that category. Any criterion treated as not relevant requires the documented `SCP-06` and DC8 analysis.

### Description Criteria mapping

The Description Criteria govern management's description of the service organization's system. They are separate from the Trust Services Criteria used to evaluate controls.

| AICPA reference | Paraphrased subject | DRN internal actions |
|---|---|---|
| `DC1` | Types of services provided | `SCP-01`, `SCP-02`, `CC2-01` |
| `DC2` | Principal service commitments and system requirements | `SCP-03`, `CC2-03`, `A1-01`, `PI1-01`, `C1-01`, `P1-01` |
| `DC3` | Infrastructure, software, people, procedures, and data | `CC2-01`, `CC2-02`, `CC3-04` |
| `DC4` | Identified system incidents and their nature, timing, effect, and disposition | `CC2-01`, `CC4-04`, `CC7-04` |
| `DC5` | Applicable Trust Services Criteria and related controls | `SCP-03`, `SCP-06`, `CC4-01`, `CC5-02`, and the mappings in this section |
| `DC6` | Complementary user entity controls | `SCP-05`, `CC2-03` |
| `DC7` | Subservice organizations, treatment method, and complementary controls | `SCP-05`, `CC2-01`, `CC9-01` |
| `DC8` | Applicable criteria judged not relevant and the reasons | `SCP-06` |
| `DC9` | Significant changes during a Type II period | `CC2-01`, `CC2-05`, `CC3-05`, `CC8-05` |

### Common Criteria mapping

| AICPA reference | Paraphrased focus | DRN internal actions |
|---|---|---|
| `CC1.1` | Integrity and ethical values | `CC1-02`, `CC1-03`, `CC1-07` |
| `CC1.2` | Independent governance oversight | `CC1-01`, `CC1-04`, `CC1-08` |
| `CC1.3` | Structures, reporting lines, authority, and responsibility | `CC1-01`, `CC1-04`, `CC1-05` |
| `CC1.4` | Competent personnel | `CC1-03`, `CC1-05` |
| `CC1.5` | Accountability for control responsibilities | `CC1-01`, `CC1-03`, `CC1-06`, `CC1-07` |
| `CC2.1` | Relevant, quality information supporting internal control | `CC2-01`, `CC2-02`, `CC2-05`, `CC4-01` |
| `CC2.2` | Internal communication of objectives and responsibilities | `CC1-02`, `CC2-04`, `CC2-05` |
| `CC2.3` | External communication affecting internal control | `CC2-03`, `SCP-05` |
| `CC3.1` | Clear objectives supporting risk identification | `SCP-01`, `SCP-02`, `SCP-03`, `CC3-01` |
| `CC3.2` | Risk identification, analysis, and response | `CC3-01`, `CC3-02`, `CC3-03`, `CC3-04` |
| `CC3.3` | Fraud risk | `CC3-06` |
| `CC3.4` | Significant change and emerging risk | `CC3-05` |
| `CC4.1` | Ongoing and separate control evaluations | `CC4-01`, `CC4-02`, `CC4-03`, `CC4-05` |
| `CC4.2` | Evaluation and communication of deficiencies | `CC4-04`, `CC4-06`, `CC1-01` |
| `CC5.1` | Selection and development of control activities | `CC5-02` |
| `CC5.2` | General technology control activities | `CC5-04`, `CC6-08` through `CC6-18`, `CC7-06`, `CC8-01` through `CC8-03` |
| `CC5.3` | Policies translated into procedures | `CC1-02`, `CC5-01` |
| `CC6.1` | Logical access security architecture and protective mechanisms | `CC6-01`, `CC6-02`, `CC6-03`, `CC6-05`, `CC6-08`, `CC6-11`, `CC6-12`, `CC6-13`, `CC6-15` |
| `CC6.2` | Registration, authorization, credentialing, and deprovisioning | `CC6-15` |
| `CC6.3` | Role-based authorization and access modification or removal | `CC6-02`, `CC6-03`, `CC6-04`, `CC6-15` |
| `CC6.4` | Restriction of physical access | `CC6-16` |
| `CC6.5` | Protection and secure disposal of physical information assets | `CC6-16` |
| `CC6.6` | Protection against threats outside system boundaries | `CC6-08`, `CC6-09`, `CC6-10`, `CC6-11`, `CC6-12`, `CC6-13` |
| `CC6.7` | Authorized and protected transmission, movement, and removal of information | `CC6-09`, `CC6-10`, `CC6-17` |
| `CC6.8` | Prevention and detection of malicious or unauthorized software | `CC6-12`, `CC6-13`, `CC6-18`, `CC7-05`, `CC7-08` |
| `CC7.1` | Detection of configuration changes that create vulnerabilities | `CC7-05`, `CC7-06` |
| `CC7.2` | Monitoring and analysis of system anomalies | `CC7-01`, `CC7-02`, `CC7-03` |
| `CC7.3` | Evaluation of security events | `CC7-03`, `CC7-04` |
| `CC7.4` | Incident response | `CC7-04`, `CC7-07` |
| `CC7.5` | Recovery from identified security incidents | `CC7-04`, `CC7-07`, `CC9-02` |
| `CC8.1` | Authorized, designed, tested, approved, and implemented changes | `CC8-01` through `CC8-08` |
| `CC9.1` | Mitigation of risks from potential business disruptions | `CC9-02`, `CC9-03`, `CC9-04`, `CC9-05` |
| `CC9.2` | Vendor and business-partner risk | `CC9-01`, `SCP-05` |

### Additional category mapping

These criteria apply only when the associated category is selected, but their applicability must still be documented through `SCP-03` and `SCP-06`.

| AICPA reference | Paraphrased focus | DRN internal actions |
|---|---|---|
| `A1.1` | Capacity monitoring and management | `A1-01`, `A1-05`, `A1-06`, `A1-07` |
| `A1.2` | Environmental protection, backup processes, and recovery infrastructure | `A1-02`, `A1-03`, `A1-05`, `A1-07` |
| `A1.3` | Recovery-plan testing | `A1-04`, `A1-08`, `CC9-02` |
| `PI1.1` | Processing objectives, specifications, and quality information | `PI1-01`, `PI1-02` |
| `PI1.2` | Complete and accurate system inputs | `PI1-02`, `PI1-03`, `PI1-04` |
| `PI1.3` | Controlled system processing | `PI1-03`, `PI1-04`, `PI1-06` |
| `PI1.4` | Complete, accurate, timely, and authorized output | `PI1-04`, `PI1-05` |
| `PI1.5` | Complete, accurate, timely, and protected storage | `PI1-03`, `PI1-04` |
| `C1.1` | Identification and maintenance of confidential information | `C1-01`, `C1-02`, `C1-03`, `C1-04` |
| `C1.2` | Disposal of confidential information | `C1-05` |
| `P1.1` | Privacy notice and timely communication of changes | `P1-01`, `P1-02` |
| `P2.1` | Choice, consent, and consequences | `P1-02` |
| `P3.1` | Collection consistent with privacy objectives | `P1-03` |
| `P3.2` | Explicit consent before collection when required | `P1-02`, `P1-03` |
| `P4.1` | Use limited to identified purposes | `P1-03` |
| `P4.2` | Retention consistent with privacy objectives | `P1-05` |
| `P4.3` | Secure disposal of personal information | `P1-05` |
| `P5.1` | Authenticated data-subject access | `P1-04` |
| `P5.2` | Correction, amendment, and related communication | `P1-04`, `P1-08` |
| `P6.1` | Third-party disclosure with prior consent | `P1-02`, `P1-06` |
| `P6.2` | Complete records of authorized disclosures | `P1-06` |
| `P6.3` | Complete records of unauthorized disclosures | `P1-07` |
| `P6.4` | Vendor privacy commitments, assessment, and correction | `P1-06`, `CC9-01` |
| `P6.5` | Vendor notification of suspected or actual unauthorized disclosures | `P1-06`, `P1-07` |
| `P6.6` | Breach and incident notification | `P1-07` |
| `P6.7` | Accounting of personal information held and disclosed | `P1-04`, `P1-06` |
| `P7.1` | Accurate, complete, current, and relevant personal information | `P1-08` |
| `P8.1` | Privacy inquiries, complaints, disputes, monitoring, and remediation | `P1-09` |

This mapping establishes coverage, not readiness. Each referenced DRN action still needs an owner, implemented control, evidence, review, exception handling, and retention. `SCP-06` remains open until management and the CPA confirm the applicability analysis.

## 6. Common Criteria checklist

### CC1. Control environment

- [ ] **CC1-01 | CRITICAL | Establish executive accountability.** Approve a security charter, appoint the accountable executive and control owners, and record quarterly oversight decisions. Exit evidence: signed charter, owner matrix, meeting minutes, and tracked actions.
- [ ] **CC1-02 | HIGH | Approve core policies.** Create and approve information security, access control, secure development, change management, incident response, acceptable use, data classification, retention, vendor risk, vulnerability management, backup, and business continuity policies. Review at least annually and after material change.
- [C] **CC1-03 | HIGH | Implement personnel controls.** Context: this evidence normally lives in HR systems. Define screening where lawful, confidentiality agreements, onboarding, annual security training, role changes, disciplinary process, and same-day offboarding. Treat as undone until evidence is supplied.
- [ ] **CC1-04 | HIGH | Define segregation of duties.** Separate code authorship, approval, deployment authorization, production administration, security review, and evidence review. For a small team, use documented compensating review by an independent person.
- [ ] **CC1-05 | MEDIUM | Set competency requirements.** Define role descriptions and minimum competencies for application security, Kubernetes, PostgreSQL, incident response, privacy, and audit evidence. Track training and renewal.
- [ ] **CC1-06 | MEDIUM | Create an exception process.** Require named risk acceptance, business rationale, compensating controls, expiry date, and approving authority. Unexpired exceptions must be reviewed at least quarterly.
- [ ] **CC1-07 | HIGH | Establish integrity and ethical conduct.** Approve a code of conduct covering integrity, conflicts of interest, acceptable behavior, control evidence, reporting concerns, and disciplinary consequences. Require acknowledgement at onboarding and periodically thereafter.
- [ ] **CC1-08 | HIGH | Establish independent governance oversight.** Define the board, owner, or equivalent oversight body and document how it challenges management, reviews internal-control performance, and remains sufficiently independent. For a small owner-managed company, agree a truthful compensating oversight model with the CPA rather than claiming separation that does not exist.

### CC2. Communication and information

- [ ] **CC2-01 | CRITICAL | Create the SOC 2 system description.** Address DC1 through DC9: services; principal commitments and system requirements; infrastructure, software, people, procedures, and data; qualifying incidents; applicable criteria and related controls; complementary user entity controls; subservice organizations and their treatment; criteria judged not relevant and the reasons; and significant Type II changes. Keep the description consistent with deployed production state.
- [ ] **CC2-02 | HIGH | Maintain system and data-flow diagrams.** Show public ingress, service-to-service traffic, identities, secrets, databases, logs, backups, administrative paths, third parties, and trust boundaries. Review after every material architecture change.
- [ ] **CC2-03 | HIGH | Define customer security communications.** Publish accurate security commitments, support routes, maintenance communication, incident notification, vulnerability reporting, and availability terms. The current [`SECURITY.md`](SECURITY.md) is an initial contact note and is not a complete vulnerability disclosure policy.
- [ ] **CC2-04 | MEDIUM | Define internal escalation.** Document how developers and operators report vulnerabilities, control failures, data handling errors, suspicious activity, and policy exceptions. Test routing and acknowledgement.
- [ ] **CC2-05 | MEDIUM | Control document versions.** Assign owners, approvers, effective dates, review dates, and change history to policies and runbooks. Prevent unapproved edits to final evidence.

### CC3. Risk assessment

- [ ] **CC3-01 | CRITICAL | Perform and approve an enterprise risk assessment.** Evaluate likelihood, impact, inherent risk, controls, residual risk, owner, due date, and acceptance. Include cyber, availability, confidentiality, privacy, fraud, vendor, legal, and operational risks.
- [ ] **CC3-02 | HIGH | Threat-model DRN production.** Cover internet ingress, identity and bearer tokens, MFA exemptions, Nexus trust keys, mounted configuration, Argo admin paths, Kubernetes API access, container supply chain, PostgreSQL, Graylog, certificate authorities, logs, and backup repositories.
- [ ] **CC3-03 | HIGH | Complete a business impact analysis.** Identify critical services and data, maximum tolerable outage, recovery time objective (RTO), recovery point objective (RPO), dependencies, minimum staffing, and restoration order.
- [ ] **CC3-04 | HIGH | Classify data.** Define public, internal, confidential, restricted, and personal data. Map each class to storage, encryption, access, logging, retention, disposal, and transfer rules.
- [ ] **CC3-05 | MEDIUM | Assess change and emerging risk.** Review major framework, Kubernetes, Linkerd edge, database, operator, identity, cryptographic, and observability changes before adoption. Record authoritative compatibility and security evidence.
- [ ] **CC3-06 | MEDIUM | Assess fraud and misuse.** Include privileged access abuse, release tampering, audit-log suppression, secret substitution, customer impersonation, destructive database actions, and false evidence.

### CC4. Monitoring activities

- [ ] **CC4-01 | CRITICAL | Build a control monitoring plan.** For every control, define owner, performer, reviewer, frequency, evidence, sample population, exception handling, and retention. Monitor overdue controls.
- [/] **CC4-02 | HIGH | Operate continuous technical scanning.** Application CI includes CodeQL, dependency review, NuGet scanning, SonarCloud, and staged container scanning. GitOps CI includes Trivy, CodeQL for Actions, SonarCloud, and manifest validation. Required action: verify every workflow runs on protected branches, archive results, define remediation SLAs, and scan deployed images and live clusters.
- [ ] **CC4-03 | HIGH | Perform independent security testing.** Commission scoped penetration testing before the examination and after material exposure changes. Track findings to closure and retest high-risk items.
- [ ] **CC4-04 | HIGH | Review control exceptions.** Review failed CI, denied deployments, vulnerabilities, access anomalies, incidents, restore failures, alert failures, and expired certificates. Preserve reviewer sign-off.
- [ ] **CC4-05 | MEDIUM | Conduct internal readiness reviews.** Use a reviewer independent of control operation to sample evidence and report deficiencies to management before the CPA examination.
- [ ] **CC4-06 | MEDIUM | Track remediation.** Use a controlled backlog with severity, owner, target date, evidence, risk acceptance, and closure approval. Do not close findings on implementation alone when live validation is required.

### CC5. Control activities

- [ ] **CC5-01 | HIGH | Convert policies into procedures.** Each policy must have runnable procedures, assigned roles, inputs, outputs, approval gates, evidence, failure handling, and escalation.
- [ ] **CC5-02 | HIGH | Define preventive and detective controls.** Map each identified risk to at least one control and identify whether the control prevents, detects, or corrects the risk. Eliminate unsupported policy statements.
- [ ] **CC5-03 | HIGH | Protect evidence integrity.** Store evidence in access-controlled, immutable or versioned storage. Record timestamps, source, reviewer, and related control. Prevent control performers from silently replacing final evidence.
- [ ] **CC5-04 | MEDIUM | Automate carefully.** Use CI, GitOps, scanners, certificate controllers, and operators where their failure modes are monitored. Document manual controls for external approvals, recovery, access review, incidents, and vendor assessment.

### CC6. Logical and physical access

- [/] **CC6-01 | CRITICAL | Complete strong authentication.** Sample has MFA redirection and the framework enforces an MFA fallback policy. Both Sample and Nexus explicitly exempt the Identity bearer scheme, and Nexus MFA redirection is not implemented. Decide and test bearer MFA semantics, complete Nexus MFA, require MFA for source control, cloud, Argo CD, Kubernetes, registries, databases, logging, and support systems.
- [C] **CC6-02 | CRITICAL | Enforce source-control protections.** CODEOWNERS, immutable action SHAs, secretless PR workflows, and aggregate gates exist. GitHub rulesets were not inspected. Require pull requests, code-owner review, required gatekeeper and code-scanning results, signed or attributable commits, administrator restrictions, and branch deletion controls. Status remains contextual until exported settings prove enforcement.
- [x] **CC6-03 | HIGH | Apply Argo project least privilege in configuration.** Workload, platform, application, certificate, and default-deny projects are separated. Required operating evidence: render validation, approved project changes, Argo RBAC export, administrator access review, and proof that no application uses `AppProject/default`.
- [x] **CC6-04 | HIGH | Use workload-specific database roles in configuration.** Nexus and Sample use distinct PostgreSQL login roles without superuser, database creation, role creation, replication, or bypass-RLS privileges. Validate effective grants in production and remove unused owner credentials from workload Pods.
- [/] **CC6-05 | CRITICAL | Complete secrets management.** Sealed Secrets is declared and workloads mount operator or externally managed Secrets. No repository-managed `SealedSecret` objects were found. cert-manager manages certificates, not arbitrary passwords or API keys. Select and operate Sealed Secrets, External Secrets plus a cloud secret manager, or Vault; define rotation, break-glass, revocation, backup, and access logging.
- [/] **CC6-06 | HIGH | Protect application cryptographic keys.** Sample persists ASP.NET Core Data Protection keys to PostgreSQL and encrypts them with AES-GCM through `SampleXmlEncryptor`; unit tests exist. Production master material is not defined. Move the master key or seed to an approved secret or key-management system, rotate it safely, test old-key decryption, restrict database access, and document compromise recovery.
- [ ] **CC6-07 | CRITICAL | Remove production-like static secret examples.** `Sample.Hosted/appsettings.Staging.json` contains deterministic sample Nexus key and seed material. Keep only unmistakably non-runnable examples or placeholders, fail startup when production secrets are absent, and prove secret scanning and rotation before use.
- [ ] **CC6-08 | CRITICAL | Create a production-safe configuration overlay.** Reviewed workloads explicitly run `Environment=Development`, allow wildcard hosts, and enable development database migration. Do not promote these settings. Use `Production`, explicit hosts, externally supplied secrets, controlled migrations, production error handling, restricted diagnostics, and startup validation that fails closed.
- [/] **CC6-09 | HIGH | Enforce encrypted service traffic.** Linkerd injection and cert-manager-managed trust resources exist, and MongoDB requires TLS. Live mesh identity, proxy coverage, certificate rotation, policy, and negative TLS tests were not performed. Make `linkerd check`, proxy checks, identity continuity, and unauthorized-traffic denial release evidence.
- [ ] **CC6-10 | CRITICAL | Add edge TLS.** The reviewed Gateway exposes HTTP on port 8000 and Traefik disables `websecure`. Before external use, terminate TLS with an approved certificate, redirect HTTP, restrict protocols and ciphers, automate renewal, monitor expiry, and test failure recovery.
- [ ] **CC6-11 | HIGH | Add Kubernetes NetworkPolicies.** No NetworkPolicy manifests were found. Default-deny ingress and egress by namespace, then allow only Gateway, Linkerd, DNS, PostgreSQL, Graylog, certificate, monitoring, and required control-plane flows. Validate with positive and negative tests.
- [ ] **CC6-12 | HIGH | Harden application Pods.** DRN Deployments disable service-account token automount and set resources and probes, but do not define container security contexts or digest-pinned application images. Set non-root execution, read-only root filesystem where compatible, dropped capabilities, no privilege escalation, seccomp, controlled writable volumes, and immutable image digests.
- [/] **CC6-13 | HIGH | Harden stateful and platform workloads.** Graylog and Data Node use digest-pinned images and disable service-account token automount, but only Pod filesystem group settings are explicit. Review upstream runtime requirements, then enforce the strongest supported container security context and admission policy without breaking storage ownership.
- [ ] **CC6-14 | HIGH | Prove encryption at rest.** No storage-class, disk, database, backup, or key-management evidence was reviewed. Require encryption for cluster disks, PostgreSQL, MongoDB, Graylog data, backups, artifacts, and evidence stores. Record key ownership and rotation.
- [C] **CC6-15 | HIGH | Operate access lifecycle controls.** Context: access grants and reviews may live outside Git. Require approved role-based access, least privilege, no shared accounts, periodic recertification, inactivity removal, and immediate offboarding across every in-scope system.
- [C] **CC6-16 | MEDIUM | Address physical security and asset disposal.** Use cloud and data-center assurance reports for hosted infrastructure. Define office, device, media, remote-work, inventory, transfer, reuse, and disposal controls. Render data and software unreadable before releasing equipment or media and retain disposal evidence. Confirm scope with the auditor.
- [ ] **CC6-17 | HIGH | Control information transmission and removal.** Authorize and inventory external transfers, exports, removable media, support downloads, and administrative data movement. Encrypt protected information in transit, restrict destinations and recipients, record material transfers, and detect unauthorized movement or removal.
- [ ] **CC6-18 | HIGH | Prevent and detect malicious or unauthorized software.** Define controls for administrator endpoints, build inputs, container images, email or support artifacts, and user uploads where applicable. Keep prevention and detection current, alert on findings, quarantine unsafe content, and test response without weakening production safeguards.

### CC7. System operations

- [/] **CC7-01 | HIGH | Complete centralized logging.** DRN emits structured request logs and routes development logs to Graylog HTTP. Graylog infrastructure exists but is optional, requires manual GELF input creation, and has no evidenced retention or alert policy. Automate or formally control input creation, protect log access, set retention, monitor ingestion, and test loss detection.
- [/] **CC7-02 | HIGH | Prevent sensitive logging.** DRN avoids query values but records IP addresses, user IDs, authentication method references, exception messages, and stack traces. Classify logs as sensitive, redact tokens and personal data, restrict raw log access, encrypt storage and transit, and test redaction regressions.
- [ ] **CC7-03 | CRITICAL | Implement security monitoring and alerting.** Define alerts for authentication abuse, privilege changes, CI or Argo failures, image or manifest drift, suspicious egress, database events, certificate expiry, backup failures, capacity, log pipeline loss, and service health. Assign on-call ownership and test every alert.
- [ ] **CC7-04 | CRITICAL | Approve and exercise incident response.** Define severity, roles, communication, evidence preservation, containment, eradication, recovery, customer or regulator notification, lessons learned, and legal escalation. Run at least one tabletop and one technical exercise before Type II.
- [/] **CC7-05 | HIGH | Operate vulnerability management.** CI scanning exists. Add asset coverage, external attack-surface scanning, base-image and deployed-image scanning, Kubernetes and cloud configuration scanning, severity-based SLAs, exception expiry, patch cadence, and verified closure.
- [ ] **CC7-06 | HIGH | Establish configuration baselines and drift response.** Argo CD provides reconciliation and validation contracts, but current GitOps improvements are uncommitted and some applications are deliberately manual. Commit and protect the baseline, define allowed manual changes, alert on drift, and preserve reconciliation evidence.
- [ ] **CC7-07 | HIGH | Create production runbooks.** Cover degraded dependencies, certificate failures, PostgreSQL failover and restore, Graylog loss, Linkerd failure, Gateway failure, Argo outage, secret rotation, compromise, capacity, and rollback. Include exact decision authority and stop conditions.
- [C] **CC7-08 | MEDIUM | Add endpoint and runtime protection.** Context: select controls based on threat model. Consider managed endpoint detection for administrator devices, container runtime detection, admission control, and malware scanning for user uploads. Do not deploy tools without alert ownership.

### CC8. Change management

- [x] **CC8-01 | HIGH | Maintain controlled CI design in source.** PR workflows separate trusted base-SHA automation from untrusted source, avoid secrets, pin third-party Actions to immutable SHAs, set permissions and timeouts, and use aggregate gates. Required operating evidence: successful runs and enforced rulesets.
- [x] **CC8-02 | HIGH | Maintain release integrity in source.** Release workflows verify release tags against `master`, run analysis, stage and scan container digests, attest packages, publish packages, and promote scanned image digests. Verify production-environment approvals and credential restrictions in GitHub settings.
- [/] **CC8-03 | HIGH | Finish immutable deployment pinning.** Platform and Graylog images include digests, but DRN Sample and Nexus Deployments use mutable version tags. Pin production workloads to promoted manifest-list digests and record the application release to GitOps change link.
- [C] **CC8-04 | HIGH | Enforce review and approval.** CODEOWNERS exist in both repositories, but repository rulesets and environment approvers were not inspected. Export and retain proof of required approvals, protected branches, denied bypasses, and production environment gates.
- [ ] **CC8-05 | HIGH | Link changes to authorization and testing.** Require an issue or change record with risk, affected controls, test evidence, migration impact, security review, rollback, approvals, deployment result, and post-change verification.
- [ ] **CC8-06 | HIGH | Define emergency change control.** Require incident linkage, minimum approver, limited access, contemporaneous logging, post-change review, rollback, and expiry of temporary permissions or configuration.
- [/] **CC8-07 | HIGH | Complete DRN Nexus before relying on it as a control.** Nexus service discovery, remote settings, cookie configuration, personal data protection, and MFA flow are incomplete. Do not use Nexus as evidence for centralized configuration or identity until contracts, authorization, availability, audit logging, key rotation, and failure behavior are implemented and tested.
- [ ] **CC8-08 | MEDIUM | Govern database changes.** Require reviewed migrations, backups before risky changes, forward and rollback plans, production approval, least-privilege migration identity, migration logs, and post-deployment data checks. Automatic development migration is not an acceptable production change control.

### CC9. Risk mitigation

- [ ] **CC9-01 | CRITICAL | Establish vendor and subservice risk management.** Inventory GitHub, SonarQube Cloud or SonarCloud, CodeRabbit, Docker Hub, NuGet, the cloud or Kubernetes provider, DNS, certificate services, and support tools. Record the actual plan or license and data shared with each provider. Assess security, availability, privacy, data location, breach terms, access, assurance reports, and exit plans before approval and at least annually.
- [ ] **CC9-02 | CRITICAL | Establish business continuity and disaster recovery.** Define loss scenarios, RTO, RPO, backups, alternate communication, recovery personnel, infrastructure recreation, dependency failure, and customer communication. Exercise full recovery and record actual results.
- [ ] **CC9-03 | HIGH | Manage concentration and single-person risk.** Current ownership is concentrated. Add independent approval and recovery capability, document credentials and runbooks securely, and ensure at least two authorized people can respond without shared accounts.
- [ ] **CC9-04 | HIGH | Define capacity and financial safeguards.** Set budgets, quotas, capacity thresholds, denial-of-service protections, registry and CI limits, and escalation. Test resource exhaustion and service degradation.
- [C] **CC9-05 | MEDIUM | Evaluate cyber insurance and contractual allocation.** Context: decide with legal and finance based on residual risk, customer contracts, indemnities, and incident costs. Insurance does not replace controls.

## 7. Additional Trust Services Criteria

### Availability

- [ ] **A1-01 | CRITICAL | Approve measurable availability commitments.** Define service-level objectives, maintenance windows, RTO, RPO, dependency objectives, measurement source, exclusions, and customer remedies. Do not promise availability above tested architecture.
- [ ] **A1-02 | CRITICAL | Remove single points of failure.** Current DRN services, PostgreSQL, MongoDB, Graylog, Data Node, and Traefik use one replica or instance. Design production topology across failure domains, add disruption budgets and anti-affinity, and prove failover.
- [ ] **A1-03 | CRITICAL | Enable and protect backups.** CloudNativePG backups are explicitly disabled. Configure encrypted, access-controlled, off-cluster backups for PostgreSQL and all required state. Define retention, immutability, monitoring, and deletion.
- [ ] **A1-04 | CRITICAL | Test restoration.** Restore databases, Graylog state if required, secrets, certificates, GitOps configuration, and the complete service into an isolated environment. Measure RTO and RPO and resolve gaps.
- [x] **A1-05 | MEDIUM | Define application resource requests, limits, and probes in configuration.** DRN Deployments and Graylog workloads have probes and resource controls. Replace sample endpoints with purpose-built health checks that validate only required dependencies and do not disclose data.
- [ ] **A1-06 | HIGH | Implement capacity, saturation, and certificate monitoring.** Alert before resource exhaustion, disk fill, queue or request saturation, certificate expiry, connection exhaustion, and backup failure. Review trends and capacity plans.
- [ ] **A1-07 | HIGH | Validate platform support.** The GitOps profile identifies a compatibility gap for the selected Linkerd edge and Kubernetes baseline. Obtain authoritative support evidence, choose a supported stable combination, or document and accept risk before production.
- [ ] **A1-08 | HIGH | Exercise disaster scenarios.** Test cluster loss, region or provider outage where relevant, database corruption, secret loss, CA compromise, registry outage, GitHub outage, Graylog outage, and failed deployment rollback.

### Processing integrity

- [C] **PI1-01 | HIGH | Define processing commitments.** Context: include this category only when customer commitments cover completeness, validity, accuracy, timeliness, or authorization. Map each commitment to inputs, processing, outputs, and controls.
- [x] **PI1-02 | MEDIUM | Maintain validation and automated test gates in source.** The application has unit, analyzer, and integration test projects, and CI validates frontend and backend changes. Type II evidence must show required checks ran and failures blocked change.
- [/] **PI1-03 | HIGH | Control database processing and migrations.** Entity Framework migrations and separate schemas exist, but production migration authorization, reconciliation, rollback, and data verification are not defined.
- [ ] **PI1-04 | HIGH | Control inputs, processing, outputs, storage, and errors.** Define input, processing, output, and storage specifications. Validate completeness, accuracy, timeliness, and authorization; retain the required input, in-process, output, and reconciliation records; and handle duplicate, missing, late, unauthorized, or partially processed data. Monitor retries, dead letters, batch totals, transaction failures, and corrective actions where applicable.
- [ ] **PI1-05 | HIGH | Provide correction and customer support procedures.** Record detected errors, impact, customer communication, approved corrections, reprocessing, verification, and prevention.
- [C] **PI1-06 | MEDIUM | Scope unfinished jobs and messaging.** DRN Jobs and MassTransit capabilities are unfinished. Exclude them from processing commitments or complete their retry, timeout, error queue, idempotency, observability, and recovery controls.

### Confidentiality

- [ ] **C1-01 | CRITICAL | Define confidentiality commitments and inventory.** Identify confidential customer, business, security, credential, source, log, and operational data. Record owners, locations, transfers, access, encryption, retention, and deletion.
- [/] **C1-02 | HIGH | Encrypt confidential data.** Internal MongoDB TLS and planned Linkerd mTLS exist. Complete edge TLS, encryption at rest, backup encryption, key management, and approved secure administrative access.
- [ ] **C1-03 | HIGH | Restrict confidential data in logs and support.** Prohibit credentials, tokens, connection strings, secrets, unnecessary personal data, and customer payloads in logs, tickets, chat, and CI artifacts. Test redaction and purge paths.
- [ ] **C1-04 | HIGH | Control disclosure and transfer.** Approve exports, support access, subprocessors, cross-border transfers, and customer data sharing. Log and periodically review disclosures.
- [ ] **C1-05 | HIGH | Dispose securely.** Define deletion for databases, object stores, backups, logs, artifacts, local devices, and terminated environments. Preserve legal holds and record destruction.

### Privacy

- [C] **P1-01 | HIGH | Decide whether Privacy is in the SOC 2 scope.** Sample processes identity, profile, email, phone, IP, and log data. Complete a personal-data inventory and obtain privacy counsel before deciding. Privacy obligations can apply even if this category is excluded from SOC 2.
- [/] **P1-02 | HIGH | Complete notice and consent.** DRN provides a consent-cookie mechanism, but applications remain responsible for accurate and timely notices, purpose-specific choices, essential-cookie classification, consequences of refusal or withdrawal, and proof of implicit or explicit consent. Obtain and retain explicit consent before collecting sensitive personal information when required.
- [ ] **P1-03 | HIGH | Define collection and use limits.** Collect only necessary personal data for documented purposes and lawful bases. Prevent secondary use without approval and notice.
- [ ] **P1-04 | HIGH | Implement data-subject request handling.** Support access, correction, deletion, restriction, objection, portability, and an accounting of personal information held and disclosed where applicable. Verify identity, deadlines, denials, appeals, exceptions, subprocessors, and evidence.
- [ ] **P1-05 | HIGH | Set retention and deletion.** Map each personal-data category to a justified retention period and automated or reviewed deletion. Include backups and logs.
- [ ] **P1-06 | HIGH | Manage privacy vendors and disclosures.** Obtain privacy commitments from vendors and subprocessors, require notification of suspected or actual unauthorized disclosures, assess compliance periodically and when risk changes, correct misuse or deficiencies, maintain transfer mechanisms and subprocessor notices, and retain complete records of authorized disclosures.
- [ ] **P1-07 | HIGH | Prepare privacy incident records and notification.** Create complete and timely records of detected or reported unauthorized disclosures. Integrate legal assessment, affected-data analysis, vendor escalation, jurisdictional deadlines, regulator and customer notification, and evidence preservation into incident response.
- [ ] **P1-08 | MEDIUM | Verify data quality.** Provide reasonable mechanisms to keep material personal data accurate and record corrections.
- [ ] **P1-09 | HIGH | Monitor privacy compliance and resolve complaints.** Publish inquiry and complaint channels, authenticate and track requests, document and communicate resolutions, periodically review compliance with privacy commitments, report deficiencies to management, and verify timely remediation.

## 8. Lean implementation for a two-to-three-person company

This is the minimum practical operating model for DRN. It reduces administration but does not remove any applicable control objective. Use automation to perform repeatable checks. Keep risk acceptance, approval, access review, incident decisions, and recovery verification human-owned.

For readability, this section uses the fictional names from Appendix D: Lina, CEO and Compliance Lead; Alan, CTO and Software Architect; and Nora, Software Engineer and Security Reviewer. Replace them with the real accountable people and titles before adopting the controls.

### Small-team role model

| Responsibility | Two people | Three people | Required boundary |
|---|---|---|---|
| Accountable and risk owner | Lina | Lina | Approves scope, policies, residual risk, vendors, continuity, and audit representations |
| Security and change reviewer | Lina or Alan, whichever is not the author; external reviewer when neither is independent or qualified | Nora | Reviews access, security findings, sensitive changes, incidents, and evidence. Does not approve their own changes |
| Service operator | Alan primary; Lina backup when qualified | Alan | Deploys, monitors, backs up, restores, and collects evidence. Sensitive work requires non-operator review |
| Independent compensating review | External adviser when Lina and Alan cannot be independent | External adviser for annual readiness and penetration testing | Samples access, changes, recovery, risk acceptance, and evidence without operating those controls |

Apply these rules:

- Never self-merge. The non-author approves the pull request.
- Never share named accounts, MFA factors, or administrator credentials.
- Use separate daily and privileged administrator identities where the platform supports them.
- Allow one-person emergency action only for an active incident. Open the incident record immediately and require peer review by the next business day.
- Make one person primary on-call and the other secondary. Test the handoff quarterly.
- Rotate quarterly access review and evidence review so the same person does not perform and approve the control.

### Minimum action set

- [ ] **SMALL-01 | CRITICAL | Assign controls and keep policy concise.** In the fictional three-person model, Lina owns the program, Nora owns security review, and Alan owns service operation. A two-person company assigns the same responsibilities by role and uses external review where independence or technical competence is missing. Keep one approved security policy, one risk register, one vendor register, one incident plan, and this control checklist in a private, versioned repository.
- [/] **SMALL-02 | CRITICAL | Enforce the two-person change rule.** CODEOWNERS and gatekeeper workflows exist. Enable GitHub rulesets that require a pull request, one non-author approval, code-owner review for security-sensitive paths, successful gatekeeper and code-scanning results, and no administrator bypass except documented break-glass.
- [x] **SMALL-03 | MEDIUM | Use CodeRabbit as an advisory reviewer.** [`.coderabbit.yaml`](.coderabbit.yaml) already enables assertive automatic review with DRN-specific security and architecture instructions. Keep human approval mandatory. Review [CodeRabbit retention and privacy terms](https://www.coderabbit.ai/privacy-policy), disable unnecessary caching, and do not send secrets. Add equivalent GitOps instructions to flag mutable images, missing security contexts, wildcard Argo permissions, plaintext secrets, development settings, absent probes, and unsafe certificate changes.
- [/] **SMALL-04 | HIGH | Keep one automated software-supply-chain gate.** Retain the existing CodeQL, SonarCloud, dependency review, NuGet checks, attestations, and [Trivy](https://trivy.dev/docs/latest/target/filesystem/) scans. Expand Dependabot to every supported dependency ecosystem or replace it with self-hosted [Renovate](https://docs.renovatebot.com/). Do not run both bots for the same dependency source. Require an owner and remediation deadline for every high or critical result.
- [ ] **SMALL-05 | CRITICAL | Enforce the production Kubernetes baseline.** Add [Kyverno Pod Security and best-practice policies](https://kyverno.io/docs/guides/security/) to block privileged application Pods, missing security contexts, mutable image tags, unapproved registries, unnecessary service-account token automount, and unsigned or unattested release images after CI signing is implemented. Start in audit mode, document necessary controller exceptions, fix existing violations, then move required policies to fail closed.
- [/] **SMALL-06 | CRITICAL | Use one secrets strategy.** Keep cert-manager for certificates. For application secrets, either complete [Sealed Secrets](https://github.com/bitnami-labs/sealed-secrets) with controller-key backup and rotation, or use [External Secrets](https://external-secrets.io/latest/) with a managed cloud secret store. A two-to-three-person team should not self-host Vault or OpenBao unless customer requirements justify the operational burden. Rotate privileged secrets and test recovery before production.
- [/] **SMALL-07 | CRITICAL | Use one observable operations stack.** The current path is Graylog for logs plus Prometheus, [Alertmanager](https://prometheus.io/docs/alerting/latest/alertmanager/), and Grafana for metrics, availability, and notifications. Add the vendor-neutral [OpenTelemetry Collector](https://opentelemetry.io/docs/collector/) when multiple services or signals need common batching, retry, filtering, or routing; it is not a storage or alerting backend. [OpenObserve](https://openobserve.ai/docs/) is a contextual alternative that can consolidate logs, metrics, traces, dashboards, and alerts. Do not operate both complete backends. Before selecting OpenObserve, prove retention, backup and restore, alert delivery, expected query performance, upgrade recovery, and access isolation. Its open-source edition does not provide per-stream RBAC, so it is unsuitable for sensitive or separated log access unless another enforceable boundary compensates for that limitation.
- [ ] **SMALL-08 | CRITICAL | Automate backup and prove restore.** Enable the [CloudNativePG Barman Cloud Plugin](https://cloudnative-pg.io/plugin-barman-cloud/docs/intro/) for encrypted base backups, WAL archiving, and point-in-time recovery to an off-cluster object store. Set the schedule from the approved RPO. Alert on every failed backup and perform a monthly isolated restore reviewed by the non-operator. Back up required MongoDB and Graylog state through supported native procedures.
- [ ] **SMALL-09 | CRITICAL | Prepare and test response.** Keep one incident runbook and one private incident template. Run a quarterly tabletop, a monthly alert-delivery test, and an [OWASP ZAP baseline scan](https://www.zaproxy.org/docs/docker/baseline-scan/) against staging for release candidates. Use an independent penetration tester before the first examination and after material exposure changes.
- [ ] **SMALL-10 | HIGH | Automate evidence, not judgment.** A scheduled workflow should collect required CI results, release digests, vulnerability summaries, backup results, certificate status, deployment revision, and alert-test result into access-controlled evidence storage. Nora, or the assigned non-operator reviewer, reviews the monthly package; Lina reviews risk, access, vendors, incidents, and continuity quarterly.

### Automation and human checkpoints

| Control | Automate | Human checkpoint | Evidence retained |
|---|---|---|---|
| Pull requests | Existing CI, CodeQL, Trivy, tests, CodeRabbit comments, CODEOWNERS | Non-author resolves findings and approves | Pull request, reviews, required checks, release link |
| Dependency and image risk | Dependabot or Renovate, Trivy, attestations, Kyverno image policy | Nora, or the assigned security reviewer, reviews high and critical findings weekly | Finding, owner, deadline, fix or expiring risk acceptance |
| Access | Scheduled export of GitHub, cloud, Argo, Kubernetes, registry, database, and Graylog memberships | Lina and Alan recertify quarterly; Nora prepares the population; remove access immediately on departure | Export, decision, removals, approval |
| Certificates and secrets | cert-manager renewal, expiry alerts, secret rotation reminders | Non-operator verifies rotation and recovery | Renewal status, rotation record, recovery result |
| Availability | Probes, Prometheus rules, Alertmanager routing, Graylog ingestion alerts | Secondary on-call acknowledges a monthly test alert | Alert rule, delivery, acknowledgement, correction |
| Backups | Scheduled backup, WAL archiving, failure alert, retention enforcement | Non-operator witnesses monthly restore | Backup result, restored version, timing, RPO and RTO result |
| Audit evidence | Scheduled evidence export and immutable or versioned storage | Nora or assigned reviewer monthly; Lina quarterly sign-off | Manifest of evidence, review record, exceptions |

### Current tools and approved alternatives

Commercial and managed tools are acceptable when they reduce operating risk. Record the service owner, selected plan or license, data sent to the provider, retention, access, renewal date, outage fallback, and exit procedure in the vendor register. Do not replace a functioning control only to make the stack open source.

| Control | Current selection and status | Delivery model | Recommendation | Reliable alternative and adoption trigger |
|---|---|---|---|---|
| Static application security | [CodeQL](https://docs.github.com/en/code-security/code-scanning/introduction-to-code-scanning/about-code-scanning-with-codeql) is active in pull-request, branch, preview, and release workflows | GitHub-managed. Public-repository use and private-repository GitHub Code Security licensing differ, so confirm the actual organization entitlement | Keep it required and retain SARIF results. Verify branch rules reject failed or missing scans | [Semgrep Community Edition](https://semgrep.dev/products/community-edition/) is a self-hosted alternative if CodeQL cost, availability, or repository policy becomes unacceptable. Prove equivalent C# and JavaScript coverage on a fixed vulnerability corpus before replacement |
| Code quality and secondary analysis | SonarQube Cloud, using the existing SonarCloud endpoint, is active on protected branch and release workflows | Managed subscription service; selected plan was not verified | Keep it while quality gates are reliable and the vendor review is current. Avoid duplicating CodeQL findings without an assigned response process | [SonarQube Community Build](https://docs.sonarsource.com/sonarqube-community-build/) is the self-hosted alternative when code residency or service dependency requires it. The team then owns patching, database backup, availability, and upgrades |
| Advisory code review | CodeRabbit is configured for automatic DRN-specific review | Managed commercial service; selected plan was not verified | Keep it advisory. A non-author remains responsible for approval, and deterministic CI remains authoritative | Human peer review plus existing CI is the reliable fallback. Do not add another AI reviewer unless measured recall, false positives, privacy, and cost are better |
| Application identity | [ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity) is implemented in Sample and Nexus with application-owned user stores and MFA work in progress | Open-source framework components operated inside DRN | Keep it for the current small team and product boundary. Complete bearer-token MFA semantics, Nexus MFA, recovery, privileged-role review, security-stamp handling, and identity-event logging | [Keycloak](https://www.keycloak.org/) is the preferred open-source alternative when DRN needs centralized SSO across several applications, OpenID Connect or SAML federation, external identity brokering, or centralized session administration. It is not a simple library swap. Adoption requires an OIDC migration plan, HA, PostgreSQL backup, upgrades, realm recovery, admin separation, and monitoring. A managed identity provider is the lower-operations alternative when budget permits |
| Dependency updates | Dependabot is configured for weekly GitHub Actions updates | GitHub-managed | Expand it to supported NuGet, npm, container, and GitHub Actions ecosystems if it meets repository needs | Use self-hosted [Renovate](https://docs.renovatebot.com/) instead when one configurable bot must cover both repositories. Never run both against the same package source |
| Container and manifest vulnerabilities | Docker Scout scans staged application images; Trivy scans GitOps files, manifests, secrets, and infrastructure configuration | Docker Scout is managed and plan-dependent; Trivy is open source and locally runnable | Keep each only for its demonstrated coverage. Define one severity policy and one finding register so duplicate alerts do not become separate controls | Standardize on [Trivy](https://trivy.dev/docs/latest/) if it proves equivalent multi-architecture image and repository coverage. Grype with Syft is a credible image and SBOM alternative, but migration is not currently justified |
| Logs, metrics, traces, and alerts | Graylog is under development. Prometheus, Alertmanager, and Grafana are required complements. OpenTelemetry Collector is not yet implemented | Primarily self-hosted components with mixed licenses; confirm the deployed Graylog edition and license | Complete the current stack if separate components remain understandable for the team. Add the Collector only for a defined routing, filtering, retry, or tracing need | OpenObserve is a consolidated self-hosted or managed alternative after a production-like proof of concept. Do not adopt its open-source edition where per-stream RBAC is required. Do not retain both full stacks after migration |
| Kubernetes policy | No production admission baseline is implemented | [Kyverno](https://kyverno.io/docs/) is open source and cluster-operated | Add Kyverno in audit mode, assign policy ownership, document controller exceptions, then enforce approved policies | Gatekeeper is reliable if Rego expertise already exists. Running both policy engines adds operational risk and is not recommended for this team |
| Certificates and application secrets | cert-manager and trust-manager exist; Sealed Secrets controller use is incomplete | Open-source, cluster-operated | Keep cert-manager for certificates. Complete one application-secret lifecycle with tested rotation and controller-key recovery | Use [External Secrets](https://external-secrets.io/latest/) with a managed secret store when centralized lifecycle and provider audit logs justify it. Do not run two secret sources of truth |
| PostgreSQL | CloudNativePG is the current operator; HA, backups, and restore evidence are incomplete | Open-source, cluster-operated | Keep it only if the team can prove upgrades, alerts, off-cluster Barman backups, point-in-time recovery, and restore | Use managed PostgreSQL when reduced database operations outweigh cost and portability concerns. The provider does not remove DRN's access, configuration, recovery, and evidence duties |
| Compliance administration | Markdown registers, GitHub issues, calendar reminders, and protected evidence storage are sufficient initially | Existing repository and collaboration services | Keep the process small and reviewable | Adopt a managed GRC platform only when auditor requests, customer questionnaires, control count, or evidence volume cannot be handled reliably by the assigned owners |

Tool alternatives are contingencies, not backlog items. Select one primary tool per control, document the decision, and avoid parallel platforms unless a time-limited migration plan requires them.

Adding a tool does not close a control. The tool must fail visibly, have an owner, generate reviewable evidence, and have a tested recovery or fallback.

## 9. Evidence and operating cadence

Use an evidence register with these minimum fields: control ID, owner, performer, reviewer, frequency, population, sample, source system, evidence link, execution date, result, exception, remediation, approval, and retention date.

| Frequency | Minimum evidence |
|---|---|
| Per change | Approved issue or change record, pull request, reviews, required CI results, security findings, artifact digest or attestation, GitOps revision, deployment result, post-change verification |
| Daily or continuous | Service health, alert delivery, log ingestion, backup result, certificate state, vulnerability and drift alerts, incident queue |
| Monthly | Vulnerability aging, privileged access changes, backup success, restore sample where appropriate, capacity, control failures, exception expiry |
| Quarterly | Access recertification, risk review, vendor changes, incident and alert trend, business continuity readiness, management oversight |
| Annual and after material change | Policies, risk assessment, threat model, business impact analysis, vendor review, penetration test, incident exercise, disaster recovery exercise, security training |

Evidence retention must cover the complete examination period and the auditor's sampling and reporting needs. Set the exact period with legal, contractual, privacy, and auditor input. Do not retain sensitive evidence longer than justified.

## 10. Actions after checklist completion

Completing every item is the start of assurance, not the end of control operation.

1. Freeze the proposed scope and obtain executive, legal, privacy, and CPA feedback.
2. Run an independent readiness assessment and remediate every critical or high exception.
3. Produce the final system description, management assertion inputs, control matrix, subservice treatment, and complementary user-entity controls.
4. Prove production operation with live security, availability, restore, failover, alert, access, and incident tests.
5. Select a CPA firm and confirm Trust Services Categories, Type I or Type II, materiality, evidence format, sampling, and examination dates.
6. For Type I, preserve point-in-time implementation evidence and resolve design gaps before the specified date.
7. For Type II, start the observation period only after controls are stable. Monitor missed controls and preserve complete populations for sampling.
8. Perform a mock evidence pull before fieldwork. Confirm every sample is attributable, dated, approved, complete, and retrievable.
9. Respond to auditor requests through a controlled evidence room. Track questions, exceptions, and management responses.
10. After report issuance, restrict report distribution as advised, remediate exceptions, monitor bridge-letter needs, and continue controls without interruption.
11. Reassess scope after major product, infrastructure, vendor, privacy, acquisition, incident, or geographic changes.

## Appendix A. Architecture, limitations, and alternatives

| Component | Preparedness value | Current limitation | Alternative or required complement |
|---|---|---|---|
| Argo CD | Declarative changes, reconciliation, reviewable deployment history | Configuration does not prove live RBAC, approval, drift response, or successful deployment | Flux is an alternative. Either requires protected Git, controlled admin access, alerts, evidence, and rollback |
| Linkerd | Workload identity and service-to-service mTLS when correctly injected and healthy | Selected edge compatibility is not proven for production; coverage and policy were not live-tested | A supported Linkerd release, Istio, or Cilium service mesh can satisfy similar goals. NetworkPolicy, edge TLS, and monitoring are still required |
| cert-manager and trust-manager | Automated certificate issuance, renewal, and trust distribution | Does not manage arbitrary application secrets; long-lived trust anchors require a tested rotation plan | Cloud CA, Vault PKI, or another approved PKI can be used. Monitor expiry and test overlap rotation |
| Sealed Secrets | Encrypts selected Kubernetes Secret manifests for repository storage | Controller declaration exists, but no SealedSecret use, rotation, recovery, or access evidence was found | External Secrets with a cloud secret manager or Vault generally provides stronger centralized lifecycle and audit options |
| CloudNativePG | Operator-managed PostgreSQL lifecycle and least-privilege application roles | One instance, PDB disabled, backups disabled, and no restore evidence | Managed PostgreSQL may reduce operational burden. Responsibility for access, configuration, backups, recovery, and evidence remains |
| Graylog | Centralized structured logs, search, retention, and alerting when configured | Optional, single-node, manual GELF input, and no evidenced retention, alert, backup, or access-review policy | OpenTelemetry alone is not a Graylog replacement. OpenObserve can consolidate logs, metrics, traces, dashboards, and alerts after a successful proof of concept, but its open-source edition lacks per-stream RBAC. Other alternatives include OpenTelemetry Collector with Loki, Tempo, Prometheus, Grafana, Elastic, OpenSearch, or a managed SIEM |
| OpenTelemetry | Standard instrumentation and transport for traces, metrics, and logs | DRN currently exposes some metrics but does not configure an exporter or backend | Use it to complement or feed Graylog and monitoring backends. Define sampling, sensitive-data filters, retention, and alert ownership |
| DRN Nexus | Intended service discovery, remote settings, topology, and internal trust | Material functions remain under development and identity hardening is incomplete | Use Kubernetes-native discovery and approved configuration or secret systems until Nexus controls are complete |

Tool choice does not create SOC 2 compliance. The control objective, ownership, reliable operation, review, evidence, and correction process matter.

## Appendix B. Minimum evidence package

- Approved scope, Trust Services Categories, system description, architecture, data flows, asset inventory, and subservice inventory.
- Control matrix with owner, frequency, evidence, reviewer, exceptions, and mappings.
- Approved policies, annual reviews, training, confidentiality agreements, onboarding, role-change, and offboarding evidence.
- Risk assessment, threat model, business impact analysis, vendor assessments, exceptions, and treatment plans.
- User and privileged-access populations, approvals, MFA settings, access reviews, removals, and break-glass tests.
- Pull-request populations, CI results, code-scanning results, release attestations, image digests, GitOps revisions, deployment approvals, and rollback records.
- Production configuration exports for GitHub rulesets, cloud IAM, Argo RBAC, Kubernetes RBAC, admission controls, NetworkPolicies, TLS, secret systems, databases, logging, monitoring, and backups.
- Vulnerability scans, penetration test, remediation evidence, patch records, and risk acceptances.
- Log review, alert tests, on-call acknowledgement, incidents, exercises, lessons learned, and corrective actions.
- Backup jobs, off-cluster storage controls, restore results, failover tests, disaster recovery exercise, and measured RTO and RPO.
- Customer commitments, availability reports, support and incident communications, privacy notice, retention schedules, data-subject requests, and deletion evidence where applicable.
- Vendor contracts, data-processing terms, assurance reports, annual reviews, subprocessors, and exit plans.

## Appendix C. Reviewed evidence

### DRN application repository

- [`ROADMAP.md`](ROADMAP.md), project and Nexus maturity.
- [`SECURITY.md`](SECURITY.md), current vulnerability contact and stated immaturity.
- [`Sample.Hosted/SampleProgram.cs`](Sample.Hosted/SampleProgram.cs), Sample MFA redirection and bearer exemption.
- [`DRN.Nexus.Hosted/Program.cs`](DRN.Nexus.Hosted/Program.cs), Nexus bearer exemption and incomplete MFA redirection.
- [`DRN.Framework.Hosting/DrnProgram/DrnProgramBase.cs`](DRN.Framework.Hosting/DrnProgram/DrnProgramBase.cs), security headers, CSP, cookie policy, host filtering, MFA fallback, rate limiting, and pipeline defaults.
- [`Sample.Hosted/SampleModule.cs`](Sample.Hosted/SampleModule.cs), Identity and encrypted, database-persisted Data Protection keys.
- [`Sample.Infra/DataProtection/SampleXmlEncryptor.cs`](Sample.Infra/DataProtection/SampleXmlEncryptor.cs), AES-GCM Data Protection key encryption.
- [`Sample.Hosted/appsettings.Staging.json`](Sample.Hosted/appsettings.Staging.json), staging examples requiring production replacement.
- [`.coderabbit.yaml`](.coderabbit.yaml), automated advisory review and path-specific DRN security and architecture instructions.
- [`.github/workflows/pull-request.yml`](.github/workflows/pull-request.yml), secretless PR trust boundary, CodeQL, dependency review, and gatekeeper.
- [`.github/workflows/release.yml`](.github/workflows/release.yml), release verification, scans, attestations, and digest promotion.
- [`.github/CODEOWNERS`](.github/CODEOWNERS), CI and dependency control-plane ownership.

### DRN GitOps repository

The following paths refer to the separately reviewed `DRN-Project-Argo-CD-Gitops` working tree. Existing local changes were not modified by this assessment.

- `.agent/repository-profile.md`, development-only stateful policy, security contracts, compatibility limits, and validation boundaries.
- `.github/workflows/pull-request.yml`, trusted base-SHA actions, Trivy, CodeQL, manifest validation, and gatekeeper.
- `.github/actions/validate-manifests/action.yml`, closed-set application and manifest contract validation.
- `apps/*-project.yaml` and `infrastructure/argocd/shared/default-project.yaml`, scoped AppProjects and default deny.
- `services/sample/**` and `services/nexus/**`, Linkerd injection, service accounts, probes, resources, mounted settings, mutable DRN image tags, and PostgreSQL Secret mounts.
- `infrastructure/postgresql/postgresql.yaml`, one PostgreSQL instance, distinct application roles, disabled PDB, and disabled backups.
- `infrastructure/cert-manager/**` and `infrastructure/linkerd/**`, certificate and mesh trust configuration.
- `infrastructure/sealed-secrets/sealed-secrets.yaml`, Sealed Secrets controller declaration.
- `infrastructure/graylog/**`, Graylog, Data Node, MongoDB TLS, service accounts, Secrets, probes, resources, and single-node state.
- `gateway/base/gateway.yaml` and `infrastructure/traefik/traefik-gateway.yaml`, HTTP-only development Gateway and disabled `websecure` exposure.

### Review limitations

- No build, test, application, container, manifest render, live deployment, or destructive command was run.
- No GitHub, cloud, Kubernetes, Argo CD, database, Graylog, certificate, secret-store, HR, legal, vendor, ticketing, or monitoring account was queried.
- No claim marked Done should be interpreted as proof of production deployment, policy approval, or Type II operating effectiveness.

## Appendix D. Fictional end-to-end example for a three-person SaaS

> **Illustrative notice:** Northstar Answers Ltd. and every person, event, control result, date, and customer commitment below are fictional. This example is not legally binding, is not audit or legal advice, does not guarantee a SOC 2 result, and must not be represented as an actual control environment. A SOC 2 examination is performed by an independent licensed CPA firm against the company's real system description, controls, evidence, and operating history.

### D.1 Purpose and source boundary

This example shows one credible operating model for a small private wiki and question-and-answer SaaS similar to the intended direction of `Sample.Hosted`. It is a composite inspired by the [AICPA Trust Services Criteria resources](https://www.aicpa-cima.com/topic/audit-assurance/audit-and-assurance-greater-than-soc-2/), [Atlassian's published security practices](https://www.atlassian.com/trust/security/security-practices), [GitLab's public access-review procedure](https://handbook.gitlab.com/handbook/security/security-assurance/security-compliance/access-reviews/), and the technical guidance listed in D.11. Atlassian and GitLab are much larger organizations. Their public material is used only as reference for control concepts, not as proof that the smaller model is sufficient or that those organizations endorse it.

### D.2 Company, service, and examination scope

Northstar Answers operates one product: a business-to-business knowledge base where customer organizations create private wiki pages, ask questions, publish answers, attach files, search their content, and manage members. It has three full-time people, no office, no internal network, no self-hosted physical servers, and no separate security or operations department.

| Subject | Defined position |
|---|---|
| Customers | Small businesses using private organization workspaces |
| Data | Account identity, organization membership, questions, answers, wiki content, attachments, audit events, support records, and billing references. Payment-card data is handled by a payment provider and never enters Northstar systems |
| Prohibited data | Customers are contractually told not to store payment-card data, health records, government secrets, or other regulated high-impact data unless a later written agreement and risk assessment explicitly permit it |
| In-scope system | People, policies, endpoints, source repositories, CI/CD, production Kubernetes, DRN application services, PostgreSQL, object storage, logging, monitoring, backups, customer support, and relevant vendors |
| Production boundary | One production environment and a logically separate staging environment. Production data is never copied to development or staging |
| Trust Services Categories | Security, Availability, and Confidentiality. Privacy and Processing Integrity are excluded from the illustrative report unless the CPA and customer commitments require them |
| Availability commitment | 99.5% monthly application availability, measured at the authenticated service edge, with exclusions and maintenance terms defined in the customer agreement |
| Recovery objectives | RPO of 15 minutes and RTO of 4 hours, accepted only after repeated recovery tests demonstrate both targets |
| Report objective | Type II examination after the controls operate consistently for the period agreed with the CPA firm. A Type I examination may be used first when customers need an earlier design assessment |

Excluding Privacy from the illustrative SOC 2 scope does not remove privacy-law or contractual duties. Northstar still maintains a privacy notice, data-processing terms, subprocessors, retention rules, and request procedures. It adds the Privacy category only when its commitments and evidence are mature enough for examination.

### D.3 People and separation of duties

| Person | Primary ownership | Activities they cannot approve alone |
|---|---|---|
| Lina, CEO and Compliance Lead | Risk, policies, contracts, vendors, privacy, customer commitments, personnel, annual management assertion, and final risk acceptance | Their own privileged access, vendor exception, expense-related vendor choice, or emergency action |
| Alan, CTO and Software Architect | Architecture, production operation, releases, availability, backups, recovery, capacity, and incident command | Their own source or GitOps change, their own access grant, or their own restore-test conclusion |
| Nora, Software Engineer and Security Reviewer | Product development, application changes, security review, vulnerability triage, access-review preparation, evidence packaging, and control monitoring | Their own source change, their own access grant, or a risk exception they requested |
| External CPA firm | Readiness advice when separately permitted and the independent SOC 2 examination under the firm's independence rules | Operating Northstar controls or creating evidence on management's behalf |
| Independent penetration tester | Annual and material-change application and infrastructure testing | Fixing the tested findings or approving management's risk acceptance |

The author never approves their own pull request. Alan and Nora review each other's technical changes. Lina approves policy, vendor, and residual-risk decisions. A single person may take emergency action only during an active incident; another person reviews the action and evidence by the next business day.

For a two-person company, both people must be capable of technical review and must review each other's changes and access. An external security adviser samples sensitive changes, access decisions, recovery tests, and exceptions at least quarterly. If neither founder can independently review a control, the control is redesigned, automated with a separate human checkpoint, or externally reviewed. The company does not claim separation that does not exist.

### D.4 Implemented service architecture

Within this fictional story, the following target state is fully implemented and operating. It does not change any DRN checklist status and is not evidence that the current DRN repositories or a live environment have reached this state. Northstar selects one tool for each control and avoids parallel platforms without a migration plan.

| Layer | Implemented design |
|---|---|
| Application | `Sample.Hosted`-style ASP.NET Core application with organization-scoped wiki, question, answer, attachment, search, administration, and audit endpoints |
| Customer identity | ASP.NET Core Identity with confirmed email, secure cookies for browsers, tested bearer-token rules for APIs, MFA for customer administrators, recovery codes, lockout, security-stamp invalidation, and organization roles. Keycloak is not operated because there is one product and no federation requirement |
| Tenant isolation | Every tenant-owned row includes an immutable organization identifier. Authorization checks the authenticated membership before every read, write, search, export, and attachment operation. Automated negative tests attempt cross-tenant access |
| Edge and workload security | Kubernetes with Traefik Gateway API, external TLS issued by cert-manager, Linkerd workload identity and mTLS, default-deny NetworkPolicies, dedicated service accounts, restricted security contexts, read-only filesystems where supported, and resource limits |
| Admission control | Kyverno blocks privileged application Pods, mutable production image tags, unapproved registries, absent required security contexts, and unapproved service-account token mounts |
| Deployment | Argo CD reconciles reviewed GitOps manifests. Production runs immutable image digests. Direct cluster changes are denied except documented break-glass response |
| Database | CloudNativePG with multiple production instances across failure domains, disruption protection, encrypted persistent storage, least-privilege application roles, and monitored replication |
| Backup | Barman Cloud writes encrypted base backups and continuous WAL archives to a separate cloud account or failure domain with retention controls. Monthly isolated restores prove recovery |
| Attachments | Private object storage with tenant-prefixed keys, server-side encryption, short-lived signed access, malware scanning, size and type limits, versioning, and lifecycle deletion |
| Certificates and secrets | cert-manager handles certificates. Sealed Secrets is the single application-secret workflow, with controller-key backup, restricted decryption access, rotation records, and tested cluster-recovery procedure |
| Logs | Graylog stores security, administration, application, database, gateway, and Kubernetes events under documented retention and access rules |
| Metrics and alerts | Prometheus, Alertmanager, and Grafana monitor availability, latency, error rate, saturation, certificates, backups, replication, workload restarts, and telemetry health |
| Telemetry routing | OpenTelemetry Collector receives, filters, batches, retries, and routes application telemetry. It is not treated as storage. Sensitive fields are removed before export |
| Source and CI | GitHub rulesets, CODEOWNERS, CodeQL, SonarQube Cloud, dependency review, Dependabot, CodeRabbit advisory comments, unit and integration tests, Docker Scout, Trivy, ZAP baseline testing, signed attestations, and digest-based promotion |
| Workforce systems | Individually assigned business-email, source-control, cloud, password-manager, and support accounts with phishing-resistant MFA where available. Company endpoints use managed encryption, screen lock, supported operating systems, automatic patching, endpoint protection, and remote removal |
| Physical responsibility | Remote-work rules prohibit shared endpoints, unattended exposure, and local production-data storage. Cloud and SaaS providers operate physical infrastructure; Northstar reviews their relevant assurance reports and complementary user-entity controls |
| Evidence | A separate access-controlled object-storage bucket retains dated evidence packages and manifests. Repository records link to evidence but sensitive audit material is not committed to source control |

### D.5 Customer commitments and shared responsibility

Northstar promises only what it can measure and evidence:

- Customer data is encrypted in transit and at rest.
- Customer workspaces are logically isolated and private by default.
- Staff production access is restricted, logged, reviewed, and used only for approved support, maintenance, security, or incident purposes.
- Availability is measured against the written 99.5% commitment.
- Backups, recovery, incident handling, vulnerability management, and customer notification follow documented procedures.
- Content is retained and deleted according to the agreement and configured retention policy.
- Material subprocessors are listed, reviewed, and communicated under the contract.

Customer responsibilities are also written into the agreement and system description. Customer administrators manage membership, remove departed users, configure available MFA and SSO options, classify uploaded content, protect exported data, and report suspected compromise. Northstar does not use customer responsibilities to excuse failures in its own service.

### D.6 Security and Common Criteria story

Security is the mandatory foundation of the illustrative scope. It includes governance, risk, personnel, access, system operations, change management, monitoring, incident response, and vendor oversight. It is not limited to scanners and firewalls. The exact control wording is agreed with the CPA.

| ID | Security control operation | Owner and frequency | Example evidence |
|---|---|---|---|
| SEC-01 | Management approves scope, commitments, policies, control owners, and unresolved exceptions | Lina annually and after material change | Signed approval, system boundary, policy version, exception list |
| SEC-02 | Management reviews risks, incidents, vendors, vulnerabilities, control failures, and corrective actions | Lina with Alan and Nora quarterly | Meeting record, risk-register changes, decisions, assigned actions |
| SEC-03 | Workers sign confidentiality and acceptable-use terms, complete security training, and receive role-specific access | Lina at onboarding and annually | Agreement, training record, onboarding checklist, approved access |
| SEC-04 | Remote-work and endpoint controls prohibit shared devices and local production-data storage and enforce encryption, lock, patching, protection, and inventory | Nora continuously; Alan reviews monthly | Device inventory, compliance export, acknowledgement, remediation |
| SEC-05 | Individual accounts and MFA are required; privileged access is approved, time-bounded where possible, and logged | Lina approves; Nora monitors continuously | Access request, MFA status, role assignment, privileged audit event |
| SEC-06 | Workforce and service access is recertified; unnecessary access is removed immediately | Nora prepares; Lina and Alan review quarterly and at departure | Complete population, decisions, removals, completion timestamp |
| SEC-07 | Every normal source and GitOps change uses a linked issue, non-author approval, required checks, and an immutable release reference | Alan or Nora per change | Issue, pull request, review, checks, image digest, GitOps revision |
| SEC-08 | Emergency changes are limited to active incidents, recorded immediately, validated, and independently reviewed | Incident commander per event; peer by next business day | Incident link, commands or diff, validation, review, retrospective |
| SEC-09 | Security requirements and abuse cases are defined for authentication, authorization, administration, uploads, exports, and deletion | Alan per feature; Nora reviews | Threat note, acceptance criteria, security tests, review comments |
| SEC-10 | Code, dependencies, secrets, images, manifests, and staging are scanned; findings have an owner and due date | CI continuously; Nora triages weekly | CodeQL, Sonar, Trivy, Scout, ZAP and dependency results |
| SEC-11 | Critical and high vulnerabilities meet approved deadlines or receive documented, expiring risk acceptance | Nora weekly; Lina accepts residual risk | Finding age, fix, retest, compensating control, approval, expiry |
| SEC-12 | Production configuration is declarative, reviewed, policy-checked, reconciled, and monitored for drift | Alan continuously; Nora reviews changes | GitOps diff, Kyverno result, Argo history, drift alert, rollback |
| SEC-13 | Secrets and certificates are inventoried, access-restricted, monitored, rotated, and recoverable | Alan continuously; Nora reviews quarterly | Inventory, expiry alert, rotation record, recovery test |
| SEC-14 | Security events are centralized, protected from ordinary modification, retained, and connected to actionable alerts | Alan continuously; Nora tests monthly | Log-source inventory, test event, alert, acknowledgement, retention |
| SEC-15 | Security incidents follow severity, command, containment, evidence, communication, recovery, and lessons-learned procedures | Assigned commander per event; tabletop quarterly | Incident record, timeline, decisions, notices, retrospective, actions |
| SEC-16 | New and material vendors receive security, privacy, contractual, availability, and exit review; critical vendors are reassessed | Lina before use and annually | Vendor register, due diligence, assurance review, contract, decision |
| SEC-17 | The evidence manifest is generated and reviewed; missed controls become exceptions rather than recreated evidence | Nora monthly; Lina reviews quarterly | Evidence manifest, reviewer sign-off, exception, remediation ticket |

#### Security-controlled change story

Northstar adds private answer bookmarks:

1. Nora opens an issue describing behavior, organization ownership, authorization, audit events, retention, abuse cases, tests, rollback, and monitoring.
2. Alan confirms the feature does not alter scope, introduce a vendor, or process a new data class. Lina joins only if a commitment or accepted risk changes.
3. Nora implements unit, integration, authorization, tenant-isolation, and migration tests.
4. The pull request runs secretless tests, CodeQL, dependency review, SonarQube Cloud, and CodeRabbit advisory review. CodeRabbit never substitutes for approval.
5. Alan reviews the code, migration, authorization boundary, telemetry, and rollback. Rulesets reject self-approval, failed checks, stale approvals, and direct pushes.
6. Release CI rebuilds the reviewed commit, scans each image, creates an SBOM and provenance attestation, verifies the release according to policy, and records the digest.
7. Alan opens the digest-pinned GitOps pull request. Nora approves it after manifest, Kyverno, and policy checks pass.
8. Argo CD deploys to staging. ZAP baseline, smoke, migration, authorization, and observability tests pass before the same digest is promoted.
9. Argo CD reconciles production. Kyverno admits only compliant workloads. External checks and telemetry confirm service health and event flow.
10. The evidence workflow records both pull requests, approvals, checks, digest, attestations, deployment revision, and post-deployment result.

For two people, the author and reviewer still differ. Emergency procedure cannot be used merely to release faster.

#### Security vulnerability story

Dependabot or another scanner opens a critical finding. Nora validates exposure the same day and creates a security ticket. If exploitable, Alan restricts or disables the affected path while Nora prepares the fix. The non-author reviews the fix, CI retests it, and the immutable release is deployed. Lina may approve only a documented, expiring exception when immediate remediation is demonstrably riskier. Evidence covers discovery, severity, exposure, containment, fix, retest, deployment, and closure.

#### Security incident story

Graylog detects a privileged login from an unexpected source. Nora revokes sessions and tokens, preserves events, and opens the incident record. Alan restricts administrative access, checks production changes, and rotates affected credentials. Lina coordinates contractual, insurer, legal, and customer decisions using verified facts. The team restores normal access only after containment and validation, completes a retrospective, and tracks corrections. The procedure follows the risk-integrated response concepts in [NIST SP 800-61 Rev. 3](https://csrc.nist.gov/pubs/sp/800/61/r3/final).

### D.7 Availability story

Availability is included because Northstar promises 99.5% monthly availability and tested recovery objectives. The criteria do not require zero downtime. They require commitments grounded in capacity, resilience, monitoring, response, recovery, and evidence.

| ID | Availability control operation | Owner and frequency | Example evidence |
|---|---|---|---|
| AVL-01 | External availability, latency, error rate, saturation, and critical dependency health are monitored and alerted | Alan continuously; Lina reviews monthly | Uptime report, SLI dashboard, alert, acknowledgement, service review |
| AVL-02 | Production uses tested redundancy, disruption protection, capacity thresholds, and scaling limits without a known single application or database instance | Alan continuously; Nora reviews quarterly | Topology, capacity report, failover result, exception record |
| AVL-03 | Database and attachment backups run automatically to a separate failure domain; failures alert; retention is enforced | Alan continuously; Nora reviews monthly | Backup job, destination control, failure alert, retention setting |
| AVL-04 | Isolated restoration measures actual RPO and RTO; disaster recovery exercises test the wider dependency sequence | Alan restores; Nora witnesses monthly; all exercise annually | Restore record, checksums, measured RPO and RTO, exercise actions |
| AVL-05 | Business impact and continuity plans define priority, contacts, alternate access, provider failure, personnel absence, customer communication, and return to normal operation | Lina annually and after material change | Impact analysis, continuity plan, exercise, corrections, approval |

#### Availability recovery story

CloudNativePG reports an unrecoverable database failure. Alertmanager pages Alan and Nora. Alan declares an incident and stops writes. Nora confirms the last valid backup and WAL position without changing them. Alan restores into an isolated replacement cluster, validates tenant counts and selected checksums, and switches traffic only after Nora approves validation. The team records actual RPO, RTO, customer impact, communications, and corrective action. A missed objective becomes a risk and remediation item; it is not omitted from evidence.

At month end, Lina compares measured availability with the customer commitment and reviews exclusions, incidents, and unresolved capacity risks. The source monitor report and incident records, rather than a manually edited spreadsheet, form the evidence population.

### D.8 Confidentiality story

Confidentiality is included because private customer knowledge and attachments are explicitly designated confidential. This story addresses approved collection, tenant isolation, encryption, restricted access, controlled disclosure, retention, and disposal. It does not claim the Privacy category, although privacy obligations still apply.

| ID | Confidentiality control operation | Owner and frequency | Example evidence |
|---|---|---|---|
| CON-01 | Data types, owners, locations, classification, retention, and prohibited-data rules are documented and reviewed | Lina annually; Alan reviews material features | Data inventory, classification, data flow, approved requirement |
| CON-02 | Every tenant-owned record and object is organization-scoped; authorization and negative tests prevent cross-tenant access | Alan continuously; Nora reviews per change and monthly | Authorization design, test results, denied event, audit sample |
| CON-03 | Confidential data is encrypted in transit and at rest; keys, secrets, and certificate access are restricted and rotated | Alan continuously; Nora reviews quarterly | TLS result, storage setting, key access, rotation and recovery record |
| CON-04 | Attachments, exports, support access, and administrative actions use least privilege, short-lived access, malware controls, and audit events | Alan continuously; Nora samples monthly | Signed-access configuration, scan result, support approval, audit event |
| CON-05 | Confidential data is excluded from ordinary logs and non-production; telemetry filtering and test-data rules are verified | Nora per change; Alan tests quarterly | Logging test, collector filter, staging sample, exception record |
| CON-06 | Customer data is retained, exported, corrected, and deleted according to contract and documented procedure | Lina owns terms; Alan operates per request; Nora verifies | Retention configuration, request ticket, export, deletion verification |
| CON-07 | Subprocessors receive only necessary data under reviewed confidentiality, security, deletion, incident, and exit terms | Lina before use and annually | Data flow, contract, assurance review, subprocessor notice, exit test |

#### Confidentiality incident story

Graylog alerts on repeated denied organization identifiers. Nora revokes the suspected sessions and preserves logs. Alan disables the affected endpoint if isolation is uncertain, queries audit events, and determines whether access succeeded. Lina owns legal, contractual, insurer, and customer notification decisions using verified facts and applicable deadlines. The team fixes the cause, searches for similar paths, expands cross-tenant tests, restores service, completes a retrospective, and tracks every corrective action.

The evidence must distinguish attempted access from confirmed disclosure. Northstar does not state that customer data was exposed unless the investigation supports it, and it does not state that no exposure occurred without sufficient logs and analysis.

Controls are placed under their primary category for readability, but several support more than one category. Access control supports Security and Confidentiality; incident response supports all three. Automation does not approve itself. The reviewer confirms population completeness, investigates failures, and records exceptions.

### D.9 Operating calendar

| Cadence | What actually happens |
|---|---|
| Continuous | CI and policy gates, health checks, log ingestion, vulnerability alerts, certificate monitoring, backup and replication monitoring, drift detection, and on-call delivery |
| Per change | Issue, risk and security requirements, non-author review, required checks, immutable artifact, GitOps deployment, verification, rollback reference, and evidence link |
| Daily | Primary checks unresolved pages, failed backups, certificate warnings, security findings, production drift, and open incidents; secondary confirms coverage |
| Weekly | Nora triages vulnerability and dependency queues; Alan reviews capacity and repeated alerts; overdue critical or high items escalate to Lina |
| Monthly | Non-operator witnesses a restore; alert delivery and log ingestion are tested; evidence package, endpoint compliance, availability, and backup trends are reviewed |
| Quarterly | Access recertification, risk and vendor changes, exception expiry, incident tabletop, on-call handoff, customer-commitment review, and management sign-off |
| Annually and after material change | Policies, scope, risk assessment, threat model, business impact, vendor due diligence, penetration test, disaster recovery exercise, training, and system description |

An activity without dated evidence is treated as not performed for examination purposes. Missing evidence is recorded as an exception; it is never reconstructed or backdated.

### D.10 Examination journey

1. **Commitments and scope:** Northstar inventories customer promises, vendors, data, systems, and responsibilities. It asks a CPA firm to validate the proposed categories, boundaries, control wording, subservice treatment, and evidence expectations.
2. **Gap closure:** The team completes production hardening, MFA, tenant-isolation testing, off-cluster backup, recovery, monitoring, incident response, vendor review, and evidence automation. An independent readiness review identifies design gaps.
3. **Design assessment:** If commercially useful, management chooses a Type I examination for a specified date. This does not prove operation over time.
4. **Stable operation:** Management starts the Type II period only after controls are working. It preserves complete populations, records every missed control and exception, and avoids changing control language without impact review.
5. **Fieldwork:** The CPA selects samples and tests design and operating effectiveness. Northstar provides source-system evidence through a controlled evidence room and answers questions without editing historical records.
6. **Results:** Management evaluates every exception and representation. Only the issued CPA report states the examination scope, period, criteria, tests, results, and opinion. Northstar does not describe itself as "SOC 2 certified."
7. **Continuation:** Controls continue after the period. Northstar fixes exceptions, monitors whether customers require a bridge letter, reassesses material changes, and prepares the next annual examination.

The fictional company is ready to begin fieldwork only when every in-scope control has an owner, frequency, complete population, review step, retained evidence, and tested exception path; access review and recovery tests are current; critical gaps are closed; customer commitments match measured performance; and the CPA agrees the system description is complete. Passing an internal checklist does not decide the examiner's opinion.

### D.11 Referenced public material

These references inform the example but are not substitutes for the Trust Services Criteria, professional advice, contracts, or evidence:

- [AICPA SOC 2 and Trust Services Criteria resources](https://www.aicpa-cima.com/topic/audit-assurance/audit-and-assurance-greater-than-soc-2/), for the authoritative SOC 2 categories and reporting context.
- [AICPA 2018 SOC 2 Description Criteria with revised implementation guidance, 2022](https://www.aicpa-cima.com/resources/download/get-description-criteria-for-your-organizations-soc-2-r-report), for the nine criteria governing management's system description.
- [Atlassian security practices](https://www.atlassian.com/trust/security/security-practices), for a public, large-company example of layered security, access control, secure development, vendor risk, resilience, and shared responsibility.
- [Atlassian Confluence and Whiteboards SOC 2 Type I report dated February 29, 2024](https://www.atlassian.com/dam/jcr%3A8cbbc4c3-66bd-4d83-9898-8b91cac3d6c1/Confluence-Databases-and-Confluence-Whiteboards-SOC-2-Type-1-HIPAA_29-Feb-2024.pdf), as a historical public example of report structure and control descriptions. It is point-in-time material belonging to Atlassian and cannot be reused as Northstar or DRN evidence.
- [GitLab access-review procedure](https://handbook.gitlab.com/handbook/security/security-assurance/security-compliance/access-reviews/), for a public example of access-review ownership and audit relevance.
- [GitHub protected branch documentation](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches), for enforceable review and status-check examples.
- [OWASP ASVS](https://owasp.org/www-project-application-security-verification-standard/), for testable web-application security requirements, including authentication, authorization, validation, logging, data protection, and configuration.
- [OWASP SAMM](https://owasp.org/www-project-samm/), for risk-driven improvement across governance, design, implementation, verification, and operations.
- [NIST SP 800-61 Rev. 3](https://csrc.nist.gov/pubs/sp/800/61/r3/final), for incident-response integration with cybersecurity risk management.
- [NIST SP 800-34 Rev. 1](https://csrc.nist.gov/pubs/sp/800/34/r1/upd1/final), for business impact, contingency planning, recovery strategy, and exercises.
- [CIS Controls Implementation Groups](https://www.cisecurity.org/controls/implementation-groups), for prioritizing essential cyber hygiene before higher-complexity safeguards.
