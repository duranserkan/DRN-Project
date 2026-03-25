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
  DRN-PROJECT:
    title: "DRN-Project Reference Implementation"
    target: https://github.com/duranserkan/DRN-Project
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
    target: https://csrc.nist.gov/publications/detail/sp/800-107/rev-1/final
  NISTSP800224:
    title: "Recommendations for Key-Derivation Methods in Key-Establishment Schemes"
    author:
      org: National Institute of Standards and Technology
    date: 2024
    seriesinfo:
      NIST: SP 800-224
    target: https://csrc.nist.gov/publications/detail/sp/800-224/final
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

date: 2026-03-24
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
topology (application identifier, instance identifier), and a
per-entity-type sequence counter.  It serves as the database primary
key, providing compact storage (8 bytes) and natural B-tree ordering.
The second tier, Source Known Entity ID (SKEID), extends the SKID into
a 128-bit UUID-compatible value by adding an entity type discriminator,
an epoch selector, and a BLAKE3 keyed message authentication code (MAC).
SKEIDs enable zero-lookup verification of identifier origin, integrity,
and entity type within trusted environments, with a big-endian byte
layout that preserves chronological ordering in lexicographic UUID
string comparisons.  The third tier, Secure SKEID, encrypts the entire
SKEID using AES-256 symmetric encryption as a single-block
pseudo-random permutation (PRP), producing ciphertext indistinguishable
from random bytes while remaining compatible with standard UUID
data-type parsers in string representation.  A collision guard mechanism
using variant byte iteration with cryptographic backward verification
prevents misclassification between encrypted and unencrypted forms.

This document specifies the bit and byte layouts, generation and parsing
algorithms, MAC computation, encryption procedures, key management
model, and a defense-in-depth security analysis.


--- middle


# Introduction

## Problem Statement

Modern distributed systems require entity identifiers that serve
multiple roles simultaneously: database primary keys, inter-service
correlation tokens, and externally visible resource handles.  Existing
identifier schemes force a choice between conflicting properties.

- **Database sequences** (auto-increment `BIGINT`) offer compact storage
  (4 or 8 bytes) and natural ordering but expose creation patterns and
  cannot be safely passed to external consumers.

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

None of these schemes provide a mechanism for zero-lookup verification,
the ability to confirm that an identifier was generated by a trusted
source without consulting a database or external service.
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

## Motivation

The necessity for a unified, multi-tier identifier protocol arises from
the conflicting architectural requirements of modern distributed
applications where data continuously flows across persistence, internal,
and external trust tiers.  Each tier imposes different constraints on
identifier design.  Existing schemes and dual-identifier patterns force
systems to compromise at least one.

At the persistence tier, optimal B-tree index performance and compact
foreign keys demand sequential integer primary keys, but exposing
sequential identifiers through external APIs creates Insecure Direct
Object Reference (IDOR) vulnerabilities [CWE639].  Exposure also
enables adversarial inference of generation velocity and record volume
(the German Tank Problem [RUGGLES1947]).  As a workaround, the dual-identifier
pattern with an integer primary key alongside a random UUID alternate key
compromises storage efficiency.

The full cost of such workarounds extends beyond the identifier columns
themselves.  For instance, a conventional schema using an auto-increment
primary key (8 bytes), a UUID external identifier (16 bytes), and a
created_at timestamp (8 bytes) requires 32 bytes of column data per record.
In a fully indexed schema, each column demands a separate B-tree index,
resulting in three indexes and their associated maintenance operations
such as vacuum, reindex, and statistics collection.  An identifier that
embeds a timestamp and derives a UUID-compatible external representation
deterministically at application runtime consolidates all three concerns
into a single 8-byte primary key column with a single index.  Under this
fully indexed baseline, this is a 75% reduction in per-record column
overhead and a reduction from three single-column indexes to one.  This
calculation accounts only for the single-column indexes on each field.
Additional composite indexes involving these fields would amplify the
savings further.

A downstream application that receives an identifier needs to verify
it.  Without embedded verification metadata, this validation requires
a query or cache lookup per identifier.  An identifier carrying a MAC
would eliminate I/O-bound validation overhead and enable zero-lookup
verification.

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
systems.  Snowflake identifiers embed a 41-bit timestamp, a 10-bit
machine identifier, and a 12-bit sequence number, enabling
approximately 4,096 identifiers per millisecond per machine.

ULIDs [ULID] add timestamp-prefixed uniqueness in a string-friendly
format, targeting environments where string-based identifiers are
standard.

The original CUID [CUID-DEPRECATED] was a collision-resistant
identifier specification that used a k-sortable, timestamp-prefixed
structure.  It was deprecated by its author due to security concerns.
The CUID deprecation notice warns that "all monotonically increasing
(auto-increment, k-sortable), and timestamp-based ids share the
security issues with Cuid" and further states that "UUID V6-V8 are
also insecure because they leak information which could be used to
exploit systems or violate user privacy" [CUID-DEPRECATED].

CUID2 [CUID2], the successor to CUID, takes a fundamentally different
approach.  CUID2 intentionally removed timestamps from the identifier
for security reasons and instead recommends a separate `createdAt`
column for time-based sorting, adding per-record storage overhead
that timestamp-embedding schemes avoid.  CUID2 generates identifiers
by using independent entropy sources then hashing the concatenation
with SHA3.  This produces identifiers with strong collision resistance
but relies on probabilistic uniqueness rather than deterministic
construction.

KSUIDs [KSUID] (K-Sortable Unique Identifier) provide a 160-bit
(20-byte) value composed of a 32-bit timestamp (1-second precision,
custom epoch from May 13, 2014 to June 19, 2150) and a 128-bit
cryptographically random payload.  KSUIDs are encoded as 27-character
Base62 strings that sort lexicographically by creation time.  The
128-bit random payload provides stronger collision resistance than
UUID V4's 122 random bits.  The string-first design targets
application-layer identifiers rather than database primary keys where
compact binary representation is critical for B-tree performance.

None of these approaches offers integrity verification or
confidentiality layers, and none simultaneously satisfies the six
desired identifier properties required for a complete distributed
identity system.

## Document Scope

This document specifies:

- Bit and byte layouts for SKID, SKEID, and Secure SKEID
- Generation, parsing, and validation algorithms
- MAC computation and verification procedures
- Encryption and decryption procedures
- Key management and key rotation model
- Security analysis and defense-in-depth properties

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
  context is provided (entity type and cryptographic keys).

Source Known ID (SKID):
: A 64-bit signed integer encoding a 250-millisecond-precision
  timestamp, application topology fields (app ID, app instance ID),
  and a per-entity-type sequence number.  Used as the database
  primary key.

Source Known Entity ID (SKEID):
: A 128-bit value structured as a UUID, embedding a SKID, entity type
  byte, epoch byte, keyed MAC, and identification markers in big-endian
  (network byte order) per RFC 9562.  Used for communication within
  trusted environments.

