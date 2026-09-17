import argparse
import sys
from pathlib import Path

from lowpoly_giant_sword_gen import (
    DEFAULT_IM_EXE,
    OUT_DIR,
    export_lowpoly_files,
)

ROOT = Path(__file__).resolve().parents[1]


def parse_args():
    parser = argparse.ArgumentParser(description="Voxel blueprint -> block mesh -> Instant Meshes low-poly")
    parser.add_argument("--instant-meshes-exe", type=Path, default=DEFAULT_IM_EXE)
    parser.add_argument("--target-faces", type=int, default=500)
    parser.add_argument("--crease-angle", type=float, default=35.0)
    parser.add_argument("--pure-quad", action="store_true", help="Pure quad mesh (more faces, no -D)")
    parser.add_argument("--keep-greedy", action="store_true", help="Also export sparse-cubes greedy mesh")
    parser.add_argument("--out-dir", type=Path, default=OUT_DIR)
    return parser.parse_args()


def main():
    args = parse_args()
    if not args.instant_meshes_exe.exists():
        print(f"Instant Meshes not found: {args.instant_meshes_exe}", file=sys.stderr)
        sys.exit(1)
    stats = export_lowpoly_files(
        args.out_dir,
        target_faces=args.target_faces,
        crease_angle=args.crease_angle,
        pure_quad=args.pure_quad,
        im_exe=args.instant_meshes_exe,
        keep_greedy=args.keep_greedy,
    )
    print("block:", stats["block"])
    print("lowpoly:", stats["lowpoly"])
    if stats["greedy"]:
        print("greedy:", stats["greedy"])
    print("readme ->", args.out_dir / "SOURCE.md")


if __name__ == "__main__":
    main()
