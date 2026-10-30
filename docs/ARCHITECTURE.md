# 아키텍처

> 스텝 description의 "읽어야 할 파일"로 지정되는 문서입니다. 실제 코드와 어긋나면 코드가 아니라 이 문서를 고치세요.

## 디렉토리 구조
```
Xamlity/
├── unity/                                  # 개발·샘플용 Unity 프로젝트 (6000.5.4f1, URP)
│   ├── Assets/                             # 샘플 씬, 샘플 XAML, 샘플용 한글 TMP 폰트
│   ├── ProjectSettings/
│   └── Packages/
│       ├── manifest.json
│       └── com.loonacia.xamlity/           # UPM 패키지 (embedded). 배포 단위
│           ├── package.json
│           ├── Core/                       # Xamlity.Core.asmdef (noEngineReferences: true)
│           ├── Runtime/                    # Xamlity.Unity.asmdef — Unity 어댑터
│           ├── Editor/                     # Xamlity.Editor.asmdef — 임포터, 코드 생성, 설정 UI
│           ├── Themes/                     # 기본 다크 테마 XAML
│           └── Tests/
│               └── Editor/                 # Unity Test Framework (EditMode)
├── dotnet/
│   ├── Xamlity.Core/                       # netstandard2.1, LangVersion 9.
│   │                                       # 패키지의 Core/**/*.cs를 Compile Include로 링크해 컴파일
│   └── Xamlity.Core.Tests/                 # NUnit. dotnet test 대상
└── docs/
```

Core 내부는 다음 영역으로 나눈다. 폴더 이름은 네임스페이스(`Xamlity.<영역>`)와 같다.

| 영역 | 내용 |
|---|---|
| `Markup` | XAML 파서, IR 정의와 직렬화, 타입·네임스페이스 해석, 마크업 확장 파싱, IR → 요소 트리 빌더 |
| `Properties` | 경량 DependencyProperty: 등록, 메타데이터(AffectsMeasure/AffectsArrange/AffectsRender/Inherits), 값 우선순위, 변경 알림, 첨부 프로퍼티 |
| `Tree` | 요소 기반 클래스, 논리 트리와 비주얼 트리, `FindName`/NameScope |
| `Layout` | Measure/Arrange, 무효화, Panel(StackPanel, Grid, Canvas) |
| `Input` | RoutedEvent 등록·라우팅, 추상 입력 이벤트, 히트 테스트, 포인터 캡처 |
| `Data` | Binding, DataContext 상속, 경로 해석(리플렉션 캐시), IValueConverter, ICommand 연결 |
| `Diagnostics` | 로그 인터페이스 `IXamlityLogger`와 전역 주입 지점 `XamlityLog.Logger` |
| `Styling` | Style/Setter, ResourceDictionary, 리소스 조회, ControlTemplate/DataTemplate, TemplateBinding, Trigger |
| `Controls` | Border, TextBlock, Image, ContentPresenter, UserControl, ContentControl, Button, ItemsControl, ScrollViewer |
| `Rendering` | `IDrawingContext`(사각형, 둥근 사각형, 이미지/9-slice, 글리프 런, 클립 push/pop, 불투명도 push/pop), `ITextMeasurer`, 이미지·폰트 핸들 추상 |

`Markup`의 IR 직렬화 형식은 UTF-8 `BinaryWriter`/`BinaryReader` 기반이다. 헤더는 ASCII `XAIR` 4바이트와 little-endian int32 버전 `3`이다. 이어서 루트 객체를 재귀적으로 저장한다.

- 객체: `TypeName`, `Name`, `Key`, `Line`, `Column`, 프로퍼티 개수, 문서 순서의 프로퍼티들.
- 프로퍼티: `PropertyName`, `OwnerTypeName`, `Line`, `Column`, `TextValue`, 자식 객체 개수, 문서 순서의 자식 객체들. `OwnerTypeName`은 첨부 프로퍼티의 소유 타입을 `"<CLR 전체 이름>, <어셈블리 이름>"`으로 저장하고 일반 프로퍼티는 null이다.
- 줄·열·개수는 int32이고, 문자열은 존재 여부 bool 1바이트 뒤에 값이 있으면 `BinaryWriter.Write(string)`의 7비트 가변 길이 바이트 수와 UTF-8 데이터를 쓴다(null과 빈 문자열 구분).