Secure Source Known Entity ID (Secure SKEID):
: A 128-bit value produced by encrypting an SKEID under AES-256-ECB.
  Used for external communication where information confidentiality
  is required.

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
: The practice of deriving independent keys for different cryptographic
  operations from a single key-ring entry.  SKID uses one key for
  BLAKE3 MAC (integrity) and a cryptographically independent key for
  AES-256-ECB (confidentiality).

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

~~~
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
  giving four ticks per second.  Sub-second ordering at 250ms
  granularity eliminates coarse-grained temporal ambiguity while
  preserving throughput within a compact 64-bit layout.
  Implementations MUST use 250-millisecond tick precision.
  Implementations MAY cache the current timestamp with a sub-tick
  refresh period.

  Maximum value: 2^32 - 1 ticks per epoch half ≈ 34 years per
  half-epoch.

App ID (7 bits):
: Application identifier within the deployment.
  Range: 0-127 (maximum 127 applications).

App Instance ID (6 bits):
: Instance discriminator for the application.
  Range: 0-63 (maximum 63 instances per application).

Sequence ID (18 bits):
: Per-entity-type, per-tick monotonic counter.  Range: 0-262,143
  (262,144 per 250ms tick; 1,048,576 per second).
  Implementations MUST reset the sequence when the timestamp advances.
  If the sequence is exhausted within a single tick, the generator
  MUST wait for the next tick before issuing new identifiers,
  applying backpressure to maintain uniqueness guarantees.

  Implementations SHOULD consider adding randomness to the sequence
  starting value to reduce predictability of sequence patterns.

## CDDL Definition

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

## Generation Algorithm

Given `entityType`, `appId`, `appInstanceId`, and a configured `epoch`:

~~~
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

## Parsing Algorithm

Given a 64-bit signed integer `value` and a configured `epoch`:

~~~
procedure ParseSKID(value, epoch):
  1. Extract bit  63       → sign bit
  2. Extract bits [62..31] → timestamp (32 bits, unsigned)
  3. Extract bits [30..24] → appId (7 bits)
  4. Extract bits [23..18] → appInstanceId (6 bits)
  5. Extract bits [17..0]  → sequenceId (18 bits)
  6. createdAt ← epoch + (timestamp × 250 milliseconds)
  7. Return {value, createdAt, sequenceId, appId, appInstanceId, signBit}
~~~

## Clock Drift Protection

The system handles backward time jumps at two levels:

- **Minor drifts**: The generator freezes the timestamp until the wall
  clock catches up, allowing the sequence counter to continue advancing
  within the frozen tick.  The reference implementation uses 5 seconds
  as the freeze threshold.

- **Critical drifts (beyond freeze threshold)**: The application
  instance MUST initiate a graceful shutdown and restart with a new
  instance ID.  This dual-threshold mechanism prevents duplicate or
  out-of-order identifiers without requiring external coordination.


# Source Known Entity ID (SKEID) - 128-bit Specification

## Byte Layout

An SKEID occupies 16 bytes (128 bits), structured as follows in
big-endian (network byte order) per RFC 9562:

~~~
Byte:  0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15
     +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
     |EP| SKID Upper|S0|VE|ET|VA|S1|S2|S3| MAC       |
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
ordering in lexicographic comparisons across both epoch halves.  The
most significant byte of the lower SKID half occupies byte 5,
immediately after the upper half, ensuring that the timestamp
least-significant bit and leading application topology bits participate
in lexicographic comparison before the entity type at byte 7.  The
remaining lower SKID half is split around the variant marker at byte 8,
with bytes 9-11 holding the last three bytes.  Since the version
marker, entity type, and variant marker are constant for all plain
SKEIDs of a given type, lexicographic comparison of the split lower
half operates correctly.

## Marker Bytes

The marker bytes at positions 6 and 8 serve dual purposes:

1. **UUID V8 Compatibility**: `0x8D` at byte 6 satisfies the RFC 9562
   Section 5.8 version octet requirement, and `0x8D` at byte 8
   satisfies the RFC 9562 Section 4.1 variant octet requirement,
   making the SKEID a valid RFC 9562 UUID V8 when formatted as a
   string.

2. **Detection Signal**: During parsing, the presence of `0x8D` at
   byte 6 and `0x8D` at byte 8 identifies a plaintext (non-encrypted)
   SKEID.  Absence of these markers triggers the secure (decryption)
   parse path.

### Variant Byte Range

The variant marker position (byte 8) uses the primary value 0x8D plus
50 alternative values 0x8E through 0xBF for the Secure SKEID collision
guard ("Encryption" below), giving 51 total attempts.  The primary
variant byte is 0x8D; alternative variant bytes are used when the
AES-256-ECB ciphertext would otherwise be indistinguishable from a
plain (plaintext) SKEID.

All values in the range 0x80-0xBF share the RFC 4122 variant bit
pattern: the two high bits are `10` (binary).  The post-decryption
(secure) path uses the bit-mask check `(byte & 0xC0) == 0x80` to
accept any RFC 4122 variant byte.

The plain (plaintext) path MUST only accept the primary variant
byte 0x8D.  The post-decryption (secure) path MUST accept the full
RFC 4122 variant range (0x80-0xBF) via the bit-mask check.

If all 51 attempts (1 primary + 50 alternatives) are exhausted without
resolving the collision (probability ~1/2^(48×51)), the implementation
MUST throw a server-error exception (e.g., JackpotException with
HTTP 500 status) rather than loop indefinitely.

| Byte | Value | Usage |
|------|-------|-------|
| Version marker (byte 6) | 0x8D | All SKEIDs (plain and secure) |
| Variant marker (byte 8) | 0x8D | Primary variant - used by default |
| Variant marker (byte 8) | 0x8E-0xBF | Alternative variants - collision guard escalation |

Total attempt budget: 1 primary (0x8D) + 50 alternatives (0x8E-0xBF) = 51.

## MAC Computation

Integrity verification relies on BLAKE3 keyed MAC [BLAKE3].  The
computation procedure clears the four MAC bytes (positions 12-15)
to zero, then computes a 4-byte BLAKE3 keyed MAC over the full
16-byte buffer.  The resulting 4 bytes are written into the contiguous
MAC positions (bytes 12-15).

~~~
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

~~~
procedure WriteMACToGUID(guidBytes, hashBytes[0..3]):
  guidBytes[12] ← hashBytes[0]
  guidBytes[13] ← hashBytes[1]
  guidBytes[14] ← hashBytes[2]
  guidBytes[15] ← hashBytes[3]
~~~

Reading extracts from the same positions:

~~~
procedure ReadMACFromGUID(guidBytes) → hashBytes[0..3]:
  hashBytes[0] ← guidBytes[12]
  hashBytes[1] ← guidBytes[13]
  hashBytes[2] ← guidBytes[14]
  hashBytes[3] ← guidBytes[15]
~~~

## CDDL Definition

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

## Generation Algorithm

