# Stack Guys

# Stack Guys

[](https://github.com/May-I-Game/stack-guys/blob/dev/Document.md#stack-guys)

## 카메라 설정

[](https://github.com/May-I-Game/stack-guys/blob/dev/Document.md#%EC%B9%B4%EB%A9%94%EB%9D%BC-%EC%84%A4%EC%A0%95)

~[https://www.youtube.com/watch?v=o7O28SFGWS4시네머신](https://www.youtube.com/watch?v=o7O28SFGWS4%EC%8B%9C%EB%84%A4%EB%A8%B8%EC%8B%A0) 카메라 추가 -> Follow 플레이어 지정Position Control -> Orbital Follow 설정, Rotation Control -> Rotation Control -> Rotation ComposerOrbital Follow에서 Add Input Controller 추가에셋 폴더에서 Input Actions Editor 클릭Action Maps에서 카메라 컨트롤 생성 -> Actions에서 MouseZoom 생성Mouse 생성 후 Scroll 선택GamepadZoom 생성 후 Value - Axis 선택기본 바인딩 삭제 후 Add Positive/Negative 바인딩 생성1D Axis에서 left Shoulder를 Negative로 Right를 생성~ -

* StarterAssets의 카메라와 컨트롤러 그대로 사용

## 트러블 슈팅

[](https://github.com/May-I-Game/stack-guys/blob/dev/Document.md#%ED%8A%B8%EB%9F%AC%EB%B8%94-%EC%8A%88%ED%8C%85)

* 메터리얼 마젠타 색으로 깨지는 경우
  * [https://velog.io/@kukudass130/%EC%9C%A0%EB%8B%88%ED%8B%B0-%EC%97%90%EC%85%8B-%EC%A0%81%EC%9A%A9%EC%8B%9C-%EB%82%98%ED%83%80%EB%82%98%EB%8A%94-%EB%A7%88%EC%A0%A0%ED%83%80%EC%83%89-%EC%88%98%EC%A0%95-%EC%97%90%EC%85%8B-%EB%A8%B8%ED%85%8C%EB%A6%AC%EC%96%BC-%EA%B9%A8%EC%A7%90](https://velog.io/@kukudass130/%EC%9C%A0%EB%8B%88%ED%8B%B0-%EC%97%90%EC%85%8B-%EC%A0%81%EC%9A%A9%EC%8B%9C-%EB%82%98%ED%83%80%EB%82%98%EB%8A%94-%EB%A7%88%EC%A0%A0%ED%83%80%EC%83%89-%EC%88%98%EC%A0%95-%EC%97%90%EC%85%8B-%EB%A8%B8%ED%85%8C%EB%A6%AC%EC%96%BC-%EA%B9%A8%EC%A7%90)
  * Window->Rendering->Render Pipeline Converter->Material Upgrade(체크)-> Initialize And Convert(클릭)

## 단축키

[](https://github.com/May-I-Game/stack-guys/blob/dev/Document.md#%EB%8B%A8%EC%B6%95%ED%82%A4)

Ctrl + Shift + F - 현재 시점 기준 카메라 이동

이동(Pan) 마우스 휠 클릭 후 드래그 or Alt + 마우스 오른쪽 버튼 드래그

회전(Orbit) Alt + 마우스 왼쪽 버튼 드래그

줌(Zoom) Alt + 마우스 오른쪽 버튼 드래그 or 휠 스크롤

## 에셋

[](https://github.com/May-I-Game/stack-guys/blob/dev/Document.md#%EC%97%90%EC%85%8B)

### ProBuilder

[](https://github.com/May-I-Game/stack-guys/blob/dev/Document.md#probuilder)

* 프리팹 언팩 이후 Center Pivot 적용

### Cinemachine

[](https://github.com/May-I-Game/stack-guys/blob/dev/Document.md#cinemachine)

### Platformer 에셋

[](https://github.com/May-I-Game/stack-guys/blob/dev/Document.md#platformer-%EC%97%90%EC%85%8B)

#### 땅

[](https://github.com/May-I-Game/stack-guys/blob/dev/Document.md#%EB%95%85)

* BaseThin_MESH (4)

#### 다리

[](https://github.com/May-I-Game/stack-guys/blob/dev/Document.md#%EB%8B%A4%EB%A6%AC)

* SupportColored_MESH (21)
* RegularBarrier_MESH (30)
* BaseThin_MESH (4) - Y 값 4.6

### Human Character Dummy

[](https://github.com/May-I-Game/stack-guys/blob/dev/Document.md#human-character-dummy)

### StarterAssets

[](https://github.com/May-I-Game/stack-guys/blob/dev/Document.md#starterassets)

### Login Module

[](https://github.com/May-I-Game/stack-guys/blob/dev/Document.md#login-module)

* [https://velog.io/@pkoi5088/Unity-%EB%A1%9C%EA%B7%B8%EC%9D%B8-UI](https://velog.io/@pkoi5088/Unity-%EB%A1%9C%EA%B7%B8%EC%9D%B8-UI)

## Scene

[](https://github.com/May-I-Game/stack-guys/blob/dev/Document.md#scene)

### Login Scene

[](https://github.com/May-I-Game/stack-guys/blob/dev/Document.md#login-scene)

* [https://velog.io/@mystic6113/Unity-%EB%A1%9C%EA%B7%B8%EC%9D%B8-UI-%EB%A7%8C%EB%93%A4%EA%B8%B0-1](https://velog.io/@mystic6113/Unity-%EB%A1%9C%EA%B7%B8%EC%9D%B8-UI-%EB%A7%8C%EB%93%A4%EA%B8%B0-1)

### Game Scene

[](https://github.com/May-I-Game/stack-guys/blob/dev/Document.md#game-scene)

#### 장애물 1코스

[](https://github.com/May-I-Game/stack-guys/blob/dev/Document.md#%EC%9E%A5%EC%95%A0%EB%AC%BC-1%EC%BD%94%EC%8A%A4)

#### 장애물 2코스

[](https://github.com/May-I-Game/stack-guys/blob/dev/Document.md#%EC%9E%A5%EC%95%A0%EB%AC%BC-2%EC%BD%94%EC%8A%A4)

#### 장애물 3코스

[](https://github.com/May-I-Game/stack-guys/blob/dev/Document.md#%EC%9E%A5%EC%95%A0%EB%AC%BC-3%EC%BD%94%EC%8A%A4

&nbsp;

## 카메라 설정

~~https://www.youtube.com/watch?v=o7O28SFGWS4시네머신 카메라 추가 -> Follow 플레이어 지정Position Control -> Orbital Follow 설정, Rotation Control -> Rotation Control -> Rotation ComposerOrbital Follow에서 Add Input Controller 추가에셋 폴더에서 Input Actions Editor 클릭Action Maps에서 카메라 컨트롤 생성 -> Actions에서 MouseZoom 생성Mouse 생성 후 Scroll 선택GamepadZoom 생성 후 Value - Axis 선택기본 바인딩 삭제 후 Add Positive/Negative 바인딩 생성1D Axis에서 left Shoulder를 Negative로 Right를 생성~~-

- StarterAssets의 카메라와 컨트롤러 그대로 사용

&nbsp;

## 트러블 슈팅

- 메터리얼 마젠타 색으로 깨지는 경우
  - https://velog.io/@kukudass130/%EC%9C%A0%EB%8B%88%ED%8B%B0-%EC%97%90%EC%85%8B-%EC%A0%81%EC%9A%A9%EC%8B%9C-%EB%82%98%ED%83%80%EB%82%98%EB%8A%94-%EB%A7%88%EC%A0%A0%ED%83%80%EC%83%89-%EC%88%98%EC%A0%95-%EC%97%90%EC%85%8B-%EB%A8%B8%ED%85%8C%EB%A6%AC%EC%96%BC-%EA%B9%A8%EC%A7%90
  - Window->Rendering->Render Pipeline Converter->Material Upgrade(체크)-> Initialize And Convert(클릭)

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

### Cinemachine

&nbsp;

### Platformer 에셋

#### 땅

- BaseThin_MESH (4)

#### 다리

- SupportColored_MESH (21)
- RegularBarrier_MESH (30)
- BaseThin_MESH (4) - Y 값 4.6

### Human Character Dummy

### StarterAssets

### Login Module

- https://velog.io/@pkoi5088/Unity-%EB%A1%9C%EA%B7%B8%EC%9D%B8-UI


## Scene


### Login Scene



### Game Scene

#### 장애물 1코스

#### 장애물 2코스

#### 장애물 3코스


## Server
