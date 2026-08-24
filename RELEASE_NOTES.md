# sZIP 1.4.1 릴리스 노트

릴리스 날짜: 2026-08-24

## 핵심 변경

- 설치 마지막 단계에서 `sZIP.App.exe` 실행이 side-by-side 구성 오류로 실패하던 문제를 수정했습니다.
- Windows 11 기본 우클릭 메뉴용 sparse identity publisher 값을 앱 manifest와 identity package manifest 모두에서 유효한 `CN=sZIP` 형식으로 맞췄습니다.
- GitHub Releases 기반 업데이터를 포함합니다. 릴리스 노트 표시, 설치 파일 다운로드, SHA-256 검증, 버전 건너뛰기, 메인 창과 트레이 메뉴의 수동 확인을 지원합니다.
- 릴리스 워크플로가 두 manifest의 sparse identity 값 일치를 검증하도록 보강했습니다.

## 자동 검증

- XML sparse identity 일치 검증 통과
- 이 로컬 환경에는 .NET SDK, Visual C++ Build Tools, Windows SDK, Inno Setup이 설치되어 있지 않아 전체 Release 빌드와 설치본 생성은 GitHub Actions에서 수행합니다.
