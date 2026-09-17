import io
import re
import subprocess
import tempfile
from dataclasses import dataclass
from pathlib import Path

import numpy as np
import sparsecubes as sc
import trimesh

ROOT = Path(__file__).resolve().parents[1]
SERIES = ROOT / "Assets/VoxelBlueprints/Weapons/OpenGameArtFantasySword01_VoxelV01"
DEFAULT_BLUEPRINT = SERIES / "PmWeaponVoxelStyle_OpenGameArtFantasySword01_VoxelV01.asset"
DEFAULT_SOURCE = SERIES / "SOURCE.md"
OUT_DIR = ROOT / "Assets/3DModel/Generated/GiantSword_LowPolyFromVoxel"
DEFAULT_IM_EXE = ROOT / "Tools/instant-meshes/Instant Meshes.exe"

PART_SPECS = [
    {
        "label": "\u5203",
        "asset": SERIES / "Part/PmWeaponVoxelPartStyle_OpenGameArtFantasySword01_VoxelV01_\u5203.asset",
        "assembly_offset_m": (0.0, 0.139896, 0.0),
    },
    {
        "label": "\u67c4",
        "asset": SERIES / "Part/PmWeaponVoxelPartStyle_OpenGameArtFantasySword01_VoxelV01_\u67c4.asset",
        "assembly_offset_m": (0.0, -0.361398, 0.0),
    },
]


@dataclass
class MeshStats:
    vertices: int
    faces: int
    bounds_m: list


@dataclass
class GenerateResult:
    glb_bytes: bytes
    block_stats: MeshStats
    lowpoly_stats: MeshStats
    target_faces: int
    crease_angle: float
    pure_quad: bool


class BlockMeshCache:
    def __init__(self):
        self._block_mesh = None
        self._block_obj_path = None
        self._pitch = None

    def clear(self):
        self._block_mesh = None
        self._block_obj_path = None
        self._pitch = None

    def get_block_mesh(self, out_dir: Path | None = None) -> tuple[trimesh.Trimesh, Path, float]:
        if self._block_mesh is not None and self._block_obj_path is not None:
            return self._block_mesh, self._block_obj_path, self._pitch

        pitch = read_pitch(DEFAULT_SOURCE)
        parts = []
        for spec in PART_SPECS:
            block, _, _, _, _ = build_part_mesh(spec, pitch, greedy=False)
            parts.append(block)
        block_mesh = trimesh.util.concatenate(parts)
        block_mesh.merge_vertices()
        block_mesh.remove_unreferenced_vertices()

        export_dir = out_dir or OUT_DIR
        export_dir.mkdir(parents=True, exist_ok=True)
        block_obj = export_dir / "GiantSword_FromVoxel_Block.obj"
        block_mesh.export(block_obj)

        self._block_mesh = block_mesh
        self._block_obj_path = block_obj
        self._pitch = pitch
        return block_mesh, block_obj, pitch


BLOCK_CACHE = BlockMeshCache()


def rgba_to_tuple(rgba: int):
    return (
        rgba & 0xFF,
        (rgba >> 8) & 0xFF,
        (rgba >> 16) & 0xFF,
        (rgba >> 24) & 0xFF,
    )


def read_pitch(source_md: Path, default: float = 0.023316) -> float:
    if not source_md.exists():
        return default
    for line in source_md.read_text(encoding="utf-8").splitlines():
        if "**Pitch:**" not in line and "**Cell world size:**" not in line:
            continue
        m = re.search(r"`([0-9.]+)`", line)
        if m:
            return float(m.group(1))
    return default


def load_blueprint_cells(asset_path: Path):
    text = asset_path.read_text(encoding="utf-8")
    sx = int(re.search(r"sizeX: (\d+)", text).group(1))
    sy = int(re.search(r"sizeY: (\d+)", text).group(1))
    sz = int(re.search(r"sizeZ: (\d+)", text).group(1))
    enabled = [m == "1" for m in re.findall(r"enabled: (\d)", text)]
    rgba_vals = [int(m.group(1)) for m in re.finditer(r"rgba: (\d+)", text)]
    cells = {}
    colors = {}
    for i, on in enumerate(enabled):
        if not on:
            continue
        z = i // (sx * sy)
        rem = i % (sx * sy)
        y = rem // sx
        x = rem % sx
        cells[(x, y, z)] = True
        colors[(x, y, z)] = rgba_to_tuple(rgba_vals[i])
    return sx, sy, sz, cells, colors


