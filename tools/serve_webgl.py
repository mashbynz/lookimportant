"""Serve the WebGL build with the headers Unity's gzip output wants.

Run through the tailnet proxy so the preview is reachable from other devices:
    tportless lookimportant uv run tools/serve_webgl.py
Locally: PORT=8000 uv run tools/serve_webgl.py
"""
import functools
import http.server
import os
import pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent / "Builds" / "WebGL"
TYPES = {".js": "application/javascript", ".wasm": "application/wasm", ".data": "application/octet-stream"}


class Handler(http.server.SimpleHTTPRequestHandler):
    def guess_type(self, path):
        p = str(path)
        if p.endswith(".gz"):
            return TYPES.get(pathlib.Path(p[:-3]).suffix, "application/octet-stream")
        return super().guess_type(path)

    def end_headers(self):
        if self.path.split("?")[0].endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        self.send_header("Cache-Control", "no-cache")
        super().end_headers()


def main():
    port = int(os.environ.get("PORT", "8000"))
    if not (ROOT / "index.html").exists():
        raise SystemExit(f"No WebGL build at {ROOT} - run tools/build.sh webgl first")
    handler = functools.partial(Handler, directory=str(ROOT))
    print(f"Serving {ROOT} on http://127.0.0.1:{port}")
    http.server.ThreadingHTTPServer(("127.0.0.1", port), handler).serve_forever()


if __name__ == "__main__":
    main()
