# 3-RenderQuad

`2-GLApp`과 동일한 WinForms UI 및 `Program` / `MainForm` / `GLView` 클래스 구성을 유지하면서, OpenGL의 정점 버퍼와 인덱스 버퍼로 사각형을 그리는 예제입니다.

사각형 자체는 OpenGL의 기본 도형이 아닙니다. 이 예제는 사각형을 두 개의 삼각형으로 나누고, 정점마다 위치와 색상을 GPU에 전달합니다. fragment shader까지 전달된 색상은 삼각형 내부에서 자동 보간되어 그라데이션으로 표시됩니다.

## 실행

```powershell
dotnet run --project Chapter3/3-RenderQuad/3-RenderQuad.csproj
```

## 화면과 데이터 흐름

```text
C# Vertices / Indices
        |
        +-- VBO: 위치와 색상 정점 데이터 업로드
        |
        +-- EBO: 정점을 참조할 인덱스 업로드
        |
        +-- VAO: VBO 해석 규칙과 EBO 연결 상태를 저장
        |
        v
vertex shader (position, color 입력)
        |
        v
래스터라이저 (삼각형 내부 색상 보간)
        |
        v
fragment shader (최종 픽셀 색상 출력)
```

## 정점 데이터: `Vertices`

`GLView.cs`의 `Vertices` 배열은 정점 네 개를 순서대로 저장합니다. 한 정점은 여섯 개의 `float`로 구성됩니다.

```text
x, y, z, r, g, b
\________/  \_______/
 위치 vec3    색상 vec3
```

예를 들어 좌하단 정점은 다음과 같습니다.

```csharp
-0.70f, -0.70f, 0.0f, 1.0f, 0.0f, 0.0f
```

앞의 세 값은 NDC(Normalized Device Coordinates) 위치이며, 화면 중앙은 `(0, 0)`이고 가시 범위는 각 축 `-1`부터 `1`까지입니다. 뒤의 세 값은 RGB 색상으로, 각 성분은 `0.0f`부터 `1.0f`까지 사용합니다. 이 값은 좌하단의 빨강입니다.

위치와 색상을 정점별로 번갈아 저장하는 방식을 **인터리브(interleaved) 정점 데이터**라고 합니다. GPU는 한 정점을 처리할 때 필요한 속성을 연속해서 읽을 수 있습니다.

## VBO: Vertex Buffer Object

VBO는 CPU의 `float[]` 정점 데이터를 GPU 메모리에 올리는 버퍼입니다. 이 예제에서는 `_vbo`가 VBO 핸들입니다.

```csharp
_vbo = GL.GenBuffer();
GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
GL.BufferData(
    BufferTarget.ArrayBuffer,
    Vertices.Length * sizeof(float),
    Vertices,
    BufferUsageHint.StaticDraw);
```

각 호출의 역할은 다음과 같습니다.

| 호출 | 역할 |
|---|---|
| `GL.GenBuffer()` | 빈 GPU 버퍼를 만들고 핸들을 받습니다. |
| `GL.BindBuffer(ArrayBuffer, ...)` | 이후의 정점 버퍼 작업 대상이 `_vbo`임을 지정합니다. |
| `GL.BufferData(...)` | CPU 배열을 GPU에 복사합니다. 크기는 바이트 단위이므로 `sizeof(float)`를 곱합니다. |
| `StaticDraw` | 생성 뒤 데이터가 거의 바뀌지 않는다는 사용 힌트입니다. |

VBO는 단지 바이트의 나열일 뿐, 어느 값이 위치이고 어느 값이 색상인지는 알지 못합니다. 그 해석 규칙은 VAO에 설정합니다.

## EBO: Element Buffer Object

EBO는 정점을 그릴 순서를 담는 인덱스 버퍼입니다. 이 예제의 인덱스는 다음과 같습니다.

```csharp
private static readonly uint[] Indices = [0, 1, 2, 2, 3, 0];
```

```text
첫 번째 삼각형: 0 -- 1 -- 2
두 번째 삼각형: 2 -- 3 -- 0
```

인덱스를 쓰지 않으면 두 삼각형의 공유 모서리 정점을 중복해 총 여섯 정점을 VBO에 넣어야 합니다. EBO를 사용하면 네 정점을 한 번씩만 저장하고, 필요할 때 인덱스로 재사용합니다.

