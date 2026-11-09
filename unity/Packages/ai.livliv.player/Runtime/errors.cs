// Port of src/errors.ts. Copyright 2026 LivLiv. Apache-2.0.
using System;
namespace LivLiv
{
    public sealed class LivLivError : Exception
    {
        public readonly string code;
        public readonly int? status;
        public LivLivError(string code, string message, int? status = null, Exception cause = null) : base(message, cause)
        { this.code = code; this.status = status; }
    }
}
