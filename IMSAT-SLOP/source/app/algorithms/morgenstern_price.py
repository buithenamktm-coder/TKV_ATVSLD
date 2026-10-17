from __future__ import annotations

from dataclasses import dataclass, field
import math
from typing import Iterable, Sequence

import numpy as np

from app.core.models import SliceData


@dataclass
class MPResult:
    fs: float
    lambda_value: float
    converged: bool
    iterations: int
    interslice_normal: list[float] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)


def normalize_base_angle(angle: float) -> float:
    """Normalize a geometric base angle to (-pi/2, pi/2]."""
    value = float(angle)
    while value <= -math.pi / 2:
        value += math.pi
    while value > math.pi / 2:
        value -= math.pi
    return value


def _half_sine(widths: np.ndarray, function: str) -> np.ndarray:
    x = np.r_[0.0, np.cumsum(widths)]
    if not len(x) or x[-1] <= 0:
        return np.ones(len(widths) + 1)
    if str(function).lower().replace("_", "-") in {"constant", "spencer", "1", "constant-1"}:
        return np.ones(len(widths) + 1)
    values = np.sin(np.pi * x / x[-1])
    values[0] = values[-1] = 0.0
    return values


def _solve_arrays(
    *,
    alpha: np.ndarray,
    phi: np.ndarray,
    cohesion: np.ndarray,
    weight: np.ndarray,
    pore_force: np.ndarray,
    widths: np.ndarray,
    base_lengths: np.ndarray,
    function: str = "Half-Sine",
    tolerance: float = 1e-8,
    max_iterations: int = 100,
    seismic_x: np.ndarray | None = None,
    external_vertical: np.ndarray | None = None,
    external_horizontal: np.ndarray | None = None,
    height: np.ndarray | None = None,
    initial_fs: float = 1.0,
    initial_lambda: float = 0.0,
) -> MPResult:
    """Zhu–Lee–Qian–Chen concise Morgenstern–Price iteration.

    ``alpha`` uses the equilibrium convention: positive in the anticipated
    sliding direction. ``pore_force`` is the resultant water force U on the
    slice base, not pressure intensity.
    """
    n = len(alpha)
    if n < 2:
        return MPResult(math.nan, math.nan, False, 0, warnings=["Cần ít nhất hai lát tính."])
    arrays = [alpha, phi, cohesion, weight, pore_force, widths, base_lengths]
    if any(len(x) != n for x in arrays):
        return MPResult(math.nan, math.nan, False, 0, warnings=["Dữ liệu lát không đồng nhất."])
    if np.any(widths <= 0) or np.any(base_lengths <= 0):
        return MPResult(math.nan, math.nan, False, 0, warnings=["Chiều rộng hoặc chiều dài đáy lát không hợp lệ."])

    f = _half_sine(widths, function)
    tan_phi = np.tan(phi)
    sx = np.zeros(n) if seismic_x is None else np.asarray(seismic_x, dtype=float)
    qv = np.zeros(n) if external_vertical is None else np.asarray(external_vertical, dtype=float)
    qh = np.zeros(n) if external_horizontal is None else np.asarray(external_horizontal, dtype=float)
    h = np.zeros(n) if height is None else np.asarray(height, dtype=float)

    # Resolve external forces into normal/driving directions. The signs are
    # compatible with the equilibrium convention used by alpha.
    normal_external = qv * np.cos(alpha) - qh * np.sin(alpha)
    driving_external = qv * np.sin(alpha) + qh * np.cos(alpha)
    R = ((weight * np.cos(alpha) - sx * np.sin(alpha) + normal_external - pore_force)
         * tan_phi + cohesion * base_lengths)
    T = weight * np.sin(alpha) + sx * np.cos(alpha) + driving_external

    def phi_values(fs: float, lam: float) -> np.ndarray:
        return ((np.sin(alpha) - lam * f[1:] * np.cos(alpha)) * tan_phi
                + (np.cos(alpha) + lam * f[1:] * np.sin(alpha)) * fs)

    def update_fs(fs: float, lam: float) -> float:
        Phi = phi_values(fs, lam)
        if np.any(~np.isfinite(Phi)) or np.any(np.abs(Phi) < 1e-13):
            return math.nan
        psi = np.empty(n - 1)
        for k in range(n - 1):
            numerator = ((math.sin(alpha[k + 1]) - lam * f[k + 1] * math.cos(alpha[k + 1]))
                         * tan_phi[k + 1]
                         + (math.cos(alpha[k + 1]) + lam * f[k + 1] * math.sin(alpha[k + 1])) * fs)
            psi[k] = numerator / Phi[k]
        suffix = np.ones(n - 1)
        product = 1.0
        for idx in range(n - 2, -1, -1):
            product *= psi[idx]
            suffix[idx] = product
        denominator = float(np.sum(T[:-1] * suffix) + T[-1])
        numerator = float(np.sum(R[:-1] * suffix) + R[-1])
        if abs(denominator) < 1e-14:
            return math.nan
        return numerator / denominator

    def interslice_forces(fs: float, lam: float) -> np.ndarray:
        E = np.zeros(n + 1)
        Phi = phi_values(fs, lam)
        if np.any(~np.isfinite(Phi)) or np.any(np.abs(Phi) < 1e-13):
            E[:] = np.nan
            return E
        for i in range(n):
            A = ((math.sin(alpha[i]) - lam * f[i] * math.cos(alpha[i])) * tan_phi[i]
                 + (math.cos(alpha[i]) + lam * f[i] * math.sin(alpha[i])) * fs)
            E[i + 1] = (A * E[i] + fs * T[i] - R[i]) / Phi[i]
        return E

    fs = float(initial_fs) if math.isfinite(initial_fs) and initial_fs > 0 else 1.0
    lam = float(initial_lambda) if math.isfinite(initial_lambda) else 0.0
    last_E = np.zeros(n + 1)
    warnings: list[str] = []
    for iteration in range(1, max_iterations + 1):
        # Zhu et al. recommend repeating the implicit Fs update once before
        # updating lambda; this materially improves convergence.
        fs_1 = update_fs(fs, lam)
        fs_2 = update_fs(fs_1, lam) if math.isfinite(fs_1) else math.nan
        if not math.isfinite(fs_2) or abs(fs_2) < 1e-12:
            return MPResult(math.nan, math.nan, False, iteration, warnings=["Không cập nhật được Fs."])
        last_E = interslice_forces(fs_2, lam)
        if np.any(~np.isfinite(last_E)):
            return MPResult(math.nan, math.nan, False, iteration, warnings=["Lực liên lát không hữu hạn."])
        denominator = float(np.sum(widths * (f[1:] * last_E[1:] + f[:-1] * last_E[:-1])))
        numerator = float(np.sum(widths * (last_E[1:] + last_E[:-1]) * np.tan(alpha)))
        # Additional moment contribution from horizontal seismic/external forces.
        if np.any(sx) or np.any(qh):
            numerator += float(np.sum(2.0 * widths * (sx + qh) * h))
        if abs(denominator) < 1e-14:
            return MPResult(math.nan, math.nan, False, iteration, warnings=["Mẫu số cập nhật λ bằng 0."])
        lam_2 = numerator / denominator
        if not (math.isfinite(lam_2) and math.isfinite(fs_2)):
            return MPResult(math.nan, math.nan, False, iteration, warnings=["Fs hoặc λ không hữu hạn."])
        if abs(fs_2 - fs) <= tolerance and abs(lam_2 - lam) <= tolerance:
            return MPResult(abs(float(fs_2)), float(lam_2), True, iteration,
                            interslice_normal=[float(v) for v in last_E], warnings=warnings)
        fs, lam = fs_2, lam_2
    warnings.append("Đạt số vòng lặp tối đa trước khi hội tụ.")
    return MPResult(abs(float(fs)), float(lam), False, max_iterations,
                    interslice_normal=[float(v) for v in last_E], warnings=warnings)


