# ASHFALL — 핵전쟁 이후의 정착지

Unity **6000.6.9f1**용 2D 탑다운 정착지·전투 프로토타입입니다.
핵전쟁 후 산악 수계 주변에 도착한 생존자 세 명을 지휘합니다.
코드와 프로젝트 자체 생성 그래픽은 MIT 라이선스로 제공합니다.

> **AI 작성 고지:** 이 프로젝트의 전체 사용자 정의 코드, 테스트, 문서와 절차적 그래픽 생성 코드는 사용자가 제시한 기획을 바탕으로 OpenAI Codex(AI)가 작성했습니다. 사람이 직접 작성한 코드로 소개하지 않습니다. Unity 엔진 및 Unity가 자동 생성한 파일은 해당 고지의 AI 작성 범위에 포함되지 않습니다.
>
> **AI authorship disclosure:** All custom code, tests, documentation, and procedural graphics generation code in this project were written by OpenAI Codex (AI), based on the user's game design. Unity itself and Unity-generated files are separate third-party/tool-generated components.

## 실행

1. Unity Hub에서 **Add / Add project from disk**를 선택합니다.
2. 이 README와 `Assets`, `Packages`, `ProjectSettings`가 들어 있는 폴더를 선택합니다.
3. **6000.6.9f1**로 열고 최초 임포트가 완료될 때까지 기다립니다.
4. 메뉴 **Ashfall → Open Prototype** 또는 `Assets/Scenes/Ashfall.unity`를 엽니다.
5. **Play**를 누르고 안내 창에서 **CONTINUE TO COLONY**를 누릅니다.

빈 씬에서 실행해도 `RuntimeInitializeOnLoadMethod`가 프로토타입을 생성합니다.
씬에 컴포넌트를 수동으로 붙이거나 프리팹을 연결할 필요가 없습니다.
추후 여러 씬을 쓰는 정식 게임으로 확장할 때는 자동 부트스트랩을 시작 씬 전용으로 바꾸세요.

기본 제공 IMGUI 이벤트를 이용하므로 Input System 패키지 설치나 Active Input Handling 변경이 필요 없습니다.
화면은 영어 UI이며 안내 문서는 한국어입니다. 한국어 폰트 에셋과 현지화는 아직 포함하지 않았습니다.
권장 화면 크기는 1280×800 이상입니다. 작은 화면에서는 UI 전체가 축소됩니다.

## 조작

| 입력 | 동작 |
| --- | --- |
| 좌클릭 | 주민 선택 / 선택한 건설 도구 사용 |
| 우클릭 지면 | 선택 주민 이동 |
| 우클릭 잔해 | 인접 위치로 이동해 고철 회수 |
| 우클릭 적 | 정지 상태에서 사정거리·시야가 확보되면 공격 |
| P | 서기 ↔ 엎드리기 |
| R | 시계 방향으로 90도 회전 |
| 1 / 2 / 3 / 4 | 칼 / 활 / 소총 / 샷건 |
| Space | 일시정지 / 재개 |
| 마우스 휠 | 확대·축소 |
| 마우스 가운데 버튼 드래그 | 지도 이동 |
| Home | 지도 전체 보기 |
| Esc | 건설 도구 취소 / 안내 창 닫기 |

왼쪽 패널에서 벽, 다리, 철거 도구를 선택합니다. **Call raider**는 현재 주민이 있는 육지에서 접근 가능한 습격자 한 명을 생성합니다.
자동 습격은 없습니다. 적이 멀면 주민을 직접 이동시켜 사거리를 확보하고 다시 공격 명령을 내리세요.
주민은 자동으로 반격하지 않으므로 개별 공격 명령이 필요합니다.

## 구현한 규칙

- **서기 1×1칸 / 엎드리기 2×1 또는 1×2칸.** 앞쪽 칸을 기준 위치로 하고 뒤쪽 칸까지 점유합니다.
- 동서남북 방향, 자세 전환 0.65초, 엎드린 회전 0.3초. 추가 점유 칸이 벽·물·암석·다른 사람과 겹치면 전환하지 못합니다.
- 엎드리면 정지 사격만 가능하고 이동과 칼 공격은 불가능합니다. 사격 명중률이 높아지고 원거리 피격 확률이 낮아집니다.
- A* 길 찾기, 물·암석·벽 회피, 다른 주민의 머리/몸 칸 회피, 진행 중 경로가 막히면 정지합니다.
- 직접 구현한 다중 주파수 **펄린 노이즈** 높이 지도. 지표 하천이며 지하 공간은 없습니다.
- 가장자리 출구에서 시작하는 **priority-flood** 배수 트리. 분지에서도 순환 없이 출구까지 이어집니다.
- 내륙 고지대에 수원 배치. 수원 수와 초기 유량을 안내 창에서 설정합니다. 하나의 수원은 단일 주하천을 생성합니다.
- 여러 수원이 같은 배수 경로에 도달하면 합류하며 유량을 더합니다. 시드에 따라 서로 다른 출구로 흐를 수도 있습니다.
- 유량에 따라 강폭 증가. 강 중심의 `Flow`에 유량이 저장되고 주변 물 타일은 강폭을 표현합니다.
- 배수 경로를 따라 강바닥 높이를 낮춰 상류에서 하류로 높이가 증가하지 않도록 합니다.
- 주민 세 명, 자원 회수, 고철을 쓰는 즉시 벽/다리 건설, 철거 환급.
- 칼·활·소총·샷건의 사거리/발사 간격/명중률/피해량 차이, 탄약 소비, 벽/암석 시야 차단, 잔해 엄폐, 체력과 사망.
- 단일 JSON 저장 슬롯과 이전 파일 `.bak` 백업. 저장은 세계·구조물·주민·자원·처치 수를 포함합니다.

