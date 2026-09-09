# 8-KeyboardMouse

오른쪽 마우스 드래그로 orbit 회전, 중간 버튼 드래그로 패닝, 휠로 줌을 수행하는 모델 뷰어 카메라 예제입니다.

`OrbitCamera`은 Target, Distance, Yaw, Pitch를 소유하고 위치와 view 행렬을 계산합니다. GLView는 MouseDown/Move/Up/Wheel 콜백을 카메라의 Rotate/Pan/Zoom으로 전달합니다. `TabStop`과 `Focus()`를 설정해 GLView가 키보드/마우스 입력 대상이 되게 합니다.
