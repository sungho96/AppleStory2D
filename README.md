# AppleStory

Unity 2D 횡스크롤 액션 포트폴리오 프로젝트입니다.  
옛날 메이플스토리 감성의 이동, 점프, 사다리/로프, 직업별 스킬, 보스전, UI, 네트워크 플레이 흐름을 직접 구현하는 것을 목표로 합니다.

> 이 저장소는 학습 및 포트폴리오 목적의 프로젝트입니다.  
> 원작 게임의 리소스, 사운드, 폰트, 상표권은 각 권리자에게 있으며, 저작권이 있는 원본 리소스를 저장소에 포함하거나 배포하지 않습니다.

---

## 게임 소개

- 장르: 2D 횡스크롤 플랫폼 액션
- 방향성: 가볍고 말랑한 조작감, 빠른 전투 템포, 보스 패턴 회피 중심 플레이
- 주요 플레이: 캐릭터 선택 후 고블린 보스전 진입
- 캐릭터: 궁수, 전사
- 현재 핵심 씬: `GameEntry`, `GoblinBoss`, `GoblinBoss_Network`, `Dungeon`, `Spring`, `Winter`, `Winter&Spring`

---

## 현재 구현된 주요 기능

### 플레이어

- 좌우 이동, 점프, 낙하, 방향 전환
- 사다리/로프 진입, 등반, 이탈 처리
- 단방향 플랫폼 충돌 처리
- 체력, MP, EXP, 레벨 기반 스탯 관리
- 피격, 넉백, 무적/방어 관련 반응 처리
- 카메라 추적 및 카메라 흔들림 연출

### 직업 및 스킬

- 궁수 기본 공격
- 궁수 스킬: 파워샷, 래피드 발리
- 전사 기본 공격
- 전사 스킬: 다운 스트라이크, 실드 블록
- 이동속도 버프, 공격속도 버프, 분노 버프
- 스킬별 시각 피드백, 차지 게이지, 잔상, 화면 피드백
- 스킬 아이콘 드래그 기반 키 설정 UI

### 몬스터 및 보스

- 일반 고블린 순찰, 접촉 피해, 피격, 사망 처리
- 고블린 보스 전투 컨트롤러
- 보스 패턴: 접근/점프 이동, 근접 반격, 얼음 파동, 낙하 공격
- 체력 구간에 따른 패턴 변화
- 보스 보호막, 그로기, 회복 패턴
- 보스 HP UI 및 승리/패배 결과 UI

### UI

- 시작 화면 로고/캐릭터 인트로 연출
- 방 생성/참가 UI
- 캐릭터 선택 UI
- 준비 완료 UI
- HP/MP/EXP HUD
- 보스 HP 바
- 스킬 패널 및 키세팅 UI
- 로딩 오버레이
- 보스전 결과 화면 및 재시작 버튼

### 네트워크

- Unity Netcode for GameObjects 기반 Host/Client 구조
- 방 생성 및 참가 흐름
- 로컬 플레이어 권한 분리
- 캐릭터 선택에 따른 플레이어 프리팹 선택
- 플레이어 이동/방향/사다리/전투 비주얼 동기화
- 보스전 네트워크 테스트 씬 구성

---

## 기술 요소

- Unity 6 기반 2D 프로젝트
- `MonoBehaviour`와 `NetworkBehaviour`를 구분한 컴포넌트 구조
- Rigidbody2D, Collider2D, Trigger 기반 이동/전투 판정
- Animation State와 직접 클립 재생을 함께 활용한 직업별 모션 처리
- Script 기반 UI 생성 및 Inspector 참조 보완
- LayerMask 기반 지면, 사다리, 플랫폼, 적 판정 분리
- NetworkVariable, ServerRpc, ClientRpc 기반 동기화
- 포트폴리오용 연출 강화를 위한 VFX, 화면 피드백, 카메라 흔들림 적용

---

## 사용 패키지

- Unity Editor: `6000.3.21f1`
- 2D Feature
- Unity UI
- TextMeshPro
- Netcode for GameObjects
- Multiplayer Play Mode
- Timeline
- Visual Scripting
- Unity Test Framework

---

## 실행 방법

1. Unity Hub에서 프로젝트 폴더를 엽니다.
2. Unity Editor 버전 `6000.3.21f1` 또는 호환 버전으로 실행합니다.
3. `Assets/Scenes/GameEntry.unity` 또는 테스트할 씬을 엽니다.
4. Play 버튼을 눌러 실행합니다.

네트워크 테스트는 `GameEntry` 또는 `GoblinBoss_Network` 흐름에서 Host/Client를 나누어 확인합니다.

---

## 조작 방법

- 이동: `A / D` 또는 방향키
- 점프: `Space`
- 사다리/로프: `W / S` 또는 상하 입력
- 기본 공격: `Left Ctrl`
- 키세팅 UI 열기: `.`
- 키세팅 UI 닫기: `Esc`
- 스킬: 키세팅 UI에서 원하는 키로 배치 후 사용

---

## 폴더 구조

```text
Assets/
  Art/                  # 캐릭터, UI, 이펙트 등 아트 리소스
  Scenes/               # 게임 및 테스트 씬
  Scripts/
    Players/            # 플레이어 이동, 공격, 스킬, 버프, 피격 처리
    Enemys/             # 일반 고블린 및 보스 AI/패턴
    UI/                 # HUD, 키세팅, 캐릭터 선택, 결과 화면
    Network/            # Netcode 기반 플레이어/씬/비주얼 동기화
    CharacterSelect/    # 캐릭터 선택 데이터 및 패널 제어
    Camera/             # 보스 인트로 카메라 연출
    Map/                # 보스 아레나 구성
    Util/               # 카메라, 디버그, 프리팹 초기화 유틸
```

---

## 개발 상태

현재는 기본 조작 데모 단계를 넘어, 캐릭터 선택부터 보스전 진입, 직업별 스킬, UI, 네트워크 테스트까지 연결하는 단계입니다.  
다음 개선 대상은 보스전 밸런스 조정, 전투 피드백 polish, 네트워크 안정성 확인, 포트폴리오 시연 영상 정리입니다.

---

## 포트폴리오 / 시연

- 포트폴리오 페이지: https://sungho96.github.io/sunghopages/index.html