def solve_morgenstern_price(
    slices: Sequence[SliceData],
    *,
    function: str = "Half-Sine",
    tolerance: float = 1e-8,
    max_iterations: int = 100,
    initial_fs: float = 1.0,
    initial_lambda: float = 0.0,
) -> MPResult:
    """Solve IMSAT-generated or imported GeoStudio slice data."""
    if not slices:
        return MPResult(math.nan, math.nan, False, 0, warnings=["Không có dữ liệu lát."])
    widths = np.asarray([abs(s.x_right - s.x_left) for s in slices], dtype=float)
    lengths = np.asarray([s.base_length or math.hypot(s.x_right-s.x_left, s.y_bot_right-s.y_bot_left)
                          for s in slices], dtype=float)
    # GeoStudio stores some relative angles outside +/-90 degrees. Normalizing
    # first and reversing the sign converts them to the Zhu equilibrium convention.
    alpha = -np.asarray([normalize_base_angle(s.base_angle) for s in slices], dtype=float)
    phi = np.radians(np.asarray([s.phi for s in slices], dtype=float))
    cohesion = np.asarray([s.cohesion for s in slices], dtype=float)
    weight = np.asarray([s.weight for s in slices], dtype=float)
    pore = np.asarray([s.pore_pressure for s in slices], dtype=float)
    height = np.asarray([(s.y_top_left+s.y_top_right-s.y_bot_left-s.y_bot_right)/2.0 for s in slices], dtype=float)
    seismic = np.asarray([getattr(s, "seismic_horizontal", 0.0) for s in slices], dtype=float)
    ext_v = np.asarray([getattr(s, "surcharge_vertical", 0.0) for s in slices], dtype=float)
    ext_h = np.asarray([getattr(s, "surcharge_horizontal", 0.0) for s in slices], dtype=float)
    return _solve_arrays(alpha=alpha, phi=phi, cohesion=cohesion, weight=weight,
                         pore_force=pore, widths=widths, base_lengths=lengths,
                         function=function, tolerance=tolerance, max_iterations=max_iterations,
                         seismic_x=seismic, external_vertical=ext_v, external_horizontal=ext_h,
                         height=height, initial_fs=initial_fs, initial_lambda=initial_lambda)


