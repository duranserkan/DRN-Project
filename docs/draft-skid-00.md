---
title: "Source Known Identifiers (SKIDs)"
docname: draft-kilic-skid-00
category: info
ipr: trust200902
area: Applications and Real-Time
workgroup: "Independent Submission"
keyword: identifier, UUID, GUID, security, deterministic
stand_alone: yes
pi:
  toc: yes
  sortrefs: yes
  symrefs: yes

author:
  - fullname: "Duran Serkan Kılıç"
    organization: "Independent"
    email: "duran.serkan@outlook.com"

normative:
  RFC2119:
  RFC8174:
  RFC9562:
  FIPS197:
    title: "Advanced Encryption Standard (AES)"
    author:
      org: National Institute of Standards and Technology
    date: 2001-11
    seriesinfo:
      NIST: FIPS 197
    target: https://csrc.nist.gov/publications/detail/fips/197/final
  BLAKE3:
    title: "BLAKE3: One function, fast everywhere"
    author:
      - name: Jack O'Connor
      - name: Jean-Philippe Aumasson
      - name: Samuel Neves
      - name: Zooko Wilcox-O'Hearn
    date: 2020-01-09
    target: https://github.com/BLAKE3-team/BLAKE3-specs/blob/master/blake3.pdf

informative:
  RFC8610:
  RFC5869:
  RFC8439:
  DRN-PROJECT:
    title: "DRN-Project Reference Implementation"
    target: https://github.com/duranserkan/DRN-Project
  SKID-PAPER:
    title: "Source Known Identifiers (SKIDs)"
    author:
      - name: Duran Serkan Kılıç
    target: https://arxiv.org/abs/2604.00151
  SNOWFLAKE:
    title: "Snowflake ID"
    author:
      org: Twitter (X)
    target: https://en.wikipedia.org/wiki/Snowflake_ID
  ULID:
    title: "Universally Unique Lexicographically Sortable Identifier"
    target: https://github.com/ulid/spec
  CUID-DEPRECATED:
    title: "CUID - Collision-resistant ids (deprecated)"
    author:
      - name: Eric Elliott
    target: https://github.com/paralleldrive/cuid
  CUID2:
    title: "CUID2 - Secure, collision-resistant ids"
    author:
      - name: Eric Elliott
    target: https://github.com/paralleldrive/cuid2
  KSUID:
    title: "K-Sortable Globally Unique IDs"
    author:
      org: Segment
    target: https://github.com/segmentio/ksuid
  NISTSP800107:
    title: "Recommendation for Applications Using Approved Hash Algorithms"
    author:
      org: National Institute of Standards and Technology
    date: 2012-08
    seriesinfo:
      NIST: SP 800-107 Rev. 1
    target: https://csrc.nist.gov/pubs/sp/800/107/r1/final
  NISTSP800224:
    title: "Keyed-Hash Message Authentication Code (HMAC): Specification of HMAC and Recommendations for Message Authentication"
    author:
      org: National Institute of Standards and Technology
    date: 2024-06
    seriesinfo:
      NIST: SP 800-224 (Initial Public Draft)
    target: https://csrc.nist.gov/pubs/sp/800/224/ipd
  CWE639:
    title: "CWE-639: Authorization Bypass Through User-Controlled Key"
    author:
      org: MITRE Corporation
    target: https://cwe.mitre.org/data/definitions/639.html
  NISTIR8319:
    title: "Review of the Advanced Encryption Standard"
    author:
      org: National Institute of Standards and Technology
    date: 2021-02
    seriesinfo:
      NISTIR: 8319
    target: https://csrc.nist.gov/publications/detail/nistir/8319/final
  BELLARE1998:
    title: "A Concrete Security Treatment of Symmetric Encryption"
    author:
      - name: Mihir Bellare
      - name: Anand Desai
      - name: E. Jokipii
      - name: Phillip Rogaway
    date: 1998
    target: https://eprint.iacr.org/1997/010
  BONNETAIN2019:
    title: "Quantum Security Analysis of AES"
    author:
      - name: Xavier Bonnetain
      - name: María Naya-Plasencia
      - name: André Schrottenloher
    date: 2019
    target: https://eprint.iacr.org/2019/272
  RUGGLES1947:
    title: "An Empirical Approach to Economic Intelligence in World War II"
    author:
      - name: Richard Ruggles
      - name: Henry Brodie
    date: 1947
    seriesinfo:
      Journal of the American Statistical Association: "Vol. 42, No. 237, pp. 72-91"
    target: https://doi.org/10.1080/01621459.1947.10501915
  SHOSTACK2014:
    title: "Threat Modeling: Designing for Security"
    author:
      - name: Adam Shostack
    date: 2014
    target: https://www.wiley.com/en-us/Threat+Modeling%3A+Designing+for+Security-p-9781118809990
  BENCHMARKDOTNET:
    title: "BenchmarkDotNet: Powerful .NET library for benchmarking"
    author:
      - name: Andrey Akinshin
    target: https://github.com/dotnet/BenchmarkDotNet
  RFC5905:
  IEEE1588:
    title: "IEEE Standard for a Precision Clock Synchronization Protocol for Networked Measurement and Control Systems"
    author:
      org: IEEE
    date: 2020
    seriesinfo:
      IEEE: 1588-2019
    target: https://doi.org/10.1109/IEEESTD.2020.9120376
  NISTSP800107W:
    title: "Withdrawal of NIST Special Publication 800-107 Revision 1"
    author:
      org: National Institute of Standards and Technology
    date: 2022-12
    target: https://csrc.nist.gov/news/2022/withdrawal-of-nist-sp-800-107-revision-1

date: 2026-07-09
---

--- abstract

This document introduces Source Known Identifiers (SKIDs), a three-tier
identity system for distributed applications.  A single entity is
represented as a 64-bit Source Known ID (SKID) for database storage,
a 128-bit Source Known Entity ID (SKEID) for trusted environment
communication, or a 128-bit Secure Source Known Entity ID (Secure SKEID)
for external communication.  Deterministic bidirectional transformations
connect all three tiers.

The first tier, Source Known ID (SKID), is a 64-bit signed integer
embedding a timestamp with 250-millisecond precision, application
topology (application identifier, application instance identifier), and a
per-entity-type sequence counter.  It serves as the database primary
key, providing compact storage (8 bytes) and natural B-tree ordering.

The second tier, Source Known Entity ID (SKEID), extends the SKID into
a strict RFC 9562 UUID Version 8 value by adding an entity type
discriminator, an epoch selector, and a BLAKE3 keyed message
authentication code (MAC).
SKEIDs provide zero-lookup integrity checks over embedded origin and
type fields within a shared-key trust domain.  Callers must still
request expected-type validation, and the shared key does not
authenticate an individual producer.  Their
big-endian byte layout preserves SKID order in lexicographic UUID string
comparisons for plain SKEIDs within a fixed entity type.

The third tier, Secure SKEID, encrypts the entire SKEID using AES-256
symmetric encryption as a single-block pseudo-random permutation (PRP).
Under the AES PRP assumption, the result is pseudorandom-looking while
remaining UUID-form and binary-compatible with standard UUID data-type
parsers.  The deterministic form permits repeat-ID correlation, and its
encrypted marker bytes usually fail strict RFC 9562 UUIDv8 validation.
A collision guard prevents same-key ciphertext from being accepted as
plaintext under the generation key.

This document specifies the bit and byte layouts, generation and parsing
algorithms, MAC computation, encryption procedures, key management
model, and a defense-in-depth security analysis.


--- middle


# Introduction

## Problem Statement

An entity is a domain object with a distinct identity that persists
across its lifecycle.  Its entity identifier is the value that
distinguishes it from all other entities of the same type.  Although
this terminology is common in Domain-Driven Design, the underlying
need for durable, unique record identity applies across distributed
systems regardless of architectural style.

Modern distributed systems require entity identifiers that serve
multiple roles simultaneously: database primary keys, inter-service
correlation tokens, and externally visible resource handles.  Existing
identifier schemes force a choice between conflicting properties.

- **Database sequences** (auto-increment `BIGINT`) offer compact storage
  (4 or 8 bytes) and natural ordering but expose creation patterns when
  passed to external consumers.  They are not an authorization boundary.

- **UUID Version 4** ([RFC9562], Section 5.4) provides 122 bits of
  randomness and global uniqueness but sacrifices ordering, embeds no
  application semantics, and consumes 16 bytes of storage.

- **UUID Version 7** ([RFC9562], Section 5.7) restores time-ordering
  with a 48-bit Unix timestamp but exposes creation patterns, carries
  no entity type, no integrity check, and no confidentiality layer.

- **Snowflake-style IDs** [SNOWFLAKE] embed timestamp and worker
  topology in 64 bits but expose creation patterns, lack integrity
  verification, entity type metadata, and lose multi-century
  addressability.

None of these schemes provide a keyed mechanism for zero-lookup
verification: confirming an identifier's structure and integrity without
consulting a database or external service.  Record existence and access
authorization remain application checks.
Chronological sortability and confidentiality are also inherently
contradictory properties, and dual-identifier alternatives (e.g.,
integer primary key paired with UUID external-facing identifier) lose
storage efficiency by maintaining two separate identifier columns and
their associated indexes.

A multi-tier identifier system can address these challenges by
providing different identifier properties for different trust levels.
This document defines the following identifier properties as desired
and achievable in a multi-tier distributed identity system:

1. Storage efficiency
2. Chronological sortability
3. Origin metadata embedding
4. Zero-lookup verifiability
5. Confidentiality for external consumers
6. Multi-century addressability

See "Feature Matrix" below for a comparative analysis of how the SKID
system and existing identifier schemes satisfy these properties.

## Motivation

The necessity for a unified, multi-tier identifier protocol arises from
the conflicting architectural requirements of modern distributed
applications where data continuously flows across persistence, internal,
and external trust tiers.  Each tier imposes different constraints on
identifier design.  Existing schemes and dual-identifier patterns force
systems to compromise at least one.

At the persistence tier, compact, time-ordered primary keys improve
B-tree locality and foreign-key density.  Exposing sequential identifiers
through external APIs enables enumeration and inference of generation
velocity or record volume (the German Tank Problem [RUGGLES1947]).  An
opaque identifier can reduce this exposure, but it does not prevent an
Insecure Direct Object Reference (IDOR) when authorization is missing
[CWE639].  The common workaround, an integer primary key alongside a
random UUID alternate key, compromises storage efficiency.

The full cost of such workarounds extends beyond the identifier columns
themselves.  For instance, a conventional schema using an auto-increment
primary key (8 bytes), a UUID external identifier (16 bytes), and a
created_at timestamp (8 bytes) requires 32 bytes of column data per
record.  In a fully indexed schema, each column also demands a separate
B-tree index and its associated maintenance operations, such as vacuum,
reindex, and statistics collection.

An identifier that embeds a timestamp and derives a UUID-compatible
external representation deterministically at application runtime
consolidates all three concerns into a single 8-byte primary key column
with one index.  Under this fully indexed baseline, this is a 75%
reduction in per-record column overhead and a reduction from three
single-column indexes to one.  This calculation accounts only for the
single-column indexes on each field.  Additional composite indexes
involving these fields would amplify the savings further.

A downstream application that receives an identifier needs to verify
its structure and integrity.  Without embedded verification metadata,
that check requires a query or cache lookup.  A MAC permits early
rejection without I/O; existence and authorization checks are separate.

