---
title: "Source-Known Identifiers (SKIDs)"
docname: draft-kilic-skid-00
category: info
submissiontype: independent
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
    initials: "D. S."
    surname: "Kılıç"
    organization: "Independent"
    email: "duran.serkan@outlook.com"

normative:
  RFC2119:
  RFC8174:
  RFC9562:
  RFC8610:
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
  SKID-PAPER:
    title: "Source-Known Identifiers: A Three-Tier Identity System for Distributed Applications"
    author:
      - name: Duran Serkan Kılıç
    seriesinfo:
      arXiv: "2604.00151"
    target: https://arxiv.org/abs/2604.00151
  DRN-PROJECT:
    title: "DRN-Project Reference Implementation"
    target: https://github.com/duranserkan/DRN-Project

date: 2026-10-17
---

--- abstract

This document specifies three representations of an entity identifier
for database storage, communication between trusted services, and
public interfaces. A 64-bit Source-Known ID (SKID) provides a
timestamp-ordered database key. A 128-bit Source-Known Entity ID
(SKEID) adds an entity type, epoch, and keyed message authentication
code. Secure SKEID encrypts that representation for external use without
increasing its size. The specification defines the layouts, generation
and parsing procedures, and deployment requirements for these forms.

--- middle

# Introduction

Source-Known Identifiers (SKIDs) represent one entity in three forms:

| Representation | Size | Intended use |
|----------------|------|--------------|
| Source-Known ID (SKID) | 64 bits | Timestamp-ordered database key |
| Source-Known Entity ID (SKEID) | 128 bits | Authenticated metadata in a shared-key trust domain |
| Secure SKEID | 128 bits | Encrypted identifier for external use |

For example, an order service can store a SKID as its database key,
send a SKEID to a trusted service, and use a Secure SKEID in a public
URL. These are representations of the same order. The service derives
the two 128-bit forms from the SKID using entity-type and epoch context
and cryptographic keys. Parsing recovers that context and the SKID
without a record lookup. The application still checks that the order
exists and that the caller may access it.

A plaintext SKEID is a Universally Unique Identifier (UUID) Version 8
as defined in [RFC9562]. Secure SKEID is an opaque 128-bit ciphertext
that can use UUID string formatting, but encryption does not preserve
UUID version or variant bits. SKID uniqueness is scoped to a deployment,
entity type, and epoch, subject to the allocation rules below.

The companion paper [SKID-PAPER] presents the design rationale,
comparisons with other identifier schemes, security analysis, and
evaluation.
{{test-vectors}} provides a worked example with exact byte values.

# Conventions and Definitions

The key words "MUST", "MUST NOT", "REQUIRED", "SHALL", "SHALL NOT",
"SHOULD", "SHOULD NOT", "RECOMMENDED", "NOT RECOMMENDED", "MAY", and
"OPTIONAL" in this document are to be interpreted as described in
BCP 14 [RFC2119] [RFC8174] when, and only when, they appear in all
capitals, as shown here.

Byte offsets are zero-based. Multi-byte fields use big-endian
(network) byte order. Signed integers use two's complement. SKID bit
positions run from 0, the least significant bit, to 63, the sign bit.
Hexadecimal constants begin with `0x`. Byte ranges include both
endpoints. In arithmetic expressions, `2^n` denotes exponentiation.

An **epoch** is a time window of 2^31 seconds selected by an 8-bit
**epoch index**. Its start is defined in {{epoch-definitions}}.
An **entity type** is an 8-bit discriminator within an application.
**App ID** identifies an application within a deployment. **App Instance
ID** identifies a generator instance within that application.

A **key pair** contains a 32-byte BLAKE3 key for the message
authentication code (MAC) and a separate 32-byte Advanced Encryption
Standard (AES) key. Exactly one pair is designated the **default key**,
used for generation and tried first during parsing. **Enabled keys**
consist of that pair and, when fallback is enabled, the configured
fallback pairs. The same enabled-key set and supported-epoch policy
apply throughout a generation, parsing, or conversion operation.

