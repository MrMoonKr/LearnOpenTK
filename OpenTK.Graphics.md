# OpenTK.Graphics / OpenGL 정리

이 문서는 이 저장소가 참조하는 **OpenTK 4.8.2** 기준이다. `OpenTK.Graphics.OpenGL4`는 C OpenGL API를 C#에서 호출할 수 있게 만든 바인딩이다. OpenGL 객체는 C# 객체가 아니라 대개 `int` 핸들로 관리한다.

## 네임스페이스

| 네임스페이스 | 목적 |
|---|---|
| `OpenTK.Graphics.OpenGL4` | 데스크톱 OpenGL 4.x. 이 저장소의 렌더링 API |
| `OpenTK.Graphics.ES11` / `ES20` / `ES30` / `ES31` / `ES32` | OpenGL ES 버전별 바인딩. 모바일/임베디드 대상 |
| `OpenTK.Graphics` | 그래픽 공통 타입을 담는 상위 어셈블리 |

이 프로젝트에서는 다음 using이 핵심이다.

```csharp
using OpenTK.Graphics.OpenGL4;
```

## `GL`: 모든 OpenGL 함수의 정적 진입점

`GL`은 OpenGL 명령의 정적 클래스다. 함수 호출은 **현재 활성화된 OpenGL 컨텍스트**에 적용되는 상태 머신 방식이다. 즉, `Bind*`로 대상을 선택한 뒤 그 대상에 데이터를 넣거나 설정한다.

| 작업 | 대표 `GL` 함수 | 관련 enum |
|---|---|---|
| 화면/상태 | `ClearColor`, `Clear`, `Enable`, `Disable`, `Viewport` | `ClearBufferMask`, `EnableCap` |
| 버퍼 | `GenBuffer`, `BindBuffer`, `BufferData`, `DeleteBuffer` | `BufferTarget`, `BufferUsageHint` |
| VAO/정점 속성 | `GenVertexArray`, `BindVertexArray`, `VertexAttribPointer`, `EnableVertexAttribArray` | `VertexAttribPointerType` |
| 셰이더 | `CreateShader`, `ShaderSource`, `CompileShader`, `GetShader`, `GetShaderInfoLog` | `ShaderType`, `ShaderParameter` |
| 프로그램 | `CreateProgram`, `AttachShader`, `LinkProgram`, `UseProgram`, `GetProgram` | `GetProgramParameterName` |
| uniform | `GetUniformLocation`, `Uniform1`, `Uniform3`, `UniformMatrix4` | `ActiveUniformType`, `UniformPName` |
| 텍스처 | `GenTexture`, `ActiveTexture`, `BindTexture`, `TexImage2D`, `TexParameter`, `GenerateMipmap` | `TextureTarget`, `TextureUnit`, `PixelFormat`, `PixelType` |
| 드로우 | `DrawArrays`, `DrawElements` | `PrimitiveType`, `DrawElementsType` |
| 프레임버퍼 | `GenFramebuffer`, `BindFramebuffer`, `FramebufferTexture2D`, `CheckFramebufferStatus` | `FramebufferTarget`, `FramebufferAttachment` |
| 삭제 | `DeleteBuffer`, `DeleteVertexArray`, `DeleteTexture`, `DeleteProgram` | 해당 핸들 |

## GPU 객체와 수명

| 객체 | 생성/바인딩 | 역할 |
|---|---|---|
| VBO | `GenBuffer` / `BindBuffer(ArrayBuffer)` | 정점 위치, 색, UV 등 원시 데이터 |
| EBO | `GenBuffer` / `BindBuffer(ElementArrayBuffer)` | 인덱스로 정점을 재사용 |
| VAO | `GenVertexArray` / `BindVertexArray` | VBO와 정점 속성 레이아웃의 연결 상태 |
| Shader | `CreateShader` | vertex/fragment 등 GPU 프로그램 단계 |
| Program | `CreateProgram` / `UseProgram` | 링크된 셰이더 프로그램 |
| Texture | `GenTexture` / `BindTexture(Texture2D)` | 이미지 샘플 데이터 |
| FBO | `GenFramebuffer` | 화면 대신 텍스처/렌더버퍼에 렌더링 |

핸들은 `int`이고, C# GC가 GPU 메모리를 자동 해제하지 않는다. 더 이상 사용하지 않는 대형 텍스처·버퍼는 명시적으로 `GL.Delete*`해야 한다. 종료 시 드라이버가 정리하는 경우도 있지만, 실행 중 장면을 교체한다면 삭제가 필요하다.

## 셰이더와 Program

OpenGL 4의 기본 렌더링에는 보통 vertex shader와 fragment shader가 필요하다.

```text
GLSL 파일 읽기
 -> CreateShader + ShaderSource + CompileShader
 -> CreateProgram + AttachShader + LinkProgram
 -> UseProgram
 -> uniform 설정 + draw
```

이 저장소의 `LearnOpenTK.Common.Shader`는 이 과정을 감싼 헬퍼다. 생성자에서 컴파일·링크하고 uniform 위치를 캐시하며, `Use`, `SetInt`, `SetFloat`, `SetVector3`, `SetMatrix4`를 제공한다. 위치 캐시는 매 프레임 `GetUniformLocation`을 반복하지 않게 한다.

주의할 점:

- `Uniform*` 호출 전에는 대상 Program이 활성화되어야 한다. 이 저장소의 `Shader.Set*`은 내부에서 `UseProgram`을 호출한다.
- 컴파일 실패는 `GetShaderInfoLog`, 링크 실패는 `GetProgramInfoLog`로 확인한다.
- `UniformMatrix4`의 transpose 인자는 행렬 라이브러리와 GLSL의 메모리 배치에 맞춰 결정한다. 이 저장소의 `Shader.SetMatrix4`는 `true`를 전달한다.

