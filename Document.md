# Stack Guys

&nbsp;

## 카메라 설정

### 1) Cinemachine으로 TPS 카메라(추천)

#### A. 플레이어(캡슐) 만들기

1. **Hierarchy** → **3D Object → Capsule** 생성, 이름 `Player`.
2. `Transform` 초기화(Pos 0,0,0 / Rot 0,0,0 / Scale 1,1,1).
3. **Rigidbody** 추가
   * Mass 70, Drag 0, Angular Drag 0.05
   * **Constraints** : Freeze Rotation **X, Z** (넘어짐 방지)
4. (선택) **CharacterController** 를 쓸 거면 Rigidbody 대신 CharacterController 사용.

#### B. 카메라 기준점(팔/어깨 피벗) 만들기

1. `Player`의 자식으로 **Empty** 생성 → 이름 `CameraRoot`
   * Position **(0, 1.6, 0)** (어깨 높이쯤)
2. `CameraRoot` 자식으로 **Empty** 생성 → 이름 `AimTarget`
   * Position **(0, 1.6, 0.5)**

#### C. 메인 카메라 + 시네머신

1. **Main Camera** 선택 → **Cinemachine Brain** 추가.
2. **Cinemachine** 창에서 **Create Virtual Camera** → `CM vcam`.
3. `CM vcam` 설정
   * **Follow** : `CameraRoot`
   * **LookAt** : `AimTarget`
   * **Body** : *Third Person Follow*
     * Camera Distance **3.5**
     * Shoulder Offset **(0.4, 0.2, 0)**
     * Vertical Arm Length **0.5**
     * Damping (X/Y/Z) **0.2 / 0.2 / 0.2**
   * **Aim** : *POV* (또는 Composer)
     * Horizontal Speed **300** deg/s
     * Vertical Speed **120** deg/s
     * Clamp(Y) **-60 ~ 70**
   * **Extensions** : **Cinemachine Collider** 추가(벽 뚫기 방지)
     * Strategy: Pull Camera Forward
     * Damping 0.2

#### D. 입력(New Input System 기준)

1. 프로젝트가 **New Input System** 이면 `CM vcam`에 **Cinemachine Input Provider** 추가
   * **XY Axis** 에 `Look` 액션(마우스 델타/패드 RightStick) 연결
2. 구 Input Manager(Old)면 `POV`의 **Input Axis Name** 을
   * Horizontal Axis: `Mouse X`
   * Vertical Axis: `Mouse Y` 로 지정

&nbsp;

## 트러블 슈팅

- 메터리얼 마젠타 색으로 깨지는 경우
  - https://velog.io/@kukudass130/%EC%9C%A0%EB%8B%88%ED%8B%B0-%EC%97%90%EC%85%8B-%EC%A0%81%EC%9A%A9%EC%8B%9C-%EB%82%98%ED%83%80%EB%82%98%EB%8A%94-%EB%A7%88%EC%A0%A0%ED%83%80%EC%83%89-%EC%88%98%EC%A0%95-%EC%97%90%EC%85%8B-%EB%A8%B8%ED%85%8C%EB%A6%AC%EC%96%BC-%EA%B9%A8%EC%A7%90

&nbsp;

## 단축키

Ctrl + Shift + F - 현재 시점 기준 카메라 이동

이동(Pan)	마우스 휠 클릭 후 드래그 or Alt + 마우스 오른쪽 버튼 드래그

회전(Orbit)	Alt + 마우스 왼쪽 버튼 드래그

줌(Zoom)	Alt + 마우스 오른쪽 버튼 드래그 or 휠 스크롤


&nbsp;

## 에셋

&nbsp;

### ProBuilder

- 프리팹 언팩 이후 Center Pivot 적용

&nbsp;

### Platformer 에셋

#### 땅

- BaseThin_MESH (4)

#### 다리

- SupportColored_MESH (21)
- RegularBarrier_MESH (30)
- BaseThin_MESH (4)
