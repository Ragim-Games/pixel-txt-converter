# Pixel TXT Converter · Python

Windows 10/11에서 이미지를 우클릭해 RGB TXT로 저장하고, **`.txt`를 `.txp`로 바꾸면 PNG를 자동 생성**하는 Python 소스 프로젝트입니다. EXE 빌드나 별도 WebP 실행 파일을 배포하지 않습니다.

## 시작하기

Python **3.11 이상**(tkinter 포함)을 설치하고 저장소를 다운로드한 뒤 저장소 폴더에서 실행합니다.

```powershell
py -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
.\.venv\Scripts\python.exe main.py
```

설치 화면에서 **설치 / 자동 변환 시작**을 누르면 우클릭 메뉴, TXP 파일 연결, 로그인 자동 시작이 현재 사용자에게 등록됩니다. 관리자 권한은 필요 없습니다.

의존성은 Pillow(이미지/WebP), watchdog(파일 감지), pystray(알림 영역)입니다. 소스는 `%LOCALAPPDATA%\PixelTxtConverter\python`에 복사됩니다. 등록된 명령은 설치에 사용한 **Python 인터프리터 또는 가상환경**을 참조하므로 해당 Python/`.venv`를 삭제하지 마세요. 소스를 업데이트한 후에는 설치를 다시 실행해야 적용됩니다.

기존 C# 버전이 설치되어 있으면 설치 과정에서 기존 감지 프로그램을 종료하고 Python 연결로 교체합니다. 성공 후 앱이 사용하던 이전 EXE와 디코더 라이선스 파일만 정리합니다.

## 사용 방법

1. 이미지 우클릭 → **TXT Convert**: `사진.png.txt`를 저장하고 메모장으로 엽니다.
2. 이 TXT의 확장자를 **`.txp`**로 바꿉니다. 예: `그림.txt` → `그림.txp`.
3. 자동 변환 프로그램이 실행 중이면 같은 폴더에 **`그림.png`**가 생성됩니다. 더블클릭할 필요 없습니다.
4. TXT/TXP 우클릭 → **TXT Restore (PNG)** / **TXP Restore (PNG)** 또는 TXP 더블클릭으로도 복원할 수 있습니다.

Windows 11의 우클릭 항목은 **더 많은 옵션**에 표시됩니다. 파일 확장자를 표시한 뒤 마지막 `.txt`를 바꾸세요. `그림.txp.txt`는 자동 변환되지 않습니다.

TXP는 아래 RGB TXT와 내용이 동일한 파일입니다. 첫 줄의 이미지 크기를 유지해야 합니다. 원본 TXT/TXP/이미지를 보존하며, 출력 이름이 겹치면 `(2)`, `(3)`을 붙입니다.

## 자동 변환

- 설치 시 즉시 시작되고 다음 Windows 로그인에도 Python으로 실행됩니다.
- 로컬 고정/이동식 드라이브에서 새로 생성되거나 이름이 바뀐 TXP만 감지합니다. 기존 파일 전체를 검색하지 않습니다.
- 새 드라이브 연결은 최대 약 10초 후 반영됩니다. 네트워크 폴더는 수동 복원을 사용하세요.
- 파일이 저장 중이면 잠시 재시도합니다. 오류는 알림과 `%LOCALAPPDATA%\PixelTxtConverter\python.log`에 기록합니다.
- 알림 영역의 **이번 로그인 동안 자동 변환 종료**로 중지합니다. 다시 켜려면 설치 화면의 시작 버튼을 누릅니다.
- 설치 화면의 **자동 변환 / 연결 해제**는 감지 프로그램, 로그인 자동 시작, 앱의 메뉴/파일 연결을 해제합니다. Python 소스와 사용자 파일은 남습니다.
- 프로그램이 꺼져 있었거나 대량 파일 작업 중 감지되지 않은 TXP는 수동 복원하세요.

## TXT / TXP 형식

```text
PIXELTXT/1/2/2
255,0,0/0,255,0/0,0,255/255,255,255
```

첫 줄은 `PIXELTXT/1/가로/세로`, 다음 줄은 `R,G,B` 묶음을 `/`로 나눈 데이터입니다. 왼쪽 위부터 오른쪽으로 읽고, 행이 끝나면 다음 행의 왼쪽으로 이동합니다. 마지막에는 `/`를 붙이지 않습니다. 각 채널은 0~255 정수입니다.

UTF-8을 사용하며, 불러올 때 BOM, CRLF와 RGB 묶음 앞뒤 공백을 허용합니다. 잘못된 크기, 값, 픽셀 수는 오류로 안내합니다.

## 지원 범위

- PNG, JPG/JPEG/JFIF, BMP, GIF, TIFF, ICO, 정지 WebP(손실·무손실·투명).
- WebP는 Pillow의 코덱으로 처리합니다. 별도 `dwebp.exe`가 필요 없습니다.
- 투명도는 흰색과 합성합니다. 애니메이션 WebP는 지원하지 않으며 GIF/TIFF는 첫 프레임을 사용합니다.
- 최대 4,194,304픽셀, 한 변 16,384픽셀, 입력 파일 64 MiB. 해상도를 임의로 줄이지 않습니다.
- 압축 원본 바이트, 메타데이터, 색상 프로파일을 보존하는 형식이 아닙니다.
- 탐색기 연결과 알림 영역은 Windows 10/11용입니다. 변환 함수는 Windows 전용 API와 분리되어 있습니다.

## 명령줄과 테스트

```powershell
python main.py --install
python main.py --to-txt "C:\사진\그림.webp"
python main.py --to-image "C:\사진\그림.txp"
python main.py --to-image "C:\사진\그림.txp" --no-open
python main.py --stop-watcher
python -m unittest discover -s tests -v
```

가상환경을 사용했다면 `python` 대신 `.\.venv\Scripts\python.exe`를 쓰세요. 테스트 전에는 기존 자동 변환을 종료해 테스트 파일이 두 번 처리되지 않게 합니다. 테스트 후에는 `python main.py --install`로 다시 시작할 수 있습니다.

선택적인 레지스트리 테스트는 실제 탐색기 연결을 바꾸지 않고 임시 키에서 검증합니다.

```powershell
$env:PIXELTXT_REGISTRY_TEST = '1'
python -m unittest discover -s tests -v
```

GitHub Actions에서 Python 3.11/3.12/3.13으로 검증합니다. Windows 10의 파일 이벤트 호환성을 확인한 watchdog 6.0.0을 사용합니다.

| 경로 | 역할 |
| --- | --- |
| `main.py` | 실행 진입점 |
| `pixeltxt/core.py` | 이미지↔RGB TXT/TXP 변환 |
| `pixeltxt/monitor.py` | TXP 생성/이름 변경 감지 |
| `pixeltxt/windows.py` | Python 소스 설치, 파일 연결, 자동 시작 |
| `pixeltxt/tray.py` | 알림 영역과 감지 프로그램 수명 관리 |
| `pixeltxt/app.py` | 설치 화면과 명령줄 |
| `requirements.txt` | Python 의존성 |
| `tests/` | 변환·WebP·실제 이름 변경·등록/해제 검증 |