Decentralized identifier generation across globally distributed
networks demands chronologically sortable identifiers.  When data from
independent generators must be merged years or decades after creation,
the identifier scheme MUST guarantee sort-order consistency past the
operational lifespan of individual system components.

These conflicting trust-boundary constraints motivate a multi-tier
identifier protocol in which each tier addresses a different trust
boundary.

## Related Work

Distributed identifier schemes have evolved considerably since the
original UUID specification.  [RFC9562] standardized UUID versions 1,
3-8, with Version 7 introducing timestamp-ordered UUIDs that address
the B-tree fragmentation problems of random Version 4 UUIDs.  UUID
Version 7 uses a 48-bit Unix timestamp prefix followed by random bits,
providing millisecond-precision ordering and global uniqueness.

Twitter's Snowflake architecture [SNOWFLAKE] pioneered timestamp-
prefixed, worker-partitioned 64-bit identifiers for high-throughput
systems.  Snowflake identifiers embed a 41-bit timestamp with
millisecond precision, a 10-bit machine identifier, and a 12-bit
sequence number, enabling approximately 4,096 identifiers per
millisecond per machine.  Its custom epoch starts from November 4,
2010 and extends to July 10, 2080, approximately 69.7 years.

ULIDs [ULID] provide a 128-bit value composed of a 48-bit timestamp
with millisecond precision and 80 bits of randomness.  The Unix epoch
starts from January 1, 1970 and extends to approximately 10889 CE
(approximately 8,919 years).  ULIDs are encoded as 26-character
Crockford Base32 strings that sort lexicographically by creation time,
targeting environments where string-based identifiers are standard.

The original CUID [CUID-DEPRECATED] was a collision-resistant
identifier specification that used a k-sortable, timestamp-prefixed
structure.  It was deprecated by its author due to security concerns.
The CUID deprecation notice warns that "all monotonically increasing
(auto-increment, k-sortable), and timestamp-based ids share the
security issues with Cuid" and further states that "UUID V6-V8 are
also insecure because they leak information which could be used to
exploit systems or violate user privacy" [CUID-DEPRECATED].

CUID2 [CUID2], the successor to CUID, takes a different
approach.  CUID2 intentionally removed timestamps from the identifier
for security reasons and instead recommends a separate `createdAt`
column for time-based sorting, adding per-record storage overhead
that timestamp-embedding schemes avoid.  CUID2 generates identifiers
by using independent entropy sources then hashing the concatenation
with SHA3.  This produces identifiers with strong collision resistance
but relies on probabilistic uniqueness rather than deterministic
construction.

KSUIDs [KSUID] (K-Sortable Unique Identifier) provide a 160-bit
(20-byte) value composed of a 32-bit timestamp.  KSUID has 1-second
precision, a custom epoch from May 13, 2014 to June 19, 2150, and a
128-bit cryptographically random payload.  KSUIDs are encoded as
27-character Base62 strings that sort lexicographically by creation
time.  The 128-bit random payload provides stronger collision
resistance than UUID V4's 122 random bits.  The string-first design
targets application-layer identifiers rather than database primary
keys where compact binary representation is critical for B-tree
performance.

None of these approaches offers integrity verification or a
confidentiality layer, and none combines all six properties evaluated
here.

## Document Scope

This document specifies the core protocol, cryptographic constructions,
bit and byte layouts, key lifecycle model, and security properties for
the three-tier SKID system.

Implementation details specific to any programming language or runtime
appear only in the informative appendices.


# Conventions and Definitions

The key words "MUST", "MUST NOT", "REQUIRED", "SHALL", "SHALL NOT",
"SHOULD", "SHOULD NOT", "RECOMMENDED", "NOT RECOMMENDED", "MAY", and
"OPTIONAL" in this document are to be interpreted as described in
BCP 14 [RFC2119] [RFC8174] when, and only when, they appear in all
capitals, as shown here.


# Terminology

Source Known Identifiers (SKIDs):
: The collective term for the three-tier identity system comprising
  Source Known IDs (SKID), Source Known Entity IDs (SKEID), and Secure
  Source Known Entity IDs (Secure SKEID).  All three representations
  are deterministically convertible to each other when the required
  context is provided (epoch, entity type, and cryptographic keys).

Source Known ID (SKID):
: A 64-bit signed integer encoding a 250-millisecond-precision
  timestamp, application topology fields (app ID, app instance ID),
  and a per-entity-type sequence number.  Used as the database
  primary key.

Source Known Entity ID (SKEID):
: A strict RFC 9562 UUID Version 8 value embedding a SKID, entity type
  byte, epoch byte, keyed MAC, and identification markers in big-endian
  (network byte order) per RFC 9562.  Used for communication within
  trusted environments.

Secure Source Known Entity ID (Secure SKEID):
: A UUID-form 128-bit opaque ciphertext produced by encrypting an SKEID
  under AES-256-ECB.  Used for external communication where information
  confidentiality is required.  Secure SKEIDs fit UUID storage and
  string formats but are not guaranteed to preserve RFC 9562 UUIDv8
  version and variant marker bits after encryption.

Epoch:
: A reference point in time from which SKID timestamps are measured.
  The default epoch is 2025-01-01T00:00:00Z.  Each epoch spans
  approximately 68 years (2 × 34-year halves via sign-bit extension).
  The 8-bit epoch index in the SKEID selects a 2^31-second window,
  giving 256 epochs and approximately 17,421 years of total coverage.

Entity Type:
: An 8-bit unsigned integer uniquely identifying the type of entity
  (e.g., User, Order, Product) within an application.  Entity types
  MUST be registered per deployment.

Application Topology:
: The combination of App ID (7 bits, max 127) and App Instance ID
  (6 bits, max 63) identifying the originating application and its
  specific instance within the deployment.

MAC (Message Authentication Code):
: A keyed cryptographic hash (BLAKE3 keyed MAC [BLAKE3], truncated
  to 32 bits) embedded in the SKEID to provide integrity verification.

PRP (Pseudo-Random Permutation):
: A bijective mapping of n-bit blocks.  AES-256 [FIPS197] on a single
  128-bit block functions as a PRP: every distinct plaintext maps to
  a distinct ciphertext and vice versa.

Key Separation:
: The practice of using independent keys for different cryptographic
  operations.  SKEID uses one key for the BLAKE3 MAC (integrity), and
  Secure SKEID uses a cryptographically independent key for AES-256-ECB
  (confidentiality).

Marker Bytes:
: The byte 0x8D at byte offset 6 (version marker, RFC 9562 Section 5.8
  octet 6) and 0x8D at byte offset 8 (variant marker, RFC 9562
  Section 4.1 octet 8) in the plaintext SKEID.  These values ensure
  RFC 9562 UUID V8 compatibility in the non-secure form and serve as
  a detection signal during parsing.


# Source Known ID (SKID) - 64-bit Specification

## Bit Layout

A SKID is a 64-bit signed integer with the following field layout,
packed most-significant bit first:

~~~text
 0                   1                   2                   3
 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|S|                    Timestamp (T - 32 bits)
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
 T| App ID (7)  | Inst (6)  |    SequenceId (18 bits)           |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
~~~

Total: 1 (Sign) + 32 (Timestamp) + 7 (App ID) + 6 (App Instance Id)
+ 18 (Sequence Id) = 64 bits.

## Field Definitions

Sign / Epoch Half (1 bit):
: Epoch half indicator for smooth epoch transitions.  When set to 1,
  the resulting `long` value is negative, covering the first
  approximately 34 years of the epoch.  When set to 0, the value
  becomes positive, extending coverage for the second approximately
  34 years.  Together, the two halves span approximately 68 years per
  epoch while preserving monotonic sort order in signed 64-bit
  representation, since negative values sort before positive values.

Timestamp (32 bits):
: Unsigned 250-millisecond ticks elapsed since the configured epoch,
  giving four ticks per second.  This provides sub-second time buckets;
  it does not recover exact creation order within one bucket.  Topology
  and sequence fields provide a deterministic order for ties.
  Implementations MUST use 250-millisecond tick precision.
  Implementations MAY cache the current timestamp with a sub-tick
  refresh period.

  Maximum value: 2^32 - 1 ticks per epoch half ≈ 34 years per
  half-epoch.

App ID (7 bits):
: Application identifier within the deployment.
  Range: 0-127 (128 application identifiers; maximum value 127).

App Instance ID (6 bits):
: Instance discriminator for the application.
  Range: 0-63 (64 instance identifiers; maximum value 63).

Sequence ID (18 bits):
: Per-entity-type, per-tick monotonic counter.  Range: 0-262,143
  (262,144 per 250ms tick; 1,048,576 per second).
  Implementations MUST reset the sequence when the timestamp advances.
  If the sequence is exhausted within a single tick, the generator
  MUST wait for the next tick before issuing new identifiers,
  applying backpressure to maintain uniqueness guarantees.

## SKID CDDL Definition

~~~cddl
; Source Known ID - 64-bit signed integer
skid = int .size 8

; Parsed components (informative)
skid-parsed = {
  sign-bit:       uint .size 1,    ; 0 or 1
  timestamp:      uint .le 4294967295,
  app-id:         uint .le 127,
  app-instance-id: uint .le 63,
  sequence-id:    uint .le 262143,
}
~~~

## SKID Generation Algorithm

Given `entityType`, `appId`, `appInstanceId`, and a configured `epoch`:

~~~pseudocode
procedure GenerateSKID(entityType, appId, appInstanceId, epoch):
  1. elapsedTicks ← floor((now_utc - epoch) / 250 milliseconds)
     -- four ticks per second
  2. Assert 0 ≤ elapsedTicks ≤ 2^33 - 1
     -- covers both epoch halves
  3. Determine epoch half:
     a. If elapsedTicks < 2^32:
        -- first half (sign bit = 1, producing negative SKID)
     b. Else:
        -- second half (sign bit = 0, producing positive SKID)
     c. timestamp ← elapsedTicks mod 2^32
        -- equivalently: elapsedTicks AND (2^32 - 1)
  4. sequenceId ← GetNextSequence(entityType, timestamp)
     -- per-entity-type atomic counter; resets on timestamp change
     -- if sequenceId > 262,143: wait until timestamp advances
  5. Pack fields into a 64-bit signed integer:
     a. bit 63 (sign bit) ← 1 for first epoch half, 0 for second
     b. bits [62..31] ← timestamp (32 bits, unsigned)
     c. bits [30..24] ← appId       (7 bits)
     d. bits [23..18] ← appInstanceId (6 bits)
     e. bits [17..0]  ← sequenceId   (18 bits)
  6. Return packed value
~~~

## SKID Parsing Algorithm

Given a 64-bit signed integer `value` and a configured `epoch`:

~~~pseudocode
procedure ParseSKID(value, epoch):
  1. Extract bit  63       → sign bit
  2. Extract bits [62..31] → timestamp (32 bits, unsigned)
  3. Extract bits [30..24] → appId (7 bits)
  4. Extract bits [23..18] → appInstanceId (6 bits)
  5. Extract bits [17..0]  → sequenceId (18 bits)
  6. If sign bit == 0:
       elapsedTicks ← timestamp + 2^32
     Else:
       elapsedTicks ← timestamp
  7. createdAt ← epoch + (elapsedTicks × 250 milliseconds)
  8. Return {value, createdAt, sequenceId, appId, appInstanceId, signBit}
~~~

## Clock Drift Protection

The system handles backward time jumps at two levels:

- **Minor drifts**: The generator freezes the timestamp until the wall
  clock catches up, allowing the sequence counter to continue advancing
  within the frozen tick.  The reference implementation uses 5 seconds
  as the freeze threshold.

- **Critical drifts (at or beyond the freeze threshold)**: The generator
  MUST stop issuing identifiers and the application MUST initiate a
  graceful shutdown.  The deployment MUST assign a new instance ID
  before restart.  Detection is local; allocating the replacement
  instance ID remains a deployment-coordination task.


# Source Known Entity ID (SKEID) - 128-bit Specification

## Byte Layout

An SKEID occupies 16 bytes (128 bits), structured as follows in
big-endian (network byte order) per RFC 9562:

~~~text
Byte:  0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15
     +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
     |EP| SKID Upper|S0|VE|ET|VA|S1|S2|S3|    MAC    |
     +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
~~~

| Field           | Byte(s) | Width   | Purpose                                               |
|-----------------|---------|---------|-------------------------------------------------------|
| Epoch           | 0       | 8 bits  | Epoch index (highest lexicographic priority)          |
| SKID upper half | 1-4     | 32 bits | SKID bits 63-32, sign-toggled for lexicographic order |
| SKID low byte 0 | 5      | 8 bits  | MSB of SKID lower half (timestamp LSB, App ID)        |
| Version marker  | 6       | 8 bits  | `0x8D` (UUID V8, RFC 9562 Section 5.8 octet 6)        |
| Entity Type     | 7       | 8 bits  | Domain entity classification                          |
| Variant marker  | 8       | 8 bits  | `0x8D` (RFC 9562 Section 4.1 octet 8 variant)         |
| SKID low bytes  | 9-11    | 24 bits | Remaining SKID lower half                             |
| MAC             | 12-15   | 32 bits | BLAKE3 keyed MAC (contiguous)                         |

All fields are encoded in big-endian (network byte order) per RFC 9562.
The epoch byte occupies byte 0 to ensure that higher epoch values sort
lexicographically after lower epoch values, regardless of timestamp.

The upper SKID half at bytes 1-4 is sign-toggled (XOR with 0x80000000)
before encoding.  This converts the signed SKID sort order (negative
before positive) to unsigned byte sort order, preserving chronological
ordering in lexicographic comparisons across both epoch halves.

The most significant byte of the lower SKID half occupies byte 5,
immediately after the upper half.  This placement ensures that the
timestamp least-significant bit and leading application topology bits
participate in lexicographic comparison before the entity type at byte
7.  The remaining lower SKID half is split around the variant marker at
byte 8, with bytes 9-11 holding the last three bytes.

Since the version marker, entity type, and variant marker are constant
for all plain SKEIDs of a given type, lexicographic comparison of the
split lower half operates correctly.

## Marker Bytes

The marker bytes at positions 6 and 8 serve dual purposes:

1. **UUID V8 Compatibility**: `0x8D` at byte 6 satisfies the RFC 9562
   Section 5.8 version octet requirement, and `0x8D` at byte 8
   satisfies the RFC 9562 Section 4.1 variant octet requirement,
   making the SKEID a valid RFC 9562 UUID V8 when formatted as a
   string.

2. **Detection Signal**: During parsing, `0x8D` at bytes 6 and 8 marks
   a candidate plain SKEID.  The MAC must also verify.  Missing markers
   or failed plain verification triggers the secure parse path.

### Variant Byte Range

The variant marker position (byte 8) uses the primary value 0x8D plus
50 alternative values 0x8E through 0xBF for the Secure SKEID collision
guard ("Encryption" below), giving 51 total attempts.  The primary
variant byte is 0x8D; alternative variant bytes are used when the
AES-256-ECB ciphertext would otherwise pass the exact plain markers and
MAC verification.

All values in the range 0x80-0xBF share the RFC 9562 variant bit
pattern: the two high bits are `10` (binary).  On the post-decryption
(secure) path, `(byte & 0xC0) == 0x80` is only a structural precheck.
The generator emits 0x8D-0xBF.  The parser MUST reject 0x80-0x8C and
MUST require backward collision-guard proof for values above 0x8D.
The plain path MUST accept only the primary value 0x8D.

If all 51 attempts (1 primary + 50 alternatives) are exhausted without
resolving the collision, the implementation MUST throw an error rather
than loop indefinitely.  Treating the idealized guard events as
independent gives probability ~1/2^(48×51).

| Byte | Value | Usage |
|------|-------|-------|
| Version marker (byte 6) | 0x8D | Plain SKEID and Secure SKEID plaintext |
| Variant marker (byte 8) | 0x8D | Plain SKEID and primary Secure SKEID plaintext |
| Variant marker (byte 8) | 0x8E-0xBF | Secure SKEID plaintext after guard escalation |

AES encryption does not preserve these values in Secure SKEID
ciphertext.

Total attempt budget: 1 primary (0x8D) + 50 alternatives (0x8E-0xBF) = 51.

## MAC Computation

Integrity verification relies on BLAKE3 keyed MAC [BLAKE3].  The
computation procedure clears the four MAC bytes (positions 12-15)
to zero, then computes a 4-byte BLAKE3 keyed MAC over the full
16-byte buffer.  The resulting 4 bytes are written into the contiguous
MAC positions (bytes 12-15).

The reference implementation uses BLAKE3.  Its small-payload
BenchmarkDotNet measurements report keyed BLAKE3 at approximately 3.5
times the speed of HMAC-SHA-256 for this workload.

~~~pseudocode
procedure ComputeMAC(guidBytes[0..15], macKey):
  1. Clear MAC bytes:
     guidBytes[12] ← 0x00
     guidBytes[13] ← 0x00
     guidBytes[14] ← 0x00
     guidBytes[15] ← 0x00
  2. hashBytes ← BLAKE3-Keyed-MAC(key=macKey, message=guidBytes[0..15], output_length=4)
     -- BLAKE3 is an XOF (extendable output function); implementations
     -- request exactly 4 bytes from the XOF output to produce the
     -- truncated MAC.  There is no need to compute the full default
     -- output and discard the excess.
  3. Return hashBytes[0..3]
~~~

The epoch byte (byte 0) is NOT cleared before MAC computation.  It
participates in the MAC input, adding tamper-resistance for the epoch
field.

The 4-byte MAC is written contiguously:

~~~pseudocode
procedure WriteMACToGUID(guidBytes, hashBytes[0..3]):
  guidBytes[12] ← hashBytes[0]
  guidBytes[13] ← hashBytes[1]
  guidBytes[14] ← hashBytes[2]
  guidBytes[15] ← hashBytes[3]
~~~

Reading extracts from the same positions:

~~~pseudocode
procedure ReadMACFromGUID(guidBytes) → hashBytes[0..3]:
  hashBytes[0] ← guidBytes[12]
  hashBytes[1] ← guidBytes[13]
  hashBytes[2] ← guidBytes[14]
  hashBytes[3] ← guidBytes[15]
~~~

MAC verification MUST compare the actual and expected tags without a
data-dependent early exit.

## SKEID CDDL Definition

~~~cddl
; Source Known Entity ID - 128-bit (16-byte) value
skeid = bytes .size 16

; Parsed SKEID (informative structure)
skeid-parsed = {
  epoch:            uint .size 1,     ; byte 0
  skid-upper-half:  uint .size 4,     ; bytes 1-4 (sign-toggled)
  skid-low-byte-0:  uint .size 1,     ; byte 5
  marker-version:   0x8D,             ; byte 6
  entity-type:      uint .size 1,     ; byte 7
  marker-variant:   uint .size 1,     ; byte 8 (0x8D default; 0x8E-0xBF collision guard)
  skid-low-bytes:   bytes .size 3,    ; bytes 9-11
  mac:              bytes .size 4,    ; bytes 12-15 (contiguous)
}
~~~

## SKEID Generation Algorithm

Given a 64-bit SKID `id`, `epoch`, `entityType`, and `macKey`:

~~~pseudocode
procedure GenerateSKEID(id, epoch, entityType, macKey, variantByte=0x8D):
  1. Allocate guidBytes[0..15], initialize to zero
  2. Split id into upper and lower 32-bit halves.
     Toggle the most significant bit of the upper half
     (XOR with 0x80000000) for lexicographic order.
  3. guidBytes[0] ← epoch             -- epoch selector
  4. Write sign-toggled upper half to guidBytes[1..4] (big-endian)
  5. Write the MSB of the SKID lower half to guidBytes[5]
  6. guidBytes[6] ← 0x8D              -- version marker (RFC 9562 §5.8)
  7. guidBytes[7] ← entityType        -- entity type
  8. guidBytes[8] ← variantByte       -- variant marker
  9. Write the remaining 3 bytes of the SKID lower half to
     guidBytes[9..11] (big-endian)
  10. hashBytes ← ComputeMAC(guidBytes, macKey)
  11. WriteMACToGUID(guidBytes, hashBytes)
  12. entityId ← construct UUID from guidBytes
      (interpreting all fields in big-endian per RFC 9562)
  13. Return entityId
~~~

## SKEID Parsing Algorithm

Given a UUID `entityId`, `macKey` (for MAC verification), and `aesKey`
(for decryption if the secure path is needed):

Note: When a key-ring is in use, this procedure is invoked by the
key-ring fallback loop described in "Parse with Key-Ring Fallback"
below.  Each iteration supplies a different (macKey, aesKey) pair.
The pseudocode below shows a single-key invocation; see
"Parse with Key-Ring Fallback" for the multi-key wrapper.  The
reference implementation [DRN-PROJECT] implements the multi-key
wrapper: generation uses the configured default key, and parsing tries
the default key first followed by remaining configured fallback keys.
It currently returns the first valid result rather than rejecting
multiple valid interpretations as required by the key-ring algorithm
below; see Appendix B.

~~~pseudocode
procedure ParseSKEID(entityId, macKey, aesKey):
  1. guidBytes ← entityId as 16-byte big-endian byte array
     (network byte order, per RFC 9562)
  2. If guidBytes[6] == 0x8D AND guidBytes[8] == 0x8D:
       -- Primary markers present: try plain parse
       result ← VerifyAndExtract(guidBytes, macKey, secure=false)
       If result.valid:
         Return result
       -- Markers were coincidental (~1/65536 for ciphertext)
       guidBytes ← entityId as byte array (restore)
  3. -- Try secure path ("Secure SKEID Specification" above)
     DecryptGUIDBlock(guidBytes, aesKey)
     If guidBytes[6] == 0x8D AND
        (guidBytes[8] AND 0xC0) == 0x80:
       -- RFC 9562 variant structural precheck (matches 0x80-0xBF)
       recoveredVariant ← guidBytes[8]
       If recoveredVariant > 0x8D AND recoveredVariant <= 0xBF:
         -- Non-default variant: backward collision-guard verification
         If NOT VerifyCollisionGuardProof(guidBytes, macKey, aesKey,
                                          recoveredVariant - 1):
           Return INVALID
       Else If recoveredVariant != 0x8D:
         Return INVALID
       Return VerifyAndExtract(guidBytes, macKey, secure=true)
     Else:
       Return INVALID
~~~