def cells_to_voxel_xyz(cells):
    if not cells:
        return np.zeros((0, 3), dtype=np.uint32)
    return np.array(list(cells.keys()), dtype=np.uint32)


def mesh_from_voxel_cells(cells, sx, sy, sz, pitch, greedy: bool):
    voxel_xyz = cells_to_voxel_xyz(cells)
    mesh = sc.mesh(voxel_xyz, smooth=False, simplify=greedy)
    mesh.apply_scale(pitch)
    offset = -np.array([sx, sy, sz], dtype=np.float64) * pitch * 0.5
    mesh.apply_translation(offset)
    mesh.merge_vertices()
    mesh.remove_unreferenced_vertices()
    return mesh


def colorize_from_cells(mesh, cells, colors, sx, sy, sz, pitch):
    grid_center = np.array([sx, sy, sz], dtype=np.float64) * 0.5
    vc = np.zeros((len(mesh.vertices), 4), dtype=np.uint8)
    for i, v in enumerate(mesh.vertices):
        g = v / pitch + grid_center
        x = int(np.clip(np.floor(g[0]), 0, sx - 1))
        y = int(np.clip(np.floor(g[1]), 0, sy - 1))
        z = int(np.clip(np.floor(g[2]), 0, sz - 1))
        key = (x, y, z)
        if key not in colors:
            found = None
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    for dz in (-1, 0, 1):
                        k2 = (x + dx, y + dy, z + dz)
                        if k2 in colors:
                            found = k2
                            break
                    if found:
                        break
                if found:
                    break
            key = found if found else key
        c = colors.get(key, (180, 180, 180, 255))
        vc[i] = [c[0], c[1], c[2], 255]
    mesh.visual.vertex_colors = vc
    return mesh


def build_part_mesh(spec, pitch, greedy: bool):
    sx, sy, sz, cells, colors = load_blueprint_cells(spec["asset"])
    mesh = mesh_from_voxel_cells(cells, sx, sy, sz, pitch, greedy=greedy)
    mesh = colorize_from_cells(mesh, cells, colors, sx, sy, sz, pitch)
    ox, oy, oz = spec["assembly_offset_m"]
    mesh.apply_translation([ox, oy, oz])
    return mesh, len(cells), sx, sy, sz


def mesh_stats(mesh: trimesh.Trimesh) -> MeshStats:
    return MeshStats(
        vertices=len(mesh.vertices),
        faces=len(mesh.faces),
        bounds_m=mesh.bounds.tolist(),
    )


def transfer_vertex_colors_nearest(source_mesh: trimesh.Trimesh, target_mesh: trimesh.Trimesh):
    src_colors = getattr(source_mesh.visual, "vertex_colors", None)
    if src_colors is None or len(src_colors) == 0:
        return target_mesh
    src_v = source_mesh.vertices
    tgt_v = target_mesh.vertices
    diff = tgt_v[:, None, :] - src_v[None, :, :]
    idx = np.argmin(np.sum(diff * diff, axis=2), axis=1)
    target_mesh.visual.vertex_colors = src_colors[idx]
    return target_mesh