`VALID`, `INVALID`, and `ERROR` denote procedure outcomes. They do not
define an error encoding or require distinct client-visible responses.

# Source-Known ID

## Bit Layout

A SKID is a signed 64-bit integer with these fields:

| Field | Bits | Width | Values |
|-------|------|-------|--------|
| Epoch half | 63 | 1 | 1 for first half, 0 for second half |
| Timestamp | 62-31 | 32 | 250-millisecond ticks within the half |
| App ID | 30-24 | 7 | 0-127 |
| App Instance ID | 23-18 | 6 | 0-63 |
| Sequence | 17-0 | 18 | 0-262,143 |

The first half of an epoch uses negative values and the second uses
nonnegative values. Signed integer order therefore follows timestamp
order across both halves. Within a tick, topology and sequence fields
break ties. This order does not establish exact creation order between
independent generators.

## Generation and Parsing

Given an entity type, app ID, instance ID, and epoch start, first reject
field values outside their specified ranges. `now` is the current UTC
time, and `epochStart` is the start of the selected epoch.
Time arithmetic in this document excludes leap seconds and treats each
UTC calendar day as 86,400 seconds.

1. Compute `elapsedTicks = floor((now - epochStart) / 250 milliseconds)`.
   Apply the clock-handling rules in {{clocks-and-instances}}.
2. Reject elapsed ticks outside the range 0 through 2^33 - 1.
3. Set the sign bit to 1 if elapsed ticks are below 2^32, otherwise 0.
   Set the timestamp field to elapsed ticks modulo 2^32.
4. Allocate the next sequence value for this application instance and
   entity type. Reset the counter when the generation timestamp advances.
5. Pack the fields at the positions specified above and return the
   signed 64-bit value.

Sequence allocation and timestamp changes MUST be coordinated across
concurrent callers of a generator. For a given app ID, instance ID,
epoch, and entity type, a generator MUST NOT reuse a sequence value
within the same elapsed tick. After issuing all 262,144 sequence values,
it MUST wait for a later tick and recompute the timestamp before issuing
another identifier. Timestamp advancement refers to the full elapsed
tick count across both epoch halves.

To parse a SKID, extract the five fields. The elapsed tick count is
the timestamp field when the sign bit is 1, or the timestamp field
plus 2^32 when the sign bit is 0. The embedded time is the epoch start
plus that count multiplied by 250 milliseconds. The entity type and
epoch index are not present in the SKID and MUST come from its context.

## Clocks and Instance Assignment {#clocks-and-instances}

Within a deployment and epoch, generators MUST NOT issue the same
combination of app ID, instance ID, entity type, elapsed tick, and
sequence more than once. This requirement applies across process
restarts, crashes, and clock rollback. App IDs MUST distinguish
applications within the deployment. The same app ID and instance ID
MUST NOT be assigned to concurrent generator processes.

For a backward clock jump below the configured freeze threshold, the
generator retains its last generation timestamp and continues allocating
sequence values until the clock catches up. Sequence exhaustion still
requires waiting. At or beyond the threshold, the generator MUST stop
issuing identifiers and request application shutdown. The reference
implementation uses a five-second threshold.

Restarting with an empty sequence counter can repeat identifiers from
a previously used tick. The paper's deployment profile uses an external
configuration or coordination service to assign a different instance ID
on every restart, including ordinary restarts. It rotates through the
64-value pool subject to the following reuse conditions:

- The previous holder of the app ID and instance ID pair MUST have
  stopped before the pair is reassigned.
- Before generation resumes under a reused pair, the new elapsed tick
  MUST exceed the durably recorded maximum elapsed tick previously
  issued under that pair in the epoch, across all entity types.
- If no instance ID can be safely assigned, startup MUST wait or fail.

Safe instance assignment prevents identifier reuse after
restart but does not preserve chronological monotonicity across a clock
rollback. Clock synchronization and instance allocation are deployment
responsibilities.