## 정점 데이터: VBO + VAO

```csharp
int vbo = GL.GenBuffer();
GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
GL.BufferData(BufferTarget.ArrayBuffer,
    vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

int vao = GL.GenVertexArray();
GL.BindVertexArray(vao);
GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float,
    false, 5 * sizeof(float), 0);
GL.EnableVertexAttribArray(0);
```

`VertexAttribPointer`의 `stride`는 한 정점의 전체 바이트 크기, 마지막 값은 해당 속성의 시작 offset이다. 예를 들어 `[x,y,z,u,v]`라면 위치는 `stride=5*sizeof(float), offset=0`, UV는 `stride=5*sizeof(float), offset=3*sizeof(float)`다.

`BufferUsageHint.StaticDraw`는 거의 바뀌지 않는 데이터, `DynamicDraw`는 자주 갱신되는 데이터, `StreamDraw`는 매 프레임 바뀌는 데이터에 사용한다.

## 텍스처

텍스처 흐름은 `GenTexture → ActiveTexture → BindTexture → TexImage2D → TexParameter → GenerateMipmap`이다.

| 타입 | 의미 |
|---|---|
| `TextureTarget.Texture2D` | 일반 2D 이미지 |
| `TextureUnit.Texture0`, `Texture1` | 셰이더 sampler에 연결할 텍스처 슬롯 |
| `TextureMinFilter` / `TextureMagFilter` | 축소/확대 샘플링 방식 |
| `TextureWrapMode` | UV 범위를 벗어났을 때 반복/경계 처리 |
| `PixelInternalFormat` | GPU 내부 저장 형식 |
| `PixelFormat`, `PixelType` | 전달하는 원본 픽셀 데이터의 형식 |

이 저장소의 `LearnOpenTK.Common.Texture`는 `StbImageSharp`로 RGBA 픽셀을 읽고 `Texture2D`에 올린다. OpenGL 텍스처 원점과 일반 이미지 원점이 반대라서 로딩 시 세로 반전을 설정한다.

## 렌더링 상태와 드로우

```csharp
GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
GL.Enable(EnableCap.DepthTest);
GL.BindVertexArray(vao);
shader.Use();
GL.DrawArrays(PrimitiveType.Triangles, 0, vertexCount);
```

- `PrimitiveType.Triangles`는 세 정점마다 삼각형을 만든다.
- EBO를 사용하는 경우 `DrawElements`와 `DrawElementsType.UnsignedInt` 등을 쓴다.
- 3D 장면은 `EnableCap.DepthTest`와 `DepthBufferBit` clear가 필요하다.
- 이중 버퍼 창에서는 프레임 마지막에 `GameWindow.SwapBuffers()`를 호출한다.

## 자주 쓰는 enum 전체 범주

`OpenGL4`에는 수백 개의 enum이 있다. 대부분은 OpenGL 상수 이름을 C# 타입으로 분류한 것이며, 실무에서 먼저 익힐 범주는 다음과 같다.

- 버퍼: `BufferTarget`, `BufferUsageHint`, `BufferAccess`, `BufferStorageFlags`
- 정점/그리기: `PrimitiveType`, `DrawElementsType`, `VertexAttribPointerType`
- 셰이더/프로그램: `ShaderType`, `ShaderParameter`, `GetProgramParameterName`, `ProgramInterface`, `ProgramProperty`
- 텍스처/픽셀: `TextureTarget`, `TextureUnit`, `TextureParameterName`, `TextureMinFilter`, `TextureMagFilter`, `TextureWrapMode`, `PixelFormat`, `PixelType`, `PixelInternalFormat`
- 렌더 상태: `EnableCap`, `ClearBufferMask`, `BlendEquationMode`, `BlendingFactorSrc`, `BlendingFactorDest`, `DepthFunction`, `CullFaceMode`, `PolygonMode`
- 오프스크린 렌더링: `FramebufferTarget`, `FramebufferAttachment`, `FramebufferStatus`, `RenderbufferTarget`, `RenderbufferStorage`
- 진단/동기화: `ErrorCode`, `DebugSource`, `DebugType`, `DebugSeverity`, `QueryTarget`, `SyncStatus`, `MemoryBarrierFlags`

`Arb*`, `Khr*`, `Ext*`, `Nv*`, `Amd*`, `Intel*`로 시작하는 타입은 OpenGL 확장 기능에 해당한다. 지원 여부를 확인하지 않은 채 사용하면 특정 GPU/드라이버에서 동작하지 않을 수 있다.

## 이 저장소의 계층

```text
Window : GameWindow
  -> GL.*                     OpenGL 명령 호출
  -> Shader                   GLSL program 래퍼
  -> Texture                  Texture2D 래퍼
  -> Camera                   view/projection 행렬 계산
```

참고할 코드:

- VBO/VAO, 셰이더, 드로우 호출: `Chapter1/2-HelloTriangle/Window.cs`
- EBO 인덱스 드로우: `Chapter1/3-ElementBufferObjects/Window.cs`
- 텍스처: `Chapter1/5-Textures/Window.cs`, `Common/Texture.cs`
- 카메라 행렬: `Chapter1/9-Camera/Window.cs`, `Common/Camera.cs`
- 다중 광원·깊이 테스트: `Chapter2/6-MultipleLights/Window.cs`