Given a 64-bit SKID `id`, `epoch`, `entityType`, and `macKey`:

~~~
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

## Parsing Algorithm

Given a UUID `entityId`, `macKey` (for MAC verification), and `aesKey`
(for decryption if the secure path is needed):

Note: When a key-ring is in use, this procedure is invoked by the
key-ring fallback loop described in "Parse with Key-Ring Fallback"
below.  Each iteration supplies a different (macKey, aesKey) pair.
The pseudocode below shows a single-key invocation; see
"Parse with Key-Ring Fallback" for the multi-key wrapper.  The
reference implementation [DRN-PROJECT] currently uses a single
default key; key-ring iteration is planned as future work.

~~~
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
       -- RFC 4122 variant bit pattern check (accepts 0x80-0xBF)
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

~~~
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
       sourceKnownId ← ParseSKID(id, epochByte)
       Return {sourceKnownId, entityId, entityType, epoch: epochByte, valid=true, secure}
     Else:
       Return INVALID
~~~


# Secure SKEID Specification

## Encryption

A Secure SKEID is produced by encrypting the entire 16-byte SKEID
plaintext using AES-256-ECB [FIPS197]:

~~~
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

~~~
procedure HasCoincidentalMACMatch(ciphertextBytes, macKey):
  1. workingCopy ← copy of ciphertextBytes[0..15]
  2. actualMAC ← ReadMACFromGUID(workingCopy)
  3. ClearMACSlots(workingCopy)  -- clear bytes 12-15
  4. expectedMAC ← ComputeMAC(workingCopy, macKey)
  5. Return actualMAC == expectedMAC
~~~

~~~
procedure EncryptGUIDBlock(guidBytes[0..15], aesKey):
  1. ciphertext ← AES-256-ECB-Encrypt(key=aesKey, plaintext=guidBytes)
     -- single 128-bit block, no padding
  2. Copy ciphertext → guidBytes[0..15]
~~~

## Decryption

~~~
procedure DecryptGUIDBlock(guidBytes[0..15], aesKey):
  1. plaintext ← AES-256-ECB-Decrypt(key=aesKey, ciphertext=guidBytes)
     -- single 128-bit block, no padding
  2. Copy plaintext → guidBytes[0..15]
~~~

## AES-ECB Justification

AES in ECB mode has a well-known weakness: when encrypting multiple
blocks with the same key, identical plaintext blocks produce identical
ciphertext blocks, leaking structural patterns.

This weakness does not apply to SKEIDs because:

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
   [BONNETAIN2019], which remains computationally infeasible.  NIST
   considers AES-256 a suitable symmetric cipher for post-quantum
   security at the 128-bit level.

Implementations MUST use AES-256 (256-bit key) for the encryption key.

## Key Separation

A SKEID system MUST use two cryptographically independent keys derived
from each key-ring entry:

| Key | Algorithm | Purpose |
|-----|-----------|---------|
| MAC Key | BLAKE3 keyed MAC | Integrity verification |
| AES Key | AES-256-ECB | Confidentiality (encryption) |

The two keys MUST NOT be the same value.  Implementations SHOULD derive
both keys from a single master secret using a key derivation function
or a deterministic hash chain that produces cryptographically
independent outputs.

## Auto-Detection Parse Logic

The parse algorithm ("Parsing Algorithm" above) automatically
determines whether a UUID is a plain SKEID, a Secure SKEID, or
an unrecognized value:

1. Convert the UUID to a 16-byte big-endian byte array (network byte
   order, per RFC 9562).
2. Check for exact primary marker bytes (`0x8D` at position 6, `0x8D`
   at position 8) in the byte array.
3. If both exact markers are present: attempt plaintext verification.
   If MAC verification succeeds → plain SKEID.
4. If exact markers are absent OR plaintext verification fails:
   decrypt the 16-byte block with the AES key.
5. Check the decrypted plaintext for markers using a wider acceptance
   rule: `guidBytes[6] == 0x8D` (exact version match) AND
   `(guidBytes[8] & 0xC0) == 0x80` (RFC 9562 §4.1 variant range
   0x80-0xBF).  The wider variant acceptance is necessary because the
   collision guard may have incremented the variant byte beyond 0x8D
   during generation.
6. If the recovered variant byte is within the RFC 9562 variant range
   but less than 0x8D, return INVALID.  The generator never produces
   variant bytes below 0x8D; such a value indicates tampering or an
   unrelated UUID.
7. If the recovered variant byte exceeds 0x8D, perform backward
   collision-guard verification as specified in the Backward
   Verification Algorithm section.  If verification fails → INVALID.
8. If markers are present AND MAC verifies → Secure SKEID.
9. Otherwise → INVALID (not a recognized SKEID).

The probability of a random or encrypted byte sequence coincidentally
containing the marker bytes 0x8D8D at the exact positions is
approximately 1/65,536.  When this occurs, the algorithm gracefully
falls back to the decryption path with no data corruption.

### Collision Guard Guarantee

Without the collision guard, there exists a combined ~1/2^48 probability
that ciphertext coincidentally matches both the plaintext marker bytes
AND produces a valid MAC when interpreted as a plaintext SKEID.  This
would cause a Secure SKEID to be misclassified as a plain SKEID
with incorrect data.

The collision guard in GenerateSecureSKEID ("Encryption" above)
eliminates this edge case by construction: when SKEID Marker and MAC
collision is detected in the ciphertext, the plaintext is regenerated
with successive variant bytes from 0x8E through 0xBF (50 alternatives).
Due to the AES avalanche effect, each variant produces completely
different ciphertext.  Deterministic termination is guaranteed: the
loop is bounded at 51 total attempts (1 primary + 50 alternatives),
with an implementation-defined error on exhaustion.  The probability
of exhausting all 51 attempts is approximately 1/2^(48×51), a value
with over 700 digits in the denominator, far below any practical
threshold.

The complete defense-in-depth architecture is described in
"Security Considerations" below.

### Backward Verification Algorithm

During parsing, when a non-default variant byte V is recovered from the
decrypted plaintext (V > 0x8D), the parse algorithm MUST verify that
the previous variant (V−1) genuinely triggered the collision guard:

~~~
procedure VerifyCollisionGuardProof(decryptedBytes, macKey, aesKey,
                                     previousVariant):
  1. Extract id, entityType from decryptedBytes
  2. epochByte ← decryptedBytes[0]
  3. Reconstruct plaintext with variant = previousVariant:
     reconstructed ← GenerateSKEID(id, epochByte, entityType, macKey,
                                    variantByte=previousVariant)
  4. EncryptGUIDBlock(reconstructed, aesKey)
  5. Return reconstructed[6] == 0x8D AND reconstructed[8] == 0x8D
         AND HasCoincidentalMACMatch(reconstructed, macKey)
     -- True: previous variant genuinely collided → legitimate escalation
     -- False: variant was tampered → INVALID
~~~