# Source-Known Entity ID

## Byte Layout

A plaintext SKEID occupies 16 bytes:

~~~text
Byte:  0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15
     +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
     |EP| SKID Upper|S0|VE|ET|VA|S1|S2|S3|    MAC    |
     +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
~~~

| Field | Bytes | Encoding |
|-------|-------|----------|
| Epoch index | 0 | Unsigned 8-bit value |
| SKID upper half | 1-4 | SKID bits 63-32 XOR 0x80000000 |
| SKID low byte 0 | 5 | SKID bits 31-24 |
| Version marker | 6 | 0x8D |
| Entity type | 7 | Unsigned 8-bit value |
| Variant marker | 8 | 0x8D for plaintext SKEID |
| Remaining SKID bytes | 9-11 | SKID bits 23-0 |
| MAC | 12-15 | First four bytes of BLAKE3 keyed output |

The sign toggle maps signed SKID order to unsigned byte order. For a
fixed entity type, lexicographic comparison of the specified bytes or
consistently formatted UUID strings preserves epoch and SKID order.
Runtime UUID comparison functions can use different ordering rules.

The version marker satisfies the UUIDv8 version field. The default
variant marker satisfies the RFC 9562 variant field. Secure generation
can use values 0x8E through 0xBF at byte 8 before encryption, as described
below. These values MUST NOT be accepted by the plaintext parser.

## MAC and Generation

To construct a plaintext SKEID, initialize a 16-byte buffer, write the
fields above, and compute its MAC with BLAKE3 keyed mode [BLAKE3]:

~~~pseudocode
ComputeMAC(bytes, macKey):
  input = copy(bytes)
  input[12..15] = 0
  return BLAKE3_keyed(macKey, input, output_length=4)
~~~

Write the four output bytes, in output order, to bytes 12-15. The MAC
covers the full 16-byte input with only its own slots zeroed, including
the epoch, entity type, and marker bytes. Implementations MAY request
four output bytes directly rather than computing a longer output.
MAC comparisons MUST avoid data-dependent early exit.

In the procedures below,
`Encode(skid, epoch, entityType, variant, macKey)` constructs this layout
and writes the MAC. Here, `epoch` is the epoch index and `variant` is
the complete byte at offset 8, including the UUID variant bits.
Ordinary plaintext generation MUST use 0x8D. Secure generation can use
the additional values specified below to construct guard candidates.

## Plaintext Parsing

Given 16 bytes and a MAC key:

1. Require a supported epoch at byte 0 and exact markers 0x8D at
   bytes 6 and 8. Otherwise return INVALID.
2. Compare bytes 12-15 with `ComputeMAC` for the input and key.
   Return INVALID on mismatch.
3. Recover the epoch and entity type. Read bytes 1-4 and reverse the
   sign toggle. Combine that upper half with byte 5 and bytes 9-11
   to recover the signed SKID.
4. Return the recovered fields as a valid plaintext SKEID.

Successful parsing authenticates the embedded fields. Expected
entity-type and app-ID checks follow the rules in {{parsing-policy}}
for both plaintext and secure input.

## CDDL Types

The following Concise Data Definition Language (CDDL) rules [RFC8610]
describe value sizes. The layouts and validation procedures above define
the contents of the byte strings. These rules do not introduce an
additional wire envelope.

~~~cddl
skid = -9223372036854775808..9223372036854775807
skeid = bytes .size 16
secure-skeid = bytes .size 16
~~~

# Secure SKEID

Secure SKEID encrypts the entire SKEID as one AES-256 block [FIPS197],
with no initialization vector, nonce, or padding. Implementations MUST
use a 32-byte AES key. The resulting 16 ciphertext bytes replace all
plaintext fields.

## Generation and Collision Guard

Secure generation uses the default key pair. A candidate ciphertext
can coincidentally pass plaintext parsing. The collision guard rejects
such candidates under every enabled verification key:

~~~pseudocode
HasPlainCollision(candidate, enabledKeys):
  if candidate[0] is not a supported epoch:
    return false
  if candidate[6] != 0x8D or candidate[8] != 0x8D:
    return false
  for key in enabledKeys:
    if candidate[12..15] matches ComputeMAC(candidate, key.macKey):
      return true
  return false

GenerateSecure(skid, epoch, entityType, defaultKey, enabledKeys):
  for variant from 0x8D through 0xBF:
    plain = Encode(skid, epoch, entityType, variant, defaultKey.macKey)
    candidate = AES256_EncryptBlock(defaultKey.aesKey, plain)
    if not HasPlainCollision(candidate, enabledKeys):
      return candidate
  return ERROR
~~~

The generator MUST recompute the MAC and encrypt with the same key pair
for each candidate. There are 51 attempts, including the default
variant. Exhaustion MUST return an error without issuing an identifier.

Generation, plaintext parsing, and backward verification MUST use the
same epoch-acceptance policy and plaintext-acceptance checks. The guard
guarantee also depends on using the same enabled verification keys.
Expected entity-type and app-ID filters MUST NOT narrow the collision
predicate, since another caller can accept different identities.

## Secure Parsing

Given 16 ciphertext bytes and one key pair:

1. Decrypt the block with the AES key to obtain the plaintext buffer
   `plain`.
2. Require a supported epoch at byte 0, byte 6 equal to 0x8D, and
   `(plain[8] & 0xC0) == 0x80`. Otherwise return INVALID.
3. Verify the decrypted block's MAC with the pair's MAC key.
   If verification fails, return INVALID.
4. Keep this authenticated key pair fixed for all remaining checks.
   Reject variant values 0x80 through 0x8C without further key fallback.
5. For a variant greater than 0x8D, perform backward verification below.
   A failed proof MUST return INVALID without further key fallback.
6. Recover the SKID, epoch, and entity type using the plaintext layout
   and return them as a valid Secure SKEID.

The structural variant check in step 2 allows 0x80 through 0xBF.
Authentication does not make values below 0x8D valid. The parser MUST
distinguish authentication failure from a subsequent variant or proof
failure when applying key-ring fallback.

## Backward Verification

A variant above the default value 0x8D is valid only if every preceding
variant would have triggered the collision guard:

~~~pseudocode
VerifyPredecessors(skid, epoch, entityType, variant,
                   authenticatedKey, enabledKeys):
  for previous from variant - 1 down through 0x8D:
    plain = Encode(skid, epoch, entityType, previous,
                   authenticatedKey.macKey)
    candidate = AES256_EncryptBlock(authenticatedKey.aesKey, plain)
    if not HasPlainCollision(candidate, enabledKeys):
      return false
  return true
~~~

Reconstruction MUST use the authenticated key pair. Collision checks
MUST use all enabled verification keys. For example, accepting variant
0x8F requires collisions at both 0x8E and 0x8D. A collision at only the
immediate predecessor is insufficient. Verification stops at the first
missing collision and requires at most 50 predecessor checks.

# Parsing Policy and Tier Conversion {#parsing-policy}

SKEID and Secure SKEID parsing requires exactly 16 bytes after decoding
any textual representation. Other lengths MUST be rejected. The caller
selects the input policy before parsing:

- **Plain:** apply only plaintext parsing.
- **Secure:** apply only decryption and secure parsing.
- **Auto:** for each attempted key pair, try plaintext parsing first,
  then secure parsing if the plaintext epoch, markers, or MAC fail.

For Auto, an unsupported epoch in the input MUST NOT prevent a secure
attempt. Ciphertext byte 0 does not identify the encrypted epoch.
Endpoints exposed to untrusted networks MUST require Secure input.
Auto is suitable only where both representations are intentionally
accepted within a trusted boundary.

