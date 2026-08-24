# sZIP 1.5.5

sZIP은 Windows용 무료 압축·해제 도구입니다. `.NET Framework 4.8` 기반이라 일반적인 Windows 10/11 PC에서 별도의 C# 개발 환경이나 .NET SDK 없이 설치해 사용할 수 있습니다.

## 주요 기능

- ZIP 및 7Z 압축 파일 생성
- ZIP, 7Z, RAR, TAR, GZ, TGZ/TAR.GZ 열기 및 안전한 압축 해제
- `그냥 풀기`: 선택한 폴더 또는 압축 파일 옆에 내용물을 바로 배치
- `알아서 풀기`: 단일 최상위 폴더는 그대로 사용하고, 혼합된 내용은 압축파일명 폴더에 정리
- `선택 풀기`: 압축 목록에서 선택한 파일·폴더와 선택 폴더의 하위 항목만 원래 경로를 유지해 해제
- 압축 파일 안의 모든 하위 폴더와 빈 폴더 보존
- 다운로드 폴더와 모든 하위 폴더 감시, 200MB 이하 파일 자동 해제
- 다운로드 완료 안정성 확인과 10초 주기 누락 복구
- 암호 파일 수동 입력, 작업 취소, 충돌 없는 결과 이름 생성
- 트레이 상주, Windows 로그인 시 실행, 단일 인스턴스
- Fluent 스타일, 밝은/어두운 Windows 앱 테마 자동 대응, 처리량·속도·남은 시간 진행 표시
- 탐색기 다중 선택 `sZIP으로 압축`, `sZIP 그냥 풀기`, `sZIP 알아서 풀기`
- Windows 11 기본 우클릭 메뉴용 네이티브 `IExplorerCommand` 확장
- ZIP/7Z/RAR/TAR/GZ/TGZ 확장자의 `연결 프로그램` 등록
- GitHub Releases 기반 자동 업데이트, SHA-256 검증 후 설치
- 메인 창과 트레이 메뉴에서 수동 업데이트 확인

창의 X 버튼은 앱을 종료하지 않고 트레이로 숨깁니다. 완전히 종료하려면 트레이 아이콘을 오른쪽 클릭한 뒤 `종료`를 선택합니다.

## 설치와 배포본

GitHub Releases의 `sZIP_Setup_1.5.5.exe`가 권장 설치본입니다. 사용자 계정의 `%LOCALAPPDATA%\Programs\sZIP`에 설치하므로 관리자 권한이 필요하지 않습니다. `sZIP-1.5.5-net48.zip`은 설치하지 않고 사용할 수 있는 포터블 배포본입니다.

설치 프로그램은 선택에 따라 바탕 화면 바로가기, Windows 자동 시작, 탐색기 메뉴와 압축 확장자 연결을 등록합니다. Windows 11에서는 sparse identity package와 x64 `IExplorerCommand` 확장을 등록해 명령을 기본 우클릭 메뉴에 표시하며, 등록할 수 없는 환경과 Windows 10에서는 기존 레거시 메뉴를 대체 경로로 유지합니다.

## 업데이트 정책

설치 앱은 시작 5초 후 업데이트를 확인합니다. 마지막 성공 확인 후 24시간이 지나야 다시 GitHub에 요청하며, 실행 중에는 매시간 재평가합니다. 네트워크 실패는 성공으로 기록하지 않아 다음 매시간 주기에 재시도합니다.

업데이트가 있으면 릴리스 노트와 설치 파일 정보를 보여 줍니다. 설치 파일은 임시 `.part` 파일로 받은 뒤 GitHub 릴리스 자산의 SHA-256 digest와 크기를 검증해야만 실행합니다. 사용자는 나중에 설치하거나 해당 버전만 건너뛸 수 있고, 트레이 메뉴의 `업데이트 확인`으로 언제든 수동 확인할 수 있습니다.

## 개발 및 자동 검증

```powershell
$szipRoot=(Get-Location).Path
$env:DOTNET_CLI_HOME="$szipRoot\.dotnet-home"
$env:NUGET_PACKAGES="$szipRoot\.packages"

& "$szipRoot\.dotnet\dotnet.exe" restore sZIP.sln
& "$szipRoot\.dotnet\dotnet.exe" test sZIP.sln --configuration Release --no-restore
& "$szipRoot\.dotnet\dotnet.exe" publish src\sZIP.App\sZIP.App.csproj --configuration Release --output artifacts\publish --no-restore
```

로컬 설치 프로그램 생성에는 Inno Setup 6, Visual C++ x64 Build Tools와 Windows 10/11 SDK가 필요합니다.

```powershell
.\build_installer.ps1
```

`v*.*.*` 태그를 푸시하면 GitHub Actions가 테스트, 포터블 ZIP, Inno Setup 설치본, SHA-256 파일과 GitHub Release를 자동 생성합니다.

## 라이선스

프로젝트는 MIT 라이선스입니다. 압축 형식 지원에는 MIT 라이선스의 SharpCompress 0.50.1을 사용하며 자세한 고지는 `THIRD-PARTY-NOTICES.md`에 있습니다.

버전별 변경 사항은 [CHANGELOG.md](CHANGELOG.md)에서 확인할 수 있습니다. 수동 UI·탐색기 QA는 사용자 요청에 따라 1.0 정식 QA 단계에서 별도로 진행합니다.