This single-step backward proof is sufficient by induction: if variant
V is legitimate, then V−1 must have collided, and V−1's legitimacy
is either V−1 = 0x8D (base case, always legitimate) or proved by V−2
having collided (which was already verified at generation time).

This provides a deterministic guarantee: no Secure SKEID produced by a
compliant implementation can ever pass the plain parse path with a
valid result.

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
bytes 1-4 receive the sign-toggled SKID upper half (`0x8BEBC200` XOR
`0x80000000` = `0x0BEBC200`), byte 5 receives the most significant
byte of the SKID lower half, byte 6 receives the version marker
(`0x8D`), byte 7 receives the entity type (`0x0A`), byte 8 receives
the default variant marker (`0x8D`), bytes 9-11 receive the remaining
SKID lower half bytes, and the BLAKE3 keyed MAC is computed and placed
at bytes 12-15.

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

The generator activates the collision guard: it changes the plaintext
variant byte (position 8) from 0x8D to 0x8E, recomputes the BLAKE3
MAC over the modified plaintext, and encrypts the result with the
same K_aes, producing ciphertext C2.  Due to AES's avalanche
property, even a single-bit change in plaintext produces ciphertext
that differs in approximately 50% of its bits.  The probability that
C2 also triggers a collision is again approximately 1/2^48,
independent of the first collision.  In practice, C2 passes the
check and becomes the final Secure SKEID.

**Step 4 — Backward Verification at Parse Time:** When a consumer
parses C2 by decrypting with K_aes, the recovered plaintext reveals
variant byte 0x8E (greater than 0x8D).  The parser MUST verify that
the escalation was legitimate:

~~~
1. Reconstruct the SKEID with variant 0x8D (replacing 0x8E and
   recomputing the MAC).
2. Encrypt that reconstruction with K_aes to obtain C1.
3. Check that C1 exhibits the marker-plus-MAC coincidence,
   confirming the collision that justified the escalation.
4. Since 0x8D is the base case (always legitimate), the single
   backward step completes the proof.
~~~

If the backward check fails (the reconstructed C1 does not exhibit the
coincidence), the parser MUST reject the identifier as invalid.  This
prevents an attacker from crafting identifiers with arbitrary variant
bytes.

Exact hex values, byte layouts, and round-trip assertions in this
walkthrough are verified by the PaperNumericWalkthroughTests unit test
suite in the reference implementation [DRN-PROJECT].  Any code change
that would invalidate these values produces a test failure.

## Secure ↔ Plain Conversion

Given a valid (parsed) SKEID, implementations MAY convert between
secure and plain representations:

~~~
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

Each key-ring entry produces two independent keys ("Key Separation"
above): a MAC key for BLAKE3 and an AES key for encryption.

## Generation

All new SKEID and Secure SKEID generation MUST use the current default
key.

## Parse with Key-Ring Fallback

When parsing an SKEID, implementations MUST attempt verification with
the current default key first.  If verification fails (MAC mismatch or
no valid markers after decryption), implementations MUST iterate
through previous keys in reverse chronological order:

~~~
procedure ParseWithKeyRing(entityId, keyRing):
  -- Try current (default) key first
  result ← ParseSKEID(entityId, keyRing.current.macKey, keyRing.current.aesKey)
  If result.valid: Return result

  -- Fall back to previous keys, newest first
  For key in keyRing.previous (reverse chronological):
    result ← ParseSKEID(entityId, key.macKey, key.aesKey)
    If result.valid: Return result

  Return INVALID
~~~

## Rotation Window

During a key rotation event, implementations MUST support at least 2
active keys simultaneously (the old default and the new default) to
allow in-flight requests to complete.

Key rotation does not require re-encrypting existing database records
because:

1. **Source Known IDs are key-independent**: The 64-bit SKID stored in
   the database contains no cryptographic material.  It is generated
   purely from timestamp, topology, and sequence.

2. **SKEID regeneration is deterministic**: Given the SKID and entity
   type, a new SKEID or Secure SKEID can be generated with the new key
   at any time.

3. **Zero-downtime rotation**: After rotating keys, new identifiers use
   the new key.  Existing identifiers are still parseable via the
   key-ring fallback.  If re-generation is desired (e.g., after key
   compromise), the system can re-generate SKEIDs from the immutable
   SKIDs with the new key.

## Key Compromise Recovery

In the event of a key compromise:

1. Add the compromised key to the key-ring with a "compromised" label.
2. Set a new key as the default.
3. Optionally re-generate all SKEIDs from the underlying SKIDs using
   the new key.  This is a data-plane operation (database update) that
   does not require identifier format changes.

Because the 64-bit SKID is key-independent, no primary key data is at
risk.  Only the 128-bit SKEID/Secure SKEID representations, which are
derived values, need re-generation.


# Context-Based Identity and Transformations

## Three-Tier Model

~~~
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

| From | To | Operation |
|------|----|-----------|
| SKID → SKEID | `GenerateSKEID(skid, entityType, macKey)` |
| SKID → Secure SKEID | `GenerateSecureSKEID(skid, entityType, macKey, aesKey)` |
| SKEID → SKID | `ParseSKEID(skeid).source.id` |
| SKEID → Secure SKEID | `ToSecure(parsedSKEID)` |
| Secure SKEID → SKID | `ParseSKEID(secureSkeid).source.id` |
| Secure SKEID → SKEID | `ToPlain(parsedSecureSKEID)` |

All transformations are deterministic given the same inputs and keys.
The SKID is always recoverable from any SKEID representation.

## Context Security Configuration

Implementations MAY support per-context security configuration:

- **External-facing endpoints**: SHOULD always use Secure SKEID to
  prevent information leakage.
- **Trusted environment communication**: MAY use plain SKEID for
  reduced computational overhead when the network is trusted.
- **Database storage**: MUST store the 64-bit SKID as the primary key
  for optimal indexing and storage efficiency.

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

- **Validation**: After parsing, the extracted entity type SHOULD be
  compared against the expected type.  A mismatch indicates either an
  incorrect identifier or a cross-entity-type collision attempt.

- **Fail-Fast**: Implementations SHOULD throw a validation exception
  immediately when entity type mismatch is detected, providing both
  the expected and actual type names in the error payload.

## Entity Type Registry

Each deployment MUST maintain a mapping between entity types (byte
values 0-254) and their corresponding entity classes.  The value 255
(0xFF) is reserved to indicate an invalid or unrecognized entity type.

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

The sign-bit toggle in the SKEID byte layout ensures that lexicographic
comparison of UUID strings matches the signed chronological ordering
of the underlying SKIDs.  Natural ordering is maintained within each
half.  The transition from the first half to the second half preserves
monotonic ordering because negative values sort before positive values.

## Epoch Byte

Byte 0 in the SKEID carries an 8-bit epoch index, where each value
selects a 2^31-second window starting from 2025-01-01.  The epoch
byte occupies byte 0 to ensure that higher epoch values sort
lexicographically after lower epoch values, regardless of timestamp.
The value identifies which epoch the SKID timestamp is relative to:

| Value | Epoch Start | Epoch End (approx.) |
|-------|-------------|---------------------|
| 0x00  | January 1, 2025    | January 19, 2093  |
| 0x01  | January 19, 2093   | February 7, 2161  |
| ...   | ...                | ...               |
| 0xFF  | 19378              | 19446             |

With 256 possible epoch values, the system spans approximately 17,421
years of total coverage.

## Epoch Configuration

Implementations SHOULD allow the epoch to be configured at startup.
The epoch value MUST be consistent across all nodes in a deployment.
Implementations MUST validate that the system clock is ahead of the
configured epoch's start time.

## Backward Compatibility

When the epoch byte is introduced in a deployment:

- SKEIDs generated before epoch support use epoch byte 0x00 (the
  default zero-initialized value).
- No existing SKEID is invalidated because the epoch byte was already
  zero.
- Parsers MUST include the epoch byte in MAC computation to
  prevent epoch byte tampering.

## Future Considerations

1. **Epoch transition**: The mechanism for transitioning from epoch
   0x00 to epoch 0x01 is not specified.  Given that epoch 0x00 spans
   until approximately 2093 CE, this is left as future work.

2. **Key-ring rotation with multiple active key versions and graceful
   rollover**: The reference implementation currently uses a single
   key pair.  The key-ring model and fallback algorithm are designed
   but planned as future work.

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
| App ID | Originating application | "Which application created this?" |
| App Instance ID | Specific instance | "Which pod/container?" |
| Sequence ID | Generation order within that tick | Volume analysis |
| Entity Type | Type of entity | "Is this a User, Order, or Product?" |
| Secure flag | Whether AES encryption was applied | Security posture check |

## Support Workflow

A support engineer receiving a GUID from an end-user can:

1. Parse the GUID as an SKEID (auto-detecting secure vs plain).
2. Extract creation time → "Created on 2026-03-07 at 14:30:00 UTC."
3. Extract app ID → "Generated by order-service (app ID 5)."
4. Extract entity type → "This is an Order entity (type 12)."
5. All without any database query or service call.

For Secure SKEIDs, parsing requires the appropriate AES key. Access to
the key-ring is sufficient to perform diagnostics.


# Performance and Storage Analysis

## Storage Comparison

| Scheme | Size | Ordered | Type-Safe | Integrity | Confidentiality |
|--------|------|---------|-----------|-----------|-----------------|
| SKID | 8 B | Yes | No | No | No |
| SKEID | 16 B | Yes (via SKID) | Yes | Yes (MAC) | No |
| Secure SKEID | 16 B | No (encrypted) | Yes (after parse) | Yes (MAC) | Yes (AES-256) |
| UUID V4 | 16 B | No | No | No | N/A |
| UUID V7 | 16 B | Yes | No | No | N/A |
| Snowflake | 8 B | Yes | No | No | No |
| ULID | 16 B | Yes | No | No | No |
| CUID2 | 24 chars | No | No | No | Hashing |
| KSUID | 20 B | Yes | No | No | No |
| DB Sequence | 4-8 B | Yes | No | No | No |
| Dual ID (int + UUID) | 8 B + 16 B = 24 B | Integer only | No | No | No |

A comprehensive feature comparison that includes additional
characteristics is provided in "Comparison with Existing Schemes"
below.

## Index Optimization

SKIDs as 64-bit integers provide superior B-tree index performance
compared to 128-bit UUIDs:

- **Smaller keys**: 8 bytes vs 16 bytes means more keys per B-tree
  page, fewer page splits, and better cache utilization.
- **Natural ordering**: Timestamp-leading layout means new records
  append to the end of the index (insert-optimized for append-heavy
  workloads).
- **Cursor-based pagination**: The SKID's monotonic nature enables
  efficient `WHERE id > :lastId ORDER BY id LIMIT :pageSize` queries
  without offset-based pagination overhead.

## Conversion Overhead

| Operation | Computational Cost |
|-----------|--------------------|
| SKID generation | ~35 ns (bit packing + atomic counter) |
| SKEID generation (plain) | ~230 ns (BLAKE3 MAC + bit packing) |
| Secure SKEID generation | ~544 ns (BLAKE3 MAC + AES-256 encrypt) |
| Plain SKEID parsing | ~224 ns (marker check + BLAKE3 verify) |
| Secure SKEID parsing | ~541 ns (AES decrypt + marker check + BLAKE3 verify) |
| ToSecure (encryption only) | ~524 ns |
| ToPlain (decryption only) | ~217 ns |

The overhead is dominated by the BLAKE3 keyed MAC and AES-256 single-
block operations, both of which execute in constant time.  Approximate
values are from the reference implementation benchmarks (see Appendix B).

## Throughput Analysis

The 18-bit sequence field caps generation at 262,144 identifiers per
250ms tick (1,048,576 per second) per instance.  With 128 applications
and 64 instances per application (8,192 total generators), the
theoretical maximum system-wide throughput is approximately 8.6 billion
identifiers per second.  Full-throttle benchmarks confirm that the
sequence manager applies backpressure when the per-instance limit is
reached, preserving uniqueness under sustained load.


# Comparison with Existing Schemes

## Feature Matrix

| Feature | SKID System | UUID V4 | UUID V7 | Snowflake | ULID | CUID2 | KSUID | DB Seq |
|---------|-------------|---------|---------|-----------|------|-------|-------|--------|
| **Uniqueness** | Per-entity-type, per-deployment | Global | Global | Per-datacenter | Global | Global | Global | Per-table |
| **Ordering** | Yes (timestamp) | No | Yes (timestamp) | Yes (timestamp) | Yes (timestamp) | No | Yes (timestamp) | Yes |
| **Embedded timestamp** | Yes (250ms) | No | Yes (millisecond) | Yes (millisecond) | Yes (millisecond) | No (hashed) | Yes (second) | No |
| **Multi-century addr.** | Yes | N/A | Yes | No | Yes | N/A | Partial | N/A |
| **Entity type** | Yes (8-bit) | No | No | No | No | No | No | No |
| **Integrity check** | Yes (BLAKE3 MAC) | No | No | No | No | No | No | No |
| **Confidentiality** | Yes (AES-256) | No | No | No | No | Hashing | No | No |
| **Zero-lookup validation** | Yes | No | No | No | No | No | No | No |
| **DB primary key size** | 8 B | 16 B | 16 B | 8 B | 16 B | 24 chars | 20 B | 4-8 B |
| **External ID size** | 16 B | 16 B | 16 B | 8 B | 26 chars | 24 chars | 27 chars | N/A |
| **UUID compatible** | Yes (V8 markers) | Yes | Yes | No | No | No | No | No |
| **Key rotation** | Yes (key-ring) | N/A | N/A | N/A | N/A | N/A | N/A | N/A |