def run_instant_meshes(
    exe: Path,
    input_obj: Path,
    output_obj: Path,
    target_faces: int,
    crease_angle: float,
    align_boundaries: bool,
    dominant: bool,
    smooth_iterations: int = 5,
    quiet: bool = False,
):
    if not exe.exists():
        raise FileNotFoundError(f"Instant Meshes not found: {exe}")
    cmd = [
        str(exe),
        "-o",
        str(output_obj),
        "-f",
        str(target_faces),
        "-c",
        str(crease_angle),
        "-S",
        str(max(0, int(smooth_iterations))),
    ]
    if align_boundaries:
        cmd.append("-b")
    if dominant:
        cmd.append("-D")
    cmd.append(str(input_obj))
    proc = subprocess.run(cmd, capture_output=True, text=True)
    if not quiet and proc.stdout:
        print(proc.stdout.rstrip())
    if proc.returncode != 0:
        detail = proc.stderr.strip() or proc.stdout.strip() or f"exit {proc.returncode}"
        raise RuntimeError(f"Instant Meshes failed: {detail}")
    if not output_obj.exists():
        raise RuntimeError(f"Instant Meshes did not write {output_obj}")


def mesh_to_glb_bytes(mesh: trimesh.Trimesh) -> bytes:
    buf = io.BytesIO()
    mesh.export(buf, file_type="glb")
    return buf.getvalue()


def smooth_block_mesh_for_im(mesh, iterations: int, tip_bias: float = 1.8):
    if iterations <= 0:
        return mesh
    m = mesh.copy()
    verts = np.array(m.vertices, dtype=np.float64)
    faces = m.faces
    adj = [set() for _ in range(len(verts))]
    for tri in faces:
        a, b, c = int(tri[0]), int(tri[1]), int(tri[2])
        adj[a].update((b, c))
        adj[b].update((a, c))
        adj[c].update((a, b))
    min_y = float(verts[:, 1].min())
    max_y = float(verts[:, 1].max())
    span = max(1e-5, max_y - min_y)
    lam, mu = 0.5, -0.53
    for _ in range(iterations):
        for factor in (lam, mu):
            nxt = verts.copy()
            for i in range(len(verts)):
                nb = list(adj[i])
                if not nb:
                    continue
                avg = verts[nb].mean(axis=0)
                y01 = (verts[i, 1] - min_y) / span
                weight = 1.0 + (tip_bias - 1.0) * (y01 * y01)
                nxt[i] = verts[i] + (avg - verts[i]) * factor * weight
            verts = nxt
    m.vertices = verts
    return m


def generate_lowpoly_glb(
    target_faces: int = 500,
    crease_angle: float = 35.0,
    pure_quad: bool = False,
    input_smooth_iterations: int = 1,
    im_smooth_iterations: int = 3,
    im_exe: Path = DEFAULT_IM_EXE,
    cache: BlockMeshCache | None = None,
    quiet: bool = True,
) -> GenerateResult:
    target_faces = int(max(50, min(5000, target_faces)))
    crease_angle = float(max(5.0, min(89.0, crease_angle)))
    mesh_cache = cache or BLOCK_CACHE
    block_mesh, block_obj, _pitch = mesh_cache.get_block_mesh()
    block_stats = mesh_stats(block_mesh)

    with tempfile.TemporaryDirectory(prefix="tuzhi_im_") as tmp:
        im_obj = Path(tmp) / "lowpoly_im.obj"
        block_smooth_obj = Path(tmp) / "block_smooth.obj"
        im_input = smooth_block_mesh_for_im(block_mesh, input_smooth_iterations)
        im_input.export(block_smooth_obj)
        run_instant_meshes(
            im_exe,
            block_smooth_obj,
            im_obj,
            target_faces=target_faces,
            crease_angle=crease_angle,
            align_boundaries=True,
            dominant=not pure_quad,
            smooth_iterations=im_smooth_iterations,
            quiet=quiet,
        )
        im_mesh = trimesh.load(im_obj, force="mesh", process=False)
        im_mesh = transfer_vertex_colors_nearest(block_mesh, im_mesh)
        lowpoly_stats = mesh_stats(im_mesh)
        glb_bytes = mesh_to_glb_bytes(im_mesh)

    return GenerateResult(
        glb_bytes=glb_bytes,
        block_stats=block_stats,
        lowpoly_stats=lowpoly_stats,
        target_faces=target_faces,
        crease_angle=crease_angle,
        pure_quad=pure_quad,
    )


