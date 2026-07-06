// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Matrix4x4Extensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Numerics;
using System.Runtime.CompilerServices;

namespace ILGPU.Numerics;

/// <summary>
/// Public extension methods on <see cref="Matrix4x4"/> that complement
/// the BCL surface with GPU-friendly numerical helpers.
/// </summary>
public static class Matrix4x4Extensions
{
    /// <summary>
    /// Iteratively decomposes an affine <see cref="Matrix4x4"/> into
    /// translation, rotation, and scale components using Higham's polar
    /// iteration on the upper-left 3×3.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="Matrix4x4.Decompose"/>, this overload tolerates
    /// inputs whose upper-left 3×3 rows are not exactly orthogonal — the
    /// result is the *closest* TRS factorization (in the Frobenius-norm
    /// sense) to <paramref name="matrix"/>. Use this when decomposing
    /// matrices accumulated from many transforms (FP-drifted), or matrices
    /// containing small amounts of shear that the closed-form algorithm
    /// would silently corrupt.
    /// </para>
    /// <para>
    /// Convergence is quadratic: typical inputs reach <paramref name="tolerance"/>
    /// in 3–8 iterations. The default <paramref name="maxIterations"/> of
    /// 16 is comfortable for any reasonably-conditioned input. Returning
    /// <c>true</c> at the budget limit means the result is the best
    /// approximation found so far — re-run with a larger budget to verify
    /// convergence on borderline-conditioned input.
    /// </para>
    /// <para>
    /// GPU-safe: scalar arithmetic only, bounded loop, no allocations,
    /// no calls into <c>System.Runtime.Intrinsics</c>.
    /// </para>
    /// </remarks>
    /// <param name="matrix">The affine matrix to decompose.</param>
    /// <param name="scale">
    /// Per-axis scale magnitudes. Sign-disambiguated so the resulting
    /// rotation has a positive determinant (X is folded with the sign of
    /// <c>det(matrix.upper3x3)</c>).
    /// </param>
    /// <param name="rotation">
    /// The rotation quaternion (proper rotation, det &gt; 0).
    /// </param>
    /// <param name="translation">The translation vector (matrix.M41..M43).</param>
    /// <param name="tolerance">
    /// Convergence threshold on max-elementwise change between iterations.
    /// Defaults to <c>1e-6f</c>.
    /// </param>
    /// <param name="maxIterations">
    /// Hard upper bound on iteration count. Defaults to <c>16</c>.
    /// </param>
    /// <returns>
    /// <c>false</c> when the upper-left 3×3 is degenerate (zero determinant
    /// at any iteration step); otherwise <c>true</c>.
    /// </returns>
    public static bool TryDecomposeIterative(
        this Matrix4x4 matrix,
        out Vector3 scale,
        out Quaternion rotation,
        out Vector3 translation,
        float tolerance = 1e-6f,
        int maxIterations = 16)
    {
        scale = default;
        rotation = default;
        translation = default;

        translation.X = matrix.M41;
        translation.Y = matrix.M42;
        translation.Z = matrix.M43;

        // We carry the upper-left 3×3 as nine scalar locals across the
        // iteration rather than as a `Matrix4x4 a` field-store struct. The
        // CPU vectorized backend treats a struct alloca that's loop-carried
        // through a phi as a single scalar slot and drops the per-field
        // initialization stores during emission (the per-lane buffer never
        // receives the input matrix's elements). Scalar locals lower to
        // ordinary loop-header phis, which the backend handles correctly.
        float m11 = matrix.M11, m12 = matrix.M12, m13 = matrix.M13;
        float m21 = matrix.M21, m22 = matrix.M22, m23 = matrix.M23;
        float m31 = matrix.M31, m32 = matrix.M32, m33 = matrix.M33;

        // Higham polar iteration: A_{n+1} = 0.5 * (A_n + (A_n^T)^{-1}).
        // For the row-vector convention used by System.Numerics, A_n
        // converges to the orthogonal factor R such that A_0 = D * R
        // (D = diagonal scale; left-polar form). We exploit
        // (A^T)^{-1} = (A^{-1})^T = cofactor(A) / det(A) — i.e., the
        // cofactor matrix of A divided by det(A) is exactly what we need,
        // no explicit transpose required.
        //
        // The polar iteration is wrapped in a single-exit loop. Every
        // intermediate state — `ok` (success accumulator), `converged`
        // (convergence flag), and the loop-carried matrix elements — is
        // updated as a value-level select rather than via mid-loop
        // branches, so the IR has exactly one back edge and one exit
        // from the loop header. Multi-exit loops force the GPU
        // control-flow structurer (ControlFlowReconstructor) to pick a
        // single `loop.Exits[0]` and emit any other exit's continuation
        // *inside* the loop body, where it terminates with `break`. The
        // post-loop Shepperd block lives at the converged-exit edge, so
        // a multi-exit shape would put Shepperd inside the loop body
        // and break out before the output is written. Bitwise `&` (not
        // short-circuit `&&`) and the trailing `safeInv = ok ? … : 0f`
        // ternary keep the determinant guard purely value-level.
        bool ok = true;
        bool converged = false;
        for (int iter = 0; iter < maxIterations; iter++)
        {
            float c11 =  (m22 * m33 - m23 * m32);
            float c12 = -(m21 * m33 - m23 * m31);
            float c13 =  (m21 * m32 - m22 * m31);
            float c21 = -(m12 * m33 - m13 * m32);
            float c22 =  (m11 * m33 - m13 * m31);
            float c23 = -(m11 * m32 - m12 * m31);
            float c31 =  (m12 * m23 - m13 * m22);
            float c32 = -(m11 * m23 - m13 * m21);
            float c33 =  (m11 * m22 - m12 * m21);

            float det = m11 * c11 + m12 * c12 + m13 * c13;
            bool detOk = XMath.Abs(det) >= 1e-30f;
            // Treat tiny |det| as failure for this iteration. Use
            // bitwise `&` so the AND is a value, not control flow.
            ok = ok & detOk;
            // `step` is 0 once iteration should freeze (failed or
            // already converged). Computing safeInv on a frozen lane
            // produces garbage but never propagates because step=0
            // selects the old value back into m11..m33.
            float step = (detOk & !converged) ? 1f : 0f;
            float safeInv = detOk ? 1f / det : 0f;

            float n11 = 0.5f * (m11 + c11 * safeInv);
            float n12 = 0.5f * (m12 + c12 * safeInv);
            float n13 = 0.5f * (m13 + c13 * safeInv);
            float n21 = 0.5f * (m21 + c21 * safeInv);
            float n22 = 0.5f * (m22 + c22 * safeInv);
            float n23 = 0.5f * (m23 + c23 * safeInv);
            float n31 = 0.5f * (m31 + c31 * safeInv);
            float n32 = 0.5f * (m32 + c32 * safeInv);
            float n33 = 0.5f * (m33 + c33 * safeInv);

            float maxDiff = XMath.Abs(n11 - m11);
            float d;
            d = XMath.Abs(n12 - m12); maxDiff = d > maxDiff ? d : maxDiff;
            d = XMath.Abs(n13 - m13); maxDiff = d > maxDiff ? d : maxDiff;
            d = XMath.Abs(n21 - m21); maxDiff = d > maxDiff ? d : maxDiff;
            d = XMath.Abs(n22 - m22); maxDiff = d > maxDiff ? d : maxDiff;
            d = XMath.Abs(n23 - m23); maxDiff = d > maxDiff ? d : maxDiff;
            d = XMath.Abs(n31 - m31); maxDiff = d > maxDiff ? d : maxDiff;
            d = XMath.Abs(n32 - m32); maxDiff = d > maxDiff ? d : maxDiff;
            d = XMath.Abs(n33 - m33); maxDiff = d > maxDiff ? d : maxDiff;

            // Lerp old → new by `step` so a frozen lane keeps the prior
            // matrix and a live lane advances. Equivalent to
            // `m? = step != 0 ? n? : m?` but expressed as arithmetic
            // so the IR has no per-element conditional branch.
            m11 += (n11 - m11) * step;
            m12 += (n12 - m12) * step;
            m13 += (n13 - m13) * step;
            m21 += (n21 - m21) * step;
            m22 += (n22 - m22) * step;
            m23 += (n23 - m23) * step;
            m31 += (n31 - m31) * step;
            m32 += (n32 - m32) * step;
            m33 += (n33 - m33) * step;

            converged = converged | (maxDiff < tolerance);
        }

        // Recover scale: D = M * R^T  =>  D_ii = M.row_i · R.row_i.
        // For aligned TRS this collapses to ||M.row_i||; for non-aligned
        // input it gives the projection of M's row onto R's row, which is
        // the *signed* scale magnitude in the closest-rotation sense.
        float sX = matrix.M11 * m11 + matrix.M12 * m12 + matrix.M13 * m13;
        float sY = matrix.M21 * m21 + matrix.M22 * m22 + matrix.M23 * m23;
        float sZ = matrix.M31 * m31 + matrix.M32 * m32 + matrix.M33 * m33;

        // Sign-disambiguate via det(R): if R encodes a reflection rather
        // than a proper rotation, fold one axis flip into scale.X (matching
        // the BCL Decompose convention) and flip row 1 of R. Row-flip (not
        // column-flip) preserves the row-vector A = D * R relationship.
        float detR =
            m11 * (m22 * m33 - m23 * m32) -
            m12 * (m21 * m33 - m23 * m31) +
            m13 * (m21 * m32 - m22 * m31);
        float flip = detR < 0f ? -1f : 1f;
        sX *= flip;
        m11 *= flip;
        m12 *= flip;
        m13 *= flip;

        scale.X = sX;
        scale.Y = sY;
        scale.Z = sZ;

        // Reconstruct the orthogonal matrix into a Matrix4x4 for the
        // shared Shepperd helper. This struct is constructed once at
        // the end of the success path, outside any loop, so its
        // SSA / alloca shape doesn't trip the loop-carry codegen issue.
        Matrix4x4 r = default;
        r.M11 = m11; r.M12 = m12; r.M13 = m13;
        r.M21 = m21; r.M22 = m22; r.M23 = m23;
        r.M31 = m31; r.M32 = m32; r.M33 = m33;
        r.M44 = 1f;

        rotation = XQuaternion.CreateFromRotationMatrix(r);

        // Per the API contract, on `false` the out-parameters are
        // undefined — we still write computed values into them rather
        // than gating with an `if (!ok) return false` post-loop, since
        // that would introduce a second exit edge from the function
        // and re-create the multi-exit shape we're avoiding here.
        return ok;
    }
}