SKID 64-bit values are unique within an (entity-type, deployment)
scope.  Two different entity types MAY share the same 64-bit SKID
value; cross-entity-type disambiguation is provided by the SKEID's
entity type byte (byte 7).

## Security Comparison

| Threat | UUID V4 | UUID V7 | Snowflake | SKEID/Secure SKEID |
|--------|---------|---------|-----------|---------------------|
| Enumeration | 122-bit random | Timestamp-ordered | Timestamp-ordered | MAC + optional AES |
| Forgery | No detection | No detection | No detection | MAC verification |
| Information leakage | None (random) | Timestamp visible | Timestamp + worker visible | AES-256 encrypted (Secure) |
| Cross-type confusion | Possible | Possible | Possible | Entity type verification |

| Threat | ULID | CUID2 | KSUID | SKEID/Secure SKEID |
|--------|------|-------|-------|---------------------|
| Enumeration | TS + 80-bit random | Hash-based | TS + 128-bit random | MAC + optional AES |
| Forgery | No detection | No detection | No detection | MAC verification |
| Information leakage | Timestamp visible | None (hashed) | Timestamp visible | AES-256 encrypted (Secure) |
| Cross-type confusion | Possible | Possible | Possible | Entity type verification |

## When to Use SKIDs

SKIDs are most appropriate when:

- Multiple trust boundaries exist (database, internal services, external
  consumers) and identifiers must be adapted per boundary.
- Zero-lookup verification of identifier origin and integrity is
  valuable.
- Entity type enforcement at the identifier level is desired.
- Compact 8-byte database keys with natural ordering are preferred.

SKIDs may not be the best choice when:

- Global uniqueness without a deployment registry is required (use
  UUID V4).