~~~pseudocode
procedure VerifyAndExtract(guidBytes, macKey, secure):
  1. actualMAC ← ReadMACFromGUID(guidBytes)
  2. epochByte ← guidBytes[0]
  3. upperHalf ← read uint32 from guidBytes[1..4] (big-endian)
     -- Reverse the sign toggle (XOR with 0x80000000)
  4. skidLowByte0 ← guidBytes[5]
  5. entityType ← guidBytes[7]
  6. skidLowBytes ← guidBytes[9..11]
  7. Reconstruct the SKID lower half from skidLowByte0 and
     skidLowBytes (big-endian)
  8. id ← reconstruct 64-bit SKID from (upperHalf, lowerHalf)
  9. ClearMACSlots(guidBytes)  -- clear bytes 12-15
  10. expectedMAC ← ComputeMAC(guidBytes, macKey)
  11. If actualMAC == expectedMAC:
       epochStart ← ResolveEpochStart(epochByte)
       sourceKnownId ← ParseSKID(id, epochStart)
       Return {sourceKnownId, entityId, entityType, epoch: epochByte, valid=true, secure}
     Else:
       Return INVALID
~~~


# Secure SKEID Specification

## Encryption

A Secure SKEID is produced by encrypting the entire 16-byte SKEID
plaintext using AES-256-ECB [FIPS197]:

~~~pseudocode
procedure GenerateSecureSKEID(id, epoch, entityType, macKey, aesKey):
  1. variantByte ← 0x8D              -- primary variant
  2. guidBytes ← GenerateSKEID(id, epoch, entityType, macKey,
                               variantByte=variantByte)
     -- produces the same plaintext layout as non-secure
  3. EncryptGUIDBlock(guidBytes, aesKey)
  4. -- Collision guard: detect marker+MAC coincidence
     If guidBytes[6] == 0x8D AND guidBytes[8] == 0x8D
           AND HasCoincidentalMACMatch(guidBytes, macKey):
       -- Ciphertext has plain markers (~1/65536) AND MAC
       -- matches (~1/2^32 given markers).
       -- Combined probability per iteration: ~1/2^48
       -- Iterate through variant space for deterministic termination
       For variantByte ← 0x8E to 0xBF:
         guidBytes ← GenerateSKEID(id, epoch, entityType, macKey,
                                     variantByte=variantByte)
         EncryptGUIDBlock(guidBytes, aesKey)
         If NOT (guidBytes[6] == 0x8D AND guidBytes[8] == 0x8D
                 AND HasCoincidentalMACMatch(guidBytes, macKey)):
           Break    -- collision resolved
       If variantByte > 0xBF:
         Throw JackpotException
         -- All 51 variants exhausted; probability ~1/2^(48×51)
  5. entityId ← construct UUID from guidBytes
  6. Return entityId
~~~

The `HasCoincidentalMACMatch` procedure treats the ciphertext as if it
were a plaintext SKEID, extracts the MAC bytes from contiguous
positions 12-15, clears those positions, recomputes the MAC, and
compares:

~~~pseudocode
procedure HasCoincidentalMACMatch(ciphertextBytes, macKey):
  1. workingCopy ← copy of ciphertextBytes[0..15]
  2. actualMAC ← ReadMACFromGUID(workingCopy)
  3. ClearMACSlots(workingCopy)  -- clear bytes 12-15
  4. expectedMAC ← ComputeMAC(workingCopy, macKey)
  5. Return actualMAC == expectedMAC
~~~

~~~pseudocode
procedure EncryptGUIDBlock(guidBytes[0..15], aesKey):
  1. ciphertext ← AES-256-ECB-Encrypt(key=aesKey, plaintext=guidBytes)
     -- single 128-bit block, no padding
  2. Copy ciphertext → guidBytes[0..15]
~~~

## Decryption

~~~pseudocode
procedure DecryptGUIDBlock(guidBytes[0..15], aesKey):
  1. plaintext ← AES-256-ECB-Decrypt(key=aesKey, ciphertext=guidBytes)
     -- single 128-bit block, no padding
  2. Copy plaintext → guidBytes[0..15]
~~~

## AES-ECB Justification

AES in ECB mode has a well-known weakness: when encrypting multiple
blocks with the same key, identical plaintext blocks produce identical
ciphertext blocks, leaking structural patterns.

The within-message, multi-block pattern weakness does not apply to
SKEIDs, but equality across repeated observations remains visible:

1. **Single block**: Each SKEID encryption operates on exactly one
   128-bit block.  There are no multi-block patterns to leak.

2. **Mathematical equivalence**: For a single block, ECB is
   mathematically identical to CBC with a zero initialization vector:
   `C = AES(Key, P ⊕ 0) = AES(Key, P)`.

3. **PRP property**: Under the standard PRP assumption [NISTIR8319],
   AES-256 on a single 128-bit block functions as a pseudo-random
   permutation.  Every distinct plaintext maps to a distinct
   ciphertext.  By the PRP/PRF switching lemma [BELLARE1998], a
   single-block PRP encryption is indistinguishable from a random
   function up to approximately 2^64 queries (the 2^(n/2)
   distinguishing bound for an n=128-bit block cipher), which is
   beyond any practical identifier generation volume.  No nonce is
   required, and there is no nonce-reuse vulnerability.

4. **No IV overhead**: ECB avoids the allocation and XOR overhead of
   a CBC initialization vector that would provide no additional security
   for single-block operations.

5. **Post-quantum readiness**: Quantum attacks on AES via Grover's
   algorithm reduce the effective security of AES-256 to 128 bits
   [BONNETAIN2019].

This construction is deterministic.  Repeating the same SKEID under
the same AES key produces the same Secure SKEID.  Encryption hides the
embedded fields but does not provide unlinkability: an observer can
correlate repeated use of the same identifier.  Applications that
require unlinkable public handles need a randomized token scheme.

Implementations MUST use AES-256 (256-bit key) for the encryption key.

## Key Separation

A SKEID system MUST use two cryptographically independent keys derived
from each key-ring entry:

| Key | Algorithm | Purpose |
|-----|-----------|---------|
| MAC Key | BLAKE3 keyed MAC | Integrity verification |
| AES Key | AES-256-ECB | Confidentiality (encryption) |

The two keys MUST NOT be the same value.  Implementations MAY provision
the MAC key and AES key as independent secrets, or derive both keys from
a single master secret using a standard Key Derivation Function (such as
HKDF per RFC 5869).  Key management and derivation mechanisms are
implementation-specific operational policies outside the scope of this
protocol specification.

## Collision Guard Guarantee

Without the collision guard, the ideal-cipher and MAC assumptions give
a combined ~1/2^48 probability that ciphertext coincidentally matches
the plaintext marker bytes and produces a valid MAC when interpreted as
a plain SKEID.  This would misclassify the Secure SKEID.

The collision guard in GenerateSecureSKEID ("Encryption" above)
eliminates this misclassification for identifiers it emits.  When a
marker-and-MAC collision is detected, the plaintext is regenerated with
successive variant bytes from 0x8E through 0xBF (50 alternatives).
AES maps each distinct plaintext to a distinct ciphertext; under the
ideal-PRP assumption, those ciphertexts are pseudorandom-looking.  The
loop terminates after at most 51 attempts, either with an identifier or
an error.  Treating attempts as independent gives an approximate
exhaustion probability of 1/2^(48×51).

The security model is described in "Security Considerations" below.

### Backward Verification Algorithm

During parsing, when a non-default variant byte V is recovered from the
decrypted plaintext (V > 0x8D), the parse algorithm MUST verify that
the previous variant (V−1) genuinely triggered the collision guard:

~~~pseudocode
procedure VerifyCollisionGuardProof(decryptedBytes, macKey, aesKey,
                                     previousVariant):
  1. Extract id, entityType from decryptedBytes
  2. epochByte ← decryptedBytes[0]
  3. Reconstruct plaintext with variant = previousVariant:
     reconstructedPlaintext ← GenerateSKEID(id, epochByte, entityType,
                                            macKey,
                                            variantByte=previousVariant)
  4. candidateCiphertext ← EncryptGUIDBlock(reconstructedPlaintext, aesKey)
  5. Return candidateCiphertext[6] == 0x8D
         AND candidateCiphertext[8] == 0x8D
         AND HasCoincidentalMACMatch(candidateCiphertext, macKey)
     -- True: previous variant genuinely collided → legitimate escalation
     -- False: variant was tampered → INVALID
~~~

This single-step backward check verifies the immediate predecessor
collision that justified the recovered non-default variant.  It rejects
arbitrary non-default variant tampering under the current generator and
parser contract, but it is not a full canonical proof that every lower
variant from 0x8D through V-1 also collided.  Implementations that need
that stronger canonicality property MUST verify each prior variant
explicitly.

For the generation key, no returned Secure SKEID can pass the plain
parse path as valid; generation fails if every guard variant collides.
Keys added later were not part of this check; see "Key-ring ambiguity"
under Limitations.

### Numeric Walkthrough

The following example illustrates the collision guard and backward
verification with concrete values.

**Setup:** Consider a SKID with value `0x8BEB_C200_1204_0005`
(timestamp = 400,000,000 ticks from epoch, equivalent to 100,000,000
seconds at 250ms precision, App ID = 18, App Instance ID = 1,
Sequence = 5), entity type `0x0A` (entity type 10), epoch `0x00`, and
key pair (K_mac, K_aes).

**Step 1 — SKEID Construction:** The 16-byte SKEID buffer is
constructed in big-endian order.  Byte 0 receives the epoch (`0x00`),
and bytes 1-4 receive the sign-toggled SKID upper half (`0x8BEBC200`
XOR `0x80000000` = `0x0BEBC200`).  Byte 5 receives the most
significant byte of the SKID lower half.

Byte 6 receives the version marker (`0x8D`), byte 7 receives the entity
type (`0x0A`), and byte 8 receives the default variant marker (`0x8D`).
Bytes 9-11 receive the remaining SKID lower half bytes.  The BLAKE3
keyed MAC is computed and placed at bytes 12-15.

**Step 2 — Encryption and Collision Check:** The 16-byte plaintext is
encrypted with AES-256-ECB using K_aes, producing ciphertext C1.  The
generator checks whether C1 coincidentally has `0x8D` at byte
position 6 and `0x8D` at byte position 8.  In approximately 65,535
of every 65,536 cases, the ciphertext does not match, and C1 is the
final Secure SKEID.

**Step 3 — Collision Scenario:** Suppose C1 happens to exhibit `0x8D`
at position 6 and `0x8D` at position 8, and furthermore the bytes at
positions 12-15 coincidentally form a valid BLAKE3 MAC over the
non-MAC bytes of C1 when interpreted as a plaintext SKEID.  This
combined event has probability approximately 1/2^48.

The generator changes the plaintext variant from 0x8D to 0x8E,
recomputes the MAC, and encrypts the result with the same K_aes to
produce C2.  Under the ideal-PRP model, C2 is a distinct,
pseudorandom-looking block.  Treating the guard events as independent,
the probability that C2 also collides is approximately 1/2^48.  In the
usual case, C2 becomes the final Secure SKEID.

**Step 4 — Backward Verification at Parse Time:** When a consumer
parses C2 by decrypting with K_aes, the recovered plaintext reveals
variant byte 0x8E (greater than 0x8D).  The parser MUST verify that
the escalation was legitimate:

~~~pseudocode
1. Reconstruct the SKEID with variant 0x8D (replacing 0x8E and
   recomputing the MAC).
2. Encrypt that reconstruction with K_aes to obtain C1.
3. Check that C1 exhibits the marker-plus-MAC coincidence,
   confirming the collision that justified the escalation.
4. This immediate-predecessor check completes the reference parser's
   supported validation for the recovered 0x8E variant.
~~~