Use Secure SKEIDs in public responses, URLs, and cursors as well.
Exposing the raw SKID or plaintext SKEID alongside an encrypted value
reveals the metadata that encryption is intended to conceal.

With a key ring, try the default key first. If fallback is explicitly
enabled, try the remaining enabled keys in configured order. Apply the
selected input policy under each key before moving to the next key.
Proceed to the next key only if that policy fails without authenticating
a candidate. Once a candidate authenticates, variant or backward-proof
failure MUST reject the input without trying another key. Return the
first successful result, or INVALID if no key succeeds. This procedure
does not detect multiple authenticated interpretations.

After successful parsing under any input policy, a caller handling a
specific entity type or application partition MUST check the applicable
expected entity type and app ID against the authenticated fields.
A mismatch MUST reject the identifier without retrying other keys or
input policies. These checks precede use of the recovered identity and
remain separate from record existence and authorization checks.

Conversion MUST start from a validated identifier:

- **ToSecure:** return an already secure value unchanged. Otherwise,
  generate a Secure SKEID from its SKID, epoch, and entity type using
  the default key and current collision policy.
- **ToPlain:** return an already plain value unchanged. Otherwise,
  reconstruct the plaintext SKEID with variant 0x8D and recompute its
  MAC with the default MAC key. Do not expose a decrypted non-default
  variant as the plaintext representation.

Conversions preserve the underlying identity. Regeneration is
deterministic for the same identity fields, generation key pair,
enabled-key set, and supported-epoch policy. Changing these inputs can
change the 128-bit representation. A conversion that returns an input
unchanged does not migrate it to the current default key.

# Deployment Considerations

## Epochs and Entity Types {#epoch-definitions}

The default base epoch is 2025-01-01T00:00:00Z. Implementations SHOULD
allow a different UTC origin to be configured before first use.
An epoch index from 0 through 255 selects a window as follows:

~~~text
epochStart = baseEpoch + epochIndex * 2^31 seconds
~~~

The base epoch is deployment context and is not encoded in any
identifier. Generators and parsers sharing a dataset MUST use the same
base epoch. It MUST remain unchanged across services and restarts for
the lifetime of those identifiers. A different origin changes timestamp
interpretation without causing MAC verification to fail.

Each epoch contains two halves of 2^32 quarter-second ticks. The
256 possible epoch indices address 256 * 2^31 seconds, approximately
17,421 years. Deployments MUST accept only supported epochs. The
reference implementation currently generates and accepts only epoch 0.

A stored SKID does not carry its epoch index. Datasets spanning epochs
MUST retain that context through partitioning, an additional epoch
field, or the SKEID representation. The eight-byte storage property
applies when the epoch comes from the surrounding dataset or partition.
Operational epoch transitions remain unspecified.

Entity-type values MUST distinguish entity types within each application,
and their assignments MUST remain stable for the lifetime of identifiers.
Different entity types can have the same SKID. Cross-deployment data
merges also need a deployment namespace or coordinated assignments.
The field widths in this document are fixed. Alternative allocations
would need a separate format and compatibility rules.

## Key Management

The MAC and AES keys MUST be cryptographically independent 32-byte
values and MUST NOT be equal. Implementations SHOULD derive them using
a domain-separated key derivation function from a master secret.
BLAKE3's `derive_key` mode supports distinct, hardcoded, globally unique
context strings identifying the application and each key purpose
[BLAKE3]. Key derivation and distribution must be agreed by participating
systems.

All generation MUST use the current default key.
Previous keys MAY remain enabled for verification during routine
rotation. Fallback MUST require explicit opt-in. Key changes do not
alter stored SKIDs, but reissuing derived identifiers changes external
links and cached values.

The collision guard covers only the verification keys and epoch policy
used during generation. Adding keys can introduce another interpretation
of an existing identifier. Removing keys can invalidate a non-default
variant whose proof depended on them. Coordinated rollout, retention,
and cross-key ambiguity remain application responsibilities. Retaining
an origin key alone does not guarantee unchanged interpretation when
another key is tried first.

