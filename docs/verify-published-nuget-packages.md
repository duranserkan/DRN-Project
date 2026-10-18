# Verify Published NuGet Packages

Published packages are available from two locations:

- For normal consumption, open the [NuGet.org profile](https://www.nuget.org/profiles/duranserkan), select the package and version, then use **Download package**.
- For the exact pre-publish files, open the [stable release](https://github.com/duranserkan/DRN-Project/actions/workflows/release.yml) or [preview release](https://github.com/duranserkan/DRN-Project/actions/workflows/release-preview.yml) workflow, select the matching release run, then download the `packages` entry under **Artifacts**. It contains the original `.nupkg` and `.snupkg` files plus the generated SBOM for the repository's configured artifact-retention period.

Replace `RELEASE_VERSION` in the example path, then verify an original workflow artifact's provenance and SPDX 2.2 SBOM attestation directly (for preview packages, replace `release.yml` with `release-preview.yml`):

```bash
gh attestation verify \
  artifacts/packages/DRN.Framework.Hosting.RELEASE_VERSION.nupkg \
  -R duranserkan/DRN-Project \
  --signer-workflow duranserkan/DRN-Project/.github/workflows/release.yml

gh attestation verify \
  artifacts/packages/DRN.Framework.Hosting.RELEASE_VERSION.nupkg \
  -R duranserkan/DRN-Project \
  --signer-workflow duranserkan/DRN-Project/.github/workflows/release.yml \
  --predicate-type https://spdx.dev/Document/v2.2
```

NuGet.org adds `.signature.p7s`, changing the package digest. [Verify the repository signature](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-verify) first, then remove it from a copy before verifying GitHub provenance and SBOM attestations:

```bash
dotnet nuget verify DRN.Framework.Hosting.RELEASE_VERSION.nupkg --all

mkdir -p attestation
cp DRN.Framework.Hosting.RELEASE_VERSION.nupkg attestation/

zip -d attestation/DRN.Framework.Hosting.RELEASE_VERSION.nupkg .signature.p7s

gh attestation verify \
  attestation/DRN.Framework.Hosting.RELEASE_VERSION.nupkg \
  -R duranserkan/DRN-Project \
  --signer-workflow duranserkan/DRN-Project/.github/workflows/release.yml

gh attestation verify \
  attestation/DRN.Framework.Hosting.RELEASE_VERSION.nupkg \
  -R duranserkan/DRN-Project \
  --signer-workflow duranserkan/DRN-Project/.github/workflows/release.yml \
  --predicate-type https://spdx.dev/Document/v2.2
```

Repeat for each package. If verification fails, use the exact package from the workflow's `packages` artifact.