If the backward check fails (the reconstructed C1 does not exhibit the
coincidence), the parser MUST reject the identifier as invalid.  This
rejects a non-default variant without a valid immediate-predecessor
collision proof.

`PaperNumericWalkthroughTests` covers the SKID hex value, structural
byte layout, ordering, and round trips in this walkthrough.  Appendix A
MAC and ciphertext vectors are covered separately by
`IetfTestVectorGeneratorTests` [DRN-PROJECT].

## Secure ↔ Plain Conversion

Given a valid (parsed) SKEID, implementations MAY convert between
secure and plain representations:

~~~pseudocode
procedure ToSecure(skeid):
  If skeid.secure: Return skeid (already encrypted)
  Return GenerateSecureSKEID(skeid.sourceKnownId.value, skeid.epoch, skeid.entityType,
                              macKey, aesKey)

procedure ToPlain(skeid):
  If NOT skeid.secure: Return skeid (already plaintext)
  Return GenerateSKEID(skeid.sourceKnownId.value, skeid.epoch, skeid.entityType, macKey)
~~~

The underlying SKID is unchanged; only the 128-bit GUID
representation changes.  ToSecure and ToPlain MUST validate the
input SKEID before conversion.


# Key Rotation and Key-Ring Fallback

## Key-Ring Model

Implementations MUST support a key-ring containing one or more key
entries.  Exactly one key MUST be designated as the "default" (active)
key at any time.  Previous keys remain in the key-ring for parse
fallback.

Parse cost is linear in the number of attempted keys.  Implementations
MUST set an operational limit on fallback entries and apply the failure
budget in "Rate Limiting" across all attempted keys.

Each key-ring entry produces two independent keys ("Key Separation"
above): a MAC key for BLAKE3 and an AES key for encryption.

## Generation

All new SKEID and Secure SKEID generation MUST use the current default
key.

## Parse with Key-Ring Fallback

When parsing an SKEID, implementations MUST evaluate the current
default key and every configured fallback key.  The default is evaluated
first, followed by fallback keys in configured order.  Exactly one valid
interpretation is accepted; zero or multiple valid interpretations are
INVALID.  Operators SHOULD configure fallbacks newest first for
consistent diagnostics:

~~~pseudocode
procedure ParseWithKeyRing(entityId, keyRing):
  candidates ← empty list
  For key in [keyRing.current, keyRing.previousConfiguredOrder...]:
    result ← ParseSKEID(entityId, key.macKey, key.aesKey)
    If result.valid: Append result to candidates

  If candidates.count == 1: Return candidates[0]
  Return INVALID  -- unrecognized or ambiguous
~~~

## Rotation Window

During a rotation window, implementations MUST generate with the new
default key and retain the previous key for parsing until the defined
window closes.  Hot configuration reload and in-flight request handling
are implementation-specific.

Key rotation does not require re-encrypting existing database records
because:

1. **Source Known IDs are key-independent**: The 64-bit SKID stored in
   the database contains no cryptographic material.  It is generated
   purely from timestamp, topology, and sequence.

2. **SKEID regeneration is deterministic**: Given the SKID, epoch,
   entity type, and new key, a new SKEID or Secure SKEID can be
   generated at any time.

3. **Fallback parsing**: After rotation, new identifiers use the new
   key.  Existing identifiers remain parseable while their previous key
   remains configured.  Re-issuing an identifier changes its external
   value and can affect links, caches, and clients.

## Key Compromise Recovery

In the event of a key compromise:

1. Remove the compromised key from normal verification.  Deployments
   that must parse legacy identifiers MAY instead retain it in a
   separate entry labeled "compromised".  The choice depends on usage.
   Retained keys MUST be isolated, and matches MUST NOT be treated as
   proof of authenticity.
2. Set a new key as the default and apply a new failure budget.
3. Re-issue SKEIDs from trusted underlying SKIDs when clients need values
   protected by the new key.  A deployment that persists derived
   SKEIDs must update those copies separately.

Because the 64-bit SKID is key-independent, no primary key data is at
risk.  Only the 128-bit SKEID/Secure SKEID representations, which are
derived values, need re-generation.


# Context-Based Identity and Transformations

## Three-Tier Model

~~~text
+-------------------+    +-------------------+    +-------------------+
| Database Tier     |    | Trusted Env Tier  |    | External Tier     |
|                   |    |                   |    |                   |
|  SKID (64-bit)    |<-->|  SKEID (128-bit)  |<-->| Secure SKEID      |
|  long / BIGINT    |    |  UUID / GUID      |    |  (128-bit)        |
|                   |    |                   |    |  Encrypted UUID   |
+-------------------+    +-------------------+    +-------------------+

    Primary Key             Internal ID             Public-Facing ID
    Natural ordering        Type + MAC integrity    AES-256 encrypted
    8 bytes                 16 bytes                16 bytes
~~~

## Transformation Rules

| Transformation | Operation |
|----------------|-----------|
| SKID → SKEID | `GenerateSKEID(skid, epoch, entityType, macKey)` |
| SKID → Secure SKEID | `GenerateSecureSKEID(skid, epoch, entityType, macKey, aesKey)` |
| SKEID → SKID | `ParseSKEID(skeid).source.id` |
| SKEID → Secure SKEID | `ToSecure(parsedSKEID)` |
| Secure SKEID → SKID | `ParseSKEID(secureSkeid).source.id` |
| Secure SKEID → SKEID | `ToPlain(parsedSecureSKEID)` |

All transformations are deterministic given the same epoch, entity
type, and keys.  A valid SKEID yields its SKID when parsed with the
matching context.

## Context Security Configuration

Implementations MAY support per-context security configuration:

- **External-facing endpoints**: SHOULD use Secure SKEID to
  prevent metadata disclosure.  They MUST still authorize access to
  the referenced resource.
- **Trusted environment communication**: MAY use plain SKEID for
  reduced computational overhead when the network is trusted.
- **Database storage**: MUST store the 64-bit SKID as the primary key
  to obtain the specified compact, time-ordered storage profile.

A global configuration flag (e.g., `UseSecureSourceKnownIds`) MAY
control the default behavior of the `Generate` operation, while
explicit `GenerateSecure` and `GeneratePlain` methods bypass this
flag.


# Type Safety and Validation

## Entity Type Verification

The entity type byte (byte 7 in the SKEID) enables type-safe
operations:

- **Generation**: The entity type is determined at generation time from
  the entity class metadata (e.g., attribute, annotation, or registry).

- **Validation**: A caller operating on a specific entity type MUST
  compare the parsed value with that expected type.  A mismatch
  indicates an incorrect identifier or cross-type use.

- **Fail-Fast**: Implementations SHOULD throw a validation exception
  immediately when an entity-type mismatch is detected.  Client-facing
  errors SHOULD remain generic; implementations MAY log expected and
  actual types in protected diagnostics.

## Entity Type Registry

Each deployment MUST maintain a mapping between entity types (byte
values 0-255) and their corresponding entity classes.  The validity
flag returned by parsing, not an entity-type byte, indicates an invalid
or unrecognized identifier.

Entity type assignments MUST be stable within a deployment.  Changing
an entity type byte for an existing entity class would invalidate all
previously generated SKEIDs for that entity.


# Epoch and Extensibility

## Epoch Structure

The 32-bit timestamp field stores 250-millisecond ticks, giving 2^32
ticks per epoch half (equivalent to 2^30 seconds).  Each epoch half
covers approximately 34 years.  The sign bit doubles this to
approximately 68 years per epoch while preserving monotonic sort order:

- **First half** (~34 years): Sign bit = 1 (negative `long` values,
  epoch-relative ticks 0 to 2^32 - 1).
- **Second half** (~34 years): Sign bit = 0 (positive `long` values,
  epoch-relative ticks 0 to 2^32 - 1).

For plain SKEIDs within one entity type, the sign-bit toggle ensures
that lexicographic UUID-string comparison matches signed SKID ordering.
The transition between halves remains monotonic because negative SKIDs
sort before positive SKIDs.  Secure SKEIDs do not preserve this order.

## Epoch Byte

Byte 0 in the SKEID carries an 8-bit epoch index for protocol-level
epoch extension.  In a full multi-epoch implementation, each value
selects a 2^31-second window starting from 2025-01-01.  Because the
epoch index is the first SKEID byte, epoch ordering precedes timestamp
ordering in lexicographic comparisons.  The value identifies which
epoch the SKID timestamp is relative to:

This section specifies the multi-epoch wire format.  The current DRN
reference implementation emits epoch `0x00` and does not resolve
nonzero epoch bytes during parsing.

~~~text
epochStart = baseEpoch + (epochByte × 2^31 seconds)
~~~

| Value | Epoch Start | Epoch End (approx.) |
|-------|-------------|---------------------|
| 0x00  | January 1, 2025    | January 19, 2093  |
| 0x01  | January 19, 2093   | February 7, 2161  |
| ...   | ...                | ...               |
| 0xFF  | ~19378             | ~19446            |

With 256 possible epoch values, a full multi-epoch implementation spans
approximately 17,421 years of total coverage.  The 0xFF values above
are year-level proleptic approximations because common runtime date
types do not represent years in the 19000 range.  The exact invariant
is 256 * 2^31 seconds of addressable epoch windows.

## Epoch Configuration

Implementations SHOULD allow the epoch to be configured at startup.
The epoch value MUST be consistent across all nodes in a deployment.
Implementations MUST validate that the system clock is ahead of the
configured epoch's start time.

## Epoch Integrity Requirement

Parsers MUST include the epoch byte in MAC computation to prevent
epoch-byte tampering.  This document does not specify compatibility
with any historical pre-epoch encoding.

## Future Considerations

1. **Epoch transition**: The mechanism for transitioning from epoch
   0x00 to epoch 0x01 is not specified.  Given that epoch 0x00 spans
   until approximately 2093 CE, this is left as future work.  See
   Appendix B for the current reference implementation status.

2. **Managed key lifecycle and rollover interoperability**: The reference
   implementation supports a configured key-ring with default-first parse
   fallback through remaining configured keys.  Operational rotation policy,
   key retention windows, key labels, compromise metadata, automated
   grace-window management, and cross-implementation rollover guidance remain
   future work.

3. **Domain-tailored bit layouts**: Custom SKID profiles with
   different field widths (e.g., nanosecond or picosecond timestamp
   precision, single-year epoch windows for mission-scoped campaigns)
   would enable workflows with different trade-off requirements.
   Configurable custom profiles are not standardized in this document
   but are under consideration.

4. **IETF standardization**: This document is authored in Internet-
   Draft format for independent review.  Formal submission to the
   IETF would enable interoperability testing and community review.

5. **Formal compositional security proof**: A formal cryptographic
   reduction proving that the composition of single-block AES-PRP
   encryption, BLAKE3 MAC, and the collision guard mechanism achieves
   the intended security goals under standard assumptions would
   strengthen the security analysis beyond the STRIDE-based threat
   modeling and quantitative probability analysis provided here.

6. **Cross-platform benchmarks**: The current benchmarks were
   conducted on ARM64 (Apple M2) with .NET 10.  Benchmarks on x86-64
   architectures and other language runtimes would validate
   portability of the performance characteristics.

7. **Multi-language implementations**: The SKID design is language-
   agnostic.  Implementations in JavaScript, Java, Go, Rust, Python
   and other languages would expand the ecosystem and validate the
   specification's portability.


# Support Operations and Observability

## Zero-Lookup Diagnostics

Any SKID or SKEID can be decomposed to reveal operational information
without accessing a database:

| Field | Information | Example Use |
|-------|-------------|-------------|
| Timestamp | Creation time (250ms precision) | "When was this resource created?" |
| App ID | Claimed source application | "Which application ID is embedded?" |
| App Instance ID | Claimed source instance | "Which instance ID is embedded?" |
| Sequence ID | Generation order within that tick | Volume analysis |
| Entity Type | Type of entity | "Is this a User, Order, or Product?" |
| Secure flag | Whether AES encryption was applied | Security posture check |

## Support Workflow

A support engineer receiving a GUID from an end-user can:

1. Parse the GUID as an SKEID (auto-detecting secure vs plain).
2. Extract creation time → "Embedded time: 2026-03-07 14:30:00 UTC."
3. Extract app ID → "Claimed app ID 5 maps to order-service."
4. Extract entity type → "Embedded type 12 maps to Order."
5. All without any database query or service call.

For Secure SKEIDs, parsing requires the appropriate AES key. Access to
the key-ring permits field extraction; interpreting application and
entity-type values also requires the deployment registries.  Successful
parsing does not establish producer attribution, record existence, or
access authorization.


# Performance and Storage Analysis

## Storage Impact

SKID and SKEID serve different storage roles.  The 64-bit SKID is the
database primary key.  SKEID and Secure SKEID are 128-bit UUID-form
representations derived at application boundaries.  A scheme-by-scheme
comparison of size, ordering, integrity, confidentiality, and UUID
compatibility appears in "Comparison with Existing Schemes" below.

## Index Optimization

Compared with 128-bit UUIDs, 64-bit SKIDs have three relevant storage
and query properties:

- **Smaller keys**: 8 bytes vs 16 bytes can increase key density per
  page and improve cache use.
- **Time locality**: Timestamp-leading layout clusters inserts by
  250-millisecond bucket.  Concurrent generators can interleave within
  a bucket, so the layout does not guarantee right-edge insertion for
  every record.
- **Cursor-based pagination**: The SKID's ordered nature enables
  efficient `WHERE id > :lastId ORDER BY id LIMIT :pageSize` queries
  without offset-based pagination overhead.

The companion paper does not report a comparative database-index
benchmark; page-split and cache effects depend on the database and
workload.

## Throughput and Conversion Evidence

The 18-bit sequence field caps generation at 262,144 identifiers per
250ms tick (1,048,576 per second) per instance.  With 128 applications
and 64 instances per application (8,192 total generators), the
theoretical maximum system-wide throughput is approximately 8.6 billion
identifiers per second if every generator sustains its cap.  This is an
arithmetic ceiling, not measured deployment throughput.  Appendix B
reports operation timings and saturation backpressure; integration
tests separately cover generated-ID distinctness.


# Comparison with Existing Schemes

## Feature Matrix

| Feature | SKID System | UUID V4 | UUID V7 | Snowflake | ULID | CUID2 | KSUID | DB Seq |
|---------|-------------|---------|---------|-----------|------|-------|-------|--------|
| **Uniqueness** | Per-entity-type, per-deployment | Deployment-independent; probabilistic | Deployment-independent; probabilistic | Per-datacenter | Deployment-independent; probabilistic | Deployment-independent; probabilistic | Deployment-independent; probabilistic | Per-table |
| **Ordering** | Yes (timestamp) | No | Yes (timestamp) | Yes (timestamp) | Yes (timestamp) | No | Yes (timestamp) | Yes |
| **Embedded timestamp** | Yes (250ms) | No | Yes (millisecond) | Yes (millisecond) | Yes (millisecond) | No (hashed) | Yes (second) | No |
| **Multi-century addr.** | Yes | N/A | Yes | No | Yes | N/A | Partial | N/A |
| **Entity type** | Yes (8-bit) | No | No | No | No | No | No | No |
| **Integrity check** | Yes (BLAKE3 MAC) | No | No | No | No | No | No | No |
| **Confidentiality** | Yes (AES-256) | No | No | No | No | Hashing | No | No |
| **Zero-lookup validation** | Integrity; type when expected-type validation is requested | No | No | No | No | No | No | No |
| **DB primary key size** | 8 B | 16 B | 16 B | 8 B | 16 B | 24 chars | 20 B | 4-8 B |
| **External ID size** | 16 B | 16 B | 16 B | 8 B | 26 chars | 24 chars | 27 chars | N/A |
| **UUID compatible** | Plain: UUIDv8; Secure: UUID-shaped | Yes | Yes | No | No | No | No | No |
| **Key rotation** | Fallback key ring; lifecycle policy open | N/A | N/A | N/A | N/A | N/A | N/A | N/A |

SKID 64-bit values are unique within an (entity-type, deployment)
scope.  Two different entity types MAY share the same 64-bit SKID
value; cross-entity-type disambiguation is provided by the SKEID's
entity type byte (byte 7).

## Security Notes

The feature matrix captures the main security differences.  Random
schemes such as UUID V4 avoid timestamp disclosure but do not embed
origin, type, or integrity metadata.  Timestamp-ordered schemes such as
UUID V7, Snowflake, ULID, and KSUID provide ordering but expose creation
time and, in some layouts, generator topology.  CUID2 avoids timestamp
disclosure by hashing its entropy inputs, but it does not provide
typed identifier validation or a keyed integrity check.

Plain SKEIDs expose metadata intentionally for trusted environments.
Secure SKEIDs add AES-256 confidentiality for external use while
retaining keyed integrity verification after decryption.

## When to Use SKIDs

SKIDs are most appropriate when:

- Multiple trust boundaries exist (database, internal services, external
  consumers) and identifiers must be adapted per boundary.
- Zero-lookup integrity and origin-metadata checks within a shared-key
  trust domain are valuable.
- Entity type enforcement at the identifier level is desired.
- Compact 8-byte database keys with natural ordering are preferred.

SKIDs may not be the best choice when:

- Global uniqueness without a deployment registry is required (use
  UUID V4).
- Ordering finer than 250 milliseconds is required (UUID V7 carries a
  millisecond timestamp).
- The system has no concept of entity types or trust boundaries.


# Operational Assumptions

## Time Synchronization

The SKID system delegates clock synchronization to the operating
system.  NTP [RFC5905], PTP [IEEE1588], or equivalent protocols are
infrastructure responsibilities outside the scope of the identifier
generator.  See "Design Trade-offs" below for the design rationale
and the clock drift protection mechanism in SKID specification above
for operational details.

## Key Distribution

Implementations MUST distribute MAC and AES keys through a
secure channel.  Key material MUST NOT be stored in plaintext in
application configuration files in production deployments.

Clearly labeled public fixture or example keys MAY be committed for
isolated local testing.  Secret or production key material MUST NOT be
committed, including in development configuration files.  Committed
fixtures MUST warn that they are non-secret and MUST NOT be used in
shared, exposed, or production-like environments.

## Topology Limits

| Parameter | Maximum Value | Bits |
|-----------|---------------|------|
| App ID | 127 | 7 |
| App Instance ID | 63 | 6 |
| Sequence per 250ms tick | 262,143 | 18 |

These limits define the default epoch-0 profile.  Custom deployment
profiles MAY redistribute bit widths (e.g., fewer app bits for more
sequence bits) provided all nodes in the deployment share the same
profile.  Custom profiles are implementation-specific extensions
and are not standardized in this document.

## Serialization

SKIDs SHOULD be serialized as 64-bit signed integers only in database
columns and internal or otherwise trusted persistence channels.  Public
APIs and externally visible contracts SHOULD NOT expose the internal
SKID value.

SKEIDs and Secure SKEIDs SHOULD be serialized using the standard UUID
string format (8-4-4-4-12 hexadecimal) as defined in [RFC9562].  DRN
public contracts use these UUID-form identifiers rather than the
internal 64-bit SKID.

## Timestamp Caching

Implementations MUST use 250-millisecond tick precision timestamps.
Implementations MAY cache the current timestamp to amortize the cost
of system clock queries across high-throughput identifier generation.
The cached value MUST refresh frequently enough to maintain tick
accuracy.


# Discussion

## Design Trade-offs

**250-millisecond tick vs. millisecond precision**: SKIDs use
250-millisecond tick timestamps (32 bits, four ticks per second)
instead of millisecond-precision (48 bits in UUID V7).  This
sub-second quantization provides finer ordering than second-precision
schemes while preserving sufficient bits for application topology and
sequence fields within 64 bits.  The 18-bit sequence (262,144
identifiers per tick; 1,048,576 per second) delivers high throughput
within each tick.  Customized domain-tailored bit layouts can be more
suitable for specific use cases.

**Clock synchronization vs. drift protection**: The SKID system
delegates clock synchronization to the operating system.  Accurate
timekeeping through NTP, PTP, or equivalent protocols is an
infrastructure responsibility outside the scope of the identifier
generator.  The clock drift protection mechanism handles only the
consequences of clock jumps.  Minor backward drifts freeze the
timestamp until the wall clock catches up, while drifts beyond the
freeze threshold stop generation and request shutdown.  Deployment
orchestration must assign a new instance identifier before restart.

**32-bit truncated MAC vs. full MAC**: The 4-byte tag preserves the
128-bit layout at the cost of a 32-bit online-forgery margin.  AES
protects confidentiality, while type, topology, existence, and
authorization are separate checks; they do not increase the tag
length.  Deployments must enforce a concrete failure budget.

**AES-ECB vs. other modes**: Single-block ECB suitability and PRP
equivalence are established in "AES-ECB Justification" above.
Implementations MUST NOT apply ECB mode to multi-block data under this
specification.

**Entity type in identifier**: Embedding entity type adds 8 bits of
overhead and supports zero-lookup type validation.  Callers must invoke
that validation to reject cross-entity confusion.

**Coordination vs. probabilistic uniqueness**: The SKID system
requires deployment-time coordination to assign App ID (7 bits, up
to 128 applications) and App Instance ID (6 bits, up to 64 instances
per application).  This stands in contrast to UUID V4, UUID V7, and
CUID2, which achieve uniqueness without any coordination through
cryptographic random number generation.  The coordination cost is an
intentional architectural trade-off: when topology assignments remain
unique and clocks satisfy the stated rules, construction avoids the
random-collision probability of probabilistic schemes.  It also enables
zero-lookup verification through MAC and topology metadata and permits
the compact 64-bit representation.

**Topology field sizing**: The 7-bit application field (128
applications) and 6-bit instance field (64 instances per application)
define the default deployment profile.  The profile targets stable,
bounded distributed application topologies where application and
instance identifiers can be assigned during deployment.

Larger deployments can use bounded contexts, deployment-specific
routing, or domain-tailored bit layouts instead of extending a single
global topology namespace.  The 6-bit instance field also leaves
headroom for rolling deployments and application restarts triggered by
clock drift protection, where the restarting instance must acquire a
new instance identifier to prevent duplicate identifiers.

**.NET Guid mixed endianness**: The .NET `Guid` struct uses a mixed-
endian internal representation: the first three fields are stored in
little-endian order, while the remaining eight bytes are sequential.
This differs from the big-endian byte order specified by RFC 9562.
The reference implementation uses constructors and serializers that
explicitly request big-endian byte order, so SKEID byte arrays are
constructed and interpreted in RFC 9562 order regardless of the
runtime's internal `Guid` representation.  The UUID string
representation remains big-endian and RFC 9562 compliant.

## Limitations