- Sub-millisecond timestamp precision is critical (UUID V7 offers
  millisecond precision vs SKID's 250-millisecond tick precision).
- The system has no concept of entity types or trust boundaries.


# Conventions and Assumptions

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

In development environments, plaintext key configuration in local
settings (e.g., `appsettings.Development.json`) is acceptable.

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

SKIDs SHOULD be serialized as 64-bit signed integers in APIs and
database columns.

SKEIDs and Secure SKEIDs SHOULD be serialized using the standard UUID
string format (8-4-4-4-12 hexadecimal) as defined in [RFC9562].

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
freeze threshold force an application restart with a new instance
identifier.  This separation of concerns keeps the identifier
generator simple and deterministic.

**32-bit truncated MAC vs. full MAC**: The 4-byte MAC is shorter than
typical MACs (16-32 bytes) but is one layer in a defense-in-depth
strategy ("Security Considerations" below).  The MAC is not the sole
security mechanism.  It works in concert with AES encryption, marker
detection, entity type matching, and record existence probability.

**AES-ECB vs. other modes**: ECB is chosen for its mathematical
fitness to single-block encryption ("AES-ECB Justification" above).
This is a deliberate, justified choice, not an oversight.

**Entity type in identifier**: Embedding entity type adds 8 bits of
overhead but enables zero-lookup type validation and prevents
cross-entity confusion attacks.

**Coordination vs. probabilistic uniqueness**: The SKID system
requires deployment-time coordination to assign App ID (7 bits, up
to 128 applications) and App Instance ID (6 bits, up to 64 instances
per application).  This stands in contrast to UUID V4, UUID V7, and
CUID2, which achieve uniqueness without any coordination through
cryptographic random number generation.  The coordination cost is an
intentional architectural trade-off: deterministic uniqueness by
construction eliminates the residual collision probability inherent
in probabilistic schemes, enables zero-lookup verification through
MAC and topology metadata, and allows the compact 64-bit
representation that probabilistic schemes cannot achieve at
equivalent uniqueness guarantees.

**Topology field sizing**: The 7-bit application field (128
applications) and 6-bit instance field (64 instances per application)
reflect practical operational boundaries rather than arbitrary bit
allocation.  Beyond 128 independently deployed applications,
coordination complexity becomes infeasible due to the increase in the
number of intercommunication paths.  Well-designed systems mitigate
this through bounded contexts rather than unbounded application
proliferation.  Similarly, beyond approximately 10 concurrently
active instances per application, resource contention on shared
infrastructure (databases, message brokers) typically becomes the
throughput bottleneck before the identifier generator itself.  The
6-bit field accommodates up to 64 instance identifiers, providing
headroom for operational needs such as rolling deployments and
application restarts triggered by clock drift protection, where the
restarting instance must acquire a new instance identifier to prevent
duplicate identifiers.  These limits are defaults for the general
case.  The system is intentionally optimized for stable, bounded
distributed application topologies.  Domain-tailored bit layouts
remain available for specialized deployments requiring different
trade-off profiles.

## Limitations

1. **Deployment-scoped uniqueness**: SKIDs are unique within a
   deployment (same epoch, same key-ring) but not globally unique
   across independent deployments.  Applications requiring cross-
   deployment identity merging must include deployment identifiers
   in their routing logic.

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
   provide formal cryptographic reductions (e.g., proving that the
   composition of AES-PRP encryption, BLAKE3 MAC, and the collision
   guard mechanism achieves the intended security goals under standard
   assumptions).  The Secure SKEID scheme inherits PRP security from
   the AES block cipher, and the uniqueness of SKEID plaintexts
   prevents ciphertext equality leakage.  A formal compositional
   security proof is left as future work.  The reference implementation
   provides test vectors in its test suites enabling independent
   verification.

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

   Since 68 years exceeds the operational lifespan of most modern
   software systems, this constraint does not diminish practical
   utility.

6. **Secure SKEID UUID format compliance**: A Secure SKEID is a valid
   UUID in length and string format (8-4-4-4-12 hexadecimal grouping)
   and is accepted by standard data-type parsers.  However, AES-256
   encryption replaces the plaintext Version and Variant marker bytes
   with pseudorandom ciphertext.  The encrypted output will almost
   never contain the 0x8 version nibble and 10xx variant bits required
   by RFC 9562 Section 4.1 and Section 5.8.  Strict RFC 9562 validators
   will reject a Secure SKEID as having an unknown version.  Systems
   requiring strict RFC 9562 compliance at the validator level SHOULD
   use plaintext SKEIDs or accept Secure SKEIDs as opaque 128-bit
   values.

## Open Questions for Community

1. Should the SKEID include a version byte to allow future layout
   changes?
2. Is there demand for a 96-bit intermediate representation (e.g.,
   for systems where 128-bit UUIDs are not used)?
3. Should the specification standardize a key derivation function (KDF)
   recommendation for producing independent MAC/AES keys from a master
   secret?


# Security Considerations

## Threat Analysis (STRIDE)

The following systematic threat analysis applies the STRIDE
categories [SHOSTACK2014] to the three-tier identity model:

**Spoofing (Identifier Fabrication)**:
An attacker who intercepts a Secure SKEID cannot derive the plaintext
SKEID without the AES-256 key.  Forging a valid Secure SKEID requires
producing ciphertext that, when decrypted, yields valid markers, a
correct BLAKE3 MAC, a valid entity type, and a SKID corresponding to
an existing record.  The multiplicative barrier across the defense-in-
depth layers makes this computationally infeasible.  For plaintext
SKEIDs within trusted environments, the BLAKE3 MAC prevents
identifier fabrication without the MAC key.

**Tampering (Identifier Modification)**:
Any modification to a Secure SKEID produces different plaintext upon
decryption (AES-256 PRP property), invalidating the MAC with
probability 1 - 2^(-32).  For plaintext SKEIDs, modifying any byte
invalidates the MAC because the MAC is computed over the full
16-byte buffer.

**Repudiation**:
The SKEID system does not provide non-repudiation in the cryptographic
sense (no digital signatures).  However, the embedded topology
metadata (App ID, App Instance ID, Entity Type) and timestamp provide
forensic traceability sufficient for audit trails in most enterprise
scenarios.

**Information Disclosure**:
Plaintext SKEIDs deliberately expose metadata within trusted
environments where this information supports routing and validation.
For external consumers, Secure SKEIDs encrypt the entire identifier
using AES-256-ECB, which functions as a PRP on the single 128-bit
block.  The ciphertext is computationally indistinguishable from
random bytes without the key.

**Denial of Service**:
The 18-bit sequence counter limits generation to 262,144 identifiers
per 250-millisecond tick (1,048,576 per second) per instance.  Exceeding
this rate triggers backpressure (the generator waits for the next tick),
which is a deliberate safety mechanism rather than a vulnerability.
Parse operations are bounded-time with no external dependencies,
preventing parse-amplification attacks.

**Elevation of Privilege (Cross-Entity Confusion)**:
The 8-bit entity type discriminator prevents cross-entity attacks
where an identifier for one entity type is submitted as another,
potentially granting unintended access.  Because the MAC includes the
entity type in its computation, a valid SKEID for one entity type
cannot pass MAC verification when checked against a different type.

## Defense in Depth

The SKEID system employs multiple verification layers.
An attacker attempting to forge a valid identifier must defeat all
applicable layers simultaneously:

| Layer | Mechanism | Bypass Probability |
|-------|-----------|--------------------|
| 1. AES-256 Encryption | PRP over 128 bits | 2^(-128) without the key |
| 2. BLAKE3 Keyed MAC | 32-bit truncated | 2^(-32) per attempt |
| 3. Marker Detection | 0x8D8D at fixed positions | 2^(-16) |
| 4. Entity Type Match | Must match the expected type | 2^(-8) |
| 5. Topology Validity | App ID and Instance ID must exist | Variable |
| 6. Existence Probability | Forged SKID must reference a record | Variable |
| 7. Rate Limiting | Restricts attempts per time period | Implementation-defined |

Even if an attacker guesses a ciphertext that, when decrypted, has
valid markers, a valid MAC, a valid entity type, and valid topology,
the resulting SKID must still correspond to an actual record in the
database.  The multiplicative combination of these barriers offers
security far exceeding any individual layer.  The layers are assumed
to be operationally independent under secure key derivation.
Each layer uses a different cryptographic primitive or validation mechanism.
A formal proof of probabilistic independence is outside the scope of
this document.

## MAC Truncation Analysis

The 32-bit truncation satisfies the minimum MacTag length specified in
NIST SP 800-107 (Section 5.3.3), although that document notes that
MacTags shorter than 64 bits are discouraged for standalone use in
Section 5.3.5.  The 128-bit SKEID format constrains the available
space for the MAC field, as the remaining bits carry the SKID, entity
type, epoch, and marker bytes.  This constraint is acceptable because
the MAC is not the sole security mechanism; it operates within the
defense-in-depth architecture described above.

Section 5.3.5 of the same document provides the general framework for
assessing truncated MAC forgery risk.  For a lambda-bit MacTag with
2^t failed verifications allowed, the likelihood of accepting forged
data is (1/2)^(lambda - t).  Applying this to the 32-bit SKEID MAC
(lambda = 32): if a system permits 2^12 (4,096) failed verification
attempts before rotating the MAC key, the forgery likelihood is
(1/2)^20, approximately one in a million.  At 100 rate-limited
attempts per second, this 2^12 budget is exhausted in roughly 41
seconds.

NIST SP 800-107 Rev. 1 was withdrawn in 2022 [NISTSP800107W].  Its
successor, NIST SP 800-224, preserves the same truncated MAC analysis
and further
stipulates that tag lengths below 64 bits require careful risk
analysis, which this section and the defense-in-depth architecture
provide.

## AES-256 and Post-Quantum Readiness

AES-256 retains 128-bit security under Grover's quantum algorithm
(which reduces effective symmetric key strength by half).  NIST
recommends AES-256 as suitable for post-quantum symmetric encryption.

Implementations MUST use 256-bit AES keys (32 bytes).  128-bit or
192-bit keys MUST NOT be used.

## Sequence Randomization

To reduce the predictability of sequence patterns, implementations
SHOULD consider randomizing the starting sequence value within each
timestamp interval.  This hardening measure prevents an attacker from
inferring generation order or volume patterns.

If sequence randomization is insufficient for the threat model,
deployments SHOULD consider using true UUID V4 identifiers instead of
SKIDs.

## Rate Limiting

Endpoints that accept SKEID values from untrusted sources (e.g.,
public APIs) MUST implement rate limiting to prevent brute-force
attacks against the MAC.  Implementations SHOULD log and alert on
sustained high-rate SKEID validation failures.

## Key Management

- Keys MUST be distributed through a secure channel.
- Keys MUST NOT be stored in plaintext in production environments.
- Key rotation MUST follow the key-ring model
  ("Key Rotation and Key-Ring Fallback" above).
- Key compromise recovery MUST follow the procedure in
  "Key Compromise Recovery" above.

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
(submitted separately) contains a full AI disclosure per journal
policy.

--- back

# Test Vectors

The following test vectors use a well-known sequential test key
(0x00 through 0x1F) for reproducibility.  Implementers SHOULD
verify that their implementation produces identical outputs for
these inputs before substituting production key material.

## Test Key Material

~~~
MAC Key (32 bytes, hex):   000102030405060708090A0B0C0D0E0F
                           101112131415161718191A1B1C1D1E1F
MAC Key (base64url):       AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8
AES Key (32 bytes, hex):   64EB58C637B051F1F3BF93A40C669E46
                           F2DB5DCDFDEAFF05592782EEB485643C
AES Key (base64url):       ZOtYxjewUfHzv5OkDGaeRvLbXc396v8FWSeC7rSFZDw
Epoch:                     2025-01-01T00:00:00Z
~~~

The AES key is derived from the MAC key via a deterministic hash
chain (see "Key Separation" above).  Implementations MUST derive
both keys from the same input to reproduce these vectors.

## SKID Generation

~~~
Input:
  entityType     = 1
  appId          = 5
  appInstanceId  = 3
  timestamp      = 272000000 (250ms ticks since epoch, = 68000000 seconds)
  sequenceId     = 42

Expected bit layout (MSB first):
  Sign:          1                                      (1 bit)
  Timestamp:     0001_0000_0011_0110_0110_0100_0000_0000 (32 bits)
  AppId:         000_0101                                (7 bits)
  AppInstanceId: 00_0011                                 (6 bits)
  SequenceId:    00_0000_0000_0010_1010                   (18 bits)

Expected SKID (decimal):  -8639256484514103254
Expected SKID (hex):      0x881B3200050C002A
~~~

## SKEID Generation (Plain)

~~~
Input:
  SKID       = -8639256484514103254 (0x881B3200050C002A)
  entityType = 1
  macKey     = [from A.1]

Expected byte layout (big-endian per RFC 9562):
  Byte  0: 0x00   (epoch)
  Byte  1: 0x08   (sign-toggled SKID upper half, byte 0)
  Byte  2: 0x1B   (sign-toggled SKID upper half, byte 1)
  Byte  3: 0x32   (sign-toggled SKID upper half, byte 2)
  Byte  4: 0x00   (sign-toggled SKID upper half, byte 3)
  Byte  5: 0x05   (SKID lower half, MSB)
  Byte  6: 0x8D   (version marker)
  Byte  7: 0x01   (entity type)
  Byte  8: 0x8D   (variant marker)
  Byte  9: 0x0C   (SKID lower half, byte 1)
  Byte 10: 0x00   (SKID lower half, byte 2)
  Byte 11: 0x2A   (SKID lower half, byte 3)
  Byte 12: 0x62   (MAC byte 0)
  Byte 13: 0x79   (MAC byte 1)
  Byte 14: 0xD1   (MAC byte 2)
  Byte 15: 0x60   (MAC byte 3)

Expected GUID:  00081b32-0005-8d01-8d0c-002a6279d160
Expected hex:   00081B3200058D018D0C002A6279D160
~~~

## Secure SKEID Generation

~~~
Input:
  SKEID plaintext from A.3
  aesKey = [from A.1]

Operation:
  ciphertext = AES-256-ECB-Encrypt(key=aesKey, plaintext=SKEID)

Expected Secure GUID:  b37e3510-cc6d-a65f-0e11-96e237568223
Expected hex:          B37E3510CC6DA65F0E1196E237568223
~~~

## Round-Trip Verification

~~~
1. Generate SKID with inputs from A.2
   → SKID = -8639256484514103254 (0x881B3200050C002A)            ✓
2. Generate SKEID from SKID (A.3)
   → GUID = 00081b32-0005-8d01-8d0c-002a6279d160                ✓
3. Parse SKEID → extracted SKID matches original
   → Valid=true, SKID=-8639256484514103254                       ✓
4. Generate Secure SKEID from SKID (A.4)
   → GUID = b37e3510-cc6d-a65f-0e11-96e237568223                ✓
5. Parse Secure SKEID → extracted SKID matches original
   → Valid=true, SKID=-8639256484514103254                       ✓
6. ToSecure(SKEID) → matches Secure SKEID from step 4
   → GUID = b37e3510-cc6d-a65f-0e11-96e237568223                ✓
7. ToPlain(Secure SKEID) → matches SKEID from step 2
   → GUID = 00081b32-0005-8d01-8d0c-002a6279d160                ✓
~~~


# Reference Implementation (Informative)

## DRN-Project

The DRN-Project [DRN-PROJECT] provides a production-quality
implementation of the SKID system in .NET 10, including:

- `SourceKnownIdUtils`: 64-bit SKID generation and parsing using
  hardware-accelerated bit packing.
- `SourceKnownEntityIdUtils`: 128-bit SKEID/Secure SKEID generation,
  parsing, and conversion with BLAKE3 keyed MAC and AES-256-ECB.
- `SequenceManager<TEntity>`: Thread-safe, per-entity-type sequence
  manager with lock-free atomic operations and automatic time-scope
  advancement.
- `NexusAppSettings`: Key-ring configuration with `MacKeys[]` list,
  key separation (MAC key + AES key), and default key designation.

## Database Patterns

The reference implementation uses PostgreSQL with the following
optimizations:

- **Primary key**: `BIGINT` (8 bytes) storing the 64-bit SKID, with
  a default B-tree index providing natural insertion-ordered access.
- **Entity ID column**: `UUID` (16 bytes) storing the SKEID or Secure
  SKEID, with a unique index for external lookups.
- **Cursor-based pagination**: Uses `WHERE id > :lastSkid ORDER BY id
  LIMIT :pageSize` for efficient, offset-free pagination.
- **CreatedAt filtering**: The SKID's embedded timestamp enables
  time-range queries directly on the primary key without a separate
  `created_at` column.

## Benchmark Summary

Performance characteristics observed in the reference implementation
(BenchmarkDotNet [BENCHMARKDOTNET] v0.15.8; Apple M2, 8 cores; .NET 10.0.5 Arm64
RyuJIT; OutlierMode=RemoveUpper, InvocationCount=262,144,
IterationCount=120, WarmupCount=120):

| Operation | Mean (ns) | Error (ns) | StdDev (ns) | Allocated |
|-----------|-----------|------------|-------------|-----------|
| Random 64-bit integer generation | 171.3 | 2.36 | 7.66 | 32 B |
| Random UUID V4 generation | 354.1 | 3.64 | 11.81 | 0 B |
| Random UUID V7 generation | 377.5 | 3.24 | 10.53 | 0 B |
| SKID generation | 35.3 | 1.21 | 3.92 | 0 B |
| SKEID generation (plain) | 230.3 | 3.45 | 11.20 | 0 B |
| Secure SKEID generation | 544.0 | 5.67 | 18.42 | 72 B |
| Secure SKEID parsing | 540.7 | 3.15 | 10.22 | 72 B |
| Plain SKEID parsing | 223.6 | 2.87 | 9.31 | 0 B |
| ToSecure (encryption only) | 524.2 | 5.56 | 17.98 | 72 B |
| ToPlain (decryption only) | 217.3 | 2.40 | 7.76 | 0 B |

Error values represent the 99.9% confidence interval margin
computed by BenchmarkDotNet over iterations remaining after upper
outlier removal.

These are informative benchmarks and will vary by hardware, runtime,
and application workload.


# Practical Utilities (Informative)

## Cursor-Based Pagination

Given the SKID's monotonic ordering, cursor-based pagination is
straightforward:

~~~
SELECT * FROM entities
WHERE id > :last_skid
ORDER BY id ASC
LIMIT :page_size
~~~

The client sends the last SKID from the previous page as the cursor.
No offset tracking is needed.

## CreatedAt Extraction

Given a SKID and the configured epoch, the creation timestamp can be
extracted without a database query:

~~~
createdAt = epoch + (ParseSKID(skid).timestamp × 250 milliseconds)
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


