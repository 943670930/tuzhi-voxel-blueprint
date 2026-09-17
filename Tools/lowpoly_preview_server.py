import argparse
import json
import mimetypes
import threading
import time
import webbrowser
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlparse

from lowpoly_giant_sword_gen import BLOCK_CACHE, generate_lowpoly_glb

ROOT = Path(__file__).resolve().parents[1]
PREVIEW_DIR = ROOT / "Tools/lowpoly_preview"

GEN_LOCK = threading.Lock()
PREVIEW_STATE = {
    "glb_bytes": b"",
    "revision": 0,
    "last_ms": 0,
    "last_error": "",
    "last_stats": {},
}


def generate_preview(target_faces: int, crease_angle: float, pure_quad: bool) -> dict:
    started = time.perf_counter()
    with GEN_LOCK:
        BLOCK_CACHE.clear()
        result = generate_lowpoly_glb(
            target_faces=target_faces,
            crease_angle=crease_angle,
            pure_quad=pure_quad,
            quiet=True,
        )
        PREVIEW_STATE["glb_bytes"] = result.glb_bytes
        PREVIEW_STATE["revision"] += 1
        PREVIEW_STATE["last_ms"] = int((time.perf_counter() - started) * 1000)
        PREVIEW_STATE["last_error"] = ""
        PREVIEW_STATE["last_stats"] = {
            "revision": PREVIEW_STATE["revision"],
            "target_faces": result.target_faces,
            "crease_angle": result.crease_angle,
            "pure_quad": result.pure_quad,
            "block_faces": result.block_stats.faces,
            "vertices": result.lowpoly_stats.vertices,
            "faces": result.lowpoly_stats.faces,
            "bounds_m": result.lowpoly_stats.bounds_m,
            "ms": PREVIEW_STATE["last_ms"],
        }
        return PREVIEW_STATE["last_stats"]


class PreviewHandler(BaseHTTPRequestHandler):
    server_version = "TuZhiLowPolyPreview/1.0"

    def log_message(self, format, *args):
        return

    def _send_json(self, code: int, payload: dict):
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()
        self.wfile.write(body)

    def _send_bytes(self, code: int, data: bytes, content_type: str):
        self.send_response(code)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()
        self.wfile.write(data)

    def _read_json_body(self) -> dict:
        length = int(self.headers.get("Content-Length", "0"))
        if length <= 0:
            return {}
        raw = self.rfile.read(length)
        return json.loads(raw.decode("utf-8"))

    def do_OPTIONS(self):
        self.send_response(204)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.end_headers()

    def do_GET(self):
        path = urlparse(self.path).path
        if path in ("/", "/index.html"):
            page = PREVIEW_DIR / "index.html"
            self._send_bytes(200, page.read_bytes(), "text/html; charset=utf-8")
            return
        if path.startswith("/static/"):
            rel = path[len("/static/") :]
            file_path = (PREVIEW_DIR / rel).resolve()
            if not str(file_path).startswith(str(PREVIEW_DIR.resolve())) or not file_path.exists():
                self.send_error(404)
                return
            ctype = mimetypes.guess_type(str(file_path))[0] or "application/octet-stream"
            self._send_bytes(200, file_path.read_bytes(), ctype)
            return
        if path == "/api/preview.glb":
            query = parse_qs(urlparse(self.path).query)
            rev = query.get("rev", [""])[0]
            if not PREVIEW_STATE["glb_bytes"]:
                self.send_error(404, "No preview mesh yet")
                return
            if rev and rev != str(PREVIEW_STATE["revision"]):
                self.send_error(409, "Stale revision")
                return
            self._send_bytes(200, PREVIEW_STATE["glb_bytes"], "model/gltf-binary")
            return
        if path == "/api/status":
            self._send_json(
                200,
                {
                    "revision": PREVIEW_STATE["revision"],
                    "has_mesh": bool(PREVIEW_STATE["glb_bytes"]),
                    "last_ms": PREVIEW_STATE["last_ms"],
                    "last_error": PREVIEW_STATE["last_error"],
                    "stats": PREVIEW_STATE["last_stats"],
                },
            )
            return
        self.send_error(404)

    def do_POST(self):
        path = urlparse(self.path).path
        if path != "/api/generate":
            self.send_error(404)
            return
        try:
            body = self._read_json_body()
            target_faces = int(body.get("target_faces", 500))
            crease_angle = float(body.get("crease_angle", 35))
            pure_quad = bool(body.get("pure_quad", False))
            stats = generate_preview(target_faces, crease_angle, pure_quad)
            self._send_json(
                200,
                {
                    "ok": True,
                    "mesh_url": f"/api/preview.glb?rev={stats['revision']}",
                    "stats": stats,
                },
            )
        except Exception as exc:
            PREVIEW_STATE["last_error"] = str(exc)
            self._send_json(500, {"ok": False, "error": str(exc)})


def main():
    parser = argparse.ArgumentParser(description="Giant sword low-poly interactive preview server")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--no-browser", action="store_true")
    args = parser.parse_args()

    print("Generating initial preview ...")
    generate_preview(500, 35.0, False)
    print(
        f"ready: {PREVIEW_STATE['last_stats'].get('faces', '?')} tris, "
        f"{PREVIEW_STATE['last_ms']} ms"
    )

    url = f"http://{args.host}:{args.port}/"
    httpd = ThreadingHTTPServer((args.host, args.port), PreviewHandler)
    print(f"preview: {url}")
    print("Ctrl+C to stop")
    if not args.no_browser:
        webbrowser.open(url)
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\nstopped")


if __name__ == "__main__":
    main()
