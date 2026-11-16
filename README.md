> 🔌 **디지털 논리회로 가상 실험실** — 3D 브레드보드에 칩을 놓고 전선을 이은 뒤, 전원을 켜서 신호가 흐르는 것을 브라우저에서 바로 확인합니다.

---
# Virtual Logic Labs (Web)

2018년 Rutgers 대학교 ECE 학과 소프트웨어 공학 프로젝트 **Virtual Logic Labs**를 Unity 2017.3 → **Unity 6 (6000.3)** 로 올리고, **로그인 없는 시작 화면**과 **3D 회로 실험실**을 새로 만들어 **WebGL**로 빌드한 버전입니다. 설치 없이 브라우저에서 바로 실행됩니다.

🌐 **바로 실행:** https://metasongi.github.io/virtual-logic-labs/

## 시작 화면 (입력 없음)
아이디·비밀번호를 칠 필요 없이 버튼 하나로 들어갑니다.

| 버튼 | 들어가는 곳 |
|---|---|
| **3D 회로 실험실 시작** | 새로 만든 3D 실험실 (아래 설명) |
| **학생 실습** | 원작 Lab 1 · Lab 2 · 카메라 (영문) |
| **관리자 화면** | 원작 Admin (영문) |

## 3D 회로 실험실
| 기능 | 설명 |
|---|---|
| 3D 브레드보드 | 네온 테두리 보드, 다리 달린 칩, 그림자. 빈 곳 드래그로 회전 · 오른쪽 드래그로 이동 · 휠/핀치로 확대 |
| ⚡ 전원 | 보드 뒤 **5V 전원 공급기**의 **POWER** 버튼(또는 스페이스바)을 눌러야 회로가 동작. 켜지면 5.0 V 표시와 전원 레일이 빛남 |
| 🔗 전선 연결 | 핀(●)을 끌어 다른 부품의 핀에 놓거나, 핀을 하나씩 차례로 클릭. 전선은 3D 곡선으로 놓임 |
| 신호 표시 | 켜진 전선은 네온 초록으로 빛나고 빛 알갱이가 흐름, LED 점등 |
| 부품 | 스위치 · LED · AND · OR · NOT · NAND · NOR · XOR |
| 진리표 | 스위치 → LED 진리표 자동 계산, 지금 상태 줄 표시 |
| 기타 | 반가산기 예제, 초기화(두 번 눌러 확인), 회로 자동 저장(브라우저) |

## 폴더 구성
| 경로 | 설명 |
|---|---|
| `docs/` | WebGL 빌드 결과물 (GitHub Pages가 이 폴더를 서비스) |
| `Assets/Scripts/Lab3D/` | 시작 화면·3D 실험실 코드 — `LogicSim.cs`(논리), `Lab3D.cs`(장면·조작), `Lab3DArt.cs`(재질·전선 메시), `UIKit.cs`(UI), `StartMenu.cs` |
| `Assets/Editor/Lab3DSetup.cs` | 시작·3D 실험실 장면과 재질 생성, 빌드 장면 순서 정리 |
| `Assets/Editor/WebGLBuilder.cs` | 메뉴 **Build → WebGL (docs)** 로 `docs/` 에 다시 빌드 |
| `Assets/WebGLTemplates/Neon/` | 전체 화면 네온 로딩 페이지 |
| `Assets/Scenes/` | 원작 장면 (Lab 1·2, Admin 등) |
| `ThirdParty/NanumGothic-OFL.txt` | 한글 폰트(나눔고딕) 라이선스 |

## 다시 빌드하기
1. Unity Hub에서 이 폴더를 Unity **6000.3.21f1** 로 엽니다.
2. 메뉴 **Build → WebGL (docs)** 를 누르면 장면 준비 후 `docs/` 가 새로 만들어집니다.
3. 커밋·푸시하면 GitHub Pages에 반영됩니다.

## 참고
- 원작의 온라인 DB(AWS)는 더 이상 동작하지 않아 계정 로그인 화면은 빼고 바로 들어가게 했습니다.
- 웹 버전에서는 원작의 웹캠 기능이 동작하지 않습니다.
- 같은 실습을 더 가볍게 하고 싶다면 three.js 버전 [3D 논리회로 랩](https://metasongi.github.io/circuit-lab-3d/)(미션 8개·휴대폰 지원)도 있습니다.
- 원작: Rutgers ECE 2018 Spring Group 10 — Khalid Akash, Vikas Khan, Dhruvik Patel, Joe Cella, Sagar Phanda, Yiwen Tao. ([데모 영상](https://www.youtube.com/watch?v=CsVZMjCCOYc))
- 한글 폰트: 나눔고딕 (SIL Open Font License).