On compromise, set a fresh key as the default and remove the compromised
key from verification on every instance. Reissue affected identifiers
from trusted SKIDs, entity types, and epoch context as needed. Retaining
a compromised verification key retains the attacker's ability to forge
accepted identifiers.

## Serialization

Use signed 64-bit storage for SKIDs. For the 128-bit forms, use the
specified network-order bytes or UUID text with `8-4-4-4-12` hexadecimal
grouping. Runtime UUID APIs MUST preserve this byte order when computing
the MAC or encrypting. In particular, a mixed-endian UUID byte-array
representation is not the specified wire representation.

Strict UUIDv8 validators can reject Secure SKEIDs. Deployments MUST
check compatibility across the complete application path or transport
Secure SKEIDs as opaque strings or 128-bit values. They MUST NOT alter
ciphertext bits to force a UUID version or variant.

# Security Considerations

## Trust Boundaries

A valid identifier is not a bearer credential. Applications MUST
check record existence and authorize access independently. The MAC
authenticates fields within a shared-key trust domain. It does not
identify an individual producer, provide non-repudiation, or prevent
replay. Any holder of the MAC key can assert another generator's
topology fields.

Plain SKEIDs expose timestamps, topology, entity types, and sequences.
Secure SKEIDs conceal these fields under the AES pseudorandom-permutation
assumption, but repeated plaintexts encrypted under the same key produce
the same ciphertext. This permits correlation of repeated identifiers.
Secure SKEIDs do not preserve timestamp order or provide unlinkability.

## Forgery Bounds

The following estimates assume secret, cryptographically independent
MAC and AES keys. The tag-guessing model treats the 32-bit BLAKE3 keyed
output for a message without a known tag as uniformly distributed and
unpredictable. Confidentiality relies on treating AES-256 as a secure
pseudorandom permutation over 128-bit blocks. These assumptions do not
establish a security proof for the combined construction.

For plaintext SKEIDs, an attacker can choose the public fields and
guess the 32-bit tag. Under the idealized tag-guessing model, success
over q attempts is bounded by `min(1, q * 2^-32)`. Public marker and
identity fields do not extend that tag bound. Reusing a known valid
identifier is replay and is outside this guessing model.

For one key pair, a ciphertext sampled uniformly from all 128-bit values
decrypts to a uniformly distributed plaintext block. For each choice of
the non-MAC bytes, exactly one four-byte tag is valid. Fixing both marker
bytes to 0x8D therefore leaves 2^80 accepted plaintext blocks out of
2^128 before epoch or expected-identity restrictions. AES permutes these
blocks without changing their number.

On this default-variant path, a uniformly random ciphertext has an
acceptance probability of 2^-48. Additionally requiring one epoch gives
2^-56, then one entity type gives 2^-64, and then one app ID gives
2^-71. Each added restriction fixes separate bits in the plaintext.
AES confidentiality is not an additional independent probability factor.

Variants above 0x8D also require a complete backward proof. The figures
above count only the default-variant path. Accepting multiple epochs,
identities, or key pairs changes the accepted set. These random-input
calculations do not bound replay or arbitrary adaptive attacks against
the combined construction. The companion paper provides further
analysis and discusses its limitations [SKID-PAPER].

## Operational Controls

Deployments MUST protect and securely distribute key material and
rate-limit untrusted input.

For plaintext tag guessing, if a deployment allows 4,096 failed
verification attempts before retiring a MAC key, the idealized bound
is 2^-20, approximately one in a million. At 100 failed attempts per
second, that budget lasts about 41 seconds. These values illustrate
the risk under the model in [SKID-PAPER]. They do not prescribe a
budget or rate.

Implementations MUST bound fallback-key counts. Each key attempt has
fixed-size input, but fallback and backward verification increase work.
Client-facing failures SHOULD remain generic. Protected diagnostics
can record validation failures without exposing keys or plaintext
metadata to untrusted callers.

