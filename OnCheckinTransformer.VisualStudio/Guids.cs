// Guids.cs
// MUST match guids.h
using System;

namespace OnCheckinTransformer.VisualStudio
{
    static class GuidList
    {
        public const string guidOnCheckinTransforms_VisualStudioPkgString = "6d64f304-7594-4d34-9caf-20fd8ef2a627";
        public const string guidOnCheckinTransforms_VisualStudioCmdSetString = "e57aeef3-a2d0-41e7-af9e-97dccbb756d3";

        public static readonly Guid guidOnCheckinTransforms_VisualStudioCmdSet = new Guid(guidOnCheckinTransforms_VisualStudioCmdSetString);
    };
}