def solve_geostudio_column_rows(rows: Iterable[dict[str, str]], *, function: str = "Half-Sine") -> MPResult:
    """Benchmark adapter for GeoStudio column_*.csv result tables."""
    data = list(rows)
    if not data:
        return MPResult(math.nan, math.nan, False, 0)
    raw_alpha = np.asarray([float(r.get("BotAlphaX", 0.0)) for r in data])
    alpha = -raw_alpha
    normal = np.asarray([float(r.get("NormalEffectiveStress", 0.0)) for r in data])
    friction = np.asarray([float(r.get("FrictionalStrength", 0.0)) for r in data])
    phi = np.arctan2(friction, normal)
    cohesion = np.asarray([float(r.get("CohesiveStrength", 0.0)) for r in data])
    weight = np.asarray([float(r.get("ColumnWeight", 0.0)) for r in data])
    pore = np.asarray([float(r.get("PoreWaterPressure", 0.0)) for r in data])
    widths = np.asarray([abs(float(r["XRight"])-float(r["XLeft"])) for r in data])
    lengths = np.hypot(widths, np.asarray([float(r["YBackBotRight"])-float(r["YBackBotLeft"]) for r in data]))
    height = np.asarray([(float(r["YBackTopLeft"])+float(r["YBackTopRight"])
                          -float(r["YBackBotLeft"])-float(r["YBackBotRight"]))/2 for r in data])
    return _solve_arrays(alpha=alpha, phi=phi, cohesion=cohesion, weight=weight,
                         pore_force=pore, widths=widths, base_lengths=lengths,
                         function=function, height=height)
