#!/usr/bin/env python3
"""Syntax-check inline JavaScript in the CENTER POINT UI using Node.js."""
from pathlib import Path
import re, shutil, subprocess, tempfile, sys
ROOT = Path(__file__).resolve().parents[1]
node = shutil.which("node")
if not node:
    raise SystemExit("FAIL: Node.js is required for UI syntax checks")
files = [ROOT/"ui/index.html", ROOT/"ui/design_all.html", ROOT/"ui/dash_preview.html"]
errors=[]
with tempfile.TemporaryDirectory() as tmp:
    for html in files:
        source=html.read_text(encoding="utf-8", errors="replace")
        scripts=re.findall(r"<script[^>]*>(.*?)</script>", source, re.S|re.I)
        for index, script in enumerate(scripts):
            if not script.strip():
                continue
            js=Path(tmp)/f"{html.stem}_{index}.js"
            js.write_text(script, encoding="utf-8")
            result=subprocess.run([node, "--check", str(js)], capture_output=True, text=True)
            if result.returncode:
                errors.append(f"{html.name} script {index}: {result.stderr.strip()}")
        print(f"PASS: {html.relative_to(ROOT)} ({len(scripts)} script blocks)")
if errors:
    print("FAIL\n"+"\n".join(errors))
    sys.exit(1)
print("PASS: all inline UI JavaScript syntax checks")