def export_lowpoly_files(
    out_dir: Path,
    target_faces: int = 500,
    crease_angle: float = 35.0,
    pure_quad: bool = False,
    im_exe: Path = DEFAULT_IM_EXE,
    keep_greedy: bool = False,
) -> dict:
    out_dir.mkdir(parents=True, exist_ok=True)
    BLOCK_CACHE.clear()
    block_mesh, block_obj, pitch = BLOCK_CACHE.get_block_mesh(out_dir)
    block_mesh.export(out_dir / "GiantSword_FromVoxel_Block.glb")
    block_stats = mesh_stats(block_mesh)

    greedy_stats = None
    if keep_greedy:
        greedy_parts = []
        for spec in PART_SPECS:
            greedy, _, _, _, _ = build_part_mesh(spec, pitch, greedy=True)
            greedy_parts.append(greedy)
        greedy_mesh = trimesh.util.concatenate(greedy_parts)
        greedy_mesh.export(out_dir / "GiantSword_FromVoxel_LowPoly_Greedy.glb")
        greedy_stats = mesh_stats(greedy_mesh)

    result = generate_lowpoly_glb(
        target_faces=target_faces,
        crease_angle=crease_angle,
        pure_quad=pure_quad,
        im_exe=im_exe,
        cache=BLOCK_CACHE,
        quiet=False,
    )
    low_glb = out_dir / "GiantSword_FromVoxel_LowPoly.glb"
    low_glb.write_bytes(result.glb_bytes)
    im_mesh = trimesh.load(io.BytesIO(result.glb_bytes), file_type="glb", force="mesh")
    im_mesh.export(out_dir / "GiantSword_FromVoxel_LowPoly_IM.obj")

    write_source_md(
        out_dir / "SOURCE.md",
        pitch,
        block_stats,
        greedy_stats,
        result.lowpoly_stats,
        target_faces,
    )
    return {
        "block": block_stats,
        "lowpoly": result.lowpoly_stats,
        "greedy": greedy_stats,
    }


def write_source_md(
    readme: Path,
    pitch: float,
    block_stats: MeshStats,
    greedy_stats: MeshStats | None,
    im_stats: MeshStats,
    im_target_faces: int,
):
    lines = [
        "# Giant sword low-poly from voxel blueprint",
        "",
        f"- **Full blueprint (reference):** `{DEFAULT_BLUEPRINT.relative_to(ROOT).as_posix()}`",
        "- **Mesh source:** part blueprints `刃` + `柄` (handle edits live in the `柄` part asset).",
        f"- **Pitch:** `{pitch}` m",
        "- **Assembly:** each part mesh translated by `assemblyLocalOffsetMeters` from Part/SOURCE.md.",
        "- **Block mesh:** sparse-cubes culled voxel faces (`smooth=False`, `simplify=False`).",
        f"- **Block:** {block_stats.faces} tris",
        "- **Low-poly mesh (main):** [Instant Meshes](https://github.com/wjakob/instant-meshes) field-aligned retopology on the block OBJ.",
        f"  - Target faces: ~{im_target_faces} (approximate; IM subdivides coarse voxel input first).",
        "  - Batch flags: `-b` align boundaries, `-c` crease angle, quad-dominant output.",
        "  - Vertex colors: nearest-neighbor transfer from block mesh.",
        f"- **Low-poly (Instant Meshes):** {im_stats.faces} tris",
    ]
    if greedy_stats:
        lines.append(
            f"- **Low-poly (greedy fallback):** {greedy_stats.faces} tris — `GiantSword_FromVoxel_LowPoly_Greedy.*`"
        )
    lines.extend(
        [
            "- **Generator:** `Tools/voxel_blueprint_to_lowpoly_mesh.py`",
            "- **Preview UI:** `Tools/lowpoly_preview_server.py`",
            "- **Instant Meshes binary:** `Tools/instant-meshes/Instant Meshes.exe`",
            "- **Not hooked to VR3 prefab.**",
            "",
        ]
    )
    readme.write_text("\n".join(lines), encoding="utf-8")
