# PRD: Xamlity

> Claude는 `/harness plan` 때 이 문서를 읽고, 각 스텝의 description에 관련 섹션을 "읽어야 할 파일"로 적습니다.

## 목표
Unity에서 WPF 문법과 컨트롤 이름을 그대로 쓰는 XAML로 게임 UI를 선언하고, 데이터 바인딩으로 게임 상태와 연결하며, 저장하면 바로 반영되는 핫리로드로 반복 개발할 수 있게 한다. 렌더링은 Unity UI 시스템(uGUI, UI Toolkit)을 쓰지 않는 자체 렌더러로 한다.

## 사용자
- 1차 사용자: 본인 게임 개발. Unity 6000.5 이상, URP 프로젝트.
- 대상: WPF/XAML 경험이 있고 MVVM으로 게임 UI를 만들고 싶은 Unity 개발자.
- 배포: UPM 패키지 `com.loonacia.xamlity`. git URL(`?path=/unity/Packages/com.loonacia.xamlity`)로 설치한다. 공개 여부는 나중에 정한다.

## 핵심 기능
각 항목의 "완료"는 괄호 안 기준으로 판정한다. Core 동작은 `dotnet test`, Unity 연동은 `unity test`(EditMode)로 검증한다.

1. **XAML 임포트**: `.xaml` 파일을 ScriptedImporter가 IR 에셋(`XamlAsset`)으로 변환한다. 문법 오류, 알 수 없는 타입·프로퍼티, 잘못된 에셋 경로, `x:DataType`이 있을 때 틀린 바인딩 경로는 `파일(줄,열): 메시지` 형식의 임포트 오류가 된다. (정상 XAML은 에셋이 생성되고, 잘못된 XAML은 줄·열이 포함된 오류가 나는 테스트)
2. **XAML 문법**: xmlns → CLR 네임스페이스 매핑, `x:Name`/`x:Class`/`x:Key`/`x:DataType`/`x:Type`, 프로퍼티 요소, 첨부 프로퍼티, content property, 타입 컨버터, 마크업 확장 `{Binding}`/`{StaticResource}`/`{TemplateBinding}`/`{x:Type}`. (각 문법 요소마다 파서 테스트)
3. **레이아웃**: Measure/Arrange 2단계 레이아웃. StackPanel, Grid(Auto/*/픽셀 행·열), Canvas, Margin/Padding/Width/Height/Min·Max, Horizontal·VerticalAlignment. (WPF 레이아웃 규칙대로 계산한 기대 좌표와 일치하는지 확인하는 Core 테스트)
4. **렌더링**: 단색 브러시, Border(Background, BorderBrush, BorderThickness, CornerRadius), Image(Texture2D/Sprite, 9-slice), TextBlock(Text, TextWrapping, TextAlignment, FontSize, Foreground, FontFamily), Opacity(자식 알파에 곱함), 사각형 클립. (기록용 `IDrawingContext`로 그리기 명령을 검증하는 Core 테스트, 샘플 씬에서 한글 텍스트가 보이는지 수동 확인)
5. **컨트롤과 기본 테마**: lookless 컨트롤 UserControl, ContentControl, ContentPresenter, Button, ItemsControl, ScrollViewer. 모양은 패키지에 내장한 다크 테마의 ControlTemplate으로 정의하고, 상태(IsMouseOver, IsPressed, IsEnabled)는 Property Trigger로 표현한다. (템플릿을 바꾸면 모양이 바뀌고 트리거 Setter가 적용·해제되는 테스트)
6. **스타일과 리소스**: 암시적 Style(TargetType), `x:Key` Style, BasedOn, ResourceDictionary, MergedDictionaries, `{StaticResource}`. 리소스는 요소 → 부모 → 앱(App.xaml) → 기본 테마 순으로 찾는다. (조회 순서와 값 우선순위 테스트)
7. **데이터 바인딩과 커맨드**: `{Binding Path}`, DataContext 상속, OneWay/TwoWay, `INotifyPropertyChanged`, `IValueConverter`, `ICommand`(`Command="{Binding ...}"`, CanExecute에 따른 비활성화). (값 전파·역전파·커맨드 실행 테스트)
8. **리스트**: ItemsControl + DataTemplate. `ObservableCollection`의 추가·삭제·이동이 화면에 반영된다. (컬렉션 변경 후 자식 요소 수·순서 테스트)
9. **스크롤**: ScrollViewer. 마우스 휠과 드래그로 스크롤하고 영역 밖은 클립한다. (스크롤 오프셋과 클립 영역 테스트)
10. **입력과 이벤트**: 라우티드 이벤트(터널링 Preview* → 버블링), `e.Handled`. 입력은 마우스와 첫 번째 터치. 게임 코드용 "포인터가 UI 위에 있는지" API. (라우팅 순서와 Handled 중단 테스트)
11. **코드비하인드**: `x:Class`가 있는 XAML은 옆에 `.xaml.g.cs`를 생성한다(`x:Name` 필드, `InitializeComponent`, `Click="OnClose"` 같은 핸들러 연결). (생성 코드가 컴파일되고 필드·핸들러가 연결되는 EditMode 테스트)
12. **화면 호스팅과 해상도**: 씬에 `XamlityRoot` 컴포넌트를 두고 XAML 에셋, sortingOrder, 기준 해상도를 지정한다. DataContext는 코드에서 넣는다. 기준 해상도에 대한 배율로 논리 픽셀을 스케일한다. 앱 리소스(App.xaml)와 기본 폰트는 Project Settings > Xamlity에서 지정한다. (샘플 씬에서 해상도를 바꿔도 배치 비율이 유지되는지 수동 확인)
13. **핫리로드**: 플레이 중 XAML을 저장하면 재임포트 후 그 에셋으로 만든 뷰를 다시 만들고 DataContext를 다시 연결한다. 스크롤 위치, 포커스 같은 UI 상태는 초기화된다. (재임포트 후 트리가 바뀌고 DataContext가 유지되는 EditMode 테스트)

## 범위 밖
- TextBox, 한글 IME 입력: 자체 렌더러에서 가장 큰 단일 작업이라 v1 이후로 미룬다.
- 포커스 이동(Tab, 방향키, 게임패드): 1차 게임에 당장 필요하지 않다.
- DynamicResource, 런타임 테마 교체: 리소스 변경 추적 장치가 필요해 미룬다.
- VisualStateManager, 애니메이션, Storyboard, DataTrigger, MultiTrigger: v1 상태 표현은 Property Trigger의 즉시 전환만 다룬다.
- 그라디언트 브러시, RenderTransform: 애니메이션이 없어 활용도가 낮다.
- 리스트 가상화: 긴 리스트 성능이 문제가 될 때 추가한다.
- DockPanel, WrapPanel, TextBlock Inlines(Run/Span), TextTrimming, 멀티터치: 필요해질 때 추가한다.
- 에디터 프리뷰 창, IDE 자동완성(XSD 스키마): 핵심 반복 흐름은 핫리로드로 해결한다.
- uGUI, UI Toolkit과 함께 쓰기(그리는 순서, 입력 우선순위 조율): Xamlity만 쓴다고 가정한다.
- URP 외 렌더 파이프라인(Built-in, HDRP): 어댑터를 추가하는 방식으로 나중에 확장한다.
- 복잡한 스크립트 셰이핑(아랍어, 태국어 등): 한글은 완성형이라 셰이핑 없이 표시된다.
- 기존 WPF XAML을 그대로 붙여 넣어 동작하는 완전 호환: WPF 이름의 부분집합만 같은 의미로 지원하고, 나머지는 임포트 오류로 알린다.
- 패키지에 폰트 포함: 폰트는 사용자가 프로젝트 설정에서 지정한다. 샘플 프로젝트에만 한글 폰트를 넣는다.

## 제약
- Unity 6000.5 이상만 지원한다. 개발과 검증은 6000.5.4f1에서 한다.
- 렌더 파이프라인은 URP 전용이다. 6000.4부터 Compatibility Mode가 제거되어 `RecordRenderGraph` 경로만 구현한다.
- 입력은 Input System 패키지를 쓴다.
- Core(`Xamlity.Core`)는 Unity를 참조하지 않고 netstandard2.1 API와 C# 9 문법 안에서 작성한다(Unity 6의 C# 버전).
- 글리프 데이터는 TextMeshPro 폰트 에셋(`TMP_FontAsset`, `com.unity.ugui` 패키지에 포함)에서 가져온다. 한글 표시는 필수다.
- IL2CPP의 기본 Managed Stripping Level(Minimal)을 가정한다. 이 수준에서는 사용자 코드가 제거되지 않아 ViewModel 리플렉션 바인딩에 link.xml이 필요 없다.
- 변화가 없는 프레임에서는 레이아웃과 메시 재생성을 하지 않고 GC 할당 0을 목표로 한다.
- 하네스 인수 조건은 저장소 루트에서 실행한다. Core는 `dotnet test dotnet/Xamlity.Core.Tests`, Unity 쪽은 `unity test unity --mode EditMode`로 검증한다. 하네스가 도는 동안 `unity/` 프로젝트를 에디터로 열어 두면 안 된다.