```csharp
_ebo = GL.GenBuffer();
GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
GL.BufferData(
    BufferTarget.ElementArrayBuffer,
    Indices.Length * sizeof(uint),
    Indices,
    BufferUsageHint.StaticDraw);
```

`ElementArrayBuffer`에 바인딩된 EBO는 VAO 상태의 일부로 저장됩니다. 따라서 EBO를 연결할 때는 반드시 해당 VAO가 바인딩된 상태여야 합니다.

## VAO: Vertex Array Object

VAO는 정점 입력을 읽는 방법을 기억하는 상태 객체입니다. 이 예제의 `_vao`는 다음 정보를 보관합니다.

- 위치 속성은 location `0`이고, 정점 시작에서 3개 `float`를 읽는다.
- 색상 속성은 location `1`이고, 정점 시작에서 3개 `float` 뒤부터 3개 `float`를 읽는다.
- 정점 데이터는 `_vbo`, 인덱스 데이터는 `_ebo`에서 읽는다.

```csharp
_vao = GL.GenVertexArray();
GL.BindVertexArray(_vao);

const int floatsPerVertex = 6;
var stride = floatsPerVertex * sizeof(float);

GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
GL.EnableVertexAttribArray(0);

GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
GL.EnableVertexAttribArray(1);
```

`stride`는 다음 정점까지 건너뛸 바이트 수입니다. 이 예제는 정점 하나가 `float` 6개이므로 `6 * 4 = 24`바이트입니다. 색상은 위치 뒤에서 시작하므로 offset이 `3 * sizeof(float)`입니다.

`GL.EnableVertexAttribArray`를 호출하지 않으면 해당 location의 배열 입력은 사용되지 않습니다.

## 셰이더 연결

정점 셰이더의 location은 VAO 설정과 정확히 일치해야 합니다.

```glsl
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aColor;

out vec3 vertexColor;

void main()
{
    gl_Position = vec4(aPosition, 1.0);
    vertexColor = aColor;
}
```

각 정점 실행 결과인 `vertexColor`는 fragment shader로 전달됩니다. 삼각형 내부의 각 픽셀에서는 세 꼭짓점 값을 가중 평균해 색상이 보간됩니다.

```glsl
in vec3 vertexColor;
out vec4 fragmentColor;

void main()
{
    fragmentColor = vec4(vertexColor, 1.0);
}
```

셰이더는 실행 중 파일에서 읽으므로 `Shaders` 폴더를 출력 폴더에 복사해야 합니다. 프로젝트 파일의 `None ... CopyToOutputDirectory` 설정이 이 역할을 합니다.

## 그리기

렌더링 시에는 VAO만 다시 바인딩한 뒤 EBO의 인덱스를 사용해 그립니다.

```csharp
_shader.Use();
GL.BindVertexArray(_vao);
GL.DrawElements(PrimitiveType.Triangles, Indices.Length,
    DrawElementsType.UnsignedInt, IntPtr.Zero);
```

`DrawElements`의 개수는 정점 수가 아니라 **인덱스 수**입니다. 따라서 이 예제는 인덱스 6개를 읽어 삼각형 2개를 그립니다. 마지막 `IntPtr.Zero`는 EBO의 첫 번째 인덱스부터 읽는다는 뜻입니다.

## 대각선 경계가 보이지 않는 이유

사각형은 실제로 두 삼각형이므로, 임의의 네 색상을 지정하면 공유 대각선을 따라 색상 변화가 꺾여 보일 수 있습니다. 이 예제는 좌하단 빨강에서 시작해, 가로 방향에는 녹색 성분을, 세로 방향에는 파란색 성분을 더하는 조합을 사용합니다.

```text
좌상단: 마젠타       우상단: 흰색
좌하단: 빨강         우하단: 노랑
```

그래서 두 삼각형 모두 대각선 위의 같은 위치에서 같은 보간 색상을 계산하며, 경계가 보이지 않는 연속적인 그라데이션이 됩니다.

## 리소스 수명

- `OnLoad`: VAO, VBO, EBO, 셰이더 프로그램 생성 및 GPU 업로드
- `OnRenderFrame`: 화면 지우기, 셰이더/VAO 바인딩, `DrawElements` 호출
- `OnUnload`: VAO, VBO, EBO, 셰이더 프로그램 해제

OpenGL 객체는 유효한 OpenGL 컨텍스트가 있는 동안에만 생성하거나 삭제해야 합니다. 따라서 이 예제는 생성과 해제를 `GLView`의 생명주기 메서드에 둡니다.
