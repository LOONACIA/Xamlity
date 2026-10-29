# Architecture Decision Records

> 되돌리기 어려운 기술 결정만 기록합니다. 스텝 description에서 "이 결정을 벗어나지 마라"의 근거로 쓰입니다.

## 원칙
- 동작하는 최소 구현을 우선한다. PRD의 "범위 밖" 항목은 스텝에서 미리 만들지 않는다.
- 동작은 WPF를 기준으로 삼는다. 지원하는 이름과 프로퍼티는 WPF와 같은 의미로 동작해야 하고, 지원하지 않는 것은 조용히 무시하지 않고 오류로 알린다.
- Core는 엔진에 의존하지 않는다. 엔진이 필요한 기능은 인터페이스로 분리해 주입한다.
- 오류는 가능한 한 이른 시점(임포트)에 줄·열 정보와 함께 알린다.
- 외부 의존성(패키지, 라이브러리)은 대안을 검토한 뒤에만 추가한다.

---

### ADR-001: 자체 렌더러를 쓰고 Unity UI 시스템은 쓰지 않는다
- **상태**: 채택
- **결정**: uGUI와 UI Toolkit을 쓰지 않는다. 레이아웃, 히트 테스트, 그리기 명령 생성을 직접 구현하고, 메시는 URP 렌더 패스로 직접 그린다.
- **이유**: WPF식 동작(Measure/Arrange, ControlTemplate, 값 우선순위)을 그대로 재현하고, Unity UI 시스템의 제약과 버전 변화에서 독립하기 위해서다.
- **트레이드오프**: 텍스트 렌더링, 클립, 배칭, 입력 라우팅, 렌더 파이프라인 통합을 모두 직접 만들어야 한다. 기존 Unity UI 생태계(프리팹 UI, EventSystem)를 쓸 수 없다.
- **하네스에 미치는 영향**: `UnityEngine.UI`, `UnityEngine.UIElements`, `UnityEngine.EventSystems`를 쓰는 코드를 추가하지 마라. 예외는 ADR-010의 `TMP_FontAsset` 하나다.

### ADR-002: Unity와 무관한 Core를 렌더링 직전까지 분리하고 소스를 공유한다
- **상태**: 채택
- **결정**: 파서/IR, 프로퍼티 시스템, 바인딩, 레이아웃, 라우티드 이벤트, 스타일·템플릿, 컨트롤 로직, 히트 테스트를 `Xamlity.Core`에 둔다. Core는 `IDrawingContext`에 그리기 명령을 내보내고 `ITextMeasurer`를 주입받는다. 소스는 `unity/Packages/com.loonacia.xamlity/Core/`에만 두고, `dotnet/Xamlity.Core`(netstandard2.1, LangVersion 9)가 같은 파일을 링크해 컴파일한다.
- **이유**: 가장 복잡한 로직을 Unity 없이 몇 초 만에 `dotnet test`로 검증할 수 있다. 하네스 인수 조건에 쓰기 좋고, Unity batchmode의 느린 시작과 에디터 점유 문제를 피한다.
- **트레이드오프**: 엔진 기능마다 추상 인터페이스가 필요하다. Core에서는 `UnityEngine.Vector2`, `Color`, `Debug.Log` 같은 편의 타입을 쓸 수 없다.
- **하네스에 미치는 영향**:
  - Core 파일에서 `UnityEngine`/`UnityEditor`/`TMPro`를 참조하지 마라.
  - netstandard2.1에 없는 API와 C# 9를 넘는 문법(file-scoped namespace, record struct, required, 원시 문자열 등)을 쓰지 마라. 이유: Unity 6의 C# 컴파일러가 C# 9까지 지원한다.
  - Core 소스를 `dotnet/` 아래로 복사하지 마라. 한 곳에만 둔다.
  - Core를 바꾸는 스텝의 인수 조건에는 `dotnet test dotnet/Xamlity.Core.Tests`를 넣는다.

### ADR-003: XAML은 임포트 시점에 IR로 컴파일한다
- **상태**: 채택
- **결정**: `.xaml`은 ScriptedImporter가 파싱·타입 해석·검증까지 마치고 IR 에셋(`XamlAsset`)으로 저장한다. 런타임은 XAML 텍스트를 파싱하지 않고 IR로 트리를 만든다. 사용자 타입을 참조하면 그 어셈블리에 커스텀 의존성을 걸어, 컴파일 후 관련 XAML만 재임포트되게 한다.
- **이유**: 오류를 실행 전에 줄 번호와 함께 보여 주고, 스크립트 재컴파일 없이 재임포트만으로 핫리로드할 수 있다.
- **트레이드오프**: 런타임에 XAML 텍스트를 받아 띄우는 일(모드, 서버 UI)은 기본 경로가 아니다. 임포터가 사용자 타입에 의존하므로 재임포트 의존성 관리가 필요하다.
- **하네스에 미치는 영향**:
  - 런타임(`Xamlity.Unity`)에서 XAML 텍스트를 파싱하는 경로를 추가하지 마라. Core 테스트에서 텍스트를 직접 파싱하는 것은 허용한다.
  - 임포터가 다른 에셋(다른 XAML 등)을 읽으면 `ctx.DependsOnSourceAsset`/`DependsOnArtifact`로 의존성을 명시해야 한다. 이유: Unity 6.4부터 artifact 의존성이 좁아져, 명시하지 않으면 연쇄 재임포트가 일어나지 않는다.