1. **Scoped uniqueness**: A SKID is unique within one entity type,
   epoch configuration, and coordinated `(AppId, AppInstanceId)`
   namespace.  Different entity types can produce the same 64-bit
   value; SKEID's entity-type byte disambiguates them.  Cryptographic
   keys do not affect SKID uniqueness.  Cross-deployment merging needs
   an additional deployment namespace.

2. **Topology limits and coordination requirement**: The default
   profile supports at most 128 applications x 64 instances = 8,192
   distinct generators.  Topology assignment (App ID and App Instance
   ID) requires deployment-time coordination, unlike UUID V4/V7/CUID2
   which achieve uniqueness without coordination.  The rationale for
   this specific field sizing is discussed in Design Trade-offs above.
   Reference implementation aims to provide a coordination application
   called `Nexus` for this purpose.

3. **Key dependency**: SKEIDs and Secure SKEIDs require key material
   for generation and parsing.  Loss of key material makes existing
   SKEIDs unparseable, though the underlying 64-bit SKIDs in the
   database remain valid and recoverable.

4. **Security analysis scope**: The security analysis in this document
   uses STRIDE-based threat modeling and quantitative probability
   analysis within the defense-in-depth architecture.  It does not
   provide formal cryptographic reductions for the composition of
   AES-PRP encryption, BLAKE3 MAC, and the collision guard mechanism.
   The single-block AES PRP assumption is established in
   "AES-ECB Justification" above.  Deterministic encryption reveals
   repeated use of the same identifier even though it hides the
   embedded fields.  A formal compositional security proof is left as
   future work.  The reference implementation provides test vectors in
   its test suites enabling independent verification.

5. **Epoch-scoped storage efficiency**: The 8-byte SKID storage
   efficiency claim applies within a single epoch (~68 years).  The
   64-bit SKID encodes a 32-bit timestamp relative to the configured
   epoch but does not carry the epoch index itself.  A database
   spanning multiple epochs has the following options.

   1. A composite key storing the epoch byte separately alongside
      the SKID.  This adds per-row overhead that negates the 8-byte
      claim.

   2. Migration to the 16-byte SKEID, which includes the epoch at
      byte 0.  This also negates the 8-byte claim.

   3. Epoch-partitioned storage, where the table or archive name
      implicitly carries the epoch context (e.g.,
      `Entity_Epoch0`, `Entity_Epoch1`).  This preserves 8-byte
      rows by encoding the epoch in the partition structure rather
      than in each record.

   Deployments whose storage horizon fits within one epoch retain the
   8-byte storage property.  Longer-lived deployments need one of the
   epoch-storage options above.

6. **Secure SKEID UUID format compliance**: A Secure SKEID is a valid
   UUID in length and string format (8-4-4-4-12 hexadecimal grouping)
   and is accepted by standard data-type parsers.  However, AES-256
   encryption replaces the plaintext Version and Variant marker bytes
   with pseudorandom ciphertext.  Under that model, only about 1 in 64
   outputs satisfies both the UUIDv8 version nibble and RFC variant
   bits by chance.  Strict RFC 9562 validators therefore reject most
   Secure SKEIDs.  Systems requiring strict RFC 9562 compliance at the
   validator level SHOULD
   use plaintext SKEIDs or accept Secure SKEIDs as opaque 128-bit
   values.

7. **Key-ring ambiguity**: The collision guard covers the generation
   key pair only.  Under idealized independence, a later MAC key can
   make an existing ciphertext look like a plain SKEID, or a later
   AES/MAC pair can decrypt it to a false valid Secure SKEID.  The
   primary-marker path is on the order of 1/2^48 per random candidate.
   Scanning all keys can reject ambiguity only while the origin key
   remains present; after it is removed, a sole false match cannot be
   distinguished without a key identifier.  During normal rotation,
   retain origin keys until affected identifiers expire or are reissued.
   Compromised keys require the isolated migration procedure above.

## Open Questions for Community

1. Is a 32-bit MAC acceptable for aggregate verification volume across
   all services and fallback keys, or should another profile allocate
   at least 64 bits?
2. Should the wire format carry a key identifier to bound fallback work
   and make rotation deterministic?
3. Should a non-default collision-guard variant prove only its immediate
   predecessor or the full chain from 0x8D?
4. What epoch-transition and multi-epoch storage procedure should be
   interoperable?
5. How should deployments allocate and safely reuse App Instance IDs
   across restarts and rolling deployments?


# Security Considerations

## Threat Analysis (STRIDE)

The following systematic threat analysis applies the STRIDE
categories [SHOSTACK2014] to the three-tier identity model:

**Spoofing (Identifier Fabrication)**:
AES-256 hides the fields of an intercepted Secure SKEID.  The keyed MAC
lets a parser reject fabricated plain or decrypted values, subject to
the 32-bit tag bound below.  A valid MAC shows that some holder of the
shared MAC key created the value; it does not identify which holder did
so.  Record existence and caller authorization are separate checks.

**Tampering (Identifier Modification)**:
A modification to either representation changes the MAC input after
decryption or direct parsing.  Verification rejects it except with the
forgery probability of the 32-bit tag under the MAC assumption.

**Repudiation**:
The SKEID system does not provide non-repudiation.  Topology metadata
can support diagnostics, but any holder of the shared MAC key can mint
an identifier that claims another application or instance.  Audit
attribution therefore requires protected application logs or a
separate signing mechanism.

**Information Disclosure**:
Plaintext SKEIDs deliberately expose metadata within trusted
environments where this information supports routing and validation.
For external consumers, Secure SKEIDs encrypt the entire identifier
using AES-256-ECB, which functions as a PRP on the single 128-bit
block.  The ciphertext is computationally indistinguishable from
random bytes without the key under the stated PRP assumptions.  The
construction hides embedded fields but permits correlation when the
same identifier is observed more than once.

**Denial of Service**:
The 18-bit sequence counter limits generation to 262,144 identifiers
per 250-millisecond tick (1,048,576 per second) per instance.  Exceeding
this rate triggers backpressure (the generator waits for the next tick),
which is a deliberate safety mechanism rather than a vulnerability.
Capacity is partitioned by active generator; deployment-wide impact
depends on routing and resource controls.  Parsing is fixed-size per
key but linear in key-ring length.  Implementations MUST cap fallback
keys and rate-limit invalid inputs.

**Elevation of Privilege (Cross-Entity Confusion)**:
The 8-bit discriminator enables rejection when an identifier for one
entity type is submitted as another.  Callers MUST supply and validate
the expected type; MAC verification alone accepts a structurally valid
SKEID of any type.  Type validation does not replace object-level
authorization.

## Authorization Boundary

An SKEID is an identifier, not a bearer credential.  Every operation
MUST authorize the authenticated principal against the referenced
resource after parsing.  Secure SKEIDs reduce metadata disclosure and
enumeration; they do not prevent IDOR by themselves.

## Defense in Depth

The controls operate at different boundaries:

| Control | Purpose | Boundary |
|---------|---------|----------|
| AES-256 PRP | Conceal SKEID fields | Confidentiality; deterministic, not authentication |
| 32-bit BLAKE3 keyed MAC | Detect fabrication or modification | Approximately 2^(-32) per independent attempt before failure-budget effects |
| Marker bytes | Select a candidate parse path | Approximately 2^(-16) for an exact random-byte match; not secret |
| Expected entity type | Reject cross-type use | Caller must request this check |
| Topology policy | Reject unassigned app or instance values | Application registry; parser only extracts fields |
| Existence and authorization | Resolve and protect the resource | Database and application policy |
| Rate and failure limits | Bound online guesses and fallback work | Deployment policy |

The barriers are assumed to be operationally independent under secure
key derivation.  Their multiplicative combination provides stronger
security than any individual barrier.  A formal proof of independence
is outside the scope of this document.

## MAC Truncation Analysis

NIST SP 800-107 Rev. 1 specifies 32 bits as the minimum truncated HMAC
tag length in Section 5.3.3 and discourages tags shorter than 64 bits
in Section 5.3.5.  The 128-bit SKEID layout leaves 32 bits after the
SKID, entity type, epoch, and markers.  A deployment MUST determine
whether that online-forgery margin is acceptable for its traffic and
threat model.

Section 5.3.5 of the same document provides the general framework for
assessing truncated MAC forgery risk.  For a lambda-bit MacTag with
2^t failed verifications allowed, the likelihood of accepting forged
data is (1/2)^(lambda - t).  Applying this to the 32-bit SKEID MAC
(lambda = 32): if a system permits 2^12 (4,096) failed verification
attempts across all endpoints and fallback keys before retiring the MAC
key, the aggregate forgery likelihood is approximately (1/2)^20, or
one in a million, under independent attempts.  At 100 failed attempts
per second, that budget lasts roughly 41 seconds.  This is a risk
example, not a recommended default.

NIST decided in 2022 to withdraw SP 800-107 Rev. 1 after its remaining
requirements are moved [NISTSP800107W].  As of 2026-07-09, SP 800-224
is an Initial Public Draft for HMAC and likewise requires careful risk
analysis below 64 bits [NISTSP800224].  These HMAC publications inform
tag-length risk; they do not approve or validate BLAKE3 keyed mode.

## AES-256 Post-Quantum Estimate

AES-256 retains an idealized 128-bit security level under Grover's
quantum search algorithm [BONNETAIN2019].

Implementations MUST use 256-bit AES keys (32 bytes).  128-bit or
192-bit keys MUST NOT be used.

## Rate Limiting

Endpoints that accept SKEID values from untrusted sources (e.g.,
public APIs) MUST implement rate limiting to prevent brute-force
attacks against the MAC.  Such endpoints MUST account for failed parse
and validation attempts per relevant subject (for example client,
principal, route, or tenant), SHOULD expose an operator-configurable
failure budget aggregated across fallback keys, and SHOULD log and
alert on sustained or bursty SKEID validation failures.

## Information Leakage (Plain SKEID)

The plain SKEID exposes the creation timestamp, app topology,
entity type, and sequence in plaintext, with the MAC occupying
the contiguous tail bytes (12-15).  This is acceptable for
communication within trusted environments but MUST NOT be used for
external-facing identifiers in security-sensitive deployments.

Deployments SHOULD configure external-facing endpoints to use
Secure SKEIDs exclusively.


# IANA Considerations

This document has no IANA actions.


# Acknowledgments

The reference implementation and this specification were developed
with the assistance of AI coding and research tools.  All generated
content was reviewed, validated, and modified by the author.  The
architectural design, cryptographic choices, and all technical
decisions are solely the work of the author.  The companion paper
[SKID-PAPER] contains a full AI disclosure per journal policy.

--- back

# Appendix A. Test Vectors

The following test vectors were generated by
`IetfTestVectorGeneratorTests.Generate_IETF_Test_Vectors`.
Implementers SHOULD verify that their implementation produces
identical outputs for these inputs before substituting production key
material.

Each hexadecimal string below represents raw octets, not UTF-8 text.
The DRN reference implementation accepts the master key through
`NexusKey` with `Format = Hex` and derives the two displayed subkeys.
Other implementations MAY supply the derived MAC and AES keys directly
to reproduce the wire vectors.

## A.1. Test Key Material

~~~text
Master Key (32 bytes, hex):      000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F
Derived MAC Key (32 bytes, hex): 5E293E89136745A96B70EB8C8F81CDFCAED177BE5358BC83D3039FB6607FD8FE
Derived AES Key (32 bytes, hex): 4988F97FF724CD086BDFEC83497C3527B3656F35F0911BEEAA6BCE4BB92D3BC7
Epoch:                           2025-01-01T00:00:00Z
~~~

