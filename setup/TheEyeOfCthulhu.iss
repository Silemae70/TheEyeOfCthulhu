; The Eye of Cthulhu - Inno Setup Script
; Version 1.0.0

#define MyAppName "The Eye of Cthulhu"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Laser Cheval"
#define MyAppURL "https://github.com/Silemae70/TheEyeOfCthulhu"
#define MyAppExeName "TheEyeOfCthulhu.Lab.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
AppId={{8E7B3F4A-9C2D-4E5F-B1A8-6D7E9F0C2B3A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; Output settings
OutputDir=output
OutputBaseFilename=TheEyeOfCthulhu_Setup_{#MyAppVersion}
; Compression
Compression=lzma2/ultra64
SolidCompression=yes
; Visual
WizardStyle=modern
; Privileges (admin pour installer dans Program Files)
PrivilegesRequired=admin
; Uninstall
UninstallDisplayName={#MyAppName}

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Main application - tous les fichiers du build Release
Source: "..\src\TheEyeOfCthulhu.Lab\bin\Release\net8.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Documentation
Source: "..\docs\README.md"; DestDir: "{app}\docs"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Documentation"; Filename: "{app}\docs\README.md"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  ResultCode: Integer;

function IsDotNet8Installed(): Boolean;
var
  Output: AnsiString;
begin
  // Check if dotnet command exists and has .NET 8
  Result := Exec('cmd.exe', '/c dotnet --list-runtimes | findstr /C:"Microsoft.WindowsDesktop.App 8."', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := Result and (ResultCode = 0);
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  
  // Simple check - try to run dotnet
  if not FileExists(ExpandConstant('{sys}\dotnet.exe')) then
  begin
    if MsgBox('Cette application nécessite .NET 8 Desktop Runtime.'#13#10#13#10 +
              'Voulez-vous le télécharger maintenant ?'#13#10#13#10 +
              'This application requires .NET 8 Desktop Runtime.'#13#10 +
              'Would you like to download it now?', 
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0', '', '', SW_SHOW, ewNoWait, ErrorCode);
    end;
    Result := False;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Optionnel: nettoyer les données utilisateur
    // DelTree(ExpandConstant('{userappdata}\TheEyeOfCthulhu'), True, True, True);
  end;
end;