### ADR-004: XAML 문법은 표준을, 컨트롤 이름은 WPF의 부분집합을 따른다
- **상태**: 채택
- **결정**: 문법은 WPF/MS-XAML과 같다(xmlns 매핑, `x:` 지시어, 프로퍼티 요소, 첨부 프로퍼티, content property, 타입 컨버터, 마크업 확장). 컨트롤과 프로퍼티 이름은 WPF에서 가져온 부분집합이고 WPF와 같은 의미로 동작한다. 지원하지 않는 요소·프로퍼티는 임포트 오류다. 기본 xmlns는 `https://github.com/loonacia/xamlity`, `x:` 네임스페이스는 `http://schemas.microsoft.com/winfx/2006/xaml`이다.
- **이유**: XAML을 쓰는 이점(익숙한 문법, 기존 WPF 지식과 문서)을 그대로 얻으면서, 시맨틱 완전 호환에 드는 비용은 피한다.
- **트레이드오프**: 기존 WPF XAML을 붙여 넣으면 지원하지 않는 부분에서 오류가 난다.
- **하네스에 미치는 영향**: WPF에 없는 이름(Label, Panel, Sprite 등)으로 컨트롤이나 프로퍼티를 새로 만들지 마라. 같은 이름을 WPF와 다른 의미로 구현하지 마라. 지원하지 않는 속성을 조용히 무시하지 마라.

### ADR-005: 경량 DependencyProperty 시스템
- **상태**: 채택
- **결정**: 프로퍼티는 정적으로 등록된 프로퍼티 객체와 메타데이터(기본값, AffectsMeasure/AffectsArrange/AffectsRender, Inherits)로 정의한다. 값 우선순위는 높은 순으로 로컬 값 → 템플릿 트리거·스타일 트리거 → 템플릿 내부 값·스타일 Setter → 상속값 → 기본값이다. 첨부 프로퍼티와 변경 알림을 지원한다. coercion, validation callback, Freezable은 만들지 않는다.
- **이유**: 바인딩 대상, 첨부 프로퍼티(Grid.Row), 상속(DataContext, FontSize), 스타일·트리거 우선순위, 레이아웃·렌더 자동 무효화가 모두 이 시스템에 달려 있다.
- **트레이드오프**: 일반 C# 프로퍼티보다 정의가 장황하다. WPF의 전체 우선순위 단계(애니메이션, coercion 등)는 없다.
- **하네스에 미치는 영향**: 컨트롤의 시각·레이아웃 관련 프로퍼티를 일반 필드나 자동 프로퍼티로 만들지 마라. 반드시 등록된 프로퍼티로 정의한다.

### ADR-006: Lookless 컨트롤과 ControlTemplate, Property Trigger
- **상태**: 채택
- **결정**: 직접 그리는 것은 프리미티브(Border, TextBlock, Image)뿐이다. Button, ScrollViewer 같은 컨트롤은 상태와 동작만 갖고, 모양은 기본 테마 XAML의 ControlTemplate(TemplateBinding, ContentPresenter)으로 정의한다. 상태별 모양 변화는 `Style.Triggers`/`ControlTemplate.Triggers`의 Property Trigger + Setter로 즉시 전환한다. 논리 트리와 비주얼 트리를 구분한다.
- **이유**: WPF의 핵심 모델이다. 사용자가 템플릿만 바꿔 모양을 완전히 바꿀 수 있다. 나중에 직접 그리기 방식에서 바꾸려면 모든 컨트롤을 다시 짜야 한다.
- **트레이드오프**: Style, Template, Trigger, 리소스 조회가 v1 초반부터 필요해 첫 버튼이 보이기까지 오래 걸린다.
- **하네스에 미치는 영향**: 컨트롤 클래스에 `OnRender` 같은 직접 그리기 코드를 넣지 마라(프리미티브는 예외). VisualStateManager, DataTrigger, MultiTrigger, 애니메이션을 추가하지 마라. 이유: PRD 범위 밖이다.