The SKEID and Secure SKEID operations below use the exact derived MAC
and AES key bytes shown above.  Master-key derivation is an
implementation detail and is not part of the wire format.
These public test keys MUST NOT be used in production.

## A.2. SKID Generation

~~~text
Input:
  entityType     = 1
  appId          = 5
  appInstanceId  = 3
  timestamp      = 272000000 (250ms ticks since epoch = 68000000 seconds)
  sequenceId     = 42

Expected bit layout (MSB first):
  Sign:          1                                      (1 bit)
  Timestamp:     0001_0000_0011_0110_0110_0100_0000_0000 (32 bits)
  AppId:         000_0101                                (7 bits)
  AppInstanceId: 00_0011                                 (6 bits)
  SequenceId:    00_0000_0000_0010_1010                   (18 bits)

Expected SKID (decimal):  -8639256484514103254
Expected SKID (hex):      0x881B3200050C002A

Parsed fields:
  AppId:           5
  AppInstanceId:   3
  SequenceId:      42
  CreatedAt:       2027-02-27T00:53:20.0000000+00:00
~~~

## A.3. SKEID Generation (Plain)

~~~text
Input:
  SKID       = -8639256484514103254 (0x881B3200050C002A)
  entityType = 1
  macKey     = [from A.1]

Expected byte layout (big-endian per RFC 9562):
  Byte  0: 0x00   (Epoch)
  Byte  1: 0x08   (ID upper half (sign-toggled, big-endian))
  Byte  2: 0x1B   (ID upper half (sign-toggled, big-endian))
  Byte  3: 0x32   (ID upper half (sign-toggled, big-endian))
  Byte  4: 0x00   (ID upper half (sign-toggled, big-endian))
  Byte  5: 0x05   (ID lower byte 0 (MSB))
  Byte  6: 0x8D   (Marker Version (0x8D) - RFC 9562 octet 6)
  Byte  7: 0x01   (Entity Type)
  Byte  8: 0x8D   (Marker Variant (0x8D) - RFC 9562 octet 8)
  Byte  9: 0x0C   (ID lower bytes 1-3)
  Byte 10: 0x00   (ID lower bytes 1-3)
  Byte 11: 0x2A   (ID lower bytes 1-3)
  Byte 12: 0x49   (MAC (BLAKE3 keyed))
  Byte 13: 0x2C   (MAC (BLAKE3 keyed))
  Byte 14: 0x0E   (MAC (BLAKE3 keyed))
  Byte 15: 0x75   (MAC (BLAKE3 keyed))

Expected GUID:  00081b32-0005-8d01-8d0c-002a492c0e75
Expected hex:   00081B3200058D018D0C002A492C0E75
~~~

## A.4. Secure SKEID Generation

~~~text
Input:
  SKEID plaintext from A.3
  aesKey = [from A.1]

Operation:
  ciphertext = AES-256-ECB-Encrypt(key=aesKey, plaintext=SKEID)

Expected Secure GUID:  652068a4-3612-cc4b-8abb-83b853dc6786
Expected hex:          652068A43612CC4B8ABB83B853DC6786
~~~

## A.5. Round-Trip Verification

~~~text
1. Parse SKEID:
   Valid=True, SKID=-8639256484514103254                         ✓
2. Parse Secure SKEID:
   Valid=True, SKID=-8639256484514103254                         ✓
3. ToSecure(SKEID):
   GUID=652068a4-3612-cc4b-8abb-83b853dc6786 == Secure           ✓
4. ToPlain(Secure):
   GUID=00081b32-0005-8d01-8d0c-002a492c0e75 == Plain            ✓

All round-trip verifications passed.
~~~


# Appendix B. Reference Implementation (Informative)

## DRN-Project

The DRN-Project [DRN-PROJECT] provides a reference implementation of
the SKID system in .NET 10, organized across four NuGet packages:

- `DRN.Framework.SharedKernel`: Interfaces, base classes, and parsed
  identity structs, including `ISourceKnownEntityIdOperations`,
  `SourceKnownEntity`, `SourceKnownId`, and `SourceKnownEntityId`.
- `DRN.Framework.Utils`: Cryptographic operations, timestamp
  management, sequence management, bit packing, and the
  `SourceKnownIdUtils` / `SourceKnownEntityIdUtils` implementations.
- `DRN.Framework.EntityFramework`: EF Core value generation,
  save-changes and materialization interceptors, and repository
  integration for SKID assignment, SKEID reconstruction, validation,
  and cursor-based pagination.
- `DRN.Framework.Testing`: Unit and integration test infrastructure,
  including Testcontainers orchestration for ephemeral PostgreSQL
  instances and convention-based test contexts.

The implementation uses attribute-based dependency injection and
entity type attributes for entity type registration, and integrates
with Domain-Driven Design patterns through the `SourceKnownEntity`
abstract base class.  Key implementation types include
`SequenceManager<TEntity>` for thread-safe per-entity-type sequence
management and `NexusAppSettings` for `Keys[]` key-ring
configuration, key separation, and default key designation.

Current implementation status: DRN-Project emits epoch byte `0x00` and
does not yet resolve nonzero epoch bytes during parsing.  The protocol
behavior for nonzero epoch bytes is specified above; nonzero epoch
support is reserved for a future reference implementation extension.
The parser tries the default key followed by configured fallback keys
and returns the first valid result; it does not yet detect rare
cross-key ambiguous interpretations.
The current tag comparison uses the runtime sequence-comparison API;
fixed-time behavior is not established by the test suite.

## Database Patterns

The reference implementation uses PostgreSQL with the following
optimizations:

- **Primary key**: `BIGINT` (8 bytes) storing the 64-bit SKID, with a
  default B-tree index for ordered access and time-localized inserts.
- **External identifier derivation**: DRN does not persist a separate
  UUID column for SKEID or Secure SKEID.  `EntityId` and
  `EntityIdSource` are runtime-computed properties initialized by
  interceptors and ignored by EF Core mapping.  Repository external
  lookups parse the UUID to recover and validate the source SKID, then
  query by the internal `BIGINT` primary key.
- **Cursor-based pagination**: Uses `WHERE id > :lastSkid ORDER BY id
  LIMIT :pageSize` for efficient, offset-free pagination.
- **CreatedAt filtering**: The SKID's embedded timestamp enables
  time-range queries directly on the primary key without a separate
  `created_at` column.

## Validation and Maturity

Active development of the SKID system began in November 2023 as part
of DRN.Framework.  The repository validates the identifier system
through multiple evidence tiers:

- **Unit tests** cover SKID generation, SKEID construction with MAC
  verification, and Secure SKEID encryption/decryption round-trips.
- **Paper-verification tests** (`PaperNumericWalkthroughTests`,
  `PaperEpochAddressabilityTests`, `PaperThroughputAnalysisTests`, and
  `IetfTestVectorGeneratorTests`)
  assert the exact numeric walkthrough values, byte layouts, epoch
  boundaries, lexicographic ordering, Appendix A vectors, and arithmetic
  throughput limits cited by the specification and companion paper
  [SKID-PAPER].  Runtime backpressure and latency claims are benchmark
  evidence, not unit-test evidence.
- **Integration tests** exercise EF Core value generation,
  interceptors, repository behavior, and cursor-based pagination with
  PostgreSQL test containers.
- **Static analysis workflows** include CodeQL and SonarCloud scans in
  the repository's GitHub Actions configuration.
- **Evidence limits**: Tests do not force a marker-and-MAC collision,
  generate or parse a nonzero-epoch SKEID, simulate clock rollback, or
  establish uniqueness across process restarts.  They do cover nonzero
  epoch addressability arithmetic.  Probability bounds and physical
  B-tree effects are analytical rather than test results.

## Benchmark Summary

Performance characteristics observed in the reference implementation
(BenchmarkDotNet [BENCHMARKDOTNET] v0.15.8; Apple M2, 1 CPU, 8
logical and 8 physical cores; macOS Tahoe 26.4; .NET 10.0.5 Arm64
RyuJIT; SDK 10.0.201; OutlierMode=RemoveUpper,
InvocationCount=262,144, IterationCount=120, WarmupCount=120):

Generation and parsing used one default key.  Fallback-key scanning was
not measured; parse work grows linearly with the number of attempted
keys.

| Operation | Mean (ns) | Error (ns) | StdDev (ns) | Allocated |
|-----------|-----------|------------|-------------|-----------|
| Random 64-bit integer generation | 171.3 | 2.36 | 7.66 | 32 B |
| Random UUID V4 generation | 354.1 | 3.64 | 11.81 | 0 B |
| Random UUID V7 generation | 377.5 | 3.24 | 10.53 | 0 B |
| Timestamp manager current time generation | 4.1 | 0.46 | 1.43 | 0 B |
| Sequence manager time-scoped ID generation | 15.4 | 0.69 | 2.19 | 0 B |
| SKID generation | 35.3 | 1.21 | 3.92 | 0 B |
| SKEID generation (with provided SKID) | 219.0 | 3.25 | 10.56 | 0 B |
| SKEID generation (with SKID generation) | 230.3 | 3.45 | 11.20 | 0 B |
| SKEID generation (with entity allocation) | 248.3 | 3.87 | 12.58 | 192 B |
| Secure SKEID generation | 544.0 | 5.67 | 18.42 | 72 B |
| SKEID parsing | 223.6 | 2.87 | 9.31 | 0 B |
| Secure SKEID parsing | 540.7 | 3.15 | 10.22 | 72 B |
| ToPlain (regenerate plain form) | 217.3 | 2.40 | 7.76 | 0 B |
| ToSecure (regenerate secure form) | 524.2 | 5.56 | 17.98 | 72 B |

Error values represent the 99.9% confidence interval margin
computed by BenchmarkDotNet over iterations remaining after upper
outlier removal.

A separate saturation configuration used 786,432 invocations per
iteration with 40 measurement and 40 warmup iterations.  It reported a
610.0 ns mean for SKID generation, including waits at tick boundaries.
This measures backpressure behavior, not deployment-wide throughput.

These are informative benchmarks and will vary by hardware, runtime,
and application workload.


# Practical Utilities (Informative)

## Cursor-Based Pagination

Given the SKID's total order, cursor-based pagination can use:

~~~sql
SELECT * FROM entities
WHERE id > :last_skid
ORDER BY id ASC
LIMIT :page_size
~~~

Trusted internal clients may send the last SKID from the previous page
as the cursor.  A public API MUST instead use an opaque protected cursor
or a Secure SKEID that the server validates and converts to its SKID.
Public APIs MUST NOT expose the raw SKID.  No offset tracking is needed.

## CreatedAt Extraction

Given a SKID and the configured epoch, the creation timestamp can be
extracted without a database query:

~~~pseudocode
parsed = ParseSKID(skid, epoch)
createdAt = parsed.createdAt
~~~

This enables time-based filtering, audit logging, and SLA monitoring
using only the identifier value.

## Validation and Conversion Helpers

Common utility operations:

| Operation | Input | Output |
|-----------|-------|--------|
| `Parse(guid)` | UUID | SKEID record (auto-detects secure/plain) |
| `Validate<TEntity>(guid)` | UUID | SKEID record or throws if type mismatch |
| `ToSecure(skeid)` | Parsed SKEID | Secure SKEID (no-op if already secure) |
| `ToPlain(skeid)` | Parsed SKEID | Plain SKEID (no-op if already plain) |
