# MSIX 배포 가이드 — 1.9.0 개발 미리보기

현재 EXE 설치본과 업데이트는 유지합니다. 이 구성은 앱 전체를 담는 MSIX를 **Store / Direct 두 경로**로 빌드하기 위한 미리보기입니다. 아직 Store에 제출하거나 서명된 MSIX를 배포하지 않았습니다.

## 배포별 역할

| 항목 | Microsoft Store | GitHub 직접 배포 |
| --- | --- | --- |
| 패키지 식별 정보 | Partner Center의 Name / Publisher | 직접 배포용 고정 Name / 서명 인증서 Subject |
| 서명 | Store 제출 과정에서 처리 | 사용자 PC에서 신뢰되는 코드 서명 필요 |
| 업데이트 | Store | HTTPS .appinstaller 파일, 실행 시 12시간 간격 확인 |
| 앱의 업데이트 버튼 | Store 업데이트 화면 | 지정한 HTTPS .appinstaller 주소 열기 |
| EXE 업데이트 실행 | 하지 않음 | 하지 않음 |

기본 방침은 두 경로에 **서로 다른 패키지 Name**을 사용하는 것입니다. 최초 배포 후 Name과 Publisher를 임의로 바꾸면 기존 설치의 업데이트 경로가 끊어집니다. Store와 직접 배포 사이의 자동 전환은 구현하지 않았습니다.

## 공통 빌드

개발 PC에는 .NET SDK(global.json), Visual Studio C++ v143 빌드 도구와 Windows SDK가 필요합니다. 사용자 PC에 개발 도구를 설치하는 방식이 아닙니다. 대상은 Windows 10 2004 이상 / Windows 11 x64이며 .NET Framework 4.8을 사용합니다.

저장소 루트에서 PowerShell로 실행합니다.

    dotnet restore sZIP.sln
    dotnet test tests/sZIP.Tests/sZIP.Tests.csproj -c Release --no-restore
    dotnet publish src/sZIP.App/sZIP.App.csproj -c Release -o artifacts/publish --no-restore
    msbuild src/sZIP.ShellExtension/sZIP.ShellExtension.vcxproj /p:Configuration=Release /p:Platform=x64

반드시 새 네이티브 DLL까지 같은 소스에서 빌드해야 합니다. 기존 릴리스 DLL은 Store/Direct 전용 COM ID를 지원하지 않습니다.

SDK를 시스템에 설치하지 않고 **패키징 검사 도구만** 프로젝트에 준비할 수도 있습니다.

    dotnet restore packaging/msix/BuildTools.csproj

이 패키지는 C++ 컴파일러를 제공하지 않습니다. MakeAppx/SignTool 위치를 찾아 빌드 스크립트의 -SdkBinDirectory로 전달합니다.

## Store 제출용

아래 꺾쇠 부분은 Partner Center의 실제 값으로 바꿉니다. 템플릿의 Name/Publisher는 제출용 신원이 아닙니다.

    .\build_msix.ps1 -Channel Store -IdentityName '<Partner Center Name>' -Publisher '<Partner Center Publisher>' -PublisherDisplayName '<게시자 표시 이름>'

결과는 별도 출력 폴더의 Store용 .msix입니다. Store 제출을 위한 미서명 파일이며, 그대로 일반 사용자에게 직접 설치하라고 배포하면 안 됩니다. 최종 제출 전에 Windows App Certification Kit 및 설치 검증을 수행합니다. runFullTrust 용도와 개인정보/지원 정보도 Store 제출에 맞게 준비해야 합니다.

## 직접 배포용

필요한 값:

- 계속 유지할 직접 배포용 패키지 Name
- 코드 서명 인증서의 정확한 Subject와 CurrentUser/My 저장소의 thumbprint
- 서명 서비스에서 제공하는 timestamp URL
- 고정 HTTPS .appinstaller 주소
- 해당 버전의 서명된 .msix가 공개될 HTTPS 주소

인증서 생성·신뢰 등록·비밀키 업로드는 자동으로 수행하지 않습니다. 인증서 비밀번호나 개인키를 소스/채팅/릴리스 파일에 넣지 마세요.

    .\build_msix.ps1 -Channel Direct -IdentityName '<직접 배포 Name>' -Publisher '<인증서 Subject>' -PublisherDisplayName '<게시자 표시 이름>' -CertificateThumbprint '<40자리 thumbprint>' -TimestampUri '<timestamp URL>' -AppInstallerUri 'https://example.org/sZIP.appinstaller' -PackageUri 'https://example.org/sZIP-1.9.0-Direct-x64.msix'

