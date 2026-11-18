using DRN.Framework.SharedKernel.Domain;

namespace Consumer.QA;

#if DRN_CONVENTION_EXCEPTION
[System.Diagnostics.CodeAnalysis.SuppressMessage("DRN.Conventions.Architecture", "DRNARCHITECTURE004",
    Justification = "This exact fixture type intentionally tests distribution and narrow suppression in a Hosted project.")]
#endif
public sealed class ConventionProbe : Dto;