### ADR-007: Measure/Arrange 레이아웃
- **상태**: 채택
- **결정**: WPF와 같은 2단계 레이아웃(Measure → DesiredSize, Arrange → 최종 사각형)을 쓴다. v1 패널은 StackPanel, Grid, Canvas다.
- **이유**: XAML 문법과 WPF 지식에 그대로 맞고, Grid의 Auto/* 크기 계산을 재현할 수 있다.
- **트레이드오프**: flexbox나 앵커(RectTransform)식 배치는 기본 제공하지 않는다. flex가 필요하면 나중에 Panel 하나로 추가한다.
- **하네스에 미치는 영향**: 다른 레이아웃 모델(flex, 앵커)을 기반 레이아웃으로 도입하지 마라.

### ADR-008: 라우티드 이벤트
- **상태**: 채택
- **결정**: 입력 이벤트는 RoutedEvent로 등록하고, 터널링(Preview*) → 버블링 순으로 라우팅한다. `e.Handled`가 true면 이후 처리를 멈춘다. 입력은 Input System에서 받아 Core의 추상 입력 이벤트로 바꾼다. v1은 마우스와 첫 번째 터치만 다룬다.
- **이유**: 템플릿 안의 Border를 클릭해도 Button이 이벤트를 받아야 한다. lookless 컨트롤에 사실상 필요하다.
- **트레이드오프**: 일반 C# 이벤트보다 구현과 디버깅이 복잡하다.
- **하네스에 미치는 영향**: 입력을 히트된 요소에만 직접 전달하는 경로를 만들지 마라. Core에서 Input System 타입을 참조하지 마라.

### ADR-009: URP RendererFeature로 AfterRendering에 직접 그린다
- **상태**: 채택
- **결정**: `ScriptableRendererFeature`가 `RecordRenderGraph`로 패스를 기록하고, `RenderPassEvent.AfterRendering`(final blit 이후, back buffer)에서 UI 메시를 그린다. 그리는 카메라는 `CameraType.Game`이면서 `cameraData.resolveFinalTarget`이 true인 카메라(스택의 마지막)로 한정한다. y-flip은 직접 처리한다. 지원 버전은 Unity 6000.5 이상, URP 전용이다.
- **이유**: render scale, 업스케일러(STP), FXAA, 컬러 그레이딩의 영향을 받지 않아 텍스트가 네이티브 해상도로 선명하게 나온다. Unity 6000.4부터 URP Compatibility Mode가 제거되어 RenderGraph 경로만 있으면 된다.
- **트레이드오프**: Built-in 파이프라인과 HDRP는 지원하지 않는다. uGUI Screen Space-Overlay Canvas와의 그리기 순서는 확인하지 않았다(PRD 범위 밖).
- **하네스에 미치는 영향**: Compatibility Mode용 `Execute` 경로나 `URP_COMPATIBILITY_MODE` 분기를 만들지 마라. Built-in·HDRP 지원 코드를 추가하지 마라.
- **근거**: https://docs.unity.com/en-us/engine/6000.5/manual/upgrade-guides/upgrade-guide-unity64.md , https://docs.unity3d.com/6000.4/Documentation/Manual/UpgradeGuideUnity62.html

### ADR-010: 글리프 데이터는 TMP_FontAsset에서 가져온다
- **상태**: 채택
- **결정**: 폰트는 TextMeshPro 폰트 에셋을 쓴다. `characterLookupTable`/`glyphLookupTable`, `TryAddCharacters`(동적 아틀라스), `atlasTextures`, `faceInfo`에서 메트릭과 아틀라스만 가져온다. 줄바꿈·정렬 같은 텍스트 레이아웃은 Core가 한다. `TMP_Text` 컴포넌트는 쓰지 않는다.
- **이유**: SDF, 동적 아틀라스, 폴백 체인, 한글 동적 생성을 이미 검증된 에셋 파이프라인으로 얻는다.
- **트레이드오프**: `com.unity.ugui` 패키지에 의존하게 된다(Unity 6에서 TMP는 이 패키지에 포함). TMP_Text 없이 단독으로 쓰는 것을 지원한다는 공식 문구는 없다.
- **하네스에 미치는 영향**: 텍스트를 다루는 첫 Unity 스텝에서 `TMP_FontAsset`을 단독으로 썼을 때 한글 동적 글리프 추가와 아틀라스 조회가 되는지 EditMode 테스트로 먼저 확인한다. 자체 폰트 래스터라이저나 BMFont 경로를 만들지 마라.
- **근거**: https://docs.unity3d.com/Packages/com.unity.ugui@2.5/api/TMPro.TMP_FontAsset.html

### ADR-011: 코드비하인드는 AssetPostprocessor가 `.xaml.g.cs`로 생성한다
- **상태**: 채택
- **결정**: `x:Class`가 있는 XAML이면 `AssetPostprocessor.OnPostprocessAllAssets`에서 XAML 옆에 `<이름>.xaml.g.cs`를 생성한다. 생성 파일에는 partial 클래스, `x:Name` 필드, `InitializeComponent`, 이벤트 핸들러 연결이 들어간다. 생성한 내용이 기존 파일과 다를 때만 쓴다. 생성 파일은 커밋한다.
- **이유**: Unity는 `Filename.[AnalyzerName].additionalfile` 형식의 파일만 소스 제너레이터에 넘기므로, `.xaml`을 그대로 읽는 소스 제너레이터를 만들 수 없다. 우회 방법(`csc.rsp`, generator 안의 파일 IO)은 Unity와 Roslyn 모두 권장하지 않는다. `OnImportAsset` 안에서 파일을 쓰면 import가 재시작되거나 막힌다. 그래서 Input System과 같은 Postprocessor 방식을 택했다.
- **트레이드오프**: 사용자 프로젝트에 생성 파일이 생긴다. 이름이나 핸들러가 바뀌면 스크립트 재컴파일이 일어난다(모양만 바뀌면 일어나지 않음).
- **하네스에 미치는 영향**:
  - Roslyn 소스 제너레이터를 추가하지 마라.
  - ScriptedImporter의 `OnImportAsset` 안에서 파일을 쓰거나 `AssetDatabase.CreateAsset`/`ImportAsset`을 호출하지 마라.
  - 생성 코드는 몇 번 다시 실행돼도 같은 결과를 내야 하고, 내용이 같으면 파일을 건드리지 않아야 한다. 이유: 그렇지 않으면 재임포트 무한 루프가 생긴다.
- **근거**: https://docs.unity.com/en-us/engine/6000.5/manual/scripting/debugging-and-diagnostics/roslyn-analyzers/additional-files , https://docs.unity.com/en-us/engine/6000.5/script-reference/unityeditor/assetpostprocessor/onpostprocessallassets

### ADR-012: 바인딩은 리플렉션 기반이고, x:DataType이 있으면 임포트 시 검증한다
- **상태**: 채택
- **결정**: 런타임 바인딩은 리플렉션(PropertyInfo 캐시)으로 경로를 해석하고 `INotifyPropertyChanged`를 구독한다. XAML에 `x:DataType`이 있으면 임포터가 경로를 검증해 오타를 임포트 오류로 만든다. 없으면 런타임에 경고 로그만 남긴다.
- **이유**: WPF처럼 동적 바인딩을 유지하면서, 원하는 곳에서는 조기 검증을 받을 수 있다. IL2CPP 기본 stripping 수준(Minimal)은 사용자 코드를 제거하지 않아 link.xml 없이 동작한다.
- **트레이드오프**: 리플렉션 비용이 있다(캐시로 줄임). UnityEngine 타입 멤버에 리플렉션으로 바인딩하면 link.xml이 필요할 수 있다.
- **하네스에 미치는 영향**: `x:DataType`을 필수로 만들지 마라. `Reflection.Emit`이나 런타임 코드 생성을 쓰지 마라. 이유: IL2CPP에서 지원되지 않는다.
- **근거**: https://docs.unity.com/en-us/engine/6000.5/manual/scripting/compilation-and-code-reload/script-compilation/managed-code-stripping/configure.md

### ADR-013: 검증은 dotnet test(NUnit)와 unity test(헤드리스)로 한다
- **상태**: 채택
- **결정**: Core는 `dotnet test dotnet/Xamlity.Core.Tests`(NUnit)로 검증한다. Unity 어댑터, 임포터, 코드 생성은 Unity Test Framework EditMode 테스트로 작성하고 `unity test unity --mode EditMode --timeout <초> --output "<경로>"`로 실행한다(Unity CLI가 batchmode 에디터를 띄움).
- **이유**: 두 명령 모두 에디터 상태에 영향을 받지 않고 종료 코드로 결과를 알려 주므로, 하네스가 무인으로 돌 수 있다. Unity Test Framework와 같은 NUnit을 써서 테스트 작성 습관을 통일한다.
- **트레이드오프**: `unity test`는 실행마다 에디터를 새로 띄워 몇 분이 걸린다. 하네스가 도는 동안 `unity/` 프로젝트를 에디터로 열어 둘 수 없다(열려 있으면 종료 코드 6). Unity CLI는 베타(1.0.0-beta.14)다.
- **하네스에 미치는 영향**:
  - Unity 쪽 스텝의 인수 조건에는 `unity test`를 쓰고, `unity recompile`이나 열린 에디터에 의존하는 명령을 쓰지 마라.
  - `--output`은 `unity/Temp/` 아래처럼 git이 무시하는 경로로 지정한다.
  - 첫 Unity 스텝에서 `unity test`가 Codex 샌드박스 안에서도 실행되는지 확인하고, 안 되면 `blockers`에 적는다.