## 저장과 불러오기

**Save / Load** 버튼을 사용합니다. 저장 경로는 `Application.persistentDataPath/ashfall-save.json`입니다.
Windows 기본 경로는 `%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProductName>`입니다.
CompanyName과 ProductName은 Unity Player Settings에서 변경할 수 있습니다.
불러오면 일시정지하며 이동/공격/회수 명령은 초기화합니다. 전투 난수 진행 상태는 저장하지 않아 재개 후 명중 결과가 달라질 수 있습니다.
새 지도 생성은 현재 진행 상황을 교체하므로 필요한 경우 먼저 저장하세요.

## 코드 구조

| 파일 | 역할 |
| --- | --- |
| `Assets/Scripts/Core/World.cs` | 좌표·타일·월드, 최소 힙, 펄린 노이즈 |
| `Assets/Scripts/Core/WorldGenerator.cs` | 지형, 배수, 수원, 유량, 강폭 |
| `Assets/Scripts/Core/Simulation.cs` | 주민 점유, 이동, 자원, 건설, 사격, 적 AI |
| `Assets/Scripts/Runtime/AshfallGame.cs` | 실행 진입점, 절차적 그래픽, IMGUI 조작, 저장 |
| `Assets/Editor/PrototypeMenu.cs` | 씬 열기와 Windows 빌드 메뉴 |
| `Assets/Editor/AshfallCoreChecks.cs` | 엔진에 의존하지 않는 규칙 회귀 검사 |

Core 어셈블리는 `UnityEngine`에 의존하지 않습니다. 월드 데이터를 유지하면서 추후 Tilemap, SpriteRenderer, UI Toolkit 화면으로 교체할 수 있습니다.
현재는 작은 프로토타입 지도(96×72)에 맞춘 구현입니다. 대규모 주민/지도에서는 점유 인덱스, 경로 요청 분산, 변경된 텍스처 영역만 갱신하는 최적화가 필요합니다.

## 테스트

Unity에서 **Ashfall → Run Core Checks**를 실행합니다.
성공하면 Console에 `ASHFALL_CHECKS_PASSED`가 기록됩니다.
100개 시드의 재현성·수원·유량 보존·하류 높이·배수 순환·시작 위치·습격 접근성을 검사합니다.
또한 자세/회전 충돌, 다리를 이용한 이동, 동적 장애물, 대각선 사선, 피해/탄약, 회수/건설 비용, 사망자와 생존자의 저장 복원을 검사합니다.

Windows .NET Framework C# 컴파일러가 있는 환경에서는 `./Tests/Run-CoreChecks.ps1`도 사용할 수 있습니다.
애플리케이션 제어 정책이 자체 생성 EXE를 막으면 Unity 메뉴를 사용하세요. 보안 정책 변경은 필요하지 않습니다.

배치 실행 예시:

```powershell
& '<Unity 설치 경로>\Editor\Unity.exe' -batchmode -nographics -quit `
  -projectPath '<프로젝트 폴더>' -executeMethod Ashfall.Editor.Validation.Run `
  -logFile '<로그 경로>'
```

## Windows 빌드

Unity Hub에서 해당 에디터의 Windows Build Support를 설치한 뒤
**Ashfall → Build Windows Prototype**를 선택하고 출력 폴더를 지정합니다.
`Ashfall.exe`와 옆에 생성되는 데이터/런타임 파일을 함께 배포해야 합니다.

## 범위와 다음 개발

이것은 완성된 림월드 규모의 게임이 아니라 핵심 규칙을 검증할 첫 프로토타입입니다.
건설은 주민 작업 없이 즉시 적용하고, 모든 원거리 무기는 공용 탄약 수치를 사용합니다.
고철로 탄약을 즉시 보급하는 버튼은 테스트 편의를 위한 임시 경제입니다.
샷건은 산탄 개별 시뮬레이션 대신 거리별 단일 피해를 적용합니다.
안내 창과 상단의 DAY 01은 고정 표기이며 시간/달력 시스템은 아직 없습니다.

이후 우선순위:

1. 작업 예약·자원 운반·건설 진행도와 창고.
2. 지붕·실내 판정·침대·식량과 욕구.
3. 오염수/방사능·정수·농사와 지역 생존 경제.
4. 무기별 탄약·장비·상처·자동 방어와 전투 조작 개선.
5. 한국어 현지화, 사운드, 전용 UI, 더 큰 지도 성능 개선.

## 오픈소스

`LICENSE`는 프로젝트 코드와 직접 생성한 그래픽에 대한 MIT 라이선스입니다.
외부 에셋 사용 내역과 Unity 기본 리소스 구분은 `ASSET_LICENSES.md`를 참고하세요.
Unity 엔진 자체는 별도 라이선스이며 저장소에 포함하지 않습니다.
공개 소스에는 로그, 로컬 캐시, 사용자 설정, 실행 파일과 인증 정보를 포함하지 않습니다.

기술 참고: [Unity 6000.6.9f1 릴리스](https://unity.com/releases/editor/whats-new/6000.6.9f1),
[Unity IMGUI 이벤트](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Event.html).
