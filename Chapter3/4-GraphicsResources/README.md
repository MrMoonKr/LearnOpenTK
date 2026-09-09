# 4-GraphicsResources

`Common.Graphics`의 GPU 리소스 래퍼를 사용해 인덱스 사각형을 그리는 WinForms/OpenGL 예제입니다. 목표는 OpenGL 핸들의 소유권과 수명을 명확히 하는 것입니다.

## 실행

```powershell
dotnet run --project Chapter3/4-GraphicsResources/4-GraphicsResources.csproj
```

## 구성과 호출 순서

`GLView.OnLoad`에서 `VertexArray`, VBO(`VertexBuffer`), EBO(`IndexBuffer`), `Shader`를 만듭니다. VBO에는 위치와 색상으로 구성된 `float[]`를, EBO에는 두 삼각형의 정점 순서인 `uint[]`를 업로드합니다. VAO에는 location 0의 position(`vec3`)과 location 1의 color(`vec3`) 레이아웃을 기록합니다.

매 프레임 `OnRenderFrame`은 clear, shader 사용, VAO 바인딩, `GL.DrawElements` 순서로 실행됩니다. vertex shader의 색상 출력은 rasterizer에서 삼각형 내부로 보간되고 fragment shader가 최종 색상을 출력합니다. 네 꼭짓점 색상을 하나의 연속 색상 평면이 되게 지정했으므로 두 삼각형의 대각선 경계는 보이지 않아야 합니다.

`Buffer`는 공통 OpenGL handle을 소유하고, `VertexBuffer`와 `IndexBuffer`가 각각 `ArrayBuffer`와 `ElementArrayBuffer`의 의미를 고정합니다. `Bind()`는 이후 OpenGL 호출의 대상을 정하고, 생성자는 CPU 배열을 GPU 메모리로 복사합니다. 이 객체들은 `IDisposable`을 구현하며, OpenGL 컨텍스트가 유효한 `GLView.OnUnload`에서 `Dispose()`하여 `GL.DeleteBuffer`와 `GL.DeleteVertexArray`를 호출합니다. 셰이더 프로그램도 같은 시점에 삭제합니다.

`Shaders/shader.vert`와 `Shaders/shader.frag`는 프로젝트 파일의 `None Update="Shaders\\**"` 설정에 의해 출력 폴더로 복사됩니다. 런타임에는 `AppContext.BaseDirectory` 아래에서 이 파일들을 읽습니다.