`{Binding ...}` 속성은 `TextValue`가 null인 프로퍼티 노드와 그 `Objects`의 `Xamlity.Data.Binding` 객체 하나로 표현한다. 위치 인자는 `Path` 프로퍼티로 저장하며, 확장에서 생성한 모든 노드의 줄·열은 원래 속성 위치다. `<TextBlock.Text><Binding ... /></TextBlock.Text>`도 같은 구조이며 각 노드의 실제 XML 위치를 보존한다. 빌더는 Binding 객체 값을 `BindingOperations.SetBinding`으로 연결한다. 대상은 FrameworkElement의 DP(첨부 DP 포함)여야 하며 암시적 content 자식과 컬렉션 항목에는 Binding을 넣을 수 없다.

`{StaticResource Key}`와 `{StaticResource ResourceKey=Key}`도 `TextValue`가 null인 프로퍼티 노드와 확장 객체 하나로 표현한다. 객체 타입은 `Xamlity.Markup.StaticResourceExtension`이고, `ResourceKey` 문자열 프로퍼티 하나를 가지며 모든 노드의 줄·열은 속성 위치다. 키의 존재 여부는 파서가 검사하지 않는다. 빌더는 생성 중인 객체를 안쪽부터 바깥쪽으로 순회하여 FrameworkElement.Resources 또는 ResourceDictionary의 이미 추가된 항목과 병합 사전을 조회한다. 항목은 생성 완료 후 추가하므로 자기 자신과 뒤쪽 리소스는 보이지 않는다. 중첩 확장과 앱·테마 대체 조회는 아직 지원하지 않는다.

객체의 `Key`는 사전 항목의 `x:Key` 문자열이며 null·빈 문자열을 구분한다. 키 없는 Style의 키는 TargetType이다. TargetType이나 Setter.Property가 StaticResource이면 관련 키·Setter 값의 타입 검증은 빌드 시점에 수행한다. ResourceDictionary 객체의 자식 항목은 내부용 `Entries` 프로퍼티 노드로 문서 순서대로 저장한다(공개 CLR 프로퍼티가 아님). MergedDictionaries 프로퍼티 사이에서도 순서를 보존한다. Resources 프로퍼티는 키 없는 ResourceDictionary 객체 하나면 사전 대입이며, 그 밖에는 자식들을 기존 사전에 추가한다. 암시적 컬렉션 자식 사이에 Resources 등 프로퍼티 요소가 있으면 컬렉션 프로퍼티 노드를 나누어 빌드 순서를 보존한다.

`{}`로 시작하는 속성 값은 마크업 확장이 아닌 리터럴이다. IR의 `TextValue`에는 접두사를 포함한 원문을 보존하고, 파서 검증과 런타임 빌더가 공통으로 쓰는 `XamlValueConverter`에서 접두사만 제거한다. 확장 인자의 인용과 역슬래시 이스케이프는 파싱 중 해제하며, 해제된 값이 `{`로 시작하면 리터럴로 변환되도록 `{}` 접두사를 붙여 저장한다. 이 표현은 IR v3에서도 같은 문자열 인코딩을 사용한다.

`System.Type` 프로퍼티의 속성 값은 XAML 타입 이름 또는 `{x:Type ...}`으로 받는다. 접두사 매핑은 IR에 남지 않으므로 파서가 요소 이름과 같은 규칙(생성자 불필요)으로 해석하여 `TextValue`에 `"<CLR 전체 이름>, <어셈블리 이름>"`으로 정규화한다. 빌더는 `Type.GetType`으로 복원한다. Style의 content property는 Setters다. Setter.Property는 입력 이름을 그대로 저장하고, 파서와 빌더가 공통 해석 함수로 둘러싼 Style.TargetType 또는 기본 xmlns의 명시적 Owner에서 DP를 찾는다. Setter.Value의 텍스트 변환과 객체 할당 검증에는 해당 DP의 타입을 사용한다.

Type 값 외의 값은 파서가 보존한 원문 문자열이며 런타임 빌더가 변환한다. 필드 추가·삭제·순서·인코딩 또는 값 해석의 호환성이 바뀌면 포맷 버전을 올리고 IR을 재임포트한다. 다른 매직·버전(v1·v2 포함)이나 잘린 데이터는 `InvalidDataException`으로 거부한다. XAML 임포터 버전은 5이며, 기존 v2 IR 에셋을 다시 임포트하여 런타임이 v3 형식으로 읽을 수 있게 한다.

