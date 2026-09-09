# 7-Lighting

텍스처 없이 normal과 Blinn-Phong 조명으로 회전 큐브의 형태를 표현하는 예제입니다.

## 실행

```powershell
dotnet run --project Chapter3/7-Lighting/7-Lighting.csproj
```

## 조명과 데이터

각 면의 normal이 달라야 하므로 큐브는 공유 정점 8개가 아니라 면마다 4개, 총 24개 정점을 사용합니다. 정점은 position, color, normal 순서이며 `Mesh`의 location 0/1/2 속성으로 GPU에 전달됩니다. 이번 조명 예제는 빛의 색과 세기를 명확히 보기 위해 vertex shader에서 흰색 base color를 사용합니다.

태양 방향광은 Key light이고, 차가운 Fill point light와 따뜻한 Rim point light가 어두운 면과 윤곽을 보완합니다. fragment shader는 ambient, Lambert diffuse, Blinn-Phong specular를 합산합니다. normal은 model 변환의 inverse-transpose normal matrix로 변환되므로 물체 회전에도 올바른 방향을 유지합니다.

Load에서 VAO/VBO/EBO, shader를 생성하고 매 프레임 model/view/projection 및 조명 uniform을 설정한 뒤 mesh를 그립니다. Disposed에서 OpenGL 컨텍스트가 유효한 상태로 GPU 리소스를 해제합니다. 셰이더 파일은 출력 폴더로 복사됩니다.