스크립트는 인증서 Subject 일치, 서명 및 신뢰 검증이 성공한 경우에만 배포용 .appinstaller를 만듭니다. 게시 시 패키지 파일을 먼저 올리고, 고정 feed를 마지막에 갱신합니다. 지정한 URL과 실제 파일 위치가 일치해야 합니다.

사용자는 **.appinstaller를 통해 처음 설치**해야 업데이트 feed가 연결됩니다. .msix 파일만 직접 설치하면 feed 기반 자동 업데이트가 설정되지 않습니다. 앱이 계속 트레이에서 실행 중이면 업데이트 적용에 종료/재실행이 필요할 수 있습니다.

현재 서명 경로는 Windows 인증서 저장소 + SignTool입니다. Azure 등의 원격 서명 서비스를 사용하려면 별도 서명 단계를 연동하고, 성공 검증 후에만 preview feed를 배포용으로 전환해야 합니다.

## 설치 없는 검사와 CI

    .\tests\Verify-MsixPackaging.ps1 -SdkBinDirectory '<MakeAppx.exe가 있는 x64 폴더>'

이 검사는 의도적으로 **가짜 네이티브 DLL**을 넣어 매니페스트, 파일 구성, 채널 분리, 업데이트 메타데이터와 미서명 직접 배포 차단만 검사합니다. 생성물은 설치용이 아니며 실제 Explorer COM 동작을 검증하지 않습니다.

.github/workflows/msix-validation.yml은 수동 실행 전용입니다. C++ 확장을 실제 빌드하고 두 경로의 미서명 검증 파일을 보관하지만, 인증서 설치/Store 제출/GitHub 릴리스 게시는 하지 않습니다. sZIP.Validation.* 및 example.invalid가 들어간 산출물은 실제 사용자에게 배포하지 마세요.

-PrepareOnly는 파일 배치만 만들고, -Unsigned는 검증용 패키지만 만듭니다. Direct 미서명 출력은 .unsigned.msix 및 .preview.appinstaller.xml로 구분됩니다.

## 앱 동작과 데이터

- 설치 매니페스트가 시작 메뉴, 파일 연결, 파일/폴더 우클릭 메뉴를 등록합니다. 기본 앱은 강제로 변경하지 않습니다.
- 자동 시작은 기본 꺼짐이며 Windows 시작 앱 설정에서 켭니다. 활성화되면 --tray로 시작합니다.
- 패키지 환경에서는 HKCU Run/legacy shell 등록을 직접 변경하지 않습니다.
- 설정은 패키지 family별 고정 경로에 저장하고, 실행 중인 앱의 통신 이름과 대기 명령도 EXE/다른 family와 분리합니다.
- 기존 EXE 설정을 자동으로 옮기거나 EXE 설치본·이전 sparse 패키지를 자동 제거하지 않습니다. 둘 다 설치하면 메뉴가 중복되거나 자동 압축 해제 감시가 중복될 수 있으므로 전환 QA에서 확인합니다.
- package identity를 실행 파일 매니페스트에 강제 삽입하지 않습니다. 일반 EXE는 패키지 등록 없이도 시작해야 합니다.

## 배포 전 남은 검증

1. 실제 Store/Direct 식별 정보와 서명 방식 확정.
2. 새 네이티브 확장 빌드와 서명된 패키지 설치.
3. 설치된 앱 시작, ZIP/7Z 열기, 파일·폴더 혼합 다중 선택 압축/해제.
4. Store/Direct 메뉴와 언어 설정, 다른 설치본 공존 시 충돌 여부.
5. 이전 MSIX에서 업데이트 후 설정/감시 폴더 유지, 장시간 트레이 실행 중 업데이트.
6. 시작 앱 켜기/끄기, 제거 후 메뉴 정리, Windows 배율·언어 QA.

## 공식 참고 문서

- [수동 MSIX 패키징](https://learn.microsoft.com/en-us/windows/msix/package/manual-packaging-root)
- [MSIX 서명](https://learn.microsoft.com/en-us/windows/msix/package/sign-app-package-using-signtool)
- [App Installer 업데이트](https://learn.microsoft.com/en-us/windows/msix/app-installer/auto-update-and-repair--overview)
- [Store 업데이트 화면 URI](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-store-app)
- [패키지 시작 작업](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-desktop-startuptask)