# IANA Considerations

This document has no IANA actions.

# Acknowledgments

AI tools assisted preparation of the reference implementation and
this document. The companion paper contains the author's disclosure
[SKID-PAPER].

--- back

# Test Vectors {#test-vectors}

This vector appears in the companion paper and the reference
implementation's `IetfDraftTestVectorTests`. Implementers SHOULD
reproduce it before using production keys. Hexadecimal key strings
below denote raw bytes, not UTF-8 text. These public test keys
MUST NOT be used in production.

## Test Key Material {#test-key-material}

The derived keys can be supplied directly. The master key is included
to identify the reference fixture. Its implementation-specific
derivation is not part of the wire format.

~~~text
Master key:
000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F
MAC key:
5E293E89136745A96B70EB8C8F81CDFCAED177BE5358BC83D3039FB6607FD8FE
AES key:
4988F97FF724CD086BDFEC83497C3527B3656F35F0911BEEAA6BCE4BB92D3BC7
~~~

## SKID Generation {#skid-generation-vector}

~~~text
Epoch start:     2025-01-01T00:00:00Z
Epoch index:     0
Elapsed ticks:   272000000
App ID:          5
App Instance ID: 3
Sequence:        42
Entity type:     1

SKID (hex):      0x881B3200050C002A
SKID (decimal):  -8639256484514103254
Embedded time:   2027-02-27T00:53:20Z
~~~

## SKEID Generation (Plain) {#plain-generation-vector}

~~~text
MAC input:       00081B3200058D018D0C002A00000000
MAC:             492C0E75
SKEID bytes:     00081B3200058D018D0C002A492C0E75
SKEID UUID:      00081b32-0005-8d01-8d0c-002a492c0e75
~~~

## Secure SKEID Generation {#secure-generation-vector}

~~~text
Secure bytes:    652068A43612CC4B8ABB83B853DC6786
Secure UUID:     652068a4-3612-cc4b-8abb-83b853dc6786
~~~

Ciphertext bytes 6 and 8 are 0xCC and 0x8A, so the collision guard
accepts the default candidate. This vector does not exercise variant
escalation.

## Round-Trip Verification

Parsing either representation recovers the same SKID, epoch, and entity
type. With the same keys, ToSecure and ToPlain reproduce the two UUIDs
above.

## Boundary and Rejection Cases {#boundary-and-rejection-cases}

The following SKID vectors use app ID 0, instance ID 0, and sequence 0.
Hexadecimal values represent the complete 64-bit pattern. Tick inputs
are after clock handling. Parsing MUST recover the listed elapsed ticks,
and signed order MUST increase across the four valid rows. Generation
MUST reject the out-of-range rows.

| Elapsed ticks | Expected SKID (hex) |
|---------------|---------------------|
| -1 | ERROR |
| 0 | 8000000000000000 |
| 2^32 - 1 | FFFFFFFF80000000 |
| 2^32 | 0000000000000000 |
| 2^33 - 1 | 7FFFFFFF80000000 |
| 2^33 | ERROR |

At tick 2^33 - 1, app ID 127, instance ID 63, and sequence 262,143
produce 7FFFFFFFFFFFFFFF. A generator that has issued all sequence
values in a tick MUST wait, then recompute the timestamp before issuing
another value. It MUST NOT wrap the sequence in the same tick.

For the parser cases below, use only the key pair in {{test-key-material}}
and accept only epoch 0. Start each modification from
{{plain-generation-vector}}. "Re-MAC" means recomputing bytes 12-15 with
ComputeMAC after the stated edit. "Encrypt" means encrypting that
complete block with the AES key in {{test-key-material}}. These are
fixture recipes and expected outcomes, not additional published
ciphertexts.

