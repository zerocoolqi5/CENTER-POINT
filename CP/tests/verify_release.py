#!/usr/bin/env python3
"""Static release checks for CENTER POINT. Does not replace Windows/device tests."""
from pathlib import Path
import re, sys, xml.etree.ElementTree as ET
ROOT = Path(__file__).resolve().parents[1]
required = [
    'Program.cs','CenterPoint.csproj','MainForm.Engine.cs','MainForm.Adb.cs',
    'MainForm.FwCenter.cs','MainForm.Samsung.cs','MainForm.Customers.cs',
    'MainForm.Inventory.cs','MainForm.Invoices.cs','MainForm.Tickets.cs',
    'MainForm.Dashboard.cs','MainForm.Settings.cs','CenterPoint_Setup.iss',
    'ui/index.html','ui/design_all.html','docs/ROADMAP_V9_V12_AR.md'
]
errors=[]
for item in required:
    if not (ROOT/item).is_file(): errors.append(f'MISSING: {item}')
try:
    tree=ET.parse(ROOT/'CenterPoint.csproj')
    props={e.tag:e.text for e in tree.getroot().iter()}
    if props.get('Version')!='13.8.0': errors.append('Project Version is not 13.8.0')
except Exception as e: errors.append(f'Invalid csproj XML: {e}')
setup=(ROOT/'CenterPoint_Setup.iss').read_text(encoding='utf-8', errors='replace')
if '#define AppVer "13.8.0"' not in setup: errors.append('Inno Setup version is not 13.8.0')
adb=(ROOT/'MainForm.Adb.cs').read_text(encoding='utf-8', errors='replace')
for marker in ['Device GetSingleAndroidTarget()', 'connected.Count > 1', 'Shell.Q(d.Id)', 'Regex.IsMatch(pkg']:
    if marker not in adb: errors.append(f'ADB safety check missing: {marker}')
# Mutating operations must use the single-target guard.
for method in ['AdbReboot(', 'AdbInstallApk(', 'AdbUninstall(', 'AdbRunShell(']:
    m=re.search(r'void\s+'+re.escape(method[:-1])+r'\s*\([^)]*\)\s*\{', adb)
    if not m: errors.append(f'Method not found: {method}'); continue
    body=adb[m.end():m.end()+900]
    if 'GetSingleAndroidTarget()' not in body: errors.append(f'{method} does not enforce single target')
if errors:
    print('FAIL')
    print('\n'.join(errors))
    sys.exit(1)
print(f'PASS: {len(required)} required project assets present')
print('PASS: project and installer metadata are version 13.8.0')
print('PASS: ADB mutating actions require one unambiguous target')
print('LIMIT: static checks only; Windows build and physical-device tests not run')
