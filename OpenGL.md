# OpenGL 개요

이 문서는 [OpenTK Learn - OpenGL](https://opentk.net/learn/chapter1/0-opengl.html)을 한국어로 정리하고, 이 저장소의 OpenTK 4.8.2 예제에 바로 적용할 수 있는 설명을 보충한 것이다.

## 1. OpenGL은 무엇인가

OpenGL(Open Graphics Library)은 2D·3D 그래픽을 GPU로 렌더링하기 위한 표준 **명세(specification)**다. 보통 `GL.Clear`, `GL.DrawArrays`처럼 호출하는 함수 묶음을 OpenGL API라고 부르지만, Khronos Group이 제공하는 것은 특정 DLL이나 단일 구현체가 아니라 함수의 결과와 동작 규칙을 정의한 명세다.

명세는 예를 들어 “이 함수를 호출하면 어떤 결과가 나와야 하는가”를 규정한다. GPU 제조사와 운영체제 공급자는 그 명세를 만족하는 드라이버 구현을 제공한다. 따라서 같은 OpenGL 명령을 NVIDIA, AMD, Intel GPU에서 실행해도 애플리케이션이 보는 결과는 같아야 하지만, 내부 구현·최적화·버그의 양상은 다를 수 있다.

```text
C# 애플리케이션
  -> OpenTK의 GL.* 바인딩
  -> 운영체제가 선택한 OpenGL 드라이버
  -> GPU
```

### 드라이버가 중요한 이유

OpenGL 오류가 코드가 아닌 특정 GPU에서만 재현되거나, 예상과 다르게 동작할 때는 드라이버 구현 문제일 수 있다. 최신 드라이버는 버그 수정과 새로운 확장 기능 지원을 포함할 수 있으므로, 그래픽 드라이버를 최신으로 유지하는 것이 유용하다.

실행 환경마다 지원 OpenGL 버전과 확장이 다르다. 즉, 개발 PC에서 동작한다고 해서 모든 사용자 PC에서도 동일하게 동작하는 것은 아니다.

## 2. OpenTK의 역할

OpenTK는 OpenGL 자체를 구현하지 않는다. C#에서 네이티브 OpenGL 함수를 타입 안전한 형태로 호출할 수 있게 하는 바인딩과, 창·컨텍스트·입력 기능을 제공한다.

이 저장소에서 사용하는 핵심 네임스페이스는 다음과 같다.

| 네임스페이스 | 역할 |
|---|---|
| `OpenTK.Graphics.OpenGL4` | `GL` 정적 클래스와 OpenGL 상수(enum) |
| `OpenTK.Windowing.Desktop` | `GameWindow`, OpenGL 컨텍스트가 있는 창 |
| `OpenTK.Windowing.Common` | 프레임 이벤트, 컨텍스트·VSync 설정 |
| `OpenTK.Mathematics` | `Vector3`, `Matrix4` 등의 수학 타입 |

```csharp
using OpenTK.Graphics.OpenGL4;

GL.ClearColor(0.1f, 0.2f, 0.3f, 1.0f);
GL.Clear(ClearBufferMask.ColorBufferBit);
```

여기서 `GL`은 OpenTK가 제공하는 C# 호출 창구이고, 실제 렌더링은 활성 OpenGL 컨텍스트에 연결된 드라이버와 GPU가 수행한다.

## 3. Core Profile과 Immediate Mode

### 과거 방식: Immediate Mode / Fixed-Function Pipeline

과거 OpenGL은 `glBegin`, `glEnd`, `glVertex`, `glColor`처럼 명령을 하나씩 즉시 전달하는 immediate mode를 주로 사용했다. 사용법은 간단하지만 CPU 호출이 많고 GPU 파이프라인을 세밀하게 제어하기 어려우며, 고성능 렌더링에 적합하지 않다.

고정 기능 파이프라인(fixed-function pipeline)은 조명, 변환, 텍스처 처리의 많은 부분을 OpenGL이 정한 방식으로 수행한다. 개발자는 편하지만 원하는 렌더링 알고리즘을 구현하는 자유도가 낮다.

### 현대 방식: Core Profile

OpenGL 3.2부터 오래된 기능은 deprecated 되었고, core profile에서는 사용할 수 없는 기능이 많다. Core Profile은 개발자가 버퍼에 데이터를 올리고, 셰이더를 작성하며, GPU 파이프라인의 핵심 부분을 직접 구성하게 한다.

| 항목 | Immediate Mode | Core Profile |
|---|---|---|
| 정점 전달 | 매 정점마다 함수 호출 | VBO에 배열을 한 번 업로드 |
| 변환·조명 | OpenGL 고정 기능 | GLSL 셰이더로 직접 구현 |
| 유연성 | 낮음 | 높음 |
| 성능 확장성 | 낮음 | 높음 |
| 이 저장소 | 사용하지 않음 | 사용 |

이 저장소의 `OpenTK.Graphics.OpenGL4`와 GLSL 셰이더 예제는 현대 Core Profile 방식을 전제로 한다. 따라서 오래된 튜토리얼의 `GL.Begin()`/`GL.End()` 코드는 그대로 적용하지 않는 것이 좋다.

### 왜 3.3 개념을 배우는가

OpenGL 3.3 Core는 현대 OpenGL의 기본 원리—버퍼, VAO, 셰이더, 텍스처, uniform, draw call—를 모두 담고 있고 지원 범위도 넓다. 이후 4.x는 이러한 기본 원리를 바꾸기보다 compute shader, direct state access, 더 효율적인 저장소·동기화 API 같은 기능을 추가한다.

따라서 3.3 Core 개념을 익힌 뒤, 필요할 때만 더 높은 버전이나 확장을 선택하는 전략이 호환성 측면에서 유리하다.

## 4. OpenGL 확장(Extension)

확장은 GPU 제조사나 Khronos가 표준 버전에 포함되기 전에 새로운 기능을 제공하는 방식이다. 이름 접두사는 보통 다음 의미를 가진다.

| 접두사 | 의미 |
|---|---|
| `ARB` | Khronos Architecture Review Board 확장. 널리 채택되기 쉬움 |
| `KHR` | Khronos 확장 |
| `EXT` | 다수 공급사가 합의한 확장 |
| `NV`, `AMD`, `INTEL` | 제조사 전용 확장 |

예를 들어 `ARB_direct_state_access`는 객체를 bind하지 않고 설정할 수 있게 해 상태 변경을 줄이는 기능을 제공한다. 하지만 특정 확장이 있다고 가정하면 지원하지 않는 GPU에서 실행이 실패할 수 있다.

안전한 확장 사용 순서:

1. 기본 OpenGL 버전만으로 구현 가능한지 먼저 확인한다.
2. 필요한 확장을 열거하거나 지원 여부를 확인한다.
3. 지원되면 빠른 경로를 사용한다.
4. 지원되지 않으면 표준 API 기반의 대체 경로를 사용한다.

OpenTK의 `OpenTK.Graphics.OpenGL4`에는 `Arb*`, `Khr*`, `Ext*`, `Nv*` 같은 확장 상수 타입이 포함되어 있다. 타입이 존재한다고 해서 현재 GPU가 해당 확장을 지원한다는 뜻은 아니다.

## 5. OpenGL은 상태 머신이다

OpenGL 컨텍스트는 “현재 어떤 객체와 설정을 사용할 것인가”를 보관하는 큰 상태 집합이다. 많은 호출은 값을 즉시 그리는 대신 **상태를 변경**하고, 이후 draw 호출은 그 상태를 사용한다.

```csharp
// 1. 현재 array buffer를 선택한다.
GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBuffer);

// 2. 선택된 buffer에 데이터를 업로드한다.
GL.BufferData(
    BufferTarget.ArrayBuffer,
    vertices.Length * sizeof(float),
    vertices,
    BufferUsageHint.StaticDraw);
```

`BufferData`에 핸들을 직접 전달하지 않는 이유는, 앞의 `BindBuffer`가 “현재 `ArrayBuffer` 대상”을 정했기 때문이다.

### 상태 변경과 상태 사용

| 종류 | 예 | 역할 |
|---|---|---|
| 상태 변경 | `BindBuffer`, `BindVertexArray`, `UseProgram`, `Enable` | 이후 명령이 참조할 대상·옵션 선택 |
| 데이터 설정 | `BufferData`, `TexImage2D`, `UniformMatrix4` | 선택된 객체 또는 program의 데이터 변경 |
| 상태 사용 | `DrawArrays`, `DrawElements`, `Clear` | 현재 상태를 읽어 실제 렌더링 수행 |

이 모델을 이해하면 “왜 셰이더를 먼저 `Use()`해야 하는가”, “왜 VAO를 bind해야 하는가”, “왜 텍스처 unit을 설정해야 하는가”가 자연스럽게 설명된다.

### 상태 머신이 만드는 흔한 오류

- 다른 VBO/VAO가 bind된 상태에서 데이터를 변경했다.
- 원하는 shader program을 활성화하지 않고 uniform을 설정했다.
- `TextureUnit.Texture1`에 바인딩했지만 sampler uniform에는 `0`을 넣었다.
- 깊이 테스트를 켰지만 `DepthBufferBit`을 clear하지 않았다.
- 창 크기 변경 뒤 `GL.Viewport`를 갱신하지 않았다.

상태 관련 버그는 호출 순서가 원인인 경우가 많다. 렌더링 코드를 `bind → 설정 → draw` 순서로 묶고, 각 draw에 필요한 상태를 명시적으로 설정하면 추적하기 쉬워진다.

## 6. OpenGL 객체와 핸들

OpenGL 객체는 C#의 `class` 인스턴스와 다르다. 대부분 GPU/드라이버 쪽에 존재하고, 애플리케이션은 이를 가리키는 정수 핸들(`int`)을 가진다.

```csharp
int vertexBuffer = GL.GenBuffer();
GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBuffer);

// ... 사용이 끝난 뒤
GL.DeleteBuffer(vertexBuffer);
```

객체를 다루는 전형적인 흐름은 다음과 같다.

```text
생성(Gen/Create)
  -> 핸들 보관
  -> bind 또는 use로 현재 대상 선택
  -> 데이터·속성 설정
  -> draw 등의 작업에서 사용
  -> 더 이상 필요 없을 때 Delete
```

| 객체 | 생성 | 주 용도 |
|---|---|---|
| Buffer (VBO/EBO) | `GenBuffer` | 정점·인덱스 등의 배열 데이터 |
| Vertex Array (VAO) | `GenVertexArray` | 정점 속성 레이아웃과 buffer 연결 상태 |
| Shader | `CreateShader` | vertex/fragment 등 GPU 프로그램 단계 |
| Program | `CreateProgram` | 링크된 shader 묶음 |
| Texture | `GenTexture` | 샘플 가능한 이미지 데이터 |
| Framebuffer | `GenFramebuffer` | 화면 대신 텍스처 등에 렌더링할 대상 |

### VAO와 VBO를 구분해야 하는 이유

- **VBO**: 실제 float/int 배열 데이터가 담긴 GPU buffer다.
- **VAO**: “이 buffer의 바이트를 정점 shader 입력으로 어떻게 해석할 것인가”를 저장한다.

예를 들어 `[x, y, z, u, v]` 구조의 정점 배열에서 VAO는 position이 앞의 3개 float이고 UV가 뒤의 2개 float이라는 레이아웃을 기억한다.

## 7. 컨텍스트(Context)

OpenGL 함수를 호출하려면 현재 스레드에 유효한 OpenGL 컨텍스트가 current 상태여야 한다. 컨텍스트는 상태 머신, 객체 공유 규칙, 드라이버 연결을 포함하는 실행 환경이다.

`GameWindow` 예제에서는 `Run()` 이후 `OnLoad`, `OnRenderFrame`에서 컨텍스트가 current다. WinForms `GLControl` 기반 `GLView`에서는 `MakeCurrent()` 이후에 OpenGL 명령을 호출해야 한다.

```csharp
// GLControl 기반 코드의 예
glView.MakeCurrent();
GL.Clear(ClearBufferMask.ColorBufferBit);
glView.SwapBuffers();
```

다른 스레드에서 GL 명령을 호출하거나, 컨텍스트가 생성되기 전인 생성자에서 shader/texture를 만들면 오류가 발생할 수 있다. 이 저장소의 리소스 생성은 `OnLoad`에, 렌더링은 `OnRenderFrame`에 두는 이유가 여기에 있다.

## 8. 최소 렌더링 흐름

현대 OpenGL의 한 프레임은 대체로 다음 순서를 따른다.

```text
초기화 한 번
  OpenGL context 생성
  -> shader 컴파일·링크
  -> VBO/EBO/VAO 생성 및 데이터 업로드
  -> texture 생성 및 업로드

프레임마다
  clear
  -> shader program 활성화
  -> texture/VAO 바인딩
  -> uniform(변환·조명 등) 갱신
  -> DrawArrays 또는 DrawElements
  -> SwapBuffers
```

```csharp
GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

shader.Use();
shader.SetMatrix4("model", model);
shader.SetMatrix4("view", camera.GetViewMatrix());
shader.SetMatrix4("projection", camera.GetProjectionMatrix());

GL.BindVertexArray(vao);
GL.DrawArrays(PrimitiveType.Triangles, 0, vertexCount);

SwapBuffers();
```

## 9. 이 저장소에서 이어서 볼 예제

| 목적 | 예제 |
|---|---|
| 창과 GameWindow 루프 | `Chapter1/1-CreatingAWindow` |
| VBO, VAO, shader, 첫 draw | `Chapter1/2-HelloTriangle` |
| EBO 인덱스 드로우 | `Chapter1/3-ElementBufferObjects` |
| shader attribute/uniform | `Chapter1/4-Shaders-*` |
| 텍스처 | `Chapter1/5-Textures`, `Common/Texture.cs` |
| 변환 행렬과 좌표계 | `Chapter1/7-Transformations`, `Chapter1/8-CoordinatesSystems` |
| 카메라 | `Chapter1/9-Camera`, `Common/Camera.cs` |
| 조명 | `Chapter2/*` |

## 10. 학습 시 기억할 핵심

1. OpenGL은 GPU 드라이버가 구현하는 표준 명세이며, OpenTK는 C# 바인딩이다.
2. Core Profile에서는 버퍼와 shader 중심의 현대 방식을 사용한다.
3. OpenGL은 상태 머신이므로 bind 순서와 활성 객체가 중요하다.
4. GPU 객체는 정수 핸들로 참조하며, C# GC가 GPU 리소스를 대신 관리하지 않는다.
5. OpenGL 호출에는 current context가 필요하다.
6. 지원 OpenGL 버전과 확장은 실행 PC마다 다를 수 있으므로, 요구 사양과 fallback을 설계해야 한다.
