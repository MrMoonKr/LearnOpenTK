# 5-MeshAndMaterial

이 예제는 4번의 개별 GPU 객체를 `Mesh`와 `Material` 책임으로 묶고, `uModel` 행렬로 제자리 회전하는 색상 큐브를 그립니다.

## 실행

```powershell
dotnet run --project Chapter3/5-MeshAndMaterial/5-MeshAndMaterial.csproj
```

## 코드와 GPU 리소스

`Mesh`는 `VertexArray`, `VertexBuffer`, `IndexBuffer`를 생성하고 소유합니다. 생성 시 VAO를 바인딩한 뒤 VBO에 위치·색상 정점 배열을, EBO에 인덱스를 올리고 attribute location 0과 1의 해석 규칙을 설정합니다. `Draw()`는 VAO 바인딩과 `GL.DrawElements`만 담당합니다.

`Material`은 공유 가능한 `Shader`를 참조하며 `Bind()`에서 프로그램을 사용합니다. 매 프레임 `GLView`는 경과 시간으로 Y축 회전과 X축 회전을 만들고, 두 행렬을 결합해 `uModel` uniform으로 전달합니다. vertex shader는 `uModel * vec4(aPosition, 1.0)`을 계산하므로, 같은 cube 정점이 매 프레임 회전된 위치로 변환됩니다. 이 분리는 다음 장의 SceneNode가 어떤 Mesh와 Material을 그릴지 표현할 기반입니다.

GPU 객체는 `GLView.Load` 중 OpenGL 컨텍스트가 활성화된 상태에서 생성됩니다. `Disposed`에서 컨텍스트를 다시 current로 만든 후 `Mesh.Dispose()`가 VBO/EBO/VAO를 삭제하고, shader program도 삭제합니다. 셰이더 파일은 프로젝트의 `None Update="Shaders\\**"` 설정으로 출력 폴더에 복사됩니다.

색상은 정점 셰이더에서 fragment shader로 보간됩니다. 깊이 테스트가 켜져 있어 회전 중 앞쪽 면이 뒤쪽 면을 가립니다. 아직 view/projection 행렬은 없으므로 원근감은 없으며, 다음 단계에서 카메라와 투영을 추가할 수 있습니다.
