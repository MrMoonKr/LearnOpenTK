# 6-Camera

회전 큐브에 camera view와 perspective projection을 추가하는 WinForms/OpenGL 예제입니다.

## 실행

```powershell
dotnet run --project Chapter3/6-Camera/6-Camera.csproj
```

## 변환 순서

이 저장소의 `Shader.SetMatrix4`는 OpenTK 행렬을 transpose해 GPU에 전달하므로, vertex shader는 `vec4(aPosition, 1.0) * uModel * uView * uProjection`을 계산합니다. `uModel`은 큐브를 두 축으로 회전시키고, `uView`는 `(0, 0, 2)`의 카메라에서 본 좌표계로 옮기며, `uProjection`은 perspective projection으로 clip space를 만듭니다.

`Camera`는 위치, aspect ratio, FOV를 소유하며 `GetViewMatrix()`와 `GetProjectionMatrix()`를 제공합니다. `GLView.ResizeViewport`는 viewport와 camera aspect ratio를 함께 갱신합니다. 따라서 창 비율이 바뀌어도 큐브가 찌그러지지 않습니다.

`Mesh`는 VAO/VBO/EBO를 소유하고, `Material`은 shader program을 사용합니다. GPU 리소스는 OpenGL 컨텍스트가 유효한 Load에서 생성하고 Disposed에서 해제합니다. `Shaders/**`는 출력 폴더로 복사되어 `AppContext.BaseDirectory`에서 읽힙니다.