## 레이어 규칙
- 의존 방향은 `Xamlity.Editor → Xamlity.Unity → Xamlity.Core` 한 방향이다. Core는 다른 어셈블리를 모른다. 이유: Core를 Unity 없이 `dotnet test`로 검증하고, 다른 엔진으로 옮길 가능성을 열어 둔다.
- Core는 `UnityEngine`, `UnityEditor`, `TMPro`를 참조하지 않는다. 엔진 기능(글자 측정, 텍스처, 폰트, 로그, 시간)은 Core가 정의한 인터페이스를 Unity 어댑터가 구현해 주입한다.
- Core 코드는 netstandard2.1 API와 C# 9 문법만 쓴다. `dotnet/Xamlity.Core`가 같은 소스를 이 조건으로 컴파일해 위반을 잡는다.
- `Xamlity.Unity`는 `UnityEngine.UI`(uGUI 컴포넌트)와 `UnityEngine.UIElements`(UI Toolkit)를 쓰지 않는다. `TMPro.TMP_FontAsset`은 글리프 데이터 소스로만 쓰고 `TMP_Text` 같은 컴포넌트는 쓰지 않는다.
- 렌더링은 URP `ScriptableRendererFeature` 하나로만 화면에 나간다. Core의 그리기 명령을 메시로 바꾸는 일은 `Xamlity.Unity` 안에서만 한다.
- 루트당 Mesh 하나를 재사용하며, 그리기 순서를 유지하고 연속한 같은 머티리얼·텍스처 명령을 submesh 배치로 묶는다. 단색은 `Xamlity/Solid`, 텍스트는 `Xamlity/Text` SDF 셰이더, 이미지는 `Xamlity/Image` 셰이더를 사용한다. 피처가 공유 머티리얼을 소유하고 배치별 텍스처는 `MaterialPropertyBlock`으로 전달한다. 글리프 UV는 레이아웃 뒤의 실제 아틀라스 크기로 계산한다.
- 컨트롤은 lookless다. 컨트롤 클래스는 상태와 동작만 갖고, 모양은 `Themes/`의 ControlTemplate으로 정의한다. 직접 그리는 것은 프리미티브(Border, TextBlock, Image)뿐이다.
- 에디터 전용 기능(임포트, 검증, 코드 생성, 재임포트 의존성, 설정 UI)은 `Xamlity.Editor`에만 둔다.

## 데이터 흐름
```
[에디터: 임포트]
.xaml 저장
  → XamlImporter (ScriptedImporter, Editor)
      Core 파서 → IR
      타입·프로퍼티 해석, x:DataType 바인딩 경로 검증
      에셋 경로 → UnityEngine.Object 참조 (IR에는 원문 문자열 유지, XamlAsset에 원문 값 → Object 참조 테이블)
      이미지 경로마다 존재하면 artifact 의존성, 없으면 소스 의존성 등록
      참조한 어셈블리마다 Xamlity/Assembly/<어셈블리 이름> 커스텀 의존성 연결 (파싱 오류 시에도 연결)
      도메인 리로드 뒤 지연 콜백에서 스크립트 어셈블리 출력 DLL 내용의 Hash128을 등록하고 Refresh 예약
      다른 XAML(MergedDictionaries 등)은 소스 의존성 등록
      오류 → ctx에 "파일(줄,열): 메시지"
  → XamlAsset (IR 바이트 + Object 참조 테이블)
  → OnPostprocessAllAssets: x:Class가 있으면 View.xaml.g.cs 생성 (내용이 바뀔 때만 씀)

[런타임: 생성]
XamlityRoot.Awake
  → XamlAsset → 참조 테이블의 UnityImageSource 해석기를 주입한 Core 빌더 → 요소 트리
     (x:Class면 그 타입을 생성하고 InitializeComponent 호출)
  → 암시적 Style·ControlTemplate 적용 (요소 → 부모 → App.xaml → 기본 테마)
  → 코드에서 DataContext 설정 → 바인딩 연결

[런타임: 매 프레임]
Input System → 추상 입력 이벤트 → 히트 테스트 → RoutedEvent 라우팅 (터널링 → 버블링)
  → 프로퍼티 변경 (바인딩, 트리거, 컨트롤 상태)
  → 무효화된 요소만 Measure/Arrange
  → 렌더 무효화가 있으면 IDrawingContext 명령 → 메시·배치 재생성
  → URP RendererFeature (AfterRendering, Game 카메라 + 스택 마지막 카메라)에서 그리기

[에디터: 핫리로드]
플레이 중 .xaml 저장 → 재임포트 → XamlAsset 변경 알림
  → 그 에셋으로 만든 뷰를 다시 만들고 DataContext 재연결 (UI 상태는 초기화)
```