| Input or operation | Policy | Expected result |
|--------------------|--------|-----------------|
| {{plain-generation-vector}} unchanged | Plain | VALID, fields from {{skid-generation-vector}} |
| {{secure-generation-vector}} unchanged | Secure or Auto | VALID, fields from {{skid-generation-vector}} |
| {{secure-generation-vector}} unchanged | Plain | INVALID |
| A byte string of length 15 or 17 | Plain, Secure, or Auto | INVALID length |
| Set byte 12 to 0x48 without re-MAC | Plain | INVALID MAC |
| Set byte 0 to 0x01, then re-MAC | Plain | INVALID epoch |
| Set byte 0 to 0x01, re-MAC, then encrypt | Secure | INVALID epoch |
| Set byte 8 to 0x8C, then re-MAC | Plain | INVALID marker |
| Set byte 8 to 0x80 or 0x8C, re-MAC, then encrypt | Secure | INVALID authenticated variant |
| Set byte 8 to 0x8E, re-MAC, then encrypt | Secure | INVALID backward proof |
| Set byte 8 to 0xC0, re-MAC, then encrypt | Secure | INVALID structure |
| Validate the unchanged plaintext against entity type 2 or app ID 6 | Plain | Reject expected-identity mismatch |
| Validate the unchanged ciphertext against entity type 2 or app ID 6 | Secure or Auto | Reject expected-identity mismatch |

The ciphertext in {{secure-generation-vector}} begins with 0x65, an
unsupported plaintext epoch in this fixture. Auto MUST still decrypt
it. For the 0x8E case, that ciphertext is the predecessor. It does not
collide with plaintext, so the backward proof fails.

## Synthetic Collision and Fallback Cases {#synthetic-collision-and-fallback-cases}

These cases inject collision-predicate or per-key parser results in a
test harness. They test control flow, not AES or BLAKE3, and do not
represent discovered cryptographic collisions. Real cryptographic
generation and parsing MUST still pass the cases from
{{plain-generation-vector}} through {{boundary-and-rejection-cases}}.

For backward verification, assume the candidate has authenticated.
Predecessors are checked in descending order. For generation, inject
the stated HasPlainCollision results in ascending variant order.

| Operation | Injected condition | Expected result |
|-----------|--------------------|-----------------|
| Generate | 0x8D collides and 0x8E does not | Emit candidate 0x8E |
| Generate | All 51 candidates collide | ERROR, no identifier |
| Verify 0x8E | 0x8D collides | Accept proof after 1 check |
| Verify 0x8F | 0x8E collides and 0x8D does not | Reject proof after 2 checks |
| Verify 0x8F | Both predecessors collide | Accept proof after 2 checks |
| Verify 0xBF | All 50 predecessors collide | Accept proof after 50 checks |

For key-ring parsing, configure default key A and fallback key B.
Use these injected outcomes to verify the fallback state machine:

| A result | B result | Fallback | Expected result |
|----------|----------|----------|-----------------|
| Authentication failure | Valid | Disabled | INVALID. Do not try B |
| Authentication failure | Valid | Enabled | Return B's identity |
| Authenticated, invalid variant or proof | Valid | Enabled | INVALID. Do not try B |
| Valid | Valid with a different identity | Enabled | Return A's identity. B is not tried |

# Implementation Status

[DRN-PROJECT] provides a C#/.NET implementation with database integration.
It currently generates and accepts epoch 0 only. Key fallback is
disabled by default and enabled through `UseSourceKnownIdKeyFallback`.

The implementation uses explicit big-endian .NET `Guid` construction
and extraction. Its PostgreSQL and .NET compatibility tests accept
Secure SKEIDs, but this does not establish acceptance by every UUID
library or strict RFC 9562 validator.

Implementation tests, benchmarks, and evidence limits are described
in [SKID-PAPER]. The additional cases in {{boundary-and-rejection-cases}}
and {{synthetic-collision-and-fallback-cases}} specify conformance
expectations. Their inclusion is not a claim of executed tests or full
reference-implementation coverage. Independent implementations,
operational epoch transitions, and formal analysis of the combined
cryptographic construction remain future work.
