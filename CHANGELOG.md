# sZIP 1.4.1

릴리스 날짜: 2026-08-24

## 수정

- 설치 마지막 단계에서 `sZIP.App.exe` 실행이 side-by-side 구성 오류로 실패하던 문제를 수정했습니다.
- Windows 11 기본 우클릭 메뉴용 sparse identity publisher 값을 앱 manifest와 identity package manifest 모두에서 유효한 `CN=sZIP` 형식으로 맞췄습니다.

## 업데이터

- GitHub Releases 기반 업데이트 확인, 릴리스 노트 표시, 설치 파일 다운로드와 SHA-256 검증 설치 흐름을 포함합니다.
- 앱 시작 후 자동 확인, 24시간 확인 주기, 메인 창과 트레이 메뉴의 수동 `업데이트 확인`, 버전 건너뛰기를 지원합니다.

## 검증

- 릴리스 워크플로에서 앱 manifest와 identity package manifest의 sparse identity 값 일치 여부를 검사하도록 보강했습니다.
