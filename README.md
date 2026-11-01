# Pixel TXT Converter

Windows 파일 탐색기에서 **이미지 우클릭 → TXT Convert**를 누르면 이미지 옆에 RGB TXT를 저장하고 **메모장으로 바로 엽니다.** 저장한 TXT는 **우클릭 → TXT Restore (PNG)**로 다시 이미지로 복원할 수 있습니다.

## 설치와 사용 (Windows 10 / 11)

[최신 Windows 버전 다운로드](https://github.com/Ragim-Games/pixel-txt-converter/releases/latest)

1. Releases에서 **PixelTxtConverter.exe**를 다운로드하고 실행합니다.
2. **설치 / 메뉴 연결**을 누릅니다. 현재 사용자 계정에만 설치하므로 관리자 권한이 필요 없습니다.
3. 파일 탐색기에서 이미지를 우클릭하고 **TXT Convert**를 누릅니다.
4. 예를 들어 `사진.png` 옆에 `사진.png.txt`가 만들어지고 메모장이 열립니다.
5. 해당 TXT에서 **TXT Restore (PNG)**를 누르면 `사진.png.restored.png`를 생성해 기본 이미지 앱으로 엽니다.

**Windows 11에서는 우클릭 → 더 많은 옵션 안에 메뉴가 표시됩니다.** Windows 10에서는 기존 우클릭 메뉴에 표시됩니다. Windows 11의 첫 번째 간소화 메뉴에 직접 등록하는 MSIX/COM 확장 방식은 사용하지 않습니다. [Microsoft의 메뉴 안내](https://blogs.windows.com/blog/2021/07/19/extending-the-context-menu-and-share-dialog-in-windows-11/)

같은 이름의 출력 파일이 있으면 `(2)`, `(3)` 등을 붙여 저장합니다. 원본 이미지와 기존 TXT를 덮어쓰지 않습니다. 모든 파일 처리는 기기 안에서 이루어집니다.

### 지원 형식과 설치 위치

- 우클릭 지원: PNG, JPG, JPEG, JFIF, BMP, GIF, TIF, TIFF, ICO.
- GIF/TIFF 등 여러 프레임이 있는 파일은 첫 프레임만 처리합니다. WebP/AVIF/HEIC는 Windows 버전에서 지원하지 않습니다.
- Windows 10/11의 .NET Framework 4.8 환경을 대상으로 하며 Python, Node.js, 웹브라우저 설치는 필요 없습니다.
- 실행 파일: `%LOCALAPPDATA%\PixelTxtConverter\PixelTxtConverter.exe`.
- 메뉴는 `HKCU\Software\Classes\SystemFileAssociations`의 지원 확장자 아래에 앱 전용 키로 등록합니다. 기본 이미지/TXT 연결 프로그램은 변경하지 않습니다.
- 일반 TXT에도 복원 메뉴가 표시되지만, `PIXELTXT/1` 형식만 복원할 수 있습니다. 형식이 다르면 오류를 안내합니다.
- 메뉴 연결 해제: 실행 파일의 **메뉴 연결 해제** 버튼 또는 Windows 설치된 앱의 **Pixel TXT Converter → 제거**를 사용합니다. 제거 후 실행 중이던 EXE 한 개는 설치 폴더에 남으므로 필요 없다면 직접 삭제할 수 있습니다.
- 배포 EXE는 코드 서명되지 않은 빌드입니다.

## TXT 형식 (버전 1)

```text
PIXELTXT/1/2/2
255,0,0/0,255,0/0,0,255/255,255,255
```

- 첫 줄: `PIXELTXT/버전/가로/세로`. 이미지 복원에 필요한 크기를 기록합니다.
- 두 번째 줄: 각 픽셀을 `R,G,B`로 기록하고 **픽셀의 RGB 묶음 사이를 `/`로 구분**합니다. 끝에 `/`를 붙이지 않습니다.
- 각 채널은 `0`~`255` 범위의 정수입니다.
- 위 예시는 첫 행이 빨강·초록, 둘째 행이 파랑·흰색인 2 × 2 이미지입니다.
- 데이터의 `n`번째 픽셀(0부터 시작)은 `x = n % 가로`, `y = floor(n / 가로)`에 배치됩니다.
- UTF-8 TXT를 저장합니다. 불러올 때 UTF-8 BOM, CRLF 줄바꿈, RGB 묶음 앞뒤의 공백도 허용합니다.

## 처리 범위

- 애니메이션 전체나 원본 파일 메타데이터는 보존하지 않습니다.
- 원본 해상도를 유지하며 자동 축소하지 않습니다. 최대 **4,194,304픽셀**, 한 변은 최대 **16,384픽셀**, 입력 파일은 최대 **64 MiB**입니다.
- RGB는 알파 채널을 포함하지 않습니다. **투명도는 흰색 배경과 합성**한 뒤 기록됩니다. TXT에서 복원되는 이미지는 불투명합니다.
- 디코더가 읽은 RGB 값을 저장합니다. 원본 압축 파일 자체를 복원하거나 색상 프로파일을 보존하는 형식이 아닙니다.
- 잘못된 헤더, 크기, 채널 범위, 누락 또는 초과한 픽셀은 오류로 안내합니다.
- 큰 이미지의 TXT는 원본 압축 이미지보다 훨씬 커질 수 있습니다. Windows 버전은 행 단위로 기록하고 진행률을 표시합니다.

## 개발 및 검증

### Windows 버전

Windows PowerShell에서 실행합니다. Windows에 포함된 .NET Framework 컴파일러를 사용하므로 패키지 다운로드가 필요 없습니다.

```powershell
.\windows\build.ps1 -Test
.\dist\WindowsTests.exe --registry
```

`dist/PixelTxtConverter.exe`가 설치 화면과 변환 기능을 모두 포함합니다. 레지스트리 테스트는 실제 탐색기 연결 대신 별도의 임시 키에서 등록·해제를 검증합니다. GitHub Actions에서도 Windows 빌드와 테스트를 실행하며 EXE를 아티팩트로 제공합니다.

설치 없이 명령줄로 변환할 수도 있습니다. 변환 후에는 결과 파일을 자동으로 엽니다.

```powershell
.\dist\PixelTxtConverter.exe --to-txt "C:\사진\이미지.png"
.\dist\PixelTxtConverter.exe --to-image "C:\사진\이미지.png.txt"
```

### 보조 브라우저 버전

기존 브라우저 도구도 `index.html`로 남아 있으며 Windows 버전과 같은 TXT 형식을 사용합니다. `index.html`을 열어 이미지/TXT를 불러오고 다운로드할 수 있습니다. Node.js 18 이상에서 이 버전의 핵심 변환 테스트를 실행합니다.

```sh
npm test
```

브라우저 통합 테스트는 선택 사항입니다.

```sh
npm install --no-save playwright
npx playwright install chromium
node tests/browser.cjs
```

테스트는 픽셀 순서와 RGB 왕복 일치, 투명도, 여러 처리 묶음에 걸친 데이터, 잘못된 TXT, 실제 파일 입력과 다운로드, 모바일 가로 넘침을 검증합니다. 브라우저 테스트 스크린샷은 Git에서 제외된 `artifacts/`에 생성됩니다.

| 파일 | 역할 |
| --- | --- |
| `windows/Core.cs` | Windows RGB TXT 인코딩/복원, 파일 보존 |
| `windows/ShellIntegration.cs` | 현재 사용자 설치 및 탐색기 메뉴 연결 |
| `windows/Program.cs` | 설치 화면, 변환 진행 창, 메모장 자동 실행 |
| `windows/build.ps1`, `windows/Tests.cs` | EXE 빌드 및 Windows 검증 |
| `index.html`, `styles.css` | 한국어 인터페이스와 반응형 디자인 |
| `codec.js` | 크기 검증, RGB TXT 인코딩 및 디코딩 |
| `app.js` | 파일 입출력, 미리보기, 다운로드 및 픽셀 확인 |
| `tests/` | Node.js 단위 테스트 및 브라우저 통합 테스트 |
