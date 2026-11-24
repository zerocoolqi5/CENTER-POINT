; ============================================================
;  CENTER POINT TRANSFER - مثبّت احترافي (Inno Setup)
;  يبني Setup.exe واحد مع: اختصارات، إزالة، وفحص WebView2 تلقائياً.
;  الطريقة: 1) شغّل «بناء_وتثبيت.bat» مرة لإنتاج مجلد publish
;           2) افتح هذا الملف ببرنامج Inno Setup واضغط Compile (أو شغّل بناء_مثبّت.bat)
; ============================================================

#define AppName "CENTER POINT TRANSFER"
#define AppVer "12.0.0"
#define Publisher "سنتر بوينت للاتصالات"
#define ExeName "CenterPoint-Transfer.exe"
#define PubDir "bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppName={#AppName}
AppVersion={#AppVer}
AppPublisher={#Publisher}
DefaultDirName={autopf}\CenterPoint Transfer
DefaultGroupName=CENTER POINT TRANSFER
UninstallDisplayIcon={app}\{#ExeName}
OutputBaseFilename=CenterPoint_Transfer_Setup_{#AppVer}
OutputDir=Setup_Output
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=icon.ico
PrivilegesRequired=admin
DisableProgramGroupPage=yes

[Languages]
Name: "ar"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "إنشاء اختصار على سطح المكتب"; GroupDescription: "اختصارات:"

[Files]
Source: "{#PubDir}\{#ExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PubDir}\ui\*"; DestDir: "{app}\ui"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "logo.png"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\CENTER POINT TRANSFER"; Filename: "{app}\{#ExeName}"
Name: "{group}\إزالة البرنامج"; Filename: "{uninstallexe}"
Name: "{commondesktop}\CENTER POINT TRANSFER"; Filename: "{app}\{#ExeName}"; Tasks: desktopicon

[Run]
; تثبيت WebView2 Runtime تلقائياً عند غيابه (يحتاج إنترنت مرة واحدة)
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""try{{ $u='https://go.microsoft.com/fwlink/p/?LinkId=2124703'; $o=Join-Path $env:TEMP 'MicrosoftEdgeWebview2Setup.exe'; Invoke-WebRequest -Uri $u -OutFile $o -UseBasicParsing; Start-Process -Wait -FilePath $o -ArgumentList '/silent','/install' }}catch{{}}"""; \
  StatusMsg: "تثبيت WebView2 Runtime..."; Check: WebView2Missing; Flags: runhidden

Filename: "{app}\{#ExeName}"; Description: "تشغيل البرنامج الآن"; Flags: nowait postinstall skipifsilent

[Code]
function WebView2Missing: Boolean;
var v: String;
begin
  Result := not (
    RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', v) or
    RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', v) or
    RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', v)
  );
end;