## 횡단 관심사
- **XAML 이름 해석**: 기본 xmlns(`https://github.com/loonacia/xamlity`)는 Core의 `Xamlity.Controls`, `Xamlity.Layout`, `Xamlity.Rendering`, `Xamlity.Data`, `Xamlity.Styling`에서 객체 타입을 찾는다. 사용자 타입은 `clr-namespace:<CLR 네임스페이스>;assembly=<어셈블리 단순 이름>`으로 매핑하며 `assembly=`는 필수다. 프로퍼티 요소의 Owner는 해당 요소의 XML 네임스페이스에서 해석하고 대상 타입 또는 그 기반 타입이어야 한다. `XamlParseResult.ReferencedAssemblies`는 사용 여부와 관계없이 선언된 매핑의 어셈블리 이름을 최초 등장 순서로 중복 없이 보존하며 파싱 오류가 있어도 반환한다. IR의 타입 이름과 빌더 해석은 기존 어셈블리 한정 이름을 그대로 사용한다.
- **x:DataType 검증**: 요소와 자손의 Binding 경로를 선언 타입의 public 인스턴스 프로퍼티로 검증하며 자손의 선언이 범위를 덮어쓴다. DataContext 프로퍼티의 바인딩은 부모 범위의 타입으로 검증하고, 범위에 선언이 없으면 검증하지 않는다. x:DataType은 IR에 저장하지 않으며 런타임 리플렉션 바인딩은 그대로 유지한다.
- **에러 처리**: XAML 오류는 가능한 한 임포트 시점에 `파일(줄,열): 메시지`로 낸다. 런타임에서 `x:DataType`이 없는 바인딩의 경로를 찾지 못하면 예외 대신 경고 로그를 남긴다(WPF 방식).
- **로깅**: Core는 `XamlityLog.Logger`로 `IXamlityLogger`를 주입받으며, null이면 로그를 버린다. `UnityXamlityLogger.Install()`은 경고를 `Debug.LogWarning`으로 연결하며, Runtime의 `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`와 Editor의 `[InitializeOnLoadMethod]`에서 호출한다.
- **설정**: Project Settings > Xamlity(`XamlitySettings` 에셋)에서 앱 리소스(App.xaml)와 기본 폰트(TMP 폰트 에셋)를 지정한다. 루트마다 다른 값(XAML 에셋, sortingOrder, 기준 해상도)은 `XamlityRoot` 컴포넌트에 둔다.
- **좌표와 해상도**: XAML 단위는 논리 픽셀이다. `XamlityRoot`의 기준 해상도는 기본 1920×1080이며, 배율은 `s = min(화면 픽셀 너비 / 기준 너비, 화면 픽셀 높이 / 기준 높이)`이다. 기준 해상도의 성분이 0 이하면 배율은 1이다. Core 레이아웃과 글자 측정은 논리 단위를 유지하고, 논리 viewport는 `(화면 픽셀 너비 / s, 화면 픽셀 높이 / s)`이다. 어댑터는 클립된 사각형과 글리프 좌표에 배율을 적용한 뒤 back buffer용 y-flip을 처리한다.
- **성능**: 레이아웃과 렌더링은 무효화 플래그로 동작한다. 변화가 없는 프레임은 Measure/Arrange와 메시 재생성을 건너뛰고 GC 할당 0을 목표로 한다.
- **스레드**: 요소 트리, 바인딩, 렌더링은 메인 스레드 전용이다.
- **리플렉션과 IL2CPP**: 바인딩 경로와 타입 생성은 리플렉션(캐시)으로 한다. IL2CPP 기본 stripping 수준(Minimal)에서는 사용자 코드가 보존된다. UnityEngine 타입의 멤버에 리플렉션으로 바인딩하는 경우에만 link.xml이 필요하다.
- **테스트**:
  - Core: `dotnet test dotnet/Xamlity.Core.Tests` (NUnit). 가짜 `ITextMeasurer`와 기록용 `IDrawingContext`로 레이아웃·렌더 명령을 검증한다.
  - Unity 어댑터·임포터·코드 생성: `unity test unity --mode EditMode` (Unity CLI가 batchmode로 실행). 같은 프로젝트가 에디터에 열려 있으면 실행이 거부된다.

## 미정 사항
첫 관련 스텝에서 정하고 이 문서에 반영한다.
- 기본 다크 테마의 구체적인 색과 치수.
