# Unity C# 컨벤션
- 공식 가이드라인 (https://unity.com/kr/blog/engine-platform/clean-up-your-code-how-to-create-your-own-c-code-style)

**서식(Formatting)**

- 들여쓰기는 **space 4칸**을 사용한다.
- 중괄호는 **Allman 스타일**(여는 `{`를 새 줄)에 맞춘다.
- **단일 문장도 중괄호를 생략하지 않는다**(항상 `{}` 사용).
- `else/catch/finally`도 **새 줄**에서 시작한다.

**using / 네임스페이스**

- `using` 지시문은 **네임스페이스 밖(파일 상단)**에 배치한다.

**var 사용 원칙**

- 타입이 명백하면 `var`를 허용한다.
- **타입이 명백하지 않으면 `var` 금지**한다.
- 기본형(built-in)에는 `var` 사용을 **지양**한다.

**접근 제한자**

- **인터페이스를 제외한 멤버는 접근 제한자**를 **명시**한다(예: `private`도 표기).

**프로퍼티**

- **읽기 전용/짧은 프로퍼티**는 **식 본문(=>)** 를 **가능하면** 사용한다(접근자도 동일).

**네이밍 규칙**

- **타입(클래스/구조체/열거형/델리게이트)**: **PascalCase**.
- **메서드/프로퍼티/이벤트**: **PascalCase**.
- **네임스페이스**: **PascalCase**.
- **인터페이스**: **`I` + PascalCase**.
- **public 필드**: **PascalCase**.
- **private/protected 필드**: **`_camel`**(언더스코어 + camelCase).
- **지역 변수·매개변수**: **camelCase**.
- **enum 이름과 멤버**: **PascalCase**.

&nbsp;

# 커밋 규칙 (Conventional Commits)

- 커밋 종류

  ```
  - feat 		: 새로운 기능 추가
  - fix 		: 버그 수정
  - docs 		: 문서 수정
  - refactor 	: 코드 리팩터링
  - comment   : 필요한 주석 추가 및 변경
  - test 		: 테스트 코드, 리팩토링 테스트 코드 추가
  - chore     : 빌드 업무 수정, 패키지 매니저 수정, 파일 혹은 폴더명 수정 및 삭제
  - style     : 코드 포맷팅, 세미콜론 누락, 코드 변경이 없는 경우
  - init      : 프로젝트 초기 생성
  ```

- 커밋 메시지 구조

  ```
  feat: 전체 내용 요약

  - 변경 사항 1
  - 변경 사항 2
  - 변경 사항 3
  ```

  &nbsp;

# Github Branch Ruleset

## main, dev
- Require a pull request before merging (PR 필수, 승인 1명)
- Block force pushes (강제 푸시 금지)
- Restrict deletions (브랜치 삭제 금지)

## dev
- Require a pull request before merging (PR 필수, 승인 1명)
