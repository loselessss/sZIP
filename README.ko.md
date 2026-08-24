# sZIP 1.6.1

[English](README.md)

sZIP은 Windows용 무료 압축·해제 도구입니다. `.NET Framework 4.8` 기반이라 일반적인 Windows 10/11 PC에서 별도의 C# 개발 환경이나 .NET SDK 없이 설치해 사용할 수 있습니다. 설정한 용량보다 작은 압축 파일은 자동 압축 해제합니다.

## 주요 기능

- ZIP 및 7Z 압축 파일 생성
- ZIP, 7Z, RAR, TAR, GZ, TGZ/TAR.GZ 열기 및 안전한 압축 해제
- `Extract`: 선택한 폴더 또는 압축 파일 옆에 내용물을 바로 배치
- `Smart Extract`: 단일 최상위 폴더는 그대로 사용하고, 혼합된 내용은 압축 파일명 폴더에 정리
- `Extract Selected`: 선택한 파일과 폴더만 원래 경로를 유지해 압축 해제
- 압축 파일 안의 모든 하위 폴더와 빈 폴더 보존
- 선택한 폴더와 모든 하위 폴더를 감시하고 설정 용량 이하의 압축 파일 자동 압축 해제
- 다운로드 완료 상태 확인 및 10초 주기 누락 복구
- 암호 수동 입력, 작업 취소, 충돌 없는 결과 이름 생성
- 트레이 상주, Windows 로그인 시 실행, 단일 인스턴스
- 밝은 테마와 어두운 테마 자동 대응, 처리량·속도·남은 시간 표시
- 탐색기 다중 선택 `Compress with sZIP`, `sZIP Extract`, `sZIP Smart Extract`
- Windows 11 기본 우클릭 메뉴용 네이티브 `IExplorerCommand` 확장
- ZIP/7Z/RAR/TAR/GZ/TGZ 확장자의 `연결 프로그램` 등록
- GitHub Releases 기반 자동 업데이트 및 설치 전 SHA-256 확인
- 메인 창과 트레이 메뉴에서 수동 업데이트 확인

창 닫기 버튼을 누르면 sZIP은 종료되지 않고 트레이로 숨습니다. 완전히 종료하려면 트레이 아이콘을 우클릭하고 `Exit`를 선택하세요.

## 설치 및 배포 파일

GitHub Releases의 `sZIP_Setup_1.6.1.exe` 설치 파일을 권장합니다. `%LOCALAPPDATA%\Programs\sZIP`에 설치되므로 관리자 권한이 필요하지 않습니다. `sZIP-1.6.1-net48.zip`은 포터블 배포 파일입니다.

설치 프로그램에서 바탕 화면 바로가기, Windows 시작 프로그램, 탐색기 메뉴, 압축 파일 연결을 등록할 수 있습니다. Windows 11에서는 기본 우클릭 메뉴를 위한 sparse identity 패키지와 x64 `IExplorerCommand` 확장을 등록합니다. 지원되지 않는 환경과 Windows 10에서는 기존 방식의 우클릭 메뉴를 사용합니다.

## 업데이트 정책

설치된 앱은 실행 5초 후 업데이트를 확인합니다. 확인에 성공하면 24시간 동안 GitHub에 다시 요청하지 않으며, 실행 중에는 한 시간마다 확인 시점을 판단합니다. 네트워크 오류는 성공한 확인으로 기록하지 않아 다음 주기에 다시 시도할 수 있습니다.

업데이트가 있으면 Windows 표시 언어가 한국어인 경우 한국어 릴리스 노트를, 그 외에는 영어 릴리스 노트를 표시합니다. 설치 파일은 임시 `.part` 파일로 내려받으며, 파일 크기와 SHA-256 값이 GitHub 릴리스 파일과 일치할 때만 실행합니다. 나중에 설치하거나 특정 버전을 건너뛸 수 있으며, 트레이 메뉴의 `Check for Updates`에서 언제든 직접 확인할 수 있습니다.

## 개발 및 자동 확인

로컬 설치 파일 빌드에는 Inno Setup 6, Visual C++ x64 Build Tools, Windows 10/11 SDK가 필요합니다.

`v*.*.*` 태그를 푸시하면 GitHub Actions가 테스트, 포터블 ZIP 빌드, Inno Setup 설치 파일 빌드, SHA-256 파일 생성, 설치 테스트, GitHub Release 게시를 수행합니다.

## 라이선스

이 프로젝트는 MIT 라이선스입니다. 압축 형식 지원에는 MIT 라이선스의 SharpCompress 0.50.1을 사용하며 자세한 고지는 `THIRD-PARTY-NOTICES.md`에 있습니다.

버전별 변경 사항은 [CHANGELOG.ko.md](CHANGELOG.ko.md)에서 확인할 수 있습니다.